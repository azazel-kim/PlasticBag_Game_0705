"""
FusionPipeline — 센서 융합 파이프라인

UdpReceiver로 FusedDataFrame을 수신하여 센서별 RingBuffer에 저장하고,
TimeSyncAligner로 타임스탬프를 정렬한 후 콜백으로 전달하는 통합 파이프라인.

동작 흐름:
    1. UDP 9001에서 FusedDataFrame 수신 (30Hz)
    2. 수신된 프레임의 센서별 데이터를 각 RingBuffer에 분배 저장
    3. TimeSyncAligner로 타임스탬프 유효성 검증
    4. 콜백(on_frame_received)으로 정렬된 프레임 전달
    5. 동기화 에러 시 on_sync_error 콜백 호출

사용 예시::

    pipeline = FusionPipeline(bind_port=9001)
    pipeline.on_frame_received = lambda frame: process(frame)
    pipeline.on_sync_error = lambda sensor, drift: log_error(sensor, drift)
    pipeline.start()
    # ... 사용 후 ...
    pipeline.stop()
    print(pipeline.stats)
"""

from __future__ import annotations

import logging
import threading
import time
from dataclasses import dataclass
from typing import Callable, Optional

from .fused_data_frame import (
    FusedDataFrame,
    SensorFlags,
)
from .ring_buffer import RingBuffer
from .time_sync_aligner import TimeSyncAligner, SensorType
from .udp_protocol import (
    UdpReceiver,
    FUSED_FRAME_PORT,
)

logger = logging.getLogger(__name__)


# ─────────────────────────────────────────────
# 파이프라인 통계
# ─────────────────────────────────────────────

@dataclass
class PipelineStats:
    """파이프라인 수신/처리 통계.

    속성:
        frames_received: 총 수신 프레임 수
        frames_aligned: 타임스탬프 정렬 성공 프레임 수
        frames_dropped: 정렬 실패(stale/drift 초과)로 폐기된 프레임 수
        sync_errors: 센서별 동기화 에러 횟수 합계
        start_time: 파이프라인 시작 시각 (monotonic)
        last_frame_time: 마지막 프레임 수신 시각 (monotonic)
    """
    frames_received: int = 0
    frames_aligned: int = 0
    frames_dropped: int = 0
    sync_errors: int = 0
    start_time: float = 0.0
    last_frame_time: float = 0.0

    @property
    def elapsed_sec(self) -> float:
        """파이프라인 가동 시간 (초)."""
        if self.start_time == 0.0:
            return 0.0
        return time.monotonic() - self.start_time

    @property
    def receive_rate_hz(self) -> float:
        """평균 수신율 (Hz)."""
        elapsed = self.elapsed_sec
        if elapsed <= 0:
            return 0.0
        return self.frames_received / elapsed

    @property
    def drop_rate(self) -> float:
        """프레임 폐기율 (0.0~1.0)."""
        if self.frames_received == 0:
            return 0.0
        return self.frames_dropped / self.frames_received

    @property
    def avg_latency_ms(self) -> float:
        """평균 처리 지연 (ms) — 마지막 프레임 기준 근사치."""
        # 정밀 측정은 per-frame 타이머가 필요하지만,
        # 여기서는 수신율 기반 근사값을 제공
        rate = self.receive_rate_hz
        if rate <= 0:
            return 0.0
        return 1000.0 / rate

    def to_dict(self) -> dict:
        """통계를 딕셔너리로 반환."""
        return {
            "frames_received": self.frames_received,
            "frames_aligned": self.frames_aligned,
            "frames_dropped": self.frames_dropped,
            "sync_errors": self.sync_errors,
            "elapsed_sec": round(self.elapsed_sec, 2),
            "receive_rate_hz": round(self.receive_rate_hz, 2),
            "drop_rate": round(self.drop_rate, 4),
        }


# ─────────────────────────────────────────────
# SensorFlags → SensorType 매핑
# ─────────────────────────────────────────────

# FusedDataFrame의 SensorFlags 비트를 TimeSyncAligner의 SensorType으로 매핑
_FLAG_TO_SENSOR: list[tuple[SensorFlags, SensorType, str]] = [
    (SensorFlags.BLENDSHAPE, SensorType.BLENDSHAPE, "blendshape"),
    (SensorFlags.EYE,        SensorType.EYE,        "eye_tracking"),
    (SensorFlags.EEG,        SensorType.EEG,        "eeg"),
    (SensorFlags.PPG,        SensorType.PPG,        "ppg"),
    (SensorFlags.ACC,        SensorType.ACC,        "acc"),
    (SensorFlags.POSE,       SensorType.POSE,       "pose"),
    (SensorFlags.IMU,        SensorType.IMU,        "imu"),
]


# ─────────────────────────────────────────────
# FusionPipeline
# ─────────────────────────────────────────────

class FusionPipeline:
    """센서 융합 파이프라인 — UDP 수신 → RingBuffer → TimeSyncAligner → 콜백.

    매개변수:
        bind_ip: UDP 바인드 IP (기본 "0.0.0.0")
        bind_port: FusedDataFrame 수신 포트 (기본 9001)
        ring_buffer_size: 센서별 RingBuffer 슬롯 수 (기본 512)
        max_drift_ms: 허용 최대 타임 드리프트 (ms, 기본 100)

    콜백:
        on_frame_received: 정렬 완료된 FusedDataFrame 수신 시 호출
        on_sync_error: 센서 동기화 에러(stale) 감지 시 호출
            인자: (sensor_type: SensorType, drift_ms: int)
    """

    def __init__(
        self,
        bind_ip: str = "0.0.0.0",
        bind_port: int = FUSED_FRAME_PORT,
        ring_buffer_size: int = 512,
        max_drift_ms: int = 100,
    ) -> None:
        self._bind_ip = bind_ip
        self._bind_port = bind_port
        self._ring_buffer_size = ring_buffer_size
        self._max_drift_ms = max_drift_ms

        # UDP 수신기
        self._receiver = UdpReceiver(
            bind_ip=bind_ip,
            bind_port=bind_port,
        )

        # 센서별 RingBuffer — FusedDataFrame 단위가 아닌 개별 센서 데이터
        self._buffers: dict[SensorType, RingBuffer] = {
            st: RingBuffer(capacity=ring_buffer_size)
            for st in SensorType
        }

        # 타임스탬프 정렬기
        self._aligner = TimeSyncAligner(max_drift_ms=max_drift_ms)

        # 전체 FusedDataFrame 버퍼 (원본 프레임 보관)
        self._frame_buffer: RingBuffer[FusedDataFrame] = RingBuffer(
            capacity=ring_buffer_size,
        )

        # 통계
        self._stats = PipelineStats()
        self._stats_lock = threading.Lock()

        # 콜백
        self.on_frame_received: Optional[Callable[[FusedDataFrame], None]] = None
        self.on_sync_error: Optional[Callable[[SensorType, int], None]] = None

        # 수신기 콜백 연결
        self._receiver.on_fused_frame = self._handle_frame

        logger.info(
            "[FusionPipeline] 초기화 — port=%d, buffer=%d, max_drift=%dms",
            bind_port, ring_buffer_size, max_drift_ms,
        )

    # ── 라이프사이클 ──

    def start(self) -> None:
        """파이프라인 시작 — UDP 수신 스레드 가동."""
        with self._stats_lock:
            self._stats = PipelineStats()
            self._stats.start_time = time.monotonic()
        self._receiver.start()
        logger.info("[FusionPipeline] 시작")

    def stop(self) -> None:
        """파이프라인 중지 — UDP 수신 스레드 종료."""
        self._receiver.stop()
        logger.info(
            "[FusionPipeline] 중지 — %s",
            self._stats.to_dict(),
        )

    @property
    def is_running(self) -> bool:
        """파이프라인 실행 여부."""
        return self._receiver.is_running

    # ── 통계/상태 ──

    @property
    def stats(self) -> dict:
        """파이프라인 + 수신기 통합 통계."""
        with self._stats_lock:
            pipeline_stats = self._stats.to_dict()
        return {
            "pipeline": pipeline_stats,
            "receiver": self._receiver.stats,
            "aligner": self._aligner.get_all_drift_stats(),
            "buffers": {
                st.value: self._buffers[st].count
                for st in SensorType
            },
        }

    @property
    def aligner(self) -> TimeSyncAligner:
        """TimeSyncAligner 인스턴스 접근 (clock offset 수동 설정 등)."""
        return self._aligner

    def get_buffer(self, sensor: SensorType) -> RingBuffer:
        """특정 센서의 RingBuffer 접근."""
        return self._buffers[sensor]

    def get_latest_frame(self) -> Optional[FusedDataFrame]:
        """가장 최근 수신된 FusedDataFrame 반환."""
        result = self._frame_buffer.get_latest()
        if result is None:
            return None
        return result[0]

    def get_nearest_frame(
        self, target_ms: int, max_drift_ms: Optional[int] = None,
    ) -> Optional[FusedDataFrame]:
        """목표 타임스탬프에 가장 가까운 FusedDataFrame 반환."""
        drift = max_drift_ms if max_drift_ms is not None else self._max_drift_ms
        result = self._frame_buffer.get_nearest(target_ms, max_drift_ms=drift)
        if result is None:
            return None
        return result[0]

    # ── 내부 처리 ──

    def _handle_frame(self, frame: FusedDataFrame, addr: tuple) -> None:
        """UdpReceiver 콜백 — 수신된 FusedDataFrame 처리.

        1. 통계 갱신
        2. 전체 프레임 RingBuffer에 저장
        3. 센서별 데이터 분배 → 개별 RingBuffer에 저장
        4. TimeSyncAligner로 타임스탬프 검증
        5. 콜백 호출
        """
        now_mono = time.monotonic()

        with self._stats_lock:
            self._stats.frames_received += 1
            self._stats.last_frame_time = now_mono

        # 전체 프레임 버퍼에 저장
        self._frame_buffer.push(frame, frame.timestamp_ms)

        # 센서별 분배 + 타임스탬프 정렬 검증
        has_sync_error = False
        # 현재 시각을 기준으로 드리프트 검증 — 오래된 프레임 감지용
        reference_ms = int(time.time() * 1000)

        for flag, sensor_type, attr_name in _FLAG_TO_SENSOR:
            if not (frame.valid_sensors & flag):
                continue

            sensor_data = getattr(frame, attr_name)
            if sensor_data is None:
                continue

            # 센서별 RingBuffer에 저장
            self._buffers[sensor_type].push(sensor_data, frame.timestamp_ms)

            # 타임스탬프 정렬 검증
            # 프레임 타임스탬프를 센서 원시 시점으로 취급하여 정렬
            aligned = self._aligner.align_timestamp(
                raw_ms=frame.timestamp_ms,
                sensor=sensor_type,
                reference_ms=reference_ms,
            )

            if aligned is None:
                # stale data — 동기화 에러
                has_sync_error = True
                drift_stats = self._aligner.get_drift_stats(sensor_type)
                drift_ms = drift_stats["last_drift_ms"]

                with self._stats_lock:
                    self._stats.sync_errors += 1

                if self.on_sync_error is not None:
                    try:
                        self.on_sync_error(sensor_type, drift_ms)
                    except Exception as e:
                        logger.error(
                            "[FusionPipeline] on_sync_error 콜백 에러: %s", e
                        )

        # 정렬 결과에 따라 통계 갱신
        with self._stats_lock:
            if has_sync_error:
                self._stats.frames_dropped += 1
            else:
                self._stats.frames_aligned += 1

        # 프레임 수신 콜백 호출 (동기화 에러와 무관하게 항상 전달)
        if self.on_frame_received is not None:
            try:
                self.on_frame_received(frame)
            except Exception as e:
                logger.error(
                    "[FusionPipeline] on_frame_received 콜백 에러: %s", e
                )

    # ── 디버그 ──

    def __repr__(self) -> str:
        running = "running" if self.is_running else "stopped"
        return (
            f"FusionPipeline({running}, "
            f"port={self._bind_port}, "
            f"frames={self._stats.frames_received})"
        )
