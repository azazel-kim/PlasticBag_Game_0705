package com.dxplab.xrcontrol.data.network

/**
 * FUSE v1.0 프로토콜 상수 — Python [udp_protocol.py]와 바이너리 호환.
 *
 * 레이아웃:
 *   Header 20 bytes:
 *     4s MAGIC("FUSE") + B version(0x01) + B packetType + I seq + q timestampMs + H payloadLen
 *   FusedFrame payload 956 bytes (고정):
 *     BlendShape 284 + Eye 33 + EEG 30 + PPG 8 + ACC 12 + Pose 528 + IMU 52 + ValidFlags 1 + Ts 8
 *
 * 바이트 순서는 리틀엔디안 고정. Android 기본은 빅엔디안이므로 ByteBuffer 생성 시
 * ByteOrder.LITTLE_ENDIAN 설정 필수.
 */
object FuseProtocol {
    val MAGIC: ByteArray = byteArrayOf(0x46, 0x55, 0x53, 0x45) // "FUSE"
    const val VERSION: Byte = 0x01
    const val HEADER_SIZE: Int = 20
    const val FUSED_PAYLOAD_SIZE: Int = 956

    // 패킷 타입
    const val PACKET_FUSED_FRAME: Byte = 0x01
    const val PACKET_ENGAGEMENT:  Byte = 0x02
    const val PACKET_HANDSHAKE:   Byte = 0x10
    const val PACKET_HEARTBEAT:   Byte = 0x11
    const val PACKET_CLOCK_SYNC:  Byte = 0x12

    // SensorFlags (비트마스크)
    const val FLAG_BLENDSHAPE: Int = 0x01
    const val FLAG_EYE:        Int = 0x02
    const val FLAG_EEG:        Int = 0x04
    const val FLAG_PPG:        Int = 0x08
    const val FLAG_ACC:        Int = 0x10
    const val FLAG_POSE:       Int = 0x20
    const val FLAG_IMU:        Int = 0x40

    // 센서별 페이로드 크기 (바이트)
    const val SIZE_BLENDSHAPE: Int = 284
    const val SIZE_EYE:        Int = 33
    const val SIZE_EEG:        Int = 30
    const val SIZE_PPG:        Int = 8
    const val SIZE_ACC:        Int = 12
    const val SIZE_POSE:       Int = 528
    const val SIZE_IMU:        Int = 52

    // 기본 포트
    const val PORT_FUSED_FRAME: Int = 9001   // PC → Galaxy XR
    const val PORT_ENGAGEMENT:  Int = 9002   // PC → Galaxy XR
    const val PORT_CONTROL:     Int = 9003   // 양방향
    const val PORT_LINKBAND_INGEST: Int = 9010 // Android → PC
}
