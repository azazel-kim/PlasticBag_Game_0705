"""
TimeSyncAligner — 센서 간 타임스탬프 정렬기

서로 다른 주기/지연을 가진 센서 데이터를 공통 타임라인(UTC epoch ms)으로 정렬.
각 센서의 고정 지연(BLE=35ms, Camera=75ms, IMU=12ms 등)을 보정하고,
NTP 기반 clock offset 계산으로 PC-XR 간 시간차를 보정함.

동작 흐름:
    1. 센서 원시 타임스탬프 수신
    2. 센서별 고정 지연 보정 (raw - latency_offset)
    3. clock_offset 적용 (NTP re-sync)
    4. max_drift_ms 초과 검증 → 초과 시 None 반환 (stale)
"""

from __future__ import annotations

import logging
import time
from dataclasses import dataclass, field
from enum import Enum
from typing import Optional

logger = logging.getLogger(__name__)


# ─────────────────────────────────────────────
# 센서 타입 열거형
# ─────────────────────────────────────────────

class SensorType(Enum):
    """센서 종류 — 각각 고유한 전송 지연 특성을 가짐."""
    EEG = "eeg"              # LinkBand2 BLE, ~500Hz
    PPG = "ppg"              # LinkBand2 BLE, ~25Hz
    ACC = "acc"              # LinkBand2 BLE, ~60Hz
    CAMERA = "camera"        # OBSBOT USB → MediaPipe, ~30Hz
    BLENDSHAPE = "blendshape"  # MediaPipe Face (Camera에서 파생)
    POSE = "pose"            # MediaPipe Pose (Camera에서 파생)
    IMU = "imu"              # X-Sens SDK, ~60-120Hz
    EYE = "eye"              # OpenXR EyeGaze, ~90Hz


# ─────────────────────────────────────────────
# 센서별 고정 지연 프로파일 (ms)
# ─────────────────────────────────────────────

# BLE 센서 (LinkBand2): 약 20~50ms → 중간값 35ms
# Camera (USB + MediaPipe 추론): 약 50~100ms → 중간값 75ms
# X-Sens IMU (USB/WiFi SDK): 약 5~20ms → 중간값 12ms
# Eye Tracking (OpenXR 내장): <10ms → 5ms
DEFAULT_LATENCY_OFFSETS_MS: dict[SensorType, int] = {
    SensorType.EEG: 35,
    SensorType.PPG: 35,
    SensorType.ACC: 35,
    SensorType.CAMERA: 75,
    SensorType.BLENDSHAPE: 75,
    SensorType.POSE: 75,
    SensorType.IMU: 12,
    SensorType.EYE: 5,
}


# ─────────────────────────────────────────────
# 드리프트 통계
# ─────────────────────────────────────────────

@dataclass
class _DriftStats:
    """센서별 드리프트 추적 — re-sync 판단에 사용."""
    total_samples: int = 0
    stale_count: int = 0         # max_drift 초과 횟수
    max_observed_drift_ms: int = 0
    last_drift_ms: int = 0
    last_aligned_ms: int = 0


# ─────────────────────────────────────────────
# TimeSyncAligner
# ─────────────────────────────────────────────

class TimeSyncAligner:
    """센서 데이터 타임스탬프 정렬기.

    매개변수:
        max_drift_ms: 허용 최대 시간 차이 (ms). 초과 시 None 반환.
        latency_offsets: 센서별 고정 지연 보정값 (ms).
            지정하지 않으면 DEFAULT_LATENCY_OFFSETS_MS 사용.
        resync_interval_sec: clock offset 재계산 주기 (초). 기본 300초(5분).
        resync_drift_threshold_ms: 이 값 이상 드리프트 감지 시 즉시 re-sync.

    사용 예시::

        aligner = TimeSyncAligner(max_drift_ms=100)
        aligned = aligner.align_timestamp(raw_ms=1713300000123, sensor=SensorType.EEG)
        if aligned is None:
            # stale data — 폐기
            pass
    """

    def __init__(
        self,
        max_drift_ms: int = 100,
        latency_offsets: Optional[dict[SensorType, int]] = None,
        resync_interval_sec: float = 300.0,
        resync_drift_threshold_ms: int = 50,
    ) -> None:
        self._max_drift_ms = max_drift_ms
        self._latency_offsets = latency_offsets or dict(DEFAULT_LATENCY_OFFSETS_MS)
        self._resync_interval_sec = resync_interval_sec
        self._resync_drift_threshold_ms = resync_drift_threshold_ms

        # clock offset: PC 로컬 시계와 기준 시계(UTC) 간 차이 (ms)
        # 양수 = PC 시계가 빠름, 음수 = PC 시계가 느림
        self._clock_offset_ms: int = 0

        # 마지막 re-sync 시각 (monotonic, 초)
        self._last_sync_time: float = 0.0

        # 센서별 드리프트 통계
        self._drift_stats: dict[SensorType, _DriftStats] = {
            st: _DriftStats() for st in SensorType
        }

        # 초기 동기화 수행
        self._perform_clock_sync()

    # ── 공개 API ──

    def align_timestamp(
        self,
        raw_ms: int,
        sensor: SensorType,
        reference_ms: Optional[int] = None,
    ) -> Optional[int]:
        """원시 타임스탬프를 보정된 공통 타임라인으로 변환.

        보정 과정:
            1. 센서 고정 지연 제거: raw_ms - latency_offset
            2. clock offset 적용: aligned = corrected - clock_offset
            3. max_drift 검증: |aligned - reference| > max_drift → None

        매개변수:
            raw_ms: 센서에서 수신한 원시 타임스탬프 (ms)
            sensor: 센서 종류
            reference_ms: 비교 기준 시각 (ms). None이면 현재 시각 사용.

        반환값:
            보정된 타임스탬프 (ms) 또는 stale 시 None
        """
        # 주기적 re-sync 확인
        self._check_resync()

        # 1) 센서 고정 지연 보정
        latency = self._latency_offsets.get(sensor, 0)
        corrected_ms = raw_ms - latency

        # 2) clock offset 적용
        aligned_ms = corrected_ms - self._clock_offset_ms

        # 3) 기준 시각 대비 드리프트 확인
        if reference_ms is None:
            reference_ms = self._now_ms()

        drift = abs(aligned_ms - reference_ms)

        # 드리프트 통계 갱신
        stats = self._drift_stats[sensor]
        stats.total_samples += 1
        stats.last_drift_ms = drift
        stats.last_aligned_ms = aligned_ms
        if drift > stats.max_observed_drift_ms:
            stats.max_observed_drift_ms = drift

        # max_drift 초과 시 stale 처리
        if drift > self._max_drift_ms:
            stats.stale_count += 1
            logger.warning(
                "[TimeSyncAligner] %s 데이터 stale — "
                "drift=%dms > max=%dms (raw=%d, aligned=%d, ref=%d)",
                sensor.value, drift, self._max_drift_ms,
                raw_ms, aligned_ms, reference_ms,
            )
            return None

        # 드리프트가 resync 임계값 초과 시 조기 re-sync 플래그
        if drift > self._resync_drift_threshold_ms:
            logger.info(
                "[TimeSyncAligner] %s 드리프트 %dms — re-sync 필요 가능",
                sensor.value, drift,
            )

        return aligned_ms

    def set_clock_offset(self, offset_ms: int) -> None:
        """clock offset을 수동으로 설정.

        외부 ClockSync 패킷(UDP)으로부터 offset을 받아 적용할 때 사용.

        매개변수:
            offset_ms: PC 시계 - 기준 시계 차이 (ms)
        """
        old = self._clock_offset_ms
        self._clock_offset_ms = offset_ms
        self._last_sync_time = time.monotonic()
        logger.info(
            "[TimeSyncAligner] clock offset 갱신: %d → %d ms",
            old, offset_ms,
        )

    def set_latency_offset(self, sensor: SensorType, offset_ms: int) -> None:
        """특정 센서의 고정 지연 보정값을 변경.

        매개변수:
            sensor: 대상 센서
            offset_ms: 새 지연 보정값 (ms)
        """
        old = self._latency_offsets.get(sensor, 0)
        self._latency_offsets[sensor] = offset_ms
        logger.info(
            "[TimeSyncAligner] %s latency offset: %d → %d ms",
            sensor.value, old, offset_ms,
        )

    def get_drift_stats(self, sensor: SensorType) -> dict:
        """센서별 드리프트 통계 조회.

        반환값:
            {total_samples, stale_count, stale_rate, max_drift_ms, last_drift_ms}
        """
        stats = self._drift_stats[sensor]
        stale_rate = (
            stats.stale_count / stats.total_samples
            if stats.total_samples > 0 else 0.0
        )
        return {
            "sensor": sensor.value,
            "total_samples": stats.total_samples,
            "stale_count": stats.stale_count,
            "stale_rate": round(stale_rate, 4),
            "max_observed_drift_ms": stats.max_observed_drift_ms,
            "last_drift_ms": stats.last_drift_ms,
        }

    def get_all_drift_stats(self) -> list[dict]:
        """모든 센서의 드리프트 통계 목록."""
        return [self.get_drift_stats(st) for st in SensorType]

    @property
    def clock_offset_ms(self) -> int:
        """현재 clock offset (ms)."""
        return self._clock_offset_ms

    @property
    def max_drift_ms(self) -> int:
        """허용 최대 드리프트 (ms)."""
        return self._max_drift_ms

    # ── 내부 메서드 ──

    def _now_ms(self) -> int:
        """현재 UTC epoch 밀리초."""
        return int(time.time() * 1000)

    def _check_resync(self) -> None:
        """re-sync 주기 확인 — 필요 시 clock offset 재계산."""
        now_mono = time.monotonic()
        elapsed = now_mono - self._last_sync_time
        if elapsed >= self._resync_interval_sec:
            self._perform_clock_sync()

    def _perform_clock_sync(self) -> None:
        """clock offset 재계산.

        현재 구현: 로컬 UTC 기준 (offset=0).
        실제 배포 시 NTP 서버 또는 UDP ClockSync 패킷 기반으로 교체.
        """
        # TODO: NTP 또는 UDP ClockSync 패킷 기반 정밀 동기화
        # 현재는 PC 로컬 시계를 기준으로 사용 (offset=0)
        self._clock_offset_ms = 0
        self._last_sync_time = time.monotonic()
        logger.debug(
            "[TimeSyncAligner] clock sync 완료 — offset=%d ms",
            self._clock_offset_ms,
        )

    def __repr__(self) -> str:
        return (
            f"TimeSyncAligner("
            f"max_drift={self._max_drift_ms}ms, "
            f"clock_offset={self._clock_offset_ms}ms, "
            f"resync_interval={self._resync_interval_sec}s)"
        )
