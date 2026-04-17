// =============================================================================
// UdpProtocol.cs — UDP 패킷 프로토콜 정의
// 네임스페이스: XRExergame.Fusion
// 역할: PC ↔ Galaxy XR 간 UDP 통신의 패킷 헤더 형식, 인코딩/디코딩,
//       포트 번호, 패킷 타입 등을 정의
// =============================================================================
//
// 패킷 구조:
//   ┌──────────┬─────────┬────────────┬──────────────┬───────────────┬─────────────────┬──────────┐
//   │ Magic(4) │ Ver.(1) │ PktType(1) │ SeqNum(4)    │ Timestamp(8)  │ PayloadLen(2)   │ Payload  │
//   │ "FUSE"   │ 0x01    │ enum       │ 순차 증가    │ Unix ms       │ 바이트 수       │ 가변     │
//   └──────────┴─────────┴────────────┴──────────────┴───────────────┴─────────────────┴──────────┘
//   헤더 크기: 4 + 1 + 1 + 4 + 8 + 2 = 20 바이트
//
// 포트 할당:
//   9001 — FusedDataFrame (PC → Galaxy XR, 30Hz)
//   9002 — Engagement Score (PC → Galaxy XR, 10Hz)
//   9003 — Control (양방향, Handshake/Heartbeat/ClockSync)
// =============================================================================

using System;

namespace XRExergame.Fusion
{
    // =========================================================================
    // PacketType — 패킷 종류 식별자
    // =========================================================================

    /// <summary>
    /// UDP 패킷의 종류. 헤더의 PacketType 필드에 기록됨.
    /// </summary>
    public enum PacketType : byte
    {
        // --- 데이터 패킷 (PC → Galaxy XR) ---
        FusedFrame      = 0x01,  // 30Hz 융합 데이터 프레임
        EngagementScore = 0x02,  // 10Hz 몰입도 점수

        // --- 제어 패킷 (양방향) ---
        Handshake       = 0x10,  // 세션 시작 시 연결 확인
        Heartbeat       = 0x11,  // 주기적 연결 유지 확인 (매 1초)
        ClockSync       = 0x12,  // 클럭 동기화 요청/응답

        // --- 확장용 (미래) ---
        SessionStart    = 0x20,  // 세션 시작 신호 (메타데이터 포함)
        SessionEnd      = 0x21,  // 세션 종료 신호
        Config          = 0x30,  // 런타임 설정 변경
    }

    // =========================================================================
    // UdpProtocol — 정적 유틸리티 클래스
    // =========================================================================

    /// <summary>
    /// UDP 패킷 인코딩/디코딩 유틸리티.
    /// 모든 메서드는 정적이며, 스레드 안전함.
    /// </summary>
    public static class UdpProtocol
    {
        // ----- 프로토콜 상수 -----

        /// <summary>매직 바이트: "FUSE" (0x46, 0x55, 0x53, 0x45)</summary>
        public static readonly byte[] MAGIC = { 0x46, 0x55, 0x53, 0x45 }; // ASCII "FUSE"

        /// <summary>현재 프로토콜 버전</summary>
        public const byte VERSION = 0x01;

        /// <summary>패킷 헤더 크기 (바이트)</summary>
        public const int HEADER_SIZE = 20; // Magic(4) + Version(1) + PacketType(1) + SeqNum(4) + Timestamp(8) + PayloadLen(2)

        /// <summary>UDP 패킷 최대 크기 (MTU 고려, 단편화 방지)</summary>
        public const int MAX_PACKET_SIZE = 1400; // 일반 이더넷 MTU(1500) - IP(20) - UDP(8) - 여유(72)

        // ----- 포트 번호 -----

        /// <summary>FusedDataFrame 전송 포트 (PC → Galaxy XR, 30Hz)</summary>
        public const int FUSED_FRAME_PORT = 9001;

        /// <summary>Engagement Score 전송 포트 (PC → Galaxy XR, 10Hz)</summary>
        public const int ENGAGEMENT_PORT = 9002;

        /// <summary>제어 채널 포트 (양방향, Handshake/Heartbeat/ClockSync)</summary>
        public const int CONTROL_PORT = 9003;

        // =====================================================================
        // Encode — 패킷 조립 (헤더 + 페이로드)
        // =====================================================================

        /// <summary>
        /// 페이로드 데이터를 프로토콜 헤더와 함께 패킷으로 조립.
        /// </summary>
        /// <param name="type">패킷 종류</param>
        /// <param name="sequenceNum">순차 번호 (수신 측에서 패킷 유실 감지용)</param>
        /// <param name="timestampMs">패킷 생성 시각 (Unix 밀리초)</param>
        /// <param name="payload">페이로드 바이트 배열 (null 허용 → 빈 페이로드)</param>
        /// <returns>전송용 패킷 바이트 배열</returns>
        public static byte[] Encode(PacketType type, uint sequenceNum, long timestampMs, byte[] payload)
        {
            int payloadLen = payload?.Length ?? 0;
            byte[] packet = new byte[HEADER_SIZE + payloadLen];

            int offset = 0;

            // Magic (4 bytes)
            packet[offset++] = MAGIC[0];
            packet[offset++] = MAGIC[1];
            packet[offset++] = MAGIC[2];
            packet[offset++] = MAGIC[3];

            // Version (1 byte)
            packet[offset++] = VERSION;

            // PacketType (1 byte)
            packet[offset++] = (byte)type;

            // SequenceNum (4 bytes, Little-Endian)
            packet[offset++] = (byte)(sequenceNum);
            packet[offset++] = (byte)(sequenceNum >> 8);
            packet[offset++] = (byte)(sequenceNum >> 16);
            packet[offset++] = (byte)(sequenceNum >> 24);

            // TimestampMs (8 bytes, Little-Endian)
            packet[offset++] = (byte)(timestampMs);
            packet[offset++] = (byte)(timestampMs >> 8);
            packet[offset++] = (byte)(timestampMs >> 16);
            packet[offset++] = (byte)(timestampMs >> 24);
            packet[offset++] = (byte)(timestampMs >> 32);
            packet[offset++] = (byte)(timestampMs >> 40);
            packet[offset++] = (byte)(timestampMs >> 48);
            packet[offset++] = (byte)(timestampMs >> 56);

            // PayloadLength (2 bytes, Little-Endian)
            packet[offset++] = (byte)(payloadLen);
            packet[offset++] = (byte)(payloadLen >> 8);

            // Payload
            if (payload != null && payloadLen > 0)
            {
                Buffer.BlockCopy(payload, 0, packet, offset, payloadLen);
            }

            return packet;
        }

        /// <summary>
        /// FusedDataFrame을 바로 UDP 패킷으로 인코딩하는 편의 메서드.
        /// </summary>
        public static byte[] EncodeFusedFrame(FusedDataFrame frame, uint sequenceNum)
        {
            byte[] payload = frame.ToBytes();
            return Encode(PacketType.FusedFrame, sequenceNum, frame.TimestampMs, payload);
        }

        // =====================================================================
        // Decode — 패킷 파싱 (헤더 검증 + 페이로드 추출)
        // =====================================================================

        /// <summary>
        /// 수신된 바이트 배열에서 패킷 헤더와 페이로드를 분리.
        /// </summary>
        /// <param name="packet">수신된 원시 바이트 배열</param>
        /// <param name="header">파싱된 헤더 (out)</param>
        /// <param name="payload">페이로드 바이트 배열 (out)</param>
        /// <returns>성공 여부 (false = 잘못된 패킷)</returns>
        public static bool Decode(byte[] packet, out PacketHeader header, out byte[] payload)
        {
            header = default;
            payload = null;

            // 최소 크기 검증
            if (packet == null || packet.Length < HEADER_SIZE)
                return false;

            int offset = 0;

            // Magic 검증
            if (packet[0] != MAGIC[0] || packet[1] != MAGIC[1] ||
                packet[2] != MAGIC[2] || packet[3] != MAGIC[3])
            {
                return false; // 우리 프로토콜이 아님
            }
            offset += 4;

            // Version
            byte version = packet[offset++];
            if (version != VERSION)
            {
                // 버전 불일치 — 현재는 단일 버전만 지원
                // 미래에 하위 호환 처리 가능
                return false;
            }

            // PacketType
            byte packetTypeByte = packet[offset++];
            if (!Enum.IsDefined(typeof(PacketType), packetTypeByte))
                return false; // 알 수 없는 패킷 타입

            // SequenceNum (Little-Endian)
            uint seqNum = (uint)packet[offset]
                        | ((uint)packet[offset + 1] << 8)
                        | ((uint)packet[offset + 2] << 16)
                        | ((uint)packet[offset + 3] << 24);
            offset += 4;

            // TimestampMs (Little-Endian)
            long timestampMs = (long)packet[offset]
                             | ((long)packet[offset + 1] << 8)
                             | ((long)packet[offset + 2] << 16)
                             | ((long)packet[offset + 3] << 24)
                             | ((long)packet[offset + 4] << 32)
                             | ((long)packet[offset + 5] << 40)
                             | ((long)packet[offset + 6] << 48)
                             | ((long)packet[offset + 7] << 56);
            offset += 8;

            // PayloadLength (Little-Endian)
            ushort payloadLen = (ushort)(packet[offset] | (packet[offset + 1] << 8));
            offset += 2;

            // 페이로드 길이 검증
            if (packet.Length < HEADER_SIZE + payloadLen)
                return false; // 패킷이 불완전

            // 헤더 구성
            header = new PacketHeader
            {
                Version = version,
                Type = (PacketType)packetTypeByte,
                SequenceNum = seqNum,
                TimestampMs = timestampMs,
                PayloadLength = payloadLen
            };

            // 페이로드 추출
            if (payloadLen > 0)
            {
                payload = new byte[payloadLen];
                Buffer.BlockCopy(packet, HEADER_SIZE, payload, 0, payloadLen);
            }
            else
            {
                payload = Array.Empty<byte>();
            }

            return true;
        }

        /// <summary>
        /// FusedDataFrame을 바로 디코딩하는 편의 메서드.
        /// 패킷 타입이 FusedFrame이 아니면 실패.
        /// </summary>
        public static bool DecodeFusedFrame(byte[] packet, out FusedDataFrame frame)
        {
            frame = default;

            if (!Decode(packet, out PacketHeader header, out byte[] payload))
                return false;

            if (header.Type != PacketType.FusedFrame)
                return false;

            if (payload == null || payload.Length == 0)
                return false;

            frame = FusedDataFrame.FromBytes(payload);
            return true;
        }

        // =====================================================================
        // 제어 패킷 헬퍼
        // =====================================================================

        /// <summary>
        /// Handshake 패킷 생성.
        /// 세션 시작 시 PC↔XR이 서로의 존재를 확인할 때 사용.
        /// </summary>
        public static byte[] CreateHandshake(uint sequenceNum)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return Encode(PacketType.Handshake, sequenceNum, now, null);
        }

        /// <summary>
        /// Heartbeat 패킷 생성.
        /// 1초 주기로 전송하여 연결 상태 확인. 3초 무응답 시 연결 끊김 판정.
        /// </summary>
        public static byte[] CreateHeartbeat(uint sequenceNum)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return Encode(PacketType.Heartbeat, sequenceNum, now, null);
        }

        /// <summary>
        /// ClockSync 요청 패킷 생성.
        /// 페이로드에 요청자의 현재 시각을 담아 전송.
        /// 응답자는 자신의 시각을 페이로드에 담아 같은 타입으로 회신.
        /// </summary>
        /// <param name="senderTimestampMs">요청자의 현재 Unix 타임스탬프 (ms)</param>
        public static byte[] CreateClockSyncRequest(uint sequenceNum, long senderTimestampMs)
        {
            // 페이로드: 요청자 시각 (8 bytes, Little-Endian)
            byte[] payload = new byte[8];
            payload[0] = (byte)(senderTimestampMs);
            payload[1] = (byte)(senderTimestampMs >> 8);
            payload[2] = (byte)(senderTimestampMs >> 16);
            payload[3] = (byte)(senderTimestampMs >> 24);
            payload[4] = (byte)(senderTimestampMs >> 32);
            payload[5] = (byte)(senderTimestampMs >> 40);
            payload[6] = (byte)(senderTimestampMs >> 48);
            payload[7] = (byte)(senderTimestampMs >> 56);

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return Encode(PacketType.ClockSync, sequenceNum, now, payload);
        }

        /// <summary>
        /// ClockSync 응답 패킷 생성.
        /// 원래 요청자의 시각 + 응답자의 현재 시각을 페이로드에 담음.
        /// </summary>
        /// <param name="originalSenderTimestampMs">원래 요청자가 보낸 시각</param>
        /// <param name="responderTimestampMs">응답자의 현재 시각</param>
        public static byte[] CreateClockSyncResponse(uint sequenceNum, long originalSenderTimestampMs, long responderTimestampMs)
        {
            // 페이로드: 원래 요청 시각(8) + 응답자 시각(8) = 16 bytes
            byte[] payload = new byte[16];

            // 원래 요청 시각
            payload[0] = (byte)(originalSenderTimestampMs);
            payload[1] = (byte)(originalSenderTimestampMs >> 8);
            payload[2] = (byte)(originalSenderTimestampMs >> 16);
            payload[3] = (byte)(originalSenderTimestampMs >> 24);
            payload[4] = (byte)(originalSenderTimestampMs >> 32);
            payload[5] = (byte)(originalSenderTimestampMs >> 40);
            payload[6] = (byte)(originalSenderTimestampMs >> 48);
            payload[7] = (byte)(originalSenderTimestampMs >> 56);

            // 응답자 시각
            payload[8] = (byte)(responderTimestampMs);
            payload[9] = (byte)(responderTimestampMs >> 8);
            payload[10] = (byte)(responderTimestampMs >> 16);
            payload[11] = (byte)(responderTimestampMs >> 24);
            payload[12] = (byte)(responderTimestampMs >> 32);
            payload[13] = (byte)(responderTimestampMs >> 40);
            payload[14] = (byte)(responderTimestampMs >> 48);
            payload[15] = (byte)(responderTimestampMs >> 56);

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return Encode(PacketType.ClockSync, sequenceNum, now, payload);
        }

        /// <summary>
        /// ClockSync 응답 페이로드에서 시각 정보를 추출.
        /// </summary>
        public static bool ParseClockSyncResponse(byte[] payload,
            out long originalSenderTimestampMs, out long responderTimestampMs)
        {
            originalSenderTimestampMs = 0;
            responderTimestampMs = 0;

            if (payload == null || payload.Length < 16)
                return false;

            originalSenderTimestampMs = (long)payload[0]
                                      | ((long)payload[1] << 8)
                                      | ((long)payload[2] << 16)
                                      | ((long)payload[3] << 24)
                                      | ((long)payload[4] << 32)
                                      | ((long)payload[5] << 40)
                                      | ((long)payload[6] << 48)
                                      | ((long)payload[7] << 56);

            responderTimestampMs = (long)payload[8]
                                 | ((long)payload[9] << 8)
                                 | ((long)payload[10] << 16)
                                 | ((long)payload[11] << 24)
                                 | ((long)payload[12] << 32)
                                 | ((long)payload[13] << 40)
                                 | ((long)payload[14] << 48)
                                 | ((long)payload[15] << 56);

            return true;
        }

        // =====================================================================
        // 패킷 검증 헬퍼
        // =====================================================================

        /// <summary>
        /// 수신된 바이트 배열이 유효한 FUSE 프로토콜 패킷인지 빠르게 확인.
        /// 전체 파싱 전에 호출하여 불필요한 처리를 방지.
        /// </summary>
        public static bool IsValidPacket(byte[] data, int length)
        {
            if (data == null || length < HEADER_SIZE)
                return false;

            // Magic 확인만으로 빠르게 필터링
            return data[0] == MAGIC[0]
                && data[1] == MAGIC[1]
                && data[2] == MAGIC[2]
                && data[3] == MAGIC[3];
        }
    }

    // =========================================================================
    // PacketHeader — 파싱된 패킷 헤더 구조체
    // =========================================================================

    /// <summary>
    /// UDP 패킷 헤더를 파싱한 결과.
    /// Decode() 메서드의 out 파라미터로 반환됨.
    /// </summary>
    public struct PacketHeader
    {
        public byte Version;          // 프로토콜 버전
        public PacketType Type;       // 패킷 종류
        public uint SequenceNum;      // 순차 번호 (유실 감지용)
        public long TimestampMs;      // 패킷 생성 시각 (Unix ms)
        public ushort PayloadLength;  // 페이로드 바이트 수

        public override string ToString()
        {
            return $"[Packet v{Version} {Type} seq={SequenceNum} @{TimestampMs}ms payload={PayloadLength}B]";
        }
    }
}
