"""
LinkBandReceiver — 스마트폰(Galaxy Fold) LinkBand2 앱에서 전송되는
파셜 FusedDataFrame을 수신하여 EEG/PPG/ACC SensorSource로 노출합니다.

동작:
    - UDP 포트 9010(기본)에 바인드
    - 스마트폰이 보낸 FusedDataFrame(valid_sensors = EEG|PPG|ACC, 나머지 0 패딩)
      을 `UdpReceiver`로 받음
    - 내부적으로 최신 EEGData/PPGData/ACCData 저장
    - BridgeOrchestrator에 등록할 수 있는 3개의 Source facade 제공

왜 별도 포트(9010)인가:
    9001은 PC→Galaxy XR(FusedFrame) 송신용으로 이미 사용됨. 스마트폰→PC는
    반대 방향이므로 충돌 회피를 위해 별도 포트로 분리.
"""

from __future__ import annotations

import logging
import threading
import time
from typing import Optional

from .fused_data_frame import (
    ACCData,
    EEGData,
    FusedDataFrame,
    PPGData,
    SensorFlags,
)
from .sensor_source import SensorSource
from .udp_protocol import UdpReceiver

logger = logging.getLogger(__name__)

# 스마트폰 → PC Bridge 전용 포트 (FUSE 프로토콜 재사용, 파셜 프레임)
LINKBAND_INGEST_PORT: int = 9010


class LinkBandReceiver:
    """스마트폰→PC 파셜 FusedDataFrame 수신자.

    매개변수:
        bind_ip: 바인드 IP (기본 "0.0.0.0" = 모든 인터페이스)
        bind_port: 바인드 포트 (기본 9010)
        stale_ms: 이 시간(ms) 이상 경과한 데이터는 get_latest에서 None 반환
    """

    def __init__(
        self,
        bind_ip: str = "0.0.0.0",
        bind_port: int = LINKBAND_INGEST_PORT,
        stale_ms: int = 500,
    ) -> None:
        self._bind_ip = bind_ip
        self._bind_port = bind_port
        self._stale_ms = int(stale_ms)

        self._receiver = UdpReceiver(bind_ip=bind_ip, bind_port=bind_port)
        self._receiver.on_fused_frame = self._handle_frame

        self._lock = threading.Lock()
        self._latest_eeg: Optional[EEGData] = None
        self._latest_ppg: Optional[PPGData] = None
        self._latest_acc: Optional[ACCData] = None
        self._last_update_mono: float = 0.0
        self._packets: int = 0

        logger.info("[LinkBandReceiver] 초기화 — %s:%d", bind_ip, bind_port)

    # ── 라이프사이클 ──

    def start(self) -> None:
        self._receiver.start()
        logger.info("[LinkBandReceiver] 수신 시작")

    def stop(self) -> None:
        self._receiver.stop()
        logger.info("[LinkBandReceiver] 수신 중지 (총 %d 프레임)", self._packets)

    @property
    def is_running(self) -> bool:
        return self._receiver.is_running

    @property
    def stats(self) -> dict:
        return {
            "bind": (self._bind_ip, self._bind_port),
            "packets": self._packets,
            "last_update_ago_ms": self._ago_ms(),
        }

    # ── 수신 핸들러 ──

    def _handle_frame(self, frame: FusedDataFrame, addr: tuple) -> None:  # noqa: ARG002
        with self._lock:
            self._packets += 1
            self._last_update_mono = time.monotonic()
            if frame.valid_sensors & SensorFlags.EEG and frame.eeg is not None:
                self._latest_eeg = frame.eeg
            if frame.valid_sensors & SensorFlags.PPG and frame.ppg is not None:
                self._latest_ppg = frame.ppg
            if frame.valid_sensors & SensorFlags.ACC and frame.acc is not None:
                self._latest_acc = frame.acc

    # ── 조회 ──

    def _is_fresh(self) -> bool:
        if self._last_update_mono == 0.0:
            return False
        age_ms = (time.monotonic() - self._last_update_mono) * 1000.0
        return age_ms <= self._stale_ms

    def _ago_ms(self) -> Optional[int]:
        if self._last_update_mono == 0.0:
            return None
        return int((time.monotonic() - self._last_update_mono) * 1000.0)

    def get_latest_eeg(self) -> Optional[EEGData]:
        with self._lock:
            return self._latest_eeg if self._is_fresh() else None

    def get_latest_ppg(self) -> Optional[PPGData]:
        with self._lock:
            return self._latest_ppg if self._is_fresh() else None

    def get_latest_acc(self) -> Optional[ACCData]:
        with self._lock:
            return self._latest_acc if self._is_fresh() else None

    # ── Source facade 생성 ──

    def eeg_source(self) -> "_LinkBandSensorFacade":
        return _LinkBandSensorFacade(
            parent=self, flag=SensorFlags.EEG, name="LinkBandEEG",
            pull=self.get_latest_eeg,
        )

    def ppg_source(self) -> "_LinkBandSensorFacade":
        return _LinkBandSensorFacade(
            parent=self, flag=SensorFlags.PPG, name="LinkBandPPG",
            pull=self.get_latest_ppg,
        )

    def acc_source(self) -> "_LinkBandSensorFacade":
        return _LinkBandSensorFacade(
            parent=self, flag=SensorFlags.ACC, name="LinkBandACC",
            pull=self.get_latest_acc,
        )


class _LinkBandSensorFacade(SensorSource):
    """LinkBandReceiver의 단일 센서 슬롯을 SensorSource로 노출하는 facade.

    parent가 실제 start/stop/수신을 담당하므로 이 facade의 start/stop은
    멱등적으로 parent에 위임(중복 start 허용).
    """

    def __init__(
        self,
        parent: LinkBandReceiver,
        flag: SensorFlags,
        name: str,
        pull,
    ) -> None:
        self._parent = parent
        self._flag = flag
        self._name = name
        self._pull = pull

    @property
    def sensor_flag(self) -> SensorFlags:
        return self._flag

    @property
    def name(self) -> str:
        return self._name

    @property
    def is_running(self) -> bool:
        return self._parent.is_running

    def start(self) -> None:
        if not self._parent.is_running:
            self._parent.start()

    def stop(self) -> None:
        # parent는 여러 facade가 공유하므로 여기선 stop하지 않는다.
        # 명시적 종료는 LinkBandReceiver.stop()을 직접 호출.
        pass

    def get_latest(self):
        return self._pull()
