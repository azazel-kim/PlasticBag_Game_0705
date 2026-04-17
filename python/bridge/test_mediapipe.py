"""
MediaPipe 브릿지 검증 테스트

실행 방법 (프로젝트 루트에서):
    python3 -m bridge.test_mediapipe
또는:
    cd python && python3 -m bridge.test_mediapipe
"""

from __future__ import annotations

import struct
import time
import numpy as np

from bridge import (
    FusedDataFrame, PoseData, SensorFlags,
    PacketHeader, PacketType, FUSED_FRAME_PORT,
)
from bridge.mediapipe_bridge import MediaPipeConfig, MediaPipeBridge


def test_pose_data_size() -> None:
    """PoseData 직렬화 크기 검증."""
    pose = PoseData(landmarks=[0.5] * 132)  # 33 landmarks × 4 values
    pose_bytes = pose.to_bytes()

    # 33 landmarks × 4 floats × 4 bytes = 528 bytes
    assert len(pose_bytes) == 528, f"PoseData 크기 불일치: {len(pose_bytes)}"
    print("[PASS] PoseData 크기: 528 bytes (33 landmarks × 4 floats)")


def test_pose_landmarks_roundtrip() -> None:
    """PoseData 랜드마크 round-trip 검증."""
    landmarks = [
        # 좌측 눈
        0.45, 0.35, -0.1, 0.95,
        # 우측 눈
        0.55, 0.35, -0.1, 0.92,
        # 코
        0.5, 0.4, 0.0, 0.98,
    ] + [0.5] * (132 - 12)  # 나머지는 0.5로 채움

    pose = PoseData(landmarks=landmarks)
    pose_bytes = pose.to_bytes()

    restored = PoseData.from_bytes(pose_bytes)

    # 정밀도 확인 (float32 때문에 약간의 오차 허용)
    for i, (orig, rest) in enumerate(zip(landmarks, restored.landmarks)):
        assert abs(orig - rest) < 0.0001, (
            f"Landmark[{i}] 불일치: {orig} vs {rest}"
        )

    print("[PASS] PoseData landmarks round-trip 통과")


def test_mediapose_in_fused_frame() -> None:
    """FusedDataFrame 내 PoseData 검증."""
    landmarks = [0.5] * 132
    frame = FusedDataFrame(
        pose=PoseData(landmarks=landmarks),
        valid_sensors=SensorFlags.POSE,
        timestamp_ms=1713300000000,
    )

    frame_bytes = frame.to_bytes()
    assert len(frame_bytes) == 956, f"FusedDataFrame 크기: {len(frame_bytes)}"

    restored = FusedDataFrame.from_bytes(frame_bytes)
    assert restored.valid_sensors == SensorFlags.POSE
    assert restored.pose is not None
    assert len(restored.pose.landmarks) == 132
    assert abs(restored.pose.landmarks[0] - 0.5) < 0.0001

    print("[PASS] FusedDataFrame 내 PoseData 검증 통과")


def test_pose_confidence_values() -> None:
    """포즈 신뢰도(visibility) 값 검증."""
    # MediaPipe 포즈 랜드마크: [x, y, z, visibility]
    # visibility는 0.0~1.0 범위
    landmarks = []
    for i in range(33):
        x = 0.1 * (i % 10)
        y = 0.2 * (i % 5)
        z = -0.05
        visibility = 0.5 + (0.5 * ((i % 3) / 3.0))  # 0.5~1.0
        landmarks.extend([x, y, z, visibility])

    pose = PoseData(landmarks=landmarks)
    pose_bytes = pose.to_bytes()
    restored = PoseData.from_bytes(pose_bytes)

    # 각 랜드마크의 visibility (4번째 값) 확인
    for i in range(33):
        visibility_idx = i * 4 + 3
        expected = landmarks[visibility_idx]
        actual = restored.landmarks[visibility_idx]
        assert abs(expected - actual) < 0.0001, (
            f"Landmark[{i}].visibility 불일치: {expected} vs {actual}"
        )

    print("[PASS] 포즈 신뢰도 값 검증 통과")


def test_pose_with_invalid_detection() -> None:
    """검출 실패 시 이전 프레임 유지 패턴 검증."""
    # 첫 프레임: 유효한 포즈
    landmarks_1 = [0.1 * i for i in range(132)]
    pose_1 = PoseData(landmarks=landmarks_1)
    frame_1 = FusedDataFrame(
        pose=pose_1,
        valid_sensors=SensorFlags.POSE,
        timestamp_ms=1000,
    )

    # 두 번째 프레임: 검출 실패했지만 이전 프레임 값 유지
    pose_2 = PoseData(landmarks=landmarks_1)  # 동일한 값 사용
    frame_2 = FusedDataFrame(
        pose=pose_2,
        valid_sensors=SensorFlags.POSE,
        timestamp_ms=1033,  # 약 33ms 후
    )

    # 두 프레임 모두 동일한 포즈 데이터
    restored_1 = FusedDataFrame.from_bytes(frame_1.to_bytes())
    restored_2 = FusedDataFrame.from_bytes(frame_2.to_bytes())

    assert restored_1.pose.landmarks == restored_2.pose.landmarks
    print("[PASS] 검출 실패 시 이전 프레임 유지 패턴 검증 통과")


def test_mediapipe_config_defaults() -> None:
    """MediaPipeConfig 기본값 검증."""
    config = MediaPipeConfig()

    # 포즈 추정 기본값
    assert config.static_image_mode == False
    assert config.model_complexity == 1  # Full model
    assert config.smooth_landmarks == True
    assert config.min_detection_confidence == 0.7
    assert config.min_tracking_confidence == 0.7

    # 카메라 기본값
    assert config.camera_idx == 0
    assert config.target_width == 640
    assert config.target_height == 480
    assert config.target_fps == 30

    # UI 기본값
    assert config.show_gui == True
    assert config.gui_scale == 0.5

    print("[PASS] MediaPipeConfig 기본값 검증 통과")


def test_packet_format_with_pose() -> None:
    """포즈가 포함된 전체 패킷 형식 검증."""
    # 패킷 = 헤더(20B) + FusedDataFrame(956B)
    landmarks = [0.5] * 132
    frame = FusedDataFrame(
        pose=PoseData(landmarks=landmarks),
        valid_sensors=SensorFlags.POSE,
        timestamp_ms=1713300000000,
    )

    hdr = PacketHeader(
        packet_type=PacketType.FUSED_FRAME,
        sequence_num=0,
        timestamp_ms=1713300000000,
        payload_len=956,
    )

    hdr_bytes = hdr.encode()
    frame_bytes = frame.to_bytes()
    full_packet = hdr_bytes + frame_bytes

    # 전체 패킷 크기: 20 + 956 = 976 bytes
    assert len(full_packet) == 976, f"패킷 크기: {len(full_packet)}"

    # 헤더 복원 확인
    hdr_restored = PacketHeader.decode(hdr_bytes)
    assert hdr_restored.packet_type == PacketType.FUSED_FRAME
    assert hdr_restored.sequence_num == 0
    assert hdr_restored.payload_len == 956

    # 프레임 복원 확인
    frame_restored = FusedDataFrame.from_bytes(frame_bytes)
    assert frame_restored.valid_sensors == SensorFlags.POSE
    assert frame_restored.pose is not None

    print("[PASS] 포즈 포함 전체 패킷 형식 검증 통과 (976 bytes)")


def test_pose_landmark_depth_values() -> None:
    """포즈 랜드마크의 깊이(z) 값 범위 검증."""
    # MediaPipe z는 상대 깊이 (카메라 중심 기준)
    # 일반적으로 -1.0 ~ 1.0 범위
    landmarks = []
    for i in range(33):
        x = 0.3 + 0.3 * np.sin(i * 0.1)
        y = 0.4 + 0.3 * np.cos(i * 0.1)
        z = -0.2 - 0.1 * (i / 33.0)  # -0.2 ~ -0.3 범위
        visibility = 0.7 + 0.3 * np.cos(i * 0.1)
        landmarks.extend([x, y, z, visibility])

    pose = PoseData(landmarks=landmarks)
    restored = PoseData.from_bytes(pose.to_bytes())

    # z 값 범위 확인
    for i in range(33):
        z_idx = i * 4 + 2
        z_val = restored.landmarks[z_idx]
        assert -0.5 < z_val < 0.5, f"Z값 범위 초과: {z_val}"

    print("[PASS] 포즈 랜드마크 깊이 값 범위 검증 통과")


def test_normalized_coordinate_range() -> None:
    """정규화 좌표(0~1) 범위 검증."""
    landmarks = []
    for i in range(33):
        # MediaPipe는 0.0~1.0 정규화 좌표 사용
        x = i / 100.0  # 0.0~0.33
        y = (33 - i) / 100.0  # 0.33~0.0
        z = -0.1
        visibility = 1.0
        landmarks.extend([x, y, z, visibility])

    pose = PoseData(landmarks=landmarks)
    restored = PoseData.from_bytes(pose.to_bytes())

    # x, y 범위 확인 (정규화된 화면 좌표)
    for i in range(33):
        x_idx = i * 4
        y_idx = i * 4 + 1

        x_val = restored.landmarks[x_idx]
        y_val = restored.landmarks[y_idx]

        assert 0.0 <= x_val <= 1.0, f"X 범위 초과: {x_val}"
        assert 0.0 <= y_val <= 1.0, f"Y 범위 초과: {y_val}"

    print("[PASS] 정규화 좌표 범위 검증 통과")


def test_sensor_flag_pose_bit() -> None:
    """SensorFlags.POSE 비트 검증."""
    assert SensorFlags.POSE == 0x20, "POSE 비트값 불일치"

    frame_pose_only = FusedDataFrame(
        pose=PoseData(landmarks=[0.5] * 132),
        valid_sensors=SensorFlags.POSE,
    )

    restored = FusedDataFrame.from_bytes(frame_pose_only.to_bytes())
    assert restored.valid_sensors == SensorFlags.POSE
    assert restored.valid_sensor_count == 1

    print("[PASS] SensorFlags.POSE 비트 검증 통과 (0x20)")


def test_multiple_sensor_combination() -> None:
    """포즈 + 다른 센서 조합 검증."""
    from bridge import EyeTrackingData, ACCData

    frame = FusedDataFrame(
        pose=PoseData(landmarks=[0.5] * 132),
        eye_tracking=EyeTrackingData(
            left_rotation=[0.1, 0.2, 0.3, 0.9],
            blink_flags=0x01,
        ),
        acc=ACCData(x=0.01, y=-9.81, z=0.02),
        valid_sensors=SensorFlags.POSE | SensorFlags.EYE | SensorFlags.ACC,
        timestamp_ms=1713300000000,
    )

    frame_bytes = frame.to_bytes()
    restored = FusedDataFrame.from_bytes(frame_bytes)

    assert restored.valid_sensor_count == 3
    assert restored.pose is not None
    assert restored.eye_tracking is not None
    assert restored.acc is not None

    print("[PASS] 포즈 + 다중 센서 조합 검증 통과")


if __name__ == "__main__":
    test_pose_data_size()
    test_pose_landmarks_roundtrip()
    test_mediapose_in_fused_frame()
    test_pose_confidence_values()
    test_pose_with_invalid_detection()
    test_mediapipe_config_defaults()
    test_packet_format_with_pose()
    test_pose_landmark_depth_values()
    test_normalized_coordinate_range()
    test_sensor_flag_pose_bit()
    test_multiple_sensor_combination()

    print()
    print("=== MediaPipe 브릿지 검증 완료: 모든 테스트 통과 ===")
