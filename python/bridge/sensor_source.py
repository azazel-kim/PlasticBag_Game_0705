"""
SensorSource — 센서 데이터 소스 추상 인터페이스

Orchestrator가 각기 다른 센서(Mock/MediaPipe/LinkBand Receiver/IMU 등)를
동일한 방식으로 pull할 수 있도록 통일된 ABC를 제공합니다.

사용 패턴:
    source = MockEegSource()
    source.start()
    sensor_flag, payload = source.get_latest()  # None이면 데이터 없음
    source.stop()
"""

from __future__ import annotations

import logging
from abc import ABC, abstractmethod
from typing import Optional

from .fused_data_frame import SensorFlags

logger = logging.getLogger(__name__)


# ─────────────────────────────────────────────
# ISensorSource — 센서 소스 ABC
# ─────────────────────────────────────────────

class SensorSource(ABC):
    """Pull-기반 센서 데이터 소스 인터페이스.

    각 구현체는 자신이 제공하는 센서 종류(SensorFlags)와 최신 페이로드를
    `get_latest()`로 반환합니다. 페이로드 타입은 FusedDataFrame 내부 DTO
    중 하나(EEGData, PPGData, ACCData, IMUData, PoseData 등)여야 합니다.

    Orchestrator는 30Hz 타이머마다 모든 소스의 `get_latest()`를 호출하여
    FusedDataFrame을 조립합니다.
    """

    @property
    @abstractmethod
    def sensor_flag(self) -> SensorFlags:
        """이 소스가 채우는 SensorFlag 비트 (EEG/PPG/ACC/POSE/IMU/...)."""

    @property
    @abstractmethod
    def name(self) -> str:
        """소스 이름 — 로그/통계 구분용."""

    @abstractmethod
    def start(self) -> None:
        """소스 기동 — 내부 스레드/콜백/소켓 등 자원 준비."""

    @abstractmethod
    def stop(self) -> None:
        """소스 정지 — 자원 정리."""

    @abstractmethod
    def get_latest(self) -> Optional[object]:
        """가장 최근 센서 데이터 페이로드 반환.

        반환값은 FusedDataFrame 필드에 그대로 할당 가능한 DTO.
        데이터가 아직 없거나 stale하면 None.
        """

    @property
    def is_running(self) -> bool:
        """소스 실행 여부. 기본은 False, 구현체에서 override."""
        return False

    def __repr__(self) -> str:
        running = "running" if self.is_running else "stopped"
        return f"{type(self).__name__}({self.name}, {running})"
