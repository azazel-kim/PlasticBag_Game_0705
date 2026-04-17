"""
FusedDataFrame — 센서 융합 데이터 프레임

6종 센서(EEG, PPG, ACC, Camera/BlendShape, X-Sens IMU, Eye Tracking)의
데이터를 30Hz 주기로 하나의 프레임으로 결합한 구조체.
struct 모듈 기반 바이너리 직렬화/역직렬화를 제공하여
C# System.BitConverter와 바이트 호환됨 (Little-endian).

바이트 레이아웃:
    BlendShape : float32[68] + float32[3] confidence = 284 bytes
    EyeTracking: float32[4] leftQuat + float32[4] rightQuat + uint8 blink = 33 bytes
    EEG        : float32[6] channels + uint8[6] contact = 30 bytes
    PPG        : float32 ir + float32 red = 8 bytes
    ACC        : float32[3] = 12 bytes
    Pose       : float32[33*4] = 528 bytes
    IMU        : float32[4] quat + float32[3] acc + float32[3] gyro + float32[3] mag = 52 bytes
    ValidFlags : uint8 = 1 byte
    TimestampMs: int64 = 8 bytes
    ────────────────────────────
    총 페이로드: 956 bytes (고정 크기)
"""

from __future__ import annotations

import struct
from dataclasses import dataclass, field
from enum import IntFlag
from typing import ClassVar, Optional


# ─────────────────────────────────────────────
# 센서 유효 플래그 (비트마스크)
# ─────────────────────────────────────────────

class SensorFlags(IntFlag):
    """각 센서의 유효 여부를 나타내는 비트 플래그.

    C# 쪽 ValidSensorFlags와 동일한 비트 배치를 사용.
    """
    NONE        = 0x00
    BLENDSHAPE  = 0x01  # bit 0: 블렌드셰이프 (MediaPipe Face)
    EYE         = 0x02  # bit 1: 눈 추적
    EEG         = 0x04  # bit 2: EEG (뇌파)
    PPG         = 0x08  # bit 3: PPG (광용적맥파)
    ACC         = 0x10  # bit 4: 가속도계
    POSE        = 0x20  # bit 5: 전신 포즈 (MediaPipe Pose)
    IMU         = 0x40  # bit 6: X-Sens IMU
    ALL         = 0x7F  # 7개 센서 모두 유효


# ─────────────────────────────────────────────
# 센서별 서브 데이터클래스
# ─────────────────────────────────────────────

@dataclass(slots=True)
class BlendShapeData:
    """얼굴 블렌드셰이프 데이터 (MediaPipe Face Mesh → FLAME 호환).

    속성:
        weights: ARKit 호환 블렌드셰이프 가중치 68개 (0.0~1.0)
        confidence: [전체 신뢰도, 좌측 눈 신뢰도, 우측 눈 신뢰도]
    """
    weights: list[float] = field(default_factory=lambda: [0.0] * 68)
    confidence: list[float] = field(default_factory=lambda: [0.0] * 3)

    # 직렬화 크기: 68*4 + 3*4 = 284 bytes
    PACKED_SIZE: ClassVar[int] = 284
    _FMT: ClassVar[str] = "<68f3f"  # Little-endian, 71 floats

    def to_bytes(self) -> bytes:
        """바이너리 직렬화 — C# float[] 배열과 호환."""
        return struct.pack(self._FMT, *self.weights, *self.confidence)

    @classmethod
    def from_bytes(cls, data: bytes, offset: int = 0) -> BlendShapeData:
        """바이너리 역직렬화."""
        values = struct.unpack_from(cls._FMT, data, offset)
        return cls(
            weights=list(values[:68]),
            confidence=list(values[68:71]),
        )


@dataclass(slots=True)
class EyeTrackingData:
    """눈 추적 데이터 (OpenXR EyeGazeInteraction).

    속성:
        left_rotation: 좌측 눈 회전 쿼터니언 [x, y, z, w]
        right_rotation: 우측 눈 회전 쿼터니언 [x, y, z, w]
        blink_flags: 비트마스크 — bit0=좌측 감김, bit1=우측 감김
    """
    left_rotation: list[float] = field(default_factory=lambda: [0.0, 0.0, 0.0, 1.0])
    right_rotation: list[float] = field(default_factory=lambda: [0.0, 0.0, 0.0, 1.0])
    blink_flags: int = 0  # uint8

    # 직렬화 크기: 4*4 + 4*4 + 1 = 33 bytes
    PACKED_SIZE: ClassVar[int] = 33
    _FMT: ClassVar[str] = "<4f4fB"

    def to_bytes(self) -> bytes:
        return struct.pack(
            self._FMT,
            *self.left_rotation,
            *self.right_rotation,
            self.blink_flags,
        )

    @classmethod
    def from_bytes(cls, data: bytes, offset: int = 0) -> EyeTrackingData:
        values = struct.unpack_from(cls._FMT, data, offset)
        return cls(
            left_rotation=list(values[:4]),
            right_rotation=list(values[4:8]),
            blink_flags=values[8],
        )


@dataclass(slots=True)
class EEGData:
    """EEG 뇌파 데이터 (LinkBand2, 2ch → 6ch 확장 대비).

    속성:
        channels: 6채널 전압값 (µV) — 현재 ch0~1만 사용, 나머지 0.0
        contact_quality: 각 채널의 전극 접촉 품질 (0~255)
    """
    channels: list[float] = field(default_factory=lambda: [0.0] * 6)
    contact_quality: list[int] = field(default_factory=lambda: [0] * 6)

    # 직렬화 크기: 6*4 + 6*1 = 30 bytes
    PACKED_SIZE: ClassVar[int] = 30
    _FMT: ClassVar[str] = "<6f6B"

    def to_bytes(self) -> bytes:
        return struct.pack(self._FMT, *self.channels, *self.contact_quality)

    @classmethod
    def from_bytes(cls, data: bytes, offset: int = 0) -> EEGData:
        values = struct.unpack_from(cls._FMT, data, offset)
        return cls(
            channels=list(values[:6]),
            contact_quality=list(int(v) for v in values[6:12]),
        )


@dataclass(slots=True)
class PPGData:
    """PPG 광용적맥파 데이터 (LinkBand2).

    속성:
        ir: 적외선 광 반사값
        red: 적색광 반사값
    """
    ir: float = 0.0
    red: float = 0.0

    # 직렬화 크기: 4 + 4 = 8 bytes
    PACKED_SIZE: ClassVar[int] = 8
    _FMT: ClassVar[str] = "<2f"

    def to_bytes(self) -> bytes:
        return struct.pack(self._FMT, self.ir, self.red)

    @classmethod
    def from_bytes(cls, data: bytes, offset: int = 0) -> PPGData:
        ir, red = struct.unpack_from(cls._FMT, data, offset)
        return cls(ir=ir, red=red)


@dataclass(slots=True)
class ACCData:
    """3축 가속도 데이터 (LinkBand2).

    속성:
        x, y, z: 가속도 (g 단위)
    """
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0

    # 직렬화 크기: 3*4 = 12 bytes
    PACKED_SIZE: ClassVar[int] = 12
    _FMT: ClassVar[str] = "<3f"

    def to_bytes(self) -> bytes:
        return struct.pack(self._FMT, self.x, self.y, self.z)

    @classmethod
    def from_bytes(cls, data: bytes, offset: int = 0) -> ACCData:
        x, y, z = struct.unpack_from(cls._FMT, data, offset)
        return cls(x=x, y=y, z=z)


@dataclass(slots=True)
class PoseData:
    """전신 포즈 랜드마크 데이터 (MediaPipe Pose).

    속성:
        landmarks: 33개 랜드마크 × [x, y, z, visibility] = 132 floats
            - x, y: 정규화 좌표 (0.0~1.0)
            - z: 깊이 (카메라 기준 상대값)
            - visibility: 가시성 확신도 (0.0~1.0)
    """
    landmarks: list[float] = field(default_factory=lambda: [0.0] * 132)

    # 직렬화 크기: 33*4*4 = 528 bytes
    PACKED_SIZE: ClassVar[int] = 528
    _FMT: ClassVar[str] = "<132f"

    def to_bytes(self) -> bytes:
        return struct.pack(self._FMT, *self.landmarks)

    @classmethod
    def from_bytes(cls, data: bytes, offset: int = 0) -> PoseData:
        values = struct.unpack_from(cls._FMT, data, offset)
        return cls(landmarks=list(values))


@dataclass(slots=True)
class IMUData:
    """X-Sens IMU 데이터 (단일 세그먼트 요약).

    속성:
        quaternion: 방향 쿼터니언 [x, y, z, w]
        acceleration: 가속도 [x, y, z] (m/s²)
        gyroscope: 각속도 [x, y, z] (rad/s)
        magnetometer: 자기장 [x, y, z] (µT)
    """
    quaternion: list[float] = field(default_factory=lambda: [0.0, 0.0, 0.0, 1.0])
    acceleration: list[float] = field(default_factory=lambda: [0.0] * 3)
    gyroscope: list[float] = field(default_factory=lambda: [0.0] * 3)
    magnetometer: list[float] = field(default_factory=lambda: [0.0] * 3)

    # 직렬화 크기: 4*4 + 3*4 + 3*4 + 3*4 = 52 bytes
    PACKED_SIZE: ClassVar[int] = 52
    _FMT: ClassVar[str] = "<4f3f3f3f"

    def to_bytes(self) -> bytes:
        return struct.pack(
            self._FMT,
            *self.quaternion,
            *self.acceleration,
            *self.gyroscope,
            *self.magnetometer,
        )

    @classmethod
    def from_bytes(cls, data: bytes, offset: int = 0) -> IMUData:
        values = struct.unpack_from(cls._FMT, data, offset)
        return cls(
            quaternion=list(values[:4]),
            acceleration=list(values[4:7]),
            gyroscope=list(values[7:10]),
            magnetometer=list(values[10:13]),
        )


# ─────────────────────────────────────────────
# FusedDataFrame — 융합 프레임
# ─────────────────────────────────────────────

@dataclass
class FusedDataFrame:
    """30Hz 주기로 생성되는 센서 융합 프레임.

    모든 센서의 최신 데이터를 하나의 프레임으로 결합.
    valid_sensors 비트마스크로 어떤 센서가 유효한지 표시.

    직렬화 순서 (C# 쪽과 동일):
        1. BlendShape  (284 bytes)
        2. EyeTracking (33 bytes)
        3. EEG         (30 bytes)
        4. PPG         (8 bytes)
        5. ACC         (12 bytes)
        6. Pose        (528 bytes)
        7. IMU         (52 bytes)
        8. ValidFlags  (1 byte)
        9. TimestampMs (8 bytes)
        ──────────────────────
        합계: 956 bytes
    """
    # 센서 데이터 — None이면 해당 센서 데이터 없음
    blendshape: Optional[BlendShapeData] = None
    eye_tracking: Optional[EyeTrackingData] = None
    eeg: Optional[EEGData] = None
    ppg: Optional[PPGData] = None
    acc: Optional[ACCData] = None
    pose: Optional[PoseData] = None
    imu: Optional[IMUData] = None

    # 메타데이터
    valid_sensors: SensorFlags = SensorFlags.NONE
    timestamp_ms: int = 0  # 융합 시점 UTC epoch ms

    # 고정 페이로드 크기
    PACKED_SIZE: int = 956

    # 꼬리 부분 포맷: ValidFlags(uint8) + TimestampMs(int64)
    _TAIL_FMT: str = "<Bq"
    _TAIL_SIZE: int = 9  # 1 + 8

    def to_bytes(self) -> bytes:
        """전체 프레임을 바이너리로 직렬화.

        각 센서 데이터가 None이면 해당 영역을 0으로 채움.
        C# BitConverter.ToSingle/ToInt64와 호환 (Little-endian).

        반환값:
            956 bytes의 바이너리 데이터
        """
        parts: list[bytes] = []

        # 1) BlendShape (284 bytes)
        if self.blendshape is not None:
            parts.append(self.blendshape.to_bytes())
        else:
            parts.append(b"\x00" * BlendShapeData.PACKED_SIZE)

        # 2) EyeTracking (33 bytes)
        if self.eye_tracking is not None:
            parts.append(self.eye_tracking.to_bytes())
        else:
            parts.append(b"\x00" * EyeTrackingData.PACKED_SIZE)

        # 3) EEG (30 bytes)
        if self.eeg is not None:
            parts.append(self.eeg.to_bytes())
        else:
            parts.append(b"\x00" * EEGData.PACKED_SIZE)

        # 4) PPG (8 bytes)
        if self.ppg is not None:
            parts.append(self.ppg.to_bytes())
        else:
            parts.append(b"\x00" * PPGData.PACKED_SIZE)

        # 5) ACC (12 bytes)
        if self.acc is not None:
            parts.append(self.acc.to_bytes())
        else:
            parts.append(b"\x00" * ACCData.PACKED_SIZE)

        # 6) Pose (528 bytes)
        if self.pose is not None:
            parts.append(self.pose.to_bytes())
        else:
            parts.append(b"\x00" * PoseData.PACKED_SIZE)

        # 7) IMU (52 bytes)
        if self.imu is not None:
            parts.append(self.imu.to_bytes())
        else:
            parts.append(b"\x00" * IMUData.PACKED_SIZE)

        # 8~9) ValidFlags + TimestampMs (9 bytes)
        parts.append(struct.pack(self._TAIL_FMT, int(self.valid_sensors), self.timestamp_ms))

        result = b"".join(parts)
        assert len(result) == self.PACKED_SIZE, (
            f"직렬화 크기 불일치: 예상={self.PACKED_SIZE}, 실제={len(result)}"
        )
        return result

    @classmethod
    def from_bytes(cls, data: bytes, offset: int = 0) -> FusedDataFrame:
        """바이너리에서 FusedDataFrame을 복원.

        매개변수:
            data: 최소 956 bytes의 바이너리 데이터
            offset: 읽기 시작 위치

        반환값:
            복원된 FusedDataFrame 인스턴스
        """
        if len(data) - offset < cls.PACKED_SIZE:
            raise ValueError(
                f"데이터 부족: 필요={cls.PACKED_SIZE}, "
                f"가용={len(data) - offset}"
            )

        pos = offset

        # 1) BlendShape
        blendshape = BlendShapeData.from_bytes(data, pos)
        pos += BlendShapeData.PACKED_SIZE

        # 2) EyeTracking
        eye_tracking = EyeTrackingData.from_bytes(data, pos)
        pos += EyeTrackingData.PACKED_SIZE

        # 3) EEG
        eeg = EEGData.from_bytes(data, pos)
        pos += EEGData.PACKED_SIZE

        # 4) PPG
        ppg = PPGData.from_bytes(data, pos)
        pos += PPGData.PACKED_SIZE

        # 5) ACC
        acc = ACCData.from_bytes(data, pos)
        pos += ACCData.PACKED_SIZE

        # 6) Pose
        pose = PoseData.from_bytes(data, pos)
        pos += PoseData.PACKED_SIZE

        # 7) IMU
        imu = IMUData.from_bytes(data, pos)
        pos += IMUData.PACKED_SIZE

        # 8~9) ValidFlags + TimestampMs
        valid_raw, timestamp_ms = struct.unpack_from(cls._TAIL_FMT, data, pos)
        valid_sensors = SensorFlags(valid_raw)

        # valid_sensors 비트에 따라 없는 센서는 None으로 설정
        frame = cls(
            blendshape=blendshape if valid_sensors & SensorFlags.BLENDSHAPE else None,
            eye_tracking=eye_tracking if valid_sensors & SensorFlags.EYE else None,
            eeg=eeg if valid_sensors & SensorFlags.EEG else None,
            ppg=ppg if valid_sensors & SensorFlags.PPG else None,
            acc=acc if valid_sensors & SensorFlags.ACC else None,
            pose=pose if valid_sensors & SensorFlags.POSE else None,
            imu=imu if valid_sensors & SensorFlags.IMU else None,
            valid_sensors=valid_sensors,
            timestamp_ms=timestamp_ms,
        )
        return frame

    # ── 유틸리티 ──

    @property
    def valid_sensor_count(self) -> int:
        """유효한 센서 수 (0~7)."""
        return bin(int(self.valid_sensors)).count("1")

    def sensor_names(self) -> list[str]:
        """유효한 센서 이름 목록."""
        names = []
        if self.valid_sensors & SensorFlags.BLENDSHAPE:
            names.append("BlendShape")
        if self.valid_sensors & SensorFlags.EYE:
            names.append("EyeTracking")
        if self.valid_sensors & SensorFlags.EEG:
            names.append("EEG")
        if self.valid_sensors & SensorFlags.PPG:
            names.append("PPG")
        if self.valid_sensors & SensorFlags.ACC:
            names.append("ACC")
        if self.valid_sensors & SensorFlags.POSE:
            names.append("Pose")
        if self.valid_sensors & SensorFlags.IMU:
            names.append("IMU")
        return names

    def __repr__(self) -> str:
        return (
            f"FusedDataFrame(t={self.timestamp_ms}, "
            f"sensors={self.valid_sensor_count}/7 "
            f"[{', '.join(self.sensor_names())}])"
        )
