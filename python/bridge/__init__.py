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
from .fusion_pipeline import FusionPipeline, PipelineStats
from .mediapipe_bridge import MediaPipeBridge, MediaPipeConfig
from .xsens_bridge import XSensBridge, XSensConfig, XSensMode
from .sensor_source import SensorSource
from .mock_sensors import (
    MockAccSource,
    MockEegSource,
    MockImuSource,
    MockPpgSource,
)
from .orchestrator import BridgeOrchestrator, OrchestratorStats
from .linkband_receiver import LinkBandReceiver, LINKBAND_INGEST_PORT
from .control_server import ControlServer, ClientState, ControlStats
from .session_logger import SessionLogger, LoggerConfig, SessionStats

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
    # 융합 파이프라인
    "FusionPipeline",
    "PipelineStats",
    # MediaPipe 브릿지
    "MediaPipeBridge",
    "MediaPipeConfig",
    # X-Sens 브릿지
    "XSensBridge",
    "XSensConfig",
    "XSensMode",
    # 센서 소스 추상화 + Mock
    "SensorSource",
    "MockEegSource",
    "MockPpgSource",
    "MockAccSource",
    "MockImuSource",
    # Orchestrator (Sender)
    "BridgeOrchestrator",
    "OrchestratorStats",
    # LinkBand2 수신기 (스마트폰→PC)
    "LinkBandReceiver",
    "LINKBAND_INGEST_PORT",
    # 제어 서버 (Handshake/Heartbeat/ClockSync)
    "ControlServer",
    "ClientState",
    "ControlStats",
    # 세션 로거
    "SessionLogger",
    "LoggerConfig",
    "SessionStats",
]
