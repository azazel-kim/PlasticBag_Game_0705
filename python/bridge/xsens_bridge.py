"""
X-Sens MVN Awinda UDP 브릿지 — 센서 데이터 수신 및 IMU 추출

MVN Analyze 소프트웨어가 UDP 9763 포트로 스트리밍하는 23개 세그먼트 데이터를
수신하여 IMUData 형식으로 추출, FusedDataFrame에 통합하는 브릿지.

설치:
    # 현재는 시뮬레이션 모드만 지원
    # 실제 X-Sens SDK: Xsens DOT 또는 MVN SDK 필요 (별도 라이센스)

사용 예시::

    from bridge.xsens_bridge import XSensBridge

    # 시뮬레이션 모드 (Mac/Linux용 테스트)
    bridge = XSensBridge(
        mode="simulation",
        imu_count=17,
        update_rate=60,  # Hz
    )
    bridge.start()
    # ... 약 10초 실행 ...
    imu_data = bridge.get_latest()
    stats = bridge.stop()
    print(stats)

    # 실제 UDP 수신 모드 (Windows RTX 4080 + MVN Analyze)
    bridge = XSensBridge(
        mode="udp",
        udp_port=9763,
        imu_count=17,
    )
    bridge.start()
    imu_data = bridge.get_latest()
    stats = bridge.stop()
"""

from __future__ import annotations

import logging
import threading
import time
from dataclasses import dataclass
from enum import Enum
from typing import Optional
import random
import struct

from .fused_data_frame import IMUData

logger = logging.getLogger(__name__)


# ─────────────────────────────────────────────
# 설정 및 상수
# ─────────────────────────────────────────────

class XSensMode(Enum):
    """X-Sens 브릿지 동작 모드."""
    SIMULATION = "simulation"  # 시뮬레이션 (테스트용)
    UDP = "udp"               # 실제 UDP 수신 (MVN Analyze)


@dataclass
class XSensConfig:
    """X-Sens 브릿지 설정."""
    # 동작 모드
    mode: XSensMode = XSensMode.SIMULATION

    # UDP 설정 (mode=UDP일 때)
    udp_port: int = 9763
    listen_address: str = "0.0.0.0"

    # 센서 구성
    imu_count: int = 17  # MVN Awinda는 17개 IMU (23 세그먼트)
    update_rate: int = 60  # Hz (MVN Awinda는 60Hz)

    # 시뮬레이션 설정
    sim_noise_level: float = 0.01  # 시뮬 노이즈 (0.01 = 1% 변동)
    sim_drift_per_sample: float = 0.001  # 드리프트 누적


# ─────────────────────────────────────────────
# X-Sens 브릿지
# ─────────────────────────────────────────────

class XSensBridge:
    """X-Sens MVN Awinda → IMUData 추출 브릿지.

    매개변수:
        mode: "simulation" (테스트) 또는 "udp" (실제 센서)
        udp_port: UDP 포트 (기본 9763)
        imu_count: IMU 센서 개수 (기본 17)
        update_rate: 업데이트 레이트 Hz (기본 60)
        config: XSensConfig 객체 (None이면 기본 설정)

    라이프사이클:
        bridge = XSensBridge(mode="simulation")
        bridge.start()
        # ... 센서 데이터 수신 ...
        imu = bridge.get_latest()
        stats = bridge.stop()
    """

    def __init__(
        self,
        mode: str = "simulation",
        udp_port: int = 9763,
        imu_count: int = 17,
        update_rate: int = 60,
        config: Optional[XSensConfig] = None,
    ) -> None:
        self._config = config or XSensConfig(
            mode=XSensMode(mode),
            udp_port=udp_port,
            imu_count=imu_count,
            update_rate=update_rate,
        )

        # UDP 수신 소켓 (UDP 모드 전용)
        self._udp_socket = None

        # 실행 제어
        self._running = threading.Event()
        self._thread: Optional[threading.Thread] = None

        # 최신 IMU 데이터 (스레드 안전)
        self._latest_imu = IMUData()
        self._imu_lock = threading.Lock()

        # 통계
        self._sample_count = 0
        self._update_failures = 0
        self._start_time = 0.0

        # 시뮬레이션 상태
        self._sim_quaternion = [0.0, 0.0, 0.0, 1.0]  # [x, y, z, w]
        self._sim_acceleration = [0.0, 9.81, 0.0]    # [x, y, z]

        logger.info(
            "[XSensBridge] 초기화 — mode=%s, imu_count=%d, update_rate=%d",
            self._config.mode.value,
            self._config.imu_count,
            self._config.update_rate,
        )

    def start(self) -> None:
        """X-Sens 수신 스레드 시작."""
        if self._running.is_set():
            logger.warning("[XSensBridge] 이미 실행 중")
            return

        if self._config.mode == XSensMode.UDP:
            self._init_udp_socket()

        self._running.set()
        self._start_time = time.time()
        self._thread = threading.Thread(
            target=self._process_loop,
            name="XSensBridge",
            daemon=True,
        )
        self._thread.start()
        logger.info(
            "[XSensBridge] 시작 — mode=%s",
            self._config.mode.value,
        )

    def stop(self) -> dict:
        """프로세스 스레드 중지 및 통계 반환."""
        self._running.clear()

        if self._thread is not None:
            self._thread.join(timeout=5.0)
            self._thread = None

        if self._udp_socket is not None:
            try:
                self._udp_socket.close()
            except Exception:
                pass
            self._udp_socket = None

        elapsed = time.time() - self._start_time
        stats = {
            "elapsed_sec": elapsed,
            "total_samples": self._sample_count,
            "update_failures": self._update_failures,
            "effective_rate_hz": self._sample_count / elapsed if elapsed > 0 else 0,
        }

        logger.info(
            "[XSensBridge] 중지 — %d 샘플, %.1f Hz, 실패 %d",
            self._sample_count,
            stats["effective_rate_hz"],
            self._update_failures,
        )

        return stats

    def get_latest(self) -> IMUData:
        """최신 IMU 데이터 반환 (스레드 안전)."""
        with self._imu_lock:
            return self._latest_imu

    # ── 내부 ──

    def _init_udp_socket(self) -> None:
        """UDP 소켓 초기화."""
        import socket
        self._udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self._udp_socket.bind((self._config.listen_address, self._config.udp_port))
        self._udp_socket.settimeout(1.0)  # 1초 타임아웃
        logger.info(
            "[XSensBridge] UDP 바인드 — %s:%d",
            self._config.listen_address,
            self._config.udp_port,
        )

    def _process_loop(self) -> None:
        """센서 데이터 수신 루프."""
        while self._running.is_set():
            try:
                if self._config.mode == XSensMode.SIMULATION:
                    self._process_simulation()
                elif self._config.mode == XSensMode.UDP:
                    self._process_udp()

                self._sample_count += 1

                # 레이트 제어
                frame_time = 1.0 / self._config.update_rate
                time.sleep(frame_time * 0.8)

            except Exception as e:
                self._update_failures += 1
                logger.debug("[XSensBridge] 루프 에러: %s", e)
                continue

    def _process_simulation(self) -> None:
        """시뮬레이션 모드: 랜덤 IMU 데이터 생성."""
        # 시뮬 드리프트: 쿼터니언에 작은 회전 추가
        angle = self._config.sim_drift_per_sample
        drift_quat = [
            random.gauss(0, angle * 0.1),
            random.gauss(0, angle * 0.1),
            random.gauss(0, angle * 0.1),
            1.0 - (angle ** 2),
        ]

        # 간단한 쿼터니언 곱 (정규화 생략, 데모용)
        self._sim_quaternion = [
            self._sim_quaternion[0] + drift_quat[0] * 0.01,
            self._sim_quaternion[1] + drift_quat[1] * 0.01,
            self._sim_quaternion[2] + drift_quat[2] * 0.01,
            self._sim_quaternion[3],
        ]

        # 가속도: 중력(0, 9.81, 0) + 노이즈
        acc = [
            self._sim_acceleration[0] + random.gauss(0, self._config.sim_noise_level),
            self._sim_acceleration[1] + random.gauss(0, self._config.sim_noise_level),
            self._sim_acceleration[2] + random.gauss(0, self._config.sim_noise_level),
        ]

        imu = IMUData(
            quaternion=self._sim_quaternion,
            acceleration=acc,
        )

        with self._imu_lock:
            self._latest_imu = imu

    def _process_udp(self) -> None:
        """UDP 모드: MVN Analyze에서 데이터 수신 및 파싱.

        Note: 실제 구현은 MVN SDK 또는 프로토콜 스펙 필요.
        현재는 스텁만 제공.
        """
        try:
            data, addr = self._udp_socket.recvfrom(4096)
            # TODO: MVN 프로토콜 파싱
            # Header: "MXTP##" + counters + timestamp + charID
            # Body: 23 segments × (Position xyz + Quaternion wxyz)
            logger.debug(
                "[XSensBridge] UDP 수신 — %d bytes from %s",
                len(data),
                addr,
            )
        except Exception as e:
            logger.debug("[XSensBridge] UDP 수신 실패: %s", e)

    # ── 디버그 ──

    def print_latest(self) -> None:
        """최신 IMU 데이터 출력 (디버깅용)."""
        imu = self.get_latest()
        print(f"[XSensBridge] Latest IMU:")
        print(f"  Quaternion: {imu.quaternion}")
        print(f"  Acceleration: {imu.acceleration}")


# ─────────────────────────────────────────────
# CLI 인터페이스
# ─────────────────────────────────────────────

def main() -> None:
    """커맨드라인 실행 예시."""
    import argparse

    parser = argparse.ArgumentParser(
        description="X-Sens MVN Awinda → IMUData 브릿지",
    )
    parser.add_argument(
        "--mode",
        choices=["simulation", "udp"],
        default="simulation",
        help="브릿지 모드 (기본 simulation)",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=9763,
        help="UDP 포트 (기본 9763)",
    )
    parser.add_argument(
        "--duration",
        type=int,
        default=10,
        help="실행 시간 (초, 기본 10)",
    )

    args = parser.parse_args()

    # 로깅 설정
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
    )

    # 브릿지 실행
    bridge = XSensBridge(mode=args.mode, udp_port=args.port)

    try:
        bridge.start()
        logger.info("브릿지 실행 중... %d초 후 종료됩니다.", args.duration)

        # 샘플 출력
        for i in range(min(3, args.duration)):
            time.sleep(1.0)
            bridge.print_latest()

        time.sleep(args.duration - min(3, args.duration))

    except KeyboardInterrupt:
        logger.info("사용자 중단 신호(Ctrl+C) 감지")
    finally:
        stats = bridge.stop()
        print("\n=== 최종 통계 ===")
        print(f"경과 시간: {stats['elapsed_sec']:.2f}초")
        print(f"총 샘플: {stats['total_samples']}")
        print(f"유효 레이트: {stats['effective_rate_hz']:.2f} Hz")
        print(f"수신 실패: {stats['update_failures']}")


if __name__ == "__main__":
    main()
