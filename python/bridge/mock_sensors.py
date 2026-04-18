"""
MockSensors — 실기기 없이 Orchestrator를 가동하기 위한 Mock 센서 소스 구현

LinkBand2 미입고 상태에서 E2E 검증·개발을 계속하기 위해 사인파·화이트노이즈
기반의 가짜 데이터를 생성합니다. 모든 클래스는 `SensorSource` 인터페이스를
구현하므로 실기기 소스와 런타임 교체 가능합니다.

구현 센서:
    - MockEegSource    : 6채널 EEG, 알파파(10Hz) + 세타파(6Hz) + 노이즈
    - MockPpgSource    : 심박수 72bpm 시뮬레이션 (IR/Red 2채널)
    - MockAccSource    : 1Hz 저주파 진동
    - MockImuSource    : 정지 상태 중력 벡터 + 약한 jitter

사용 예시::

    src = MockEegSource(sampling_hz=250, num_channels=2)
    src.start()
    eeg = src.get_latest()   # EEGData
    src.stop()
"""

from __future__ import annotations

import logging
import math
import random
import threading
import time
from typing import Optional

from .fused_data_frame import (
    ACCData,
    EEGData,
    IMUData,
    PPGData,
    SensorFlags,
)
from .sensor_source import SensorSource

logger = logging.getLogger(__name__)


# ─────────────────────────────────────────────
# 내부 공통 — 백그라운드 루프 기반 기본 Mock
# ─────────────────────────────────────────────

class _BaseMockSource(SensorSource):
    """Mock 공통: 백그라운드 스레드에서 주기적으로 최신값을 갱신.

    구현 서브클래스는 `_generate(t_sec)`만 재정의하면 됩니다.
    """

    _sampling_hz: float
    _thread: Optional[threading.Thread]
    _running: bool
    _latest: Optional[object]
    _lock: threading.Lock
    _started_mono: float

    def __init__(self, sampling_hz: float = 30.0) -> None:
        self._sampling_hz = max(1.0, float(sampling_hz))
        self._thread = None
        self._running = False
        self._latest = None
        self._lock = threading.Lock()
        self._started_mono = 0.0

    @property
    def is_running(self) -> bool:
        return self._running

    def start(self) -> None:
        if self._running:
            return
        self._running = True
        self._started_mono = time.monotonic()
        self._thread = threading.Thread(
            target=self._loop,
            name=f"Mock-{self.name}",
            daemon=True,
        )
        self._thread.start()
        logger.info("[%s] 시작 — sampling=%.1fHz", self.name, self._sampling_hz)

    def stop(self) -> None:
        self._running = False
        if self._thread is not None:
            self._thread.join(timeout=1.0)
            self._thread = None
        logger.info("[%s] 중지", self.name)

    def get_latest(self) -> Optional[object]:
        with self._lock:
            return self._latest

    def _loop(self) -> None:
        period = 1.0 / self._sampling_hz
        next_tick = time.monotonic()
        while self._running:
            t_sec = time.monotonic() - self._started_mono
            payload = self._generate(t_sec)
            with self._lock:
                self._latest = payload
            next_tick += period
            sleep_for = next_tick - time.monotonic()
            if sleep_for > 0:
                time.sleep(sleep_for)
            else:
                # 뒤쳐지면 따라잡기만 하고 리셋
                next_tick = time.monotonic()

    # 서브클래스 구현부
    def _generate(self, t_sec: float) -> object:
        raise NotImplementedError


# ─────────────────────────────────────────────
# EEG
# ─────────────────────────────────────────────

class MockEegSource(_BaseMockSource):
    """EEG Mock — 알파(10Hz) + 세타(6Hz) + 가우시안 노이즈."""

    def __init__(
        self,
        sampling_hz: float = 250.0,
        num_channels: int = 2,
        amplitude_uv: float = 30.0,
        noise_uv: float = 5.0,
    ) -> None:
        super().__init__(sampling_hz=sampling_hz)
        self._num_channels = min(6, max(1, int(num_channels)))
        self._amp = float(amplitude_uv)
        self._noise = float(noise_uv)

    @property
    def sensor_flag(self) -> SensorFlags:
        return SensorFlags.EEG

    @property
    def name(self) -> str:
        return "MockEEG"

    def _generate(self, t_sec: float) -> EEGData:
        channels = [0.0] * 6
        contact = [255] * 6  # 완전 접촉
        for ch in range(self._num_channels):
            phase = 0.5 * ch  # 채널별 위상차
            alpha = math.sin(2 * math.pi * 10.0 * t_sec + phase)
            theta = 0.6 * math.sin(2 * math.pi * 6.0 * t_sec)
            noise = random.gauss(0.0, self._noise)
            channels[ch] = self._amp * (alpha + theta) + noise
        for ch in range(self._num_channels, 6):
            # 미사용 채널은 접촉 품질을 0으로
            contact[ch] = 0
        return EEGData(channels=channels, contact_quality=contact)


# ─────────────────────────────────────────────
# PPG
# ─────────────────────────────────────────────

class MockPpgSource(_BaseMockSource):
    """PPG Mock — 72bpm 심박 시뮬레이션."""

    def __init__(self, sampling_hz: float = 50.0, bpm: float = 72.0) -> None:
        super().__init__(sampling_hz=sampling_hz)
        self._bpm = float(bpm)

    @property
    def sensor_flag(self) -> SensorFlags:
        return SensorFlags.PPG

    @property
    def name(self) -> str:
        return "MockPPG"

    def _generate(self, t_sec: float) -> PPGData:
        hz = self._bpm / 60.0
        pulse = math.sin(2 * math.pi * hz * t_sec)
        # IR은 큰 DC + 맥동, Red는 약간 작은 진폭
        ir = 30000.0 + 800.0 * pulse + random.gauss(0.0, 20.0)
        red = 28000.0 + 650.0 * pulse + random.gauss(0.0, 20.0)
        return PPGData(ir=float(ir), red=float(red))


# ─────────────────────────────────────────────
# ACC
# ─────────────────────────────────────────────

class MockAccSource(_BaseMockSource):
    """3축 가속도 Mock — 1Hz 느린 진동 + 중력 1g."""

    def __init__(self, sampling_hz: float = 25.0) -> None:
        super().__init__(sampling_hz=sampling_hz)

    @property
    def sensor_flag(self) -> SensorFlags:
        return SensorFlags.ACC

    @property
    def name(self) -> str:
        return "MockACC"

    def _generate(self, t_sec: float) -> ACCData:
        sway = 0.05 * math.sin(2 * math.pi * 1.0 * t_sec)
        return ACCData(
            x=sway + random.gauss(0.0, 0.01),
            y=-1.0 + random.gauss(0.0, 0.01),  # 중력
            z=sway + random.gauss(0.0, 0.01),
        )


# ─────────────────────────────────────────────
# IMU
# ─────────────────────────────────────────────

class MockImuSource(_BaseMockSource):
    """X-Sens IMU Mock — 정지 상태 + 작은 jitter."""

    def __init__(self, sampling_hz: float = 60.0) -> None:
        super().__init__(sampling_hz=sampling_hz)

    @property
    def sensor_flag(self) -> SensorFlags:
        return SensorFlags.IMU

    @property
    def name(self) -> str:
        return "MockIMU"

    def _generate(self, t_sec: float) -> IMUData:
        j = lambda: random.gauss(0.0, 0.002)
        return IMUData(
            quaternion=[j(), j(), j(), 1.0],
            acceleration=[j(), -9.81 + j(), j()],
            gyroscope=[j(), j(), j()],
            magnetometer=[30.0 + j(), 1.0 + j(), 40.0 + j()],
        )
