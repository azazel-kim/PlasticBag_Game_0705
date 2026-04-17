"""
XR Exergame Sensor Fusion Bridge 패키지

PC(RTX 4080)에서 LinkBand2/MediaPipe/X-Sens 센서 데이터를 수신하고,
FusedDataFrame으로 융합하여 UDP로 Galaxy XR Unity에 전송하는 브릿지 모듈.

토폴로지:
    PC (RTX 4080 Windows)
    ├── LinkBand2 BLE Receiver → RingBuffer
    ├── MediaPipe (OBSBOT USB) → RingBuffer
    ├── X-Sens SDK → RingBuffer
    └── TimeSyncAligner → FusedDataFrame @30Hz → UDP 9001 → Galaxy XR Unity
"""

from .fused_data_frame import (
    FusedDataFrame,
    BlendShapeData,
    EyeTrackingData,
    EEGData,
    PPGData,
    ACCData,
    PoseData,
    IMUData,
    SensorFlags,
)
from .ring_buffer import RingBuffer
from .time_sync_aligner import TimeSyncAligner, SensorType
from .udp_protocol import (
    PacketHeader,
    PacketType,
    UdpSender,
    UdpReceiver,
    FUSED_FRAME_PORT,
    ENGAGEMENT_PORT,
    CONTROL_PORT,
)

__version__ = "0.1.0"

__all__ = [
    # 데이터 프레임
    "FusedDataFrame",
    "BlendShapeData",
    "EyeTrackingData",
    "EEGData",
    "PPGData",
    "ACCData",
    "PoseData",
    "IMUData",
    "SensorFlags",
    # 링 버퍼
    "RingBuffer",
    # 시간 동기화
    "TimeSyncAligner",
    "SensorType",
    # UDP 프로토콜
    "PacketHeader",
    "PacketType",
    "UdpSender",
    "UdpReceiver",
    "FUSED_FRAME_PORT",
    "ENGAGEMENT_PORT",
    "CONTROL_PORT",
]
