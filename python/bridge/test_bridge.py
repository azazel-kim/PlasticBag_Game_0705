"""
Bridge 모듈 검증 테스트

실행 방법 (프로젝트 루트에서):
    python3 -m bridge.test_bridge
또는:
    cd python && python3 -m bridge.test_bridge
"""

from __future__ import annotations

import struct
import time

from bridge import (
    FusedDataFrame, BlendShapeData, EyeTrackingData, EEGData, PPGData, ACCData,
    PoseData, IMUData, SensorFlags, RingBuffer, TimeSyncAligner, SensorType,
    PacketHeader, PacketType, UdpSender, UdpReceiver,
    FUSED_FRAME_PORT, ENGAGEMENT_PORT, CONTROL_PORT,
)
from bridge.udp_protocol import ClockSyncPayload, HEADER_SIZE


def test_sensor_data_sizes() -> None:
    """각 센서 데이터 직렬화 크기 검증."""
    assert len(BlendShapeData().to_bytes()) == 284, "BlendShape 크기 불일치"
    assert len(EyeTrackingData().to_bytes()) == 33, "EyeTracking 크기 불일치"
    assert len(EEGData().to_bytes()) == 30, "EEG 크기 불일치"
    assert len(PPGData().to_bytes()) == 8, "PPG 크기 불일치"
    assert len(ACCData().to_bytes()) == 12, "ACC 크기 불일치"
    assert len(PoseData().to_bytes()) == 528, "Pose 크기 불일치"
    assert len(IMUData().to_bytes()) == 52, "IMU 크기 불일치"
    print("[PASS] 모든 센서 데이터 크기 검증 통과")


def test_fused_frame_roundtrip() -> None:
    """FusedDataFrame 직렬화/역직렬화 왕복 테스트."""
    frame = FusedDataFrame(
        blendshape=BlendShapeData(weights=[0.5] * 68, confidence=[0.9, 0.8, 0.85]),
        eye_tracking=EyeTrackingData(left_rotation=[0.1, 0.2, 0.3, 0.9], blink_flags=0x03),
        eeg=EEGData(channels=[100.0, 200.0, 0, 0, 0, 0], contact_quality=[255, 200, 0, 0, 0, 0]),
        ppg=PPGData(ir=1234.5, red=6789.0),
        acc=ACCData(x=0.01, y=-9.81, z=0.02),
        pose=PoseData(landmarks=[0.5] * 132),
        imu=IMUData(quaternion=[0, 0, 0, 1], acceleration=[0, 9.81, 0]),
        valid_sensors=SensorFlags.ALL,
        timestamp_ms=1713300000000,
    )
    data = frame.to_bytes()
    assert len(data) == 956, f"FusedDataFrame 크기 불일치: {len(data)}"

    restored = FusedDataFrame.from_bytes(data)
    assert restored.valid_sensors == SensorFlags.ALL
    assert restored.timestamp_ms == 1713300000000
    assert restored.valid_sensor_count == 7
    assert abs(restored.ppg.ir - 1234.5) < 0.01
    assert abs(restored.acc.y - (-9.81)) < 0.01
    assert abs(restored.blendshape.weights[0] - 0.5) < 0.01
    assert restored.eye_tracking.blink_flags == 0x03
    print("[PASS] FusedDataFrame round-trip 검증 통과")


def test_partial_frame() -> None:
    """일부 센서만 유효한 프레임 테스트."""
    partial = FusedDataFrame(
        eeg=EEGData(channels=[50.0, 80.0, 0, 0, 0, 0]),
        ppg=PPGData(ir=500.0, red=300.0),
        valid_sensors=SensorFlags.EEG | SensorFlags.PPG,
        timestamp_ms=1713300000000,
    )
    data = partial.to_bytes()
    assert len(data) == 956

    restored = FusedDataFrame.from_bytes(data)
    assert restored.valid_sensor_count == 2
    assert restored.eeg is not None
    assert restored.ppg is not None
    assert restored.blendshape is None
    assert restored.imu is None
    print("[PASS] Partial frame (2/7 센서) round-trip 통과")


def test_packet_header_roundtrip() -> None:
    """PacketHeader 직렬화/역직렬화 왕복 테스트."""
    hdr = PacketHeader(
        packet_type=PacketType.FUSED_FRAME,
        sequence_num=42,
        timestamp_ms=1713300000000,
        payload_len=956,
    )
    hdr_bytes = hdr.encode()
    assert len(hdr_bytes) == HEADER_SIZE, f"헤더 크기 불일치: {len(hdr_bytes)}"

    hdr_restored = PacketHeader.decode(hdr_bytes)
    assert hdr_restored.packet_type == PacketType.FUSED_FRAME
    assert hdr_restored.sequence_num == 42
    assert hdr_restored.timestamp_ms == 1713300000000
    assert hdr_restored.payload_len == 956
    print("[PASS] PacketHeader round-trip 검증 통과")


def test_full_packet_size() -> None:
    """전체 패킷 크기 검증 (Header + FusedFrame)."""
    frame = FusedDataFrame(valid_sensors=SensorFlags.NONE, timestamp_ms=0)
    hdr = PacketHeader(
        packet_type=PacketType.FUSED_FRAME,
        payload_len=FusedDataFrame.PACKED_SIZE,
    )
    full = hdr.encode() + frame.to_bytes()
    assert len(full) == 20 + 956, f"전체 패킷 크기: {len(full)}"
    print(f"[PASS] 전체 패킷 크기: {len(full)} bytes (header=20 + payload=956)")


def test_clock_sync_payload() -> None:
    """ClockSync 페이로드 테스트."""
    cs = ClockSyncPayload(t1_ms=1713300000000, t2_ms=1713300000050)
    data = cs.encode()
    assert len(data) == 16

    restored = ClockSyncPayload.decode(data)
    assert restored.t1_ms == 1713300000000
    assert restored.t2_ms == 1713300000050
    print("[PASS] ClockSync 페이로드 round-trip 통과")


def test_ring_buffer() -> None:
    """RingBuffer 기본 동작 테스트."""
    buf: RingBuffer[float] = RingBuffer(capacity=4)
    assert buf.is_empty

    buf.push(1.0, 100)
    buf.push(2.0, 200)
    buf.push(3.0, 300)
    buf.push(4.0, 400)
    buf.push(5.0, 500)  # 1.0 덮어씀 (capacity=4)

    # get_nearest: 310에 가장 가까운 건 300
    result = buf.get_nearest(310, max_drift_ms=50)
    assert result is not None
    assert result[0] == 3.0 and result[1] == 300

    # get_latest: 가장 최근 = 5.0
    latest = buf.get_latest()
    assert latest is not None and latest[0] == 5.0

    # stale: 1.0은 이미 덮어써짐, 100ms 근처에 데이터 없음
    stale = buf.get_nearest(100, max_drift_ms=50)
    assert stale is None, "stale data는 None이어야 함"

    # get_range: 200~400 범위
    ranged = buf.get_range(200, 400)
    assert len(ranged) == 3
    assert ranged[0][1] == 200
    assert ranged[2][1] == 400

    print("[PASS] RingBuffer 전체 테스트 통과")


def test_time_sync_aligner() -> None:
    """TimeSyncAligner 보정 테스트."""
    aligner = TimeSyncAligner(max_drift_ms=100)
    now_ms = int(time.time() * 1000)

    # BLE 지연 35ms 보정: 센서가 35ms 늦게 찍힌 타임스탬프
    raw = now_ms + 35
    aligned = aligner.align_timestamp(raw, SensorType.EEG, reference_ms=now_ms)
    assert aligned is not None, "BLE 보정 후 유효해야 함"
    assert abs(aligned - now_ms) <= 5, f"보정 오차 과대: {abs(aligned - now_ms)}ms"
    print(f"[PASS] TimeSyncAligner BLE 보정 통과 (drift={abs(aligned - now_ms)}ms)")

    # stale 테스트: 200ms 오래된 데이터
    stale_raw = now_ms - 200
    stale_result = aligner.align_timestamp(stale_raw, SensorType.CAMERA, reference_ms=now_ms)
    assert stale_result is None, "stale data는 None이어야 함"
    print("[PASS] TimeSyncAligner stale data 처리 통과")

    # 드리프트 통계 확인
    stats = aligner.get_drift_stats(SensorType.CAMERA)
    assert stats["stale_count"] == 1
    assert stats["total_samples"] == 1
    print("[PASS] TimeSyncAligner 드리프트 통계 통과")


def test_c_sharp_byte_compatibility() -> None:
    """C# System.BitConverter와 바이트 호환성 검증.

    Little-endian float32/int64가 C# 쪽과 동일한지 확인.
    C# BitConverter.GetBytes(1234.5f) = [0x00, 0x50, 0x9A, 0x44]
    C# BitConverter.GetBytes((long)1713300000000) = [0x00, 0xC0, 0x88, 0xD4, 0x8E, 0x01, 0x00, 0x00]
    """
    # float32: 1234.5
    f_bytes = struct.pack("<f", 1234.5)
    assert f_bytes == bytes([0x00, 0x50, 0x9A, 0x44]), (
        f"float32 바이트 불일치: {f_bytes.hex()}"
    )

    # int64: 1713300000000
    i_bytes = struct.pack("<q", 1713300000000)
    assert i_bytes == bytes([0x00, 0x4D, 0xA3, 0xE8, 0x8E, 0x01, 0x00, 0x00]), (
        f"int64 바이트 불일치: {i_bytes.hex()}"
    )

    # Magic "FUSE"
    m_bytes = struct.pack("<4s", b"FUSE")
    assert m_bytes == b"FUSE"

    print("[PASS] C# BitConverter 바이트 호환성 통과")


if __name__ == "__main__":
    test_sensor_data_sizes()
    test_fused_frame_roundtrip()
    test_partial_frame()
    test_packet_header_roundtrip()
    test_full_packet_size()
    test_clock_sync_payload()
    test_ring_buffer()
    test_time_sync_aligner()
    test_c_sharp_byte_compatibility()
    print()
    print("=== 전체 검증 완료: 모든 테스트 통과 ===")
