"""
FusionPipeline 통합 테스트

실행 방법 (프로젝트 루트에서):
    cd python && python3 -m bridge.test_pipeline
"""

from __future__ import annotations

import socket
import struct
import threading
import time
from typing import Optional

from bridge import (
    FusedDataFrame, EEGData, PPGData, ACCData, IMUData,
    SensorFlags, RingBuffer, SensorType,
    PacketHeader, PacketType, UdpSender,
    FUSED_FRAME_PORT,
)
from bridge.fusion_pipeline import FusionPipeline, PipelineStats
from bridge.udp_protocol import HEADER_SIZE


# ─────────────────────────────────────────────
# 헬퍼: 모의 FusedDataFrame UDP 패킷 전송
# ─────────────────────────────────────────────

def _make_test_frame(
    timestamp_ms: int,
    valid: SensorFlags = SensorFlags.EEG | SensorFlags.PPG,
) -> FusedDataFrame:
    """테스트용 FusedDataFrame 생성."""
    frame = FusedDataFrame(
        eeg=EEGData(channels=[100.0, 200.0, 0, 0, 0, 0]) if valid & SensorFlags.EEG else None,
        ppg=PPGData(ir=1234.5, red=6789.0) if valid & SensorFlags.PPG else None,
        acc=ACCData(x=0.01, y=-9.81, z=0.02) if valid & SensorFlags.ACC else None,
        imu=IMUData(quaternion=[0, 0, 0, 1]) if valid & SensorFlags.IMU else None,
        valid_sensors=valid,
        timestamp_ms=timestamp_ms,
    )
    return frame


def _send_frame_udp(
    frame: FusedDataFrame,
    port: int,
    seq: int = 0,
    target_ip: str = "127.0.0.1",
) -> None:
    """FusedDataFrame을 UDP 패킷으로 직접 전송."""
    payload = frame.to_bytes()
    header = PacketHeader(
        packet_type=PacketType.FUSED_FRAME,
        sequence_num=seq,
        timestamp_ms=frame.timestamp_ms,
        payload_len=len(payload),
    )
    packet = header.encode() + payload
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        sock.sendto(packet, (target_ip, port))
    finally:
        sock.close()


# ─────────────────────────────────────────────
# 테스트 1: PipelineStats 기본 동작
# ─────────────────────────────────────────────

def test_pipeline_stats() -> None:
    """PipelineStats 속성 계산 검증."""
    stats = PipelineStats()
    assert stats.elapsed_sec == 0.0
    assert stats.receive_rate_hz == 0.0
    assert stats.drop_rate == 0.0

    stats.start_time = time.monotonic() - 1.0  # 1초 전 시작
    stats.frames_received = 30
    stats.frames_aligned = 28
    stats.frames_dropped = 2

    assert abs(stats.receive_rate_hz - 30.0) < 2.0  # ~30Hz (타이밍 오차 허용)
    assert abs(stats.drop_rate - 2 / 30) < 0.01

    d = stats.to_dict()
    assert "frames_received" in d
    assert "receive_rate_hz" in d
    print("[PASS] PipelineStats 기본 동작 검증 통과")


# ─────────────────────────────────────────────
# 테스트 2: FusionPipeline 초기화 / start / stop
# ─────────────────────────────────────────────

def test_pipeline_lifecycle() -> None:
    """파이프라인 start/stop 라이프사이클 검증."""
    # 테스트용 임시 포트 (충돌 방지)
    port = _find_free_port()
    pipeline = FusionPipeline(bind_port=port)

    assert not pipeline.is_running
    pipeline.start()
    assert pipeline.is_running

    # 중복 start — 경고만 출력, 에러 없음
    pipeline.start()
    assert pipeline.is_running

    pipeline.stop()
    assert not pipeline.is_running
    print("[PASS] 파이프라인 라이프사이클 검증 통과")


# ─────────────────────────────────────────────
# 테스트 3: UDP 패킷 수신 → 콜백 호출
# ─────────────────────────────────────────────

def test_frame_reception() -> None:
    """모의 UDP 패킷 전송 → FusionPipeline 수신 → 콜백 확인."""
    port = _find_free_port()
    pipeline = FusionPipeline(bind_port=port, max_drift_ms=5000)

    received_frames: list[FusedDataFrame] = []
    event = threading.Event()

    def on_frame(frame: FusedDataFrame) -> None:
        received_frames.append(frame)
        if len(received_frames) >= 3:
            event.set()

    pipeline.on_frame_received = on_frame
    pipeline.start()

    try:
        now_ms = int(time.time() * 1000)
        for i in range(3):
            frame = _make_test_frame(timestamp_ms=now_ms + i * 33)
            _send_frame_udp(frame, port=port, seq=i)

        # 최대 2초 대기
        received = event.wait(timeout=2.0)
        assert received, f"프레임 수신 타임아웃 (받은 수: {len(received_frames)})"
        assert len(received_frames) == 3

        # 프레임 데이터 검증
        f = received_frames[0]
        assert f.valid_sensors & SensorFlags.EEG
        assert f.valid_sensors & SensorFlags.PPG
        assert f.eeg is not None
        assert abs(f.eeg.channels[0] - 100.0) < 0.01

        print(f"[PASS] UDP 프레임 수신 검증 통과 ({len(received_frames)}개)")
    finally:
        pipeline.stop()


# ─────────────────────────────────────────────
# 테스트 4: 센서별 RingBuffer 분배 저장
# ─────────────────────────────────────────────

def test_sensor_buffer_distribution() -> None:
    """수신된 프레임의 센서 데이터가 개별 RingBuffer에 저장되는지 검증."""
    port = _find_free_port()
    pipeline = FusionPipeline(bind_port=port, max_drift_ms=5000)
    event = threading.Event()

    pipeline.on_frame_received = lambda f: event.set()
    pipeline.start()

    try:
        now_ms = int(time.time() * 1000)
        frame = _make_test_frame(
            timestamp_ms=now_ms,
            valid=SensorFlags.EEG | SensorFlags.PPG | SensorFlags.ACC,
        )
        _send_frame_udp(frame, port=port)

        event.wait(timeout=2.0)

        # EEG, PPG, ACC 버퍼에 데이터가 있어야 함
        assert pipeline.get_buffer(SensorType.EEG).count >= 1, "EEG 버퍼 비어있음"
        assert pipeline.get_buffer(SensorType.PPG).count >= 1, "PPG 버퍼 비어있음"
        assert pipeline.get_buffer(SensorType.ACC).count >= 1, "ACC 버퍼 비어있음"

        # IMU 버퍼는 비어있어야 함 (프레임에 IMU 없음)
        assert pipeline.get_buffer(SensorType.IMU).count == 0, "IMU 버퍼에 데이터 있음"

        print("[PASS] 센서별 RingBuffer 분배 저장 검증 통과")
    finally:
        pipeline.stop()


# ─────────────────────────────────────────────
# 테스트 5: 타임스탬프 정렬 검증
# ─────────────────────────────────────────────

def test_timestamp_alignment() -> None:
    """TimeSyncAligner를 통한 타임스탬프 정렬 검증."""
    port = _find_free_port()
    pipeline = FusionPipeline(bind_port=port, max_drift_ms=5000)
    event = threading.Event()

    pipeline.on_frame_received = lambda f: event.set()
    pipeline.start()

    try:
        now_ms = int(time.time() * 1000)
        frame = _make_test_frame(timestamp_ms=now_ms)
        _send_frame_udp(frame, port=port)

        event.wait(timeout=2.0)

        # 정렬된 프레임 통계 확인
        s = pipeline.stats
        assert s["pipeline"]["frames_received"] >= 1
        assert s["pipeline"]["frames_aligned"] >= 1

        # aligner 드리프트 통계 확인
        eeg_stats = pipeline.aligner.get_drift_stats(SensorType.EEG)
        assert eeg_stats["total_samples"] >= 1

        print("[PASS] 타임스탬프 정렬 검증 통과")
    finally:
        pipeline.stop()


# ─────────────────────────────────────────────
# 테스트 6: 동기화 에러 콜백 검증
# ─────────────────────────────────────────────

def test_sync_error_callback() -> None:
    """max_drift 초과 시 on_sync_error 콜백 호출 검증."""
    port = _find_free_port()
    # 매우 작은 drift 허용 → 거의 모든 프레임에서 에러 발생
    pipeline = FusionPipeline(bind_port=port, max_drift_ms=1)

    sync_errors: list[tuple[SensorType, int]] = []
    frame_event = threading.Event()

    def on_error(sensor: SensorType, drift: int) -> None:
        sync_errors.append((sensor, drift))

    def on_frame(frame: FusedDataFrame) -> None:
        frame_event.set()

    pipeline.on_sync_error = on_error
    pipeline.on_frame_received = on_frame
    pipeline.start()

    try:
        # 과거 타임스탬프 (확실히 stale)
        old_ms = int(time.time() * 1000) - 10000  # 10초 전
        frame = _make_test_frame(timestamp_ms=old_ms)
        _send_frame_udp(frame, port=port)

        frame_event.wait(timeout=2.0)
        # 약간의 처리 시간 대기
        time.sleep(0.1)

        assert len(sync_errors) > 0, "동기화 에러 콜백이 호출되지 않음"
        # 에러가 발생한 센서 확인
        error_sensors = {e[0] for e in sync_errors}
        assert SensorType.EEG in error_sensors or SensorType.PPG in error_sensors, (
            f"EEG/PPG 중 하나에서 에러 발생해야 함, 실제: {error_sensors}"
        )

        # 통계에 반영
        s = pipeline.stats
        assert s["pipeline"]["sync_errors"] > 0
        assert s["pipeline"]["frames_dropped"] > 0

        print(f"[PASS] 동기화 에러 콜백 검증 통과 (에러 {len(sync_errors)}건)")
    finally:
        pipeline.stop()


# ─────────────────────────────────────────────
# 테스트 7: get_latest_frame / get_nearest_frame
# ─────────────────────────────────────────────

def test_frame_query() -> None:
    """프레임 버퍼 조회 기능 검증."""
    port = _find_free_port()
    pipeline = FusionPipeline(bind_port=port, max_drift_ms=5000)
    event = threading.Event()

    frame_count = 0

    def on_frame(f: FusedDataFrame) -> None:
        nonlocal frame_count
        frame_count += 1
        if frame_count >= 2:
            event.set()

    pipeline.on_frame_received = on_frame
    pipeline.start()

    try:
        now_ms = int(time.time() * 1000)
        ts1 = now_ms
        ts2 = now_ms + 33

        _send_frame_udp(_make_test_frame(ts1), port=port, seq=0)
        time.sleep(0.05)
        _send_frame_udp(_make_test_frame(ts2), port=port, seq=1)

        event.wait(timeout=2.0)

        # get_latest_frame
        latest = pipeline.get_latest_frame()
        assert latest is not None, "최근 프레임이 없음"
        assert latest.timestamp_ms == ts2, (
            f"최근 프레임 타임스탬프 불일치: {latest.timestamp_ms} != {ts2}"
        )

        # get_nearest_frame
        nearest = pipeline.get_nearest_frame(ts1 + 10, max_drift_ms=50)
        assert nearest is not None, "최근접 프레임이 없음"
        assert nearest.timestamp_ms == ts1, (
            f"최근접 프레임 타임스탬프 불일치: {nearest.timestamp_ms} != {ts1}"
        )

        print("[PASS] 프레임 버퍼 조회 검증 통과")
    finally:
        pipeline.stop()


# ─────────────────────────────────────────────
# 테스트 8: 통계 출력 검증
# ─────────────────────────────────────────────

def test_stats_output() -> None:
    """통합 통계 구조 검증."""
    port = _find_free_port()
    pipeline = FusionPipeline(bind_port=port)
    pipeline.start()

    try:
        s = pipeline.stats
        # 최상위 키 존재 확인
        assert "pipeline" in s
        assert "receiver" in s
        assert "aligner" in s
        assert "buffers" in s

        # pipeline 통계 키
        p = s["pipeline"]
        assert "frames_received" in p
        assert "frames_aligned" in p
        assert "frames_dropped" in p
        assert "receive_rate_hz" in p
        assert "drop_rate" in p

        # buffers: 모든 SensorType 키 존재
        for st in SensorType:
            assert st.value in s["buffers"], f"버퍼에 {st.value} 키 없음"

        print("[PASS] 통계 출력 구조 검증 통과")
    finally:
        pipeline.stop()


# ─────────────────────────────────────────────
# 테스트 9: UdpSender와 연동 수신
# ─────────────────────────────────────────────

def test_with_udp_sender() -> None:
    """UdpSender로 전송 → FusionPipeline 수신 연동 검증."""
    port = _find_free_port()
    pipeline = FusionPipeline(bind_port=port, max_drift_ms=5000)

    received: list[FusedDataFrame] = []
    event = threading.Event()

    def on_frame(frame: FusedDataFrame) -> None:
        received.append(frame)
        event.set()

    pipeline.on_frame_received = on_frame
    pipeline.start()

    sender = UdpSender(
        target_ip="127.0.0.1",
        fused_port=port,
    )

    try:
        now_ms = int(time.time() * 1000)
        frame = _make_test_frame(
            timestamp_ms=now_ms,
            valid=SensorFlags.EEG | SensorFlags.PPG | SensorFlags.IMU,
        )
        sender.send_fused_frame(frame)

        event.wait(timeout=2.0)
        assert len(received) >= 1, "UdpSender 프레임 미수신"

        f = received[0]
        assert f.valid_sensors & SensorFlags.EEG
        assert f.valid_sensors & SensorFlags.PPG
        assert f.valid_sensors & SensorFlags.IMU

        print("[PASS] UdpSender ↔ FusionPipeline 연동 검증 통과")
    finally:
        sender.close()
        pipeline.stop()


# ─────────────────────────────────────────────
# 테스트 10: 다중 프레임 연속 수신 (부하 테스트)
# ─────────────────────────────────────────────

def test_burst_reception() -> None:
    """10개 프레임 연속 전송 → 전부 수신 확인."""
    port = _find_free_port()
    pipeline = FusionPipeline(bind_port=port, max_drift_ms=5000)

    received_count = 0
    count_lock = threading.Lock()
    event = threading.Event()
    target_count = 10

    def on_frame(frame: FusedDataFrame) -> None:
        nonlocal received_count
        with count_lock:
            received_count += 1
            if received_count >= target_count:
                event.set()

    pipeline.on_frame_received = on_frame
    pipeline.start()

    try:
        now_ms = int(time.time() * 1000)
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            for i in range(target_count):
                frame = _make_test_frame(timestamp_ms=now_ms + i * 33)
                payload = frame.to_bytes()
                header = PacketHeader(
                    packet_type=PacketType.FUSED_FRAME,
                    sequence_num=i,
                    timestamp_ms=frame.timestamp_ms,
                    payload_len=len(payload),
                )
                packet = header.encode() + payload
                sock.sendto(packet, ("127.0.0.1", port))
        finally:
            sock.close()

        received = event.wait(timeout=3.0)
        with count_lock:
            final_count = received_count

        assert final_count >= target_count, (
            f"부하 테스트 실패: {final_count}/{target_count} 수신"
        )

        s = pipeline.stats
        assert s["pipeline"]["frames_received"] >= target_count

        print(f"[PASS] 다중 프레임 연속 수신 검증 통과 ({final_count}/{target_count})")
    finally:
        pipeline.stop()


# ─────────────────────────────────────────────
# 유틸: 빈 포트 찾기
# ─────────────────────────────────────────────

def _find_free_port() -> int:
    """OS가 할당한 빈 UDP 포트 반환."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("127.0.0.1", 0))
    port = sock.getsockname()[1]
    sock.close()
    return port


# ─────────────────────────────────────────────
# 실행
# ─────────────────────────────────────────────

if __name__ == "__main__":
    test_pipeline_stats()
    test_pipeline_lifecycle()
    test_frame_reception()
    test_sensor_buffer_distribution()
    test_timestamp_alignment()
    test_sync_error_callback()
    test_frame_query()
    test_stats_output()
    test_with_udp_sender()
    test_burst_reception()
    print()
    print("=== FusionPipeline 전체 검증 완료: 모든 테스트 통과 ===")
