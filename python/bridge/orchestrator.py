"""
BridgeOrchestrator — 여러 SensorSource를 30Hz로 융합하여 Galaxy XR로 UDP 송신

동작:
    1. 등록된 모든 SensorSource를 start
    2. 30Hz 타이머마다 각 소스의 get_latest()를 pull
    3. FusedDataFrame 조립 (제공된 센서만 valid_sensors 비트 set)
    4. UdpSender.send_fused_frame으로 Galaxy XR에 송신
    5. 주기적 Heartbeat, 옵션으로 SessionLogger 호출

토폴로지:
    [Mock EEG]─┐
    [Mock PPG]─┤
    [Mock ACC]─┤
    [Mock IMU]─┼─▶ BridgeOrchestrator @30Hz ─▶ UdpSender ─▶ Galaxy XR Unity
    [MediaPipe]┤
    [LinkBand RX]┘
"""

from __future__ import annotations

import logging
import threading
import time
from dataclasses import dataclass, field
from typing import Callable, Optional

from .fused_data_frame import (
    ACCData,
    BlendShapeData,
    EEGData,
    EyeTrackingData,
    FusedDataFrame,
    IMUData,
    PPGData,
    PoseData,
    SensorFlags,
)
from .sensor_source import SensorSource
from .udp_protocol import UdpSender

logger = logging.getLogger(__name__)


# ─────────────────────────────────────────────
# 통계
# ─────────────────────────────────────────────

@dataclass
class OrchestratorStats:
    """Orchestrator 가동 통계."""
    frames_sent: int = 0
    frames_skipped: int = 0
    send_errors: int = 0
    start_time: float = 0.0
    last_frame_time: float = 0.0
    source_hits: dict[str, int] = field(default_factory=dict)

    @property
    def elapsed_sec(self) -> float:
        if self.start_time == 0.0:
            return 0.0
        return time.monotonic() - self.start_time

    @property
    def send_rate_hz(self) -> float:
        elapsed = self.elapsed_sec
        if elapsed <= 0:
            return 0.0
        return self.frames_sent / elapsed

    def to_dict(self) -> dict:
        return {
            "frames_sent": self.frames_sent,
            "frames_skipped": self.frames_skipped,
            "send_errors": self.send_errors,
            "elapsed_sec": round(self.elapsed_sec, 2),
            "send_rate_hz": round(self.send_rate_hz, 2),
            "source_hits": dict(self.source_hits),
        }


# ─────────────────────────────────────────────
# BridgeOrchestrator
# ─────────────────────────────────────────────

# FusedDataFrame 필드 이름과 SensorFlags 매핑 — 조립 시 reflection 회피
_FLAG_TO_FIELD: dict[SensorFlags, str] = {
    SensorFlags.BLENDSHAPE: "blendshape",
    SensorFlags.EYE:        "eye_tracking",
    SensorFlags.EEG:        "eeg",
    SensorFlags.PPG:        "ppg",
    SensorFlags.ACC:        "acc",
    SensorFlags.POSE:       "pose",
    SensorFlags.IMU:        "imu",
}

# 센서별 예상 DTO 타입 (런타임 검증용)
_FLAG_TO_DTO_TYPE: dict[SensorFlags, type] = {
    SensorFlags.BLENDSHAPE: BlendShapeData,
    SensorFlags.EYE:        EyeTrackingData,
    SensorFlags.EEG:        EEGData,
    SensorFlags.PPG:        PPGData,
    SensorFlags.ACC:        ACCData,
    SensorFlags.POSE:       PoseData,
    SensorFlags.IMU:        IMUData,
}


class BridgeOrchestrator:
    """30Hz 송신 Orchestrator.

    매개변수:
        sender: 설정 완료된 UdpSender 인스턴스 (target_ip 포함)
        fusion_rate_hz: 프레임 조립/송신 주기 (기본 30Hz)
        heartbeat_hz: Heartbeat 송신 주기 (기본 1Hz, 0이면 비활성)
        on_frame: 송신 직전 콜백 — 로거 연결용 (frame: FusedDataFrame)
    """

    def __init__(
        self,
        sender: UdpSender,
        fusion_rate_hz: float = 30.0,
        heartbeat_hz: float = 1.0,
        on_frame: Optional[Callable[[FusedDataFrame], None]] = None,
    ) -> None:
        self._sender = sender
        self._fusion_period = 1.0 / max(1.0, fusion_rate_hz)
        self._heartbeat_period = 1.0 / max(0.1, heartbeat_hz) if heartbeat_hz > 0 else 0.0
        self._on_frame = on_frame

        self._sources: list[SensorSource] = []
        self._running = False
        self._thread: Optional[threading.Thread] = None
        self._stats = OrchestratorStats()
        self._stats_lock = threading.Lock()

        logger.info(
            "[Orchestrator] 초기화 — %.1fHz, heartbeat=%s",
            fusion_rate_hz,
            f"{heartbeat_hz}Hz" if heartbeat_hz > 0 else "off",
        )

    # ── 소스 등록 ──

    def add_source(self, source: SensorSource) -> None:
        """SensorSource 등록 — start() 이전에만 호출 가능."""
        if self._running:
            raise RuntimeError("실행 중에는 소스 추가 불가")
        self._sources.append(source)
        with self._stats_lock:
            self._stats.source_hits[source.name] = 0
        logger.info("[Orchestrator] 소스 등록: %s (flag=%s)", source.name, source.sensor_flag.name)

    # ── 라이프사이클 ──

    def start(self) -> None:
        if self._running:
            return
        if not self._sources:
            raise RuntimeError("등록된 소스 없음 — add_source() 먼저 호출")

        # 전체 소스 시작
        for src in self._sources:
            try:
                src.start()
            except Exception as e:
                logger.error("[Orchestrator] %s 시작 실패: %s", src.name, e)

        # 핸드셰이크 — Unity 측이 있으면 응답, 없어도 무시
        try:
            self._sender.send_handshake()
        except Exception as e:
            logger.warning("[Orchestrator] handshake 실패(무시): %s", e)

        self._running = True
        with self._stats_lock:
            self._stats = OrchestratorStats()
            self._stats.start_time = time.monotonic()
            self._stats.source_hits = {src.name: 0 for src in self._sources}

        self._thread = threading.Thread(
            target=self._loop,
            name="BridgeOrchestrator",
            daemon=True,
        )
        self._thread.start()
        logger.info("[Orchestrator] 시작 — %d개 소스", len(self._sources))

    def stop(self) -> None:
        self._running = False
        if self._thread is not None:
            self._thread.join(timeout=2.0)
            self._thread = None
        for src in self._sources:
            try:
                src.stop()
            except Exception as e:
                logger.error("[Orchestrator] %s 정지 실패: %s", src.name, e)
        logger.info("[Orchestrator] 중지 — %s", self._stats.to_dict())

    @property
    def is_running(self) -> bool:
        return self._running

    @property
    def stats(self) -> dict:
        with self._stats_lock:
            return self._stats.to_dict()

    # ── 메인 루프 ──

    def _loop(self) -> None:
        next_tick = time.monotonic()
        last_heartbeat = time.monotonic()

        while self._running:
            # 1) 프레임 조립
            frame = self._build_frame()

            # 2) 송신
            try:
                self._sender.send_fused_frame(frame)
                with self._stats_lock:
                    self._stats.frames_sent += 1
                    self._stats.last_frame_time = time.monotonic()
                if self._on_frame is not None:
                    try:
                        self._on_frame(frame)
                    except Exception as e:
                        logger.error("[Orchestrator] on_frame 콜백 에러: %s", e)
            except Exception as e:
                with self._stats_lock:
                    self._stats.send_errors += 1
                logger.error("[Orchestrator] 프레임 송신 실패: %s", e)

            # 3) Heartbeat (옵션)
            now = time.monotonic()
            if self._heartbeat_period > 0 and now - last_heartbeat >= self._heartbeat_period:
                try:
                    self._sender.send_heartbeat()
                except Exception as e:
                    logger.warning("[Orchestrator] heartbeat 실패: %s", e)
                last_heartbeat = now

            # 4) 다음 틱까지 대기
            next_tick += self._fusion_period
            sleep_for = next_tick - time.monotonic()
            if sleep_for > 0:
                time.sleep(sleep_for)
            else:
                # 뒤쳐지면 스킵 카운트 증가, 타이밍 재설정
                with self._stats_lock:
                    self._stats.frames_skipped += 1
                next_tick = time.monotonic()

    # ── 프레임 조립 ──

    def _build_frame(self) -> FusedDataFrame:
        """모든 소스에서 최신값을 pull해 FusedDataFrame 조립."""
        frame = FusedDataFrame()
        frame.timestamp_ms = self._now_ms()

        for src in self._sources:
            payload = src.get_latest()
            if payload is None:
                continue

            flag = src.sensor_flag
            expected_type = _FLAG_TO_DTO_TYPE.get(flag)
            if expected_type is not None and not isinstance(payload, expected_type):
                logger.warning(
                    "[Orchestrator] %s 타입 불일치: 기대=%s, 실제=%s",
                    src.name, expected_type.__name__, type(payload).__name__,
                )
                continue

            field_name = _FLAG_TO_FIELD.get(flag)
            if field_name is None:
                continue

            setattr(frame, field_name, payload)
            frame.valid_sensors |= flag

            with self._stats_lock:
                self._stats.source_hits[src.name] = self._stats.source_hits.get(src.name, 0) + 1

        return frame

    @staticmethod
    def _now_ms() -> int:
        """현재 시각 ms — time.time_ns() 기반(NTP 점프 감지 용이)."""
        return time.time_ns() // 1_000_000
