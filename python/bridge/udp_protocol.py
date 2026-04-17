"""
UDP 프로토콜 — PC Bridge ↔ Galaxy XR Unity 통신

패킷 구조 (Little-endian):
    ┌──────────────────────────────────────────────┐
    │ Header (20 bytes)                            │
    │  Magic       : 4 bytes  "FUSE" (0x46555345)  │
    │  Version     : 1 byte   현재 0x01             │
    │  PacketType  : 1 byte   (enum)               │
    │  SequenceNum : 4 bytes  uint32               │
    │  TimestampMs : 8 bytes  int64 (UTC epoch ms) │
    │  PayloadLen  : 2 bytes  uint16               │
    ├──────────────────────────────────────────────┤
    │ Payload (가변 크기)                            │
    │  FusedFrame: 956 bytes                       │
    │  EngagementScore: 4 bytes (float32)          │
    │  Handshake: 0 bytes (헤더만)                  │
    │  Heartbeat: 0 bytes (헤더만)                  │
    │  ClockSync: 16 bytes (2 × int64)             │
    └──────────────────────────────────────────────┘

포트 배정:
    9001: FUSED_FRAME_PORT  — FusedDataFrame 전송 (30Hz)
    9002: ENGAGEMENT_PORT   — EngagementScore 전송 (5Hz)
    9003: CONTROL_PORT      — Handshake/Heartbeat/ClockSync
"""

from __future__ import annotations

import logging
import socket
import struct
import threading
import time
from dataclasses import dataclass
from enum import IntEnum
from typing import Callable, ClassVar, Optional

from .fused_data_frame import FusedDataFrame

logger = logging.getLogger(__name__)

# ─────────────────────────────────────────────
# 상수
# ─────────────────────────────────────────────

MAGIC = b"FUSE"                   # 4 bytes — 패킷 식별자
PROTOCOL_VERSION: int = 0x01      # 현재 프로토콜 버전

FUSED_FRAME_PORT: int = 9001      # FusedDataFrame 전송 포트
ENGAGEMENT_PORT: int = 9002       # EngagementScore 전송 포트
CONTROL_PORT: int = 9003          # 제어 메시지 포트

HEADER_SIZE: int = 20             # 헤더 고정 크기

# 수신 버퍼 크기 — FusedFrame(956) + Header(20) + 여유
RECV_BUFFER_SIZE: int = 2048


# ─────────────────────────────────────────────
# 패킷 타입
# ─────────────────────────────────────────────

class PacketType(IntEnum):
    """패킷 종류 — C# 쪽 PacketType enum과 동일."""
    FUSED_FRAME     = 0x01  # FusedDataFrame 페이로드
    ENGAGEMENT      = 0x02  # EngagementScore (float32)
    HANDSHAKE       = 0x10  # 연결 초기화 (페이로드 없음)
    HEARTBEAT       = 0x11  # 생존 확인 (페이로드 없음)
    CLOCK_SYNC      = 0x12  # 시간 동기화 (t1, t2 int64)


# ─────────────────────────────────────────────
# 패킷 헤더
# ─────────────────────────────────────────────

@dataclass(slots=True)
class PacketHeader:
    """UDP 패킷 헤더 (20 bytes).

    Little-endian 바이트 순서:
        4s  magic         "FUSE"
        B   version       0x01
        B   packet_type   PacketType
        I   sequence_num  uint32 (0부터 증가)
        q   timestamp_ms  int64 (UTC epoch ms)
        H   payload_len   uint16
    """
    magic: bytes = MAGIC
    version: int = PROTOCOL_VERSION
    packet_type: PacketType = PacketType.FUSED_FRAME
    sequence_num: int = 0
    timestamp_ms: int = 0
    payload_len: int = 0

    # struct 포맷: 4s(magic) + B(ver) + B(type) + I(seq) + q(ts) + H(len)
    _FMT: ClassVar[str] = "<4sBBIqH"

    def encode(self) -> bytes:
        """헤더를 20 bytes로 직렬화."""
        return struct.pack(
            self._FMT,
            self.magic,
            self.version,
            int(self.packet_type),
            self.sequence_num,
            self.timestamp_ms,
            self.payload_len,
        )

    @classmethod
    def decode(cls, data: bytes, offset: int = 0) -> PacketHeader:
        """바이너리에서 헤더 복원.

        매개변수:
            data: 최소 20 bytes
            offset: 읽기 시작 위치

        예외:
            ValueError: magic 불일치 또는 버전 불일치 시
        """
        if len(data) - offset < HEADER_SIZE:
            raise ValueError(
                f"헤더 데이터 부족: 필요={HEADER_SIZE}, "
                f"가용={len(data) - offset}"
            )

        magic, version, ptype, seq, ts, plen = struct.unpack_from(
            cls._FMT, data, offset
        )

        if magic != MAGIC:
            raise ValueError(
                f"잘못된 magic: {magic!r} (예상: {MAGIC!r})"
            )
        if version != PROTOCOL_VERSION:
            raise ValueError(
                f"프로토콜 버전 불일치: {version:#x} (예상: {PROTOCOL_VERSION:#x})"
            )

        return cls(
            magic=magic,
            version=version,
            packet_type=PacketType(ptype),
            sequence_num=seq,
            timestamp_ms=ts,
            payload_len=plen,
        )

    def __repr__(self) -> str:
        return (
            f"PacketHeader(type={self.packet_type.name}, "
            f"seq={self.sequence_num}, ts={self.timestamp_ms}, "
            f"payload={self.payload_len}B)"
        )


# ─────────────────────────────────────────────
# ClockSync 페이로드
# ─────────────────────────────────────────────

@dataclass(slots=True)
class ClockSyncPayload:
    """ClockSync 패킷 페이로드 — NTP 스타일 왕복 시간 측정.

    필드:
        t1_ms: 요청자가 패킷을 보낸 시각 (요청자 시계)
        t2_ms: 응답자가 패킷을 받은 시각 (응답자 시계)

    clock offset 계산:
        RTT = (t4 - t1) - (t3 - t2)
        offset = ((t2 - t1) + (t3 - t4)) / 2
        (t3, t4는 응답 패킷의 t1, t2 → 별도 요청/응답 쌍)
    """
    t1_ms: int = 0
    t2_ms: int = 0

    PACKED_SIZE: ClassVar[int] = 16
    _FMT: ClassVar[str] = "<qq"

    def encode(self) -> bytes:
        return struct.pack(self._FMT, self.t1_ms, self.t2_ms)

    @classmethod
    def decode(cls, data: bytes, offset: int = 0) -> ClockSyncPayload:
        t1, t2 = struct.unpack_from(cls._FMT, data, offset)
        return cls(t1_ms=t1, t2_ms=t2)


# ─────────────────────────────────────────────
# UdpSender — 데이터 전송기
# ─────────────────────────────────────────────

class UdpSender:
    """FusedDataFrame/EngagementScore를 UDP로 전송.

    각 패킷 타입별 포트를 분리하여 전송하며, 자동 시퀀스 번호 증가.

    매개변수:
        target_ip: Galaxy XR 디바이스 IP 주소
        fused_port: FusedDataFrame 전송 포트 (기본 9001)
        engagement_port: EngagementScore 전송 포트 (기본 9002)
        control_port: 제어 메시지 포트 (기본 9003)

    사용 예시::

        sender = UdpSender(target_ip="192.168.1.50")
        sender.send_fused_frame(frame)
        sender.send_engagement_score(0.85)
        sender.send_heartbeat()
        sender.close()
    """

    def __init__(
        self,
        target_ip: str,
        fused_port: int = FUSED_FRAME_PORT,
        engagement_port: int = ENGAGEMENT_PORT,
        control_port: int = CONTROL_PORT,
    ) -> None:
        self._target_ip = target_ip
        self._fused_addr = (target_ip, fused_port)
        self._engagement_addr = (target_ip, engagement_port)
        self._control_addr = (target_ip, control_port)

        # UDP 소켓 (논블로킹 전송)
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

        # 패킷 타입별 시퀀스 번호 (독립 카운터)
        self._seq: dict[PacketType, int] = {pt: 0 for pt in PacketType}

        # 전송 통계
        self._send_count: int = 0
        self._send_bytes: int = 0

        logger.info(
            "[UdpSender] 초기화 — target=%s, ports=(fused=%d, engage=%d, ctrl=%d)",
            target_ip, fused_port, engagement_port, control_port,
        )

    def send_fused_frame(self, frame: FusedDataFrame) -> None:
        """FusedDataFrame을 UDP 9001로 전송.

        매개변수:
            frame: 직렬화할 융합 프레임
        """
        payload = frame.to_bytes()
        self._send_packet(
            packet_type=PacketType.FUSED_FRAME,
            payload=payload,
            addr=self._fused_addr,
            timestamp_ms=frame.timestamp_ms,
        )

    def send_engagement_score(self, score: float, timestamp_ms: int = 0) -> None:
        """EngagementScore(몰입도 점수)를 UDP 9002로 전송.

        매개변수:
            score: 몰입도 점수 (0.0~1.0)
            timestamp_ms: 점수 산출 시점. 0이면 현재 시각 사용.
        """
        if timestamp_ms == 0:
            timestamp_ms = int(time.time() * 1000)
        payload = struct.pack("<f", score)
        self._send_packet(
            packet_type=PacketType.ENGAGEMENT,
            payload=payload,
            addr=self._engagement_addr,
            timestamp_ms=timestamp_ms,
        )

    def send_handshake(self) -> None:
        """Handshake 패킷 전송 — 연결 초기화 요청.

        Unity 쪽에서 Handshake 수신 시 ClockSync 시퀀스를 시작함.
        """
        self._send_packet(
            packet_type=PacketType.HANDSHAKE,
            payload=b"",
            addr=self._control_addr,
        )
        logger.info("[UdpSender] Handshake 전송 → %s", self._control_addr)

    def send_heartbeat(self) -> None:
        """Heartbeat 패킷 전송 — 연결 생존 확인.

        1초 주기 전송 권장. Unity 쪽에서 5초 이상 Heartbeat 미수신 시
        연결 끊김(disconnected)으로 판단.
        """
        self._send_packet(
            packet_type=PacketType.HEARTBEAT,
            payload=b"",
            addr=self._control_addr,
        )

    def send_clock_sync_request(self) -> int:
        """ClockSync 요청 패킷 전송.

        반환값:
            t1_ms — 이 패킷의 전송 시각 (offset 계산에 사용)
        """
        t1_ms = int(time.time() * 1000)
        payload = ClockSyncPayload(t1_ms=t1_ms, t2_ms=0).encode()
        self._send_packet(
            packet_type=PacketType.CLOCK_SYNC,
            payload=payload,
            addr=self._control_addr,
            timestamp_ms=t1_ms,
        )
        logger.debug("[UdpSender] ClockSync 요청 → t1=%d", t1_ms)
        return t1_ms

    @property
    def stats(self) -> dict:
        """전송 통계."""
        return {
            "target_ip": self._target_ip,
            "total_packets": self._send_count,
            "total_bytes": self._send_bytes,
            "sequences": {pt.name: seq for pt, seq in self._seq.items()},
        }

    def close(self) -> None:
        """소켓 종료."""
        self._sock.close()
        logger.info("[UdpSender] 소켓 종료 (총 %d 패킷 전송)", self._send_count)

    # ── 내부 ──

    def _send_packet(
        self,
        packet_type: PacketType,
        payload: bytes,
        addr: tuple[str, int],
        timestamp_ms: int = 0,
    ) -> None:
        """패킷 헤더 + 페이로드를 조합하여 UDP 전송."""
        if timestamp_ms == 0:
            timestamp_ms = int(time.time() * 1000)

        seq = self._seq[packet_type]
        self._seq[packet_type] = (seq + 1) % (2**32)  # uint32 순환

        header = PacketHeader(
            packet_type=packet_type,
            sequence_num=seq,
            timestamp_ms=timestamp_ms,
            payload_len=len(payload),
        )

        packet = header.encode() + payload

        try:
            self._sock.sendto(packet, addr)
            self._send_count += 1
            self._send_bytes += len(packet)
        except OSError as e:
            logger.error(
                "[UdpSender] 전송 실패 → %s: %s (type=%s, seq=%d)",
                addr, e, packet_type.name, seq,
            )


# ─────────────────────────────────────────────
# UdpReceiver — 데이터 수신기
# ─────────────────────────────────────────────

class UdpReceiver:
    """UDP 패킷 수신 → 파싱 → 콜백 호출.

    별도 스레드에서 논블로킹 수신 루프를 실행.
    패킷 타입별 콜백을 등록하여 처리.

    매개변수:
        bind_ip: 바인드 IP (기본 "0.0.0.0" = 모든 인터페이스)
        bind_port: 수신 포트
        buffer_size: 수신 버퍼 크기 (bytes)

    사용 예시::

        receiver = UdpReceiver(bind_port=9001)
        receiver.on_fused_frame = lambda frame, addr: process(frame)
        receiver.start()
        # ... 사용 후 ...
        receiver.stop()
    """

    def __init__(
        self,
        bind_ip: str = "0.0.0.0",
        bind_port: int = FUSED_FRAME_PORT,
        buffer_size: int = RECV_BUFFER_SIZE,
    ) -> None:
        self._bind_addr = (bind_ip, bind_port)
        self._buffer_size = buffer_size

        self._sock: Optional[socket.socket] = None
        self._thread: Optional[threading.Thread] = None
        self._running = threading.Event()

        # 패킷 타입별 콜백
        # FusedFrame: (FusedDataFrame, sender_addr) → None
        self.on_fused_frame: Optional[Callable[[FusedDataFrame, tuple], None]] = None
        # Engagement: (float, int, sender_addr) → None (score, timestamp_ms, addr)
        self.on_engagement: Optional[Callable[[float, int, tuple], None]] = None
        # Handshake: (sender_addr,) → None
        self.on_handshake: Optional[Callable[[tuple], None]] = None
        # Heartbeat: (sender_addr,) → None
        self.on_heartbeat: Optional[Callable[[tuple], None]] = None
        # ClockSync: (ClockSyncPayload, sender_addr) → None
        self.on_clock_sync: Optional[Callable[[ClockSyncPayload, tuple], None]] = None

        # 수신 통계
        self._recv_count: int = 0
        self._recv_bytes: int = 0
        self._error_count: int = 0

    def start(self) -> None:
        """수신 스레드 시작."""
        if self._running.is_set():
            logger.warning("[UdpReceiver] 이미 실행 중")
            return

        self._sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self._sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._sock.bind(self._bind_addr)
        # 타임아웃 설정 — stop() 호출 시 루프 탈출용
        self._sock.settimeout(0.5)

        self._running.set()
        self._thread = threading.Thread(
            target=self._recv_loop,
            name=f"UdpReceiver-{self._bind_addr[1]}",
            daemon=True,
        )
        self._thread.start()
        logger.info("[UdpReceiver] 수신 시작 — %s", self._bind_addr)

    def stop(self) -> None:
        """수신 스레드 중지."""
        self._running.clear()
        if self._thread is not None:
            self._thread.join(timeout=2.0)
            self._thread = None
        if self._sock is not None:
            self._sock.close()
            self._sock = None
        logger.info(
            "[UdpReceiver] 수신 중지 (총 %d 패킷, 에러 %d)",
            self._recv_count, self._error_count,
        )

    @property
    def is_running(self) -> bool:
        return self._running.is_set()

    @property
    def stats(self) -> dict:
        return {
            "bind": self._bind_addr,
            "total_packets": self._recv_count,
            "total_bytes": self._recv_bytes,
            "errors": self._error_count,
        }

    # ── 수신 루프 ──

    def _recv_loop(self) -> None:
        """수신 스레드 메인 루프 — 패킷 수신 → 파싱 → 콜백."""
        while self._running.is_set():
            try:
                data, addr = self._sock.recvfrom(self._buffer_size)
            except socket.timeout:
                # 타임아웃 — _running 플래그 확인 후 재시도
                continue
            except OSError:
                # 소켓 닫힘 등
                if self._running.is_set():
                    logger.error("[UdpReceiver] 소켓 에러 — 수신 중단")
                break

            self._recv_count += 1
            self._recv_bytes += len(data)

            try:
                self._dispatch_packet(data, addr)
            except Exception as e:
                self._error_count += 1
                logger.error(
                    "[UdpReceiver] 패킷 파싱 실패 from %s: %s",
                    addr, e,
                )

    def _dispatch_packet(self, data: bytes, addr: tuple) -> None:
        """패킷을 파싱하고 적절한 콜백으로 전달."""
        header = PacketHeader.decode(data, offset=0)
        payload = data[HEADER_SIZE: HEADER_SIZE + header.payload_len]

        if header.packet_type == PacketType.FUSED_FRAME:
            if self.on_fused_frame is not None:
                frame = FusedDataFrame.from_bytes(payload)
                self.on_fused_frame(frame, addr)

        elif header.packet_type == PacketType.ENGAGEMENT:
            if self.on_engagement is not None:
                (score,) = struct.unpack_from("<f", payload, 0)
                self.on_engagement(score, header.timestamp_ms, addr)

        elif header.packet_type == PacketType.HANDSHAKE:
            if self.on_handshake is not None:
                self.on_handshake(addr)

        elif header.packet_type == PacketType.HEARTBEAT:
            if self.on_heartbeat is not None:
                self.on_heartbeat(addr)

        elif header.packet_type == PacketType.CLOCK_SYNC:
            if self.on_clock_sync is not None:
                cs = ClockSyncPayload.decode(payload)
                self.on_clock_sync(cs, addr)

        else:
            logger.warning(
                "[UdpReceiver] 알 수 없는 패킷 타입: %#x from %s",
                header.packet_type, addr,
            )
