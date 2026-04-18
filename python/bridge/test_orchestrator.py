"""
test_orchestrator — BridgeOrchestrator self-loopback 검증

시나리오:
    1. FusionPipeline을 127.0.0.1:9001 수신기로 기동 (로컬호스트 self-loop)
    2. BridgeOrchestrator에 Mock 4종(EEG/PPG/ACC/IMU) 등록
    3. 1초간 30Hz 송신
    4. 수신측에서 29~31 프레임 수신 확인
    5. 마지막 프레임의 valid_sensors == EEG|PPG|ACC|IMU 확인
    6. 각 센서 페이로드가 None이 아닌지 확인

실행: python3 -m bridge.test_orchestrator
"""

from __future__ import annotations

import sys
import time

from .fused_data_frame import FusedDataFrame, SensorFlags
from .fusion_pipeline import FusionPipeline
from .mock_sensors import (
    MockAccSource,
    MockEegSource,
    MockImuSource,
    MockPpgSource,
)
from .orchestrator import BridgeOrchestrator
from .udp_protocol import UdpSender, FUSED_FRAME_PORT


def test_self_loopback_30hz(duration_sec: float = 1.0) -> bool:
    """Mock 4종 → Orchestrator → self-loop 수신 확인."""
    received: list[FusedDataFrame] = []

    # 1) Receiver 기동
    receiver = FusionPipeline(bind_ip="127.0.0.1", bind_port=FUSED_FRAME_PORT)
    receiver.on_frame_received = lambda f: received.append(f)
    receiver.start()

    try:
        # 2) Sender 구성
        sender = UdpSender(target_ip="127.0.0.1")
        orchestrator = BridgeOrchestrator(
            sender=sender,
            fusion_rate_hz=30.0,
            heartbeat_hz=0.0,  # 테스트 중엔 heartbeat 비활성
        )
        orchestrator.add_source(MockEegSource(sampling_hz=250.0, num_channels=2))
        orchestrator.add_source(MockPpgSource(sampling_hz=50.0))
        orchestrator.add_source(MockAccSource(sampling_hz=25.0))
        orchestrator.add_source(MockImuSource(sampling_hz=60.0))

        # 3) 가동
        orchestrator.start()
        # Mock 소스가 첫 샘플 생성하도록 잠깐 대기
        time.sleep(0.15)
        # 이 시점부터 측정
        start = time.monotonic()
        time.sleep(duration_sec)
        elapsed = time.monotonic() - start
        orchestrator.stop()

    finally:
        # 수신기가 in-flight 패킷 처리하도록 짧게 대기 후 종료
        time.sleep(0.1)
        receiver.stop()

    # 4) 수신 프레임 수 검증
    # (orchestrator가 1.15초 가량 동작했지만 "측정 구간"만 비교)
    frames_in_window = len(received)
    expected_min = int(30 * (duration_sec + 0.1) * 0.9)  # 10% 마진
    expected_max = int(30 * (duration_sec + 0.3) * 1.1)
    print(f"수신 프레임: {frames_in_window}개, 실측 구간: {elapsed:.2f}s")
    if not (expected_min <= frames_in_window <= expected_max):
        print(f"[FAIL] 프레임 수 범위 외 (기대 {expected_min}~{expected_max})")
        return False

    # 5) valid_sensors 검증 — 마지막 프레임
    last = received[-1]
    expected_flags = (
        SensorFlags.EEG | SensorFlags.PPG | SensorFlags.ACC | SensorFlags.IMU
    )
    if last.valid_sensors != expected_flags:
        print(f"[FAIL] valid_sensors 불일치: 기대={expected_flags.name}, 실제={last.valid_sensors.name}")
        return False

    # 6) 페이로드 존재 확인
    if last.eeg is None or last.ppg is None or last.acc is None or last.imu is None:
        print("[FAIL] 센서 페이로드 중 None 존재")
        return False

    # 7) EEG 신호 실제 변동 확인 (Mock이 꺼져있지 않은지)
    if abs(last.eeg.channels[0]) < 1e-6 and abs(last.eeg.channels[1]) < 1e-6:
        print("[FAIL] EEG 채널이 모두 0 — Mock 미동작 의심")
        return False

    # 8) 타임스탬프 단조 증가 검증
    timestamps = [f.timestamp_ms for f in received]
    if timestamps != sorted(timestamps):
        print("[FAIL] 타임스탬프가 단조 증가하지 않음")
        return False

    print("[PASS] self-loopback 30Hz 검증 통과")
    print(f"  - 프레임 수: {frames_in_window}")
    print(f"  - valid_sensors: {last.valid_sensors.name}")
    print(f"  - EEG ch0: {last.eeg.channels[0]:.2f} µV, ch1: {last.eeg.channels[1]:.2f} µV")
    print(f"  - PPG IR: {last.ppg.ir:.1f}, Red: {last.ppg.red:.1f}")
    print(f"  - ACC: ({last.acc.x:.3f}, {last.acc.y:.3f}, {last.acc.z:.3f})")
    print(f"  - IMU quat: {last.imu.quaternion}")
    return True


def test_add_source_after_start_raises() -> bool:
    """start() 이후 add_source 시 RuntimeError."""
    sender = UdpSender(target_ip="127.0.0.1")
    orch = BridgeOrchestrator(sender=sender, fusion_rate_hz=30.0, heartbeat_hz=0.0)
    orch.add_source(MockEegSource(sampling_hz=250.0))
    try:
        orch.start()
        try:
            orch.add_source(MockPpgSource(sampling_hz=50.0))
        except RuntimeError:
            print("[PASS] 실행 중 add_source → RuntimeError 정상")
            return True
        else:
            print("[FAIL] 실행 중 add_source에서 예외 미발생")
            return False
    finally:
        orch.stop()


def test_no_sources_raises() -> bool:
    """등록된 소스 없을 때 start 시 RuntimeError."""
    sender = UdpSender(target_ip="127.0.0.1")
    orch = BridgeOrchestrator(sender=sender, fusion_rate_hz=30.0, heartbeat_hz=0.0)
    try:
        orch.start()
    except RuntimeError:
        print("[PASS] 소스 0개 start → RuntimeError 정상")
        return True
    else:
        orch.stop()
        print("[FAIL] 소스 0개에서 start가 성공")
        return False


def main() -> int:
    results = [
        ("self-loopback 30Hz", test_self_loopback_30hz()),
        ("add_source after start", test_add_source_after_start_raises()),
        ("no sources", test_no_sources_raises()),
    ]
    print("\n=== Orchestrator 테스트 요약 ===")
    all_pass = True
    for name, passed in results:
        mark = "PASS" if passed else "FAIL"
        print(f"  [{mark}] {name}")
        all_pass = all_pass and passed
    return 0 if all_pass else 1


if __name__ == "__main__":
    sys.exit(main())
