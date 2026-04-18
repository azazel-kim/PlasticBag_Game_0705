"""
ControlServer — 9003 포트 Handshake/Heartbeat/ClockSync 처리

역할:
    - 클라이언트(Galaxy XR Unity, Galaxy Fold Android) 중 하나가 먼저 Handshake를
      보내오면 응답하고 연결 상태로 전환
    - Heartbeat를 수신하여 클라이언트 생존을 추적
    - ClockSync 요청 수신 시 현재 서버 시각으로 응답 (NTP 스타일)

스레딩:
    - 내부에 UdpReceiver 1개를 소유, 별도 스레드에서 수신 루프 가동
    - 응답 송신은 동일 스레드에서 즉시 수행(소켓은 UdpSender를 지연 생성)
"""

from __future__ import annotations

import logging
import socket
import threading
import time
from dataclasses import dataclass, field
from typing import Optional

from .udp_protocol import (
    CONTROL_PORT,
    ClockSyncPayload,
    PacketHeader,
    PacketType,
    UdpReceiver,
)

logger = logging.getLogger(__name__)


@dataclass
class ClientState:
    """단일 클라이언트(IP, port) 상태."""
    addr: tuple
    connected_at: float = 0.0
    last_heartbeat: float = 0.0
    last_clock_sync: float = 0.0
    clock_syncs: int = 0

    def is_alive(self, timeout_sec: float = 5.0) -> bool:
        if self.last_heartbeat == 0.0:
            return False
        return (time.monotonic() - self.last_heartbeat) <= timeout_sec


@dataclass
class ControlStats:
    """ControlServer 통계."""
    handshakes: int = 0
    heartbeats: int = 0
    clock_syncs: int = 0
    clients: dict[str, dict] = field(default_factory=dict)


class ControlServer:
    """9003 포트 Handshake/Heartbeat/ClockSync 서버.

    매개변수:
        bind_ip: 바인드 IP (기본 "0.0.0.0")
        bind_port: 바인드 포트 (기본 9003)
        heartbeat_timeout_sec: Heartbeat 미수신 시 연결 끊김 판정 시간
    """

    def __init__(
        self,
        bind_ip: str = "0.0.0.0",
        bind_port: int = CONTROL_PORT,
        heartbeat_timeout_sec: float = 5.0,
    ) -> None:
        self._bind_ip = bind_ip
        self._bind_port = bind_port
        self._heartbeat_timeout = float(heartbeat_timeout_sec)

        self._receiver = UdpReceiver(bind_ip=bind_ip, bind_port=bind_port)
        self._receiver.on_handshake = self._handle_handshake
        self._receiver.on_heartbeat = self._handle_heartbeat
        self._receiver.on_clock_sync = self._handle_clock_sync

        self._send_sock: Optional[socket.socket] = None
        self._clients: dict[tuple, ClientState] = {}
        self._lock = threading.Lock()
        self._stats = ControlStats()
        self._handshake_seq = 0
        self._clock_seq = 0

        logger.info("[ControlServer] 초기화 — %s:%d", bind_ip, bind_port)

    # ── 라이프사이클 ──

    def start(self) -> None:
        self._send_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self._receiver.start()
        logger.info("[ControlServer] 시작")

    def stop(self) -> None:
        self._receiver.stop()
        if self._send_sock is not None:
            self._send_sock.close()
            self._send_sock = None
        logger.info("[ControlServer] 중지")

    @property
    def is_running(self) -> bool:
        return self._receiver.is_running

    @property
    def stats(self) -> dict:
        with self._lock:
            alive = {
                f"{addr[0]}:{addr[1]}": {
                    "connected_at": st.connected_at,
                    "last_heartbeat_ago_ms": int(
                        (time.monotonic() - st.last_heartbeat) * 1000
                    ) if st.last_heartbeat > 0 else None,
                    "clock_syncs": st.clock_syncs,
                    "alive": st.is_alive(self._heartbeat_timeout),
                }
                for addr, st in self._clients.items()
            }
        return {
            "handshakes": self._stats.handshakes,
            "heartbeats": self._stats.heartbeats,
            "clock_syncs": self._stats.clock_syncs,
            "clients": alive,
        }

    def list_alive_clients(self) -> list[tuple]:
        """현재 생존 중인 클라이언트 주소 목록."""
        with self._lock:
            return [
                addr for addr, st in self._clients.items()
                if st.is_alive(self._heartbeat_timeout)
            ]

    # ── 수신 핸들러 ──

    def _handle_handshake(self, addr: tuple) -> None:
        now = time.monotonic()
        with self._lock:
            state = self._clients.get(addr)
            if state is None:
                state = ClientState(addr=addr, connected_at=now)
                self._clients[addr] = state
            state.last_heartbeat = now  # handshake도 생존 신호로 간주
            self._stats.handshakes += 1
        logger.info("[ControlServer] handshake from %s", addr)
        # 응답: Handshake ACK (payload 없음, sequence 증가)
        self._send_control(PacketType.HANDSHAKE, b"", addr)

    def _handle_heartbeat(self, addr: tuple) -> None:
        now = time.monotonic()
        with self._lock:
            state = self._clients.get(addr)
            if state is None:
                state = ClientState(addr=addr, connected_at=now)
                self._clients[addr] = state
            state.last_heartbeat = now
            self._stats.heartbeats += 1
        # 응답 없음 — Heartbeat는 단방향

    def _handle_clock_sync(self, payload: ClockSyncPayload, addr: tuple) -> None:
        """ClockSync 요청 수신 — t2를 서버 시각으로 채워 응답."""
        now_ms = int(time.time() * 1000)
        response = ClockSyncPayload(t1_ms=payload.t1_ms, t2_ms=now_ms)
        with self._lock:
            state = self._clients.get(addr)
            if state is None:
                state = ClientState(addr=addr, connected_at=time.monotonic())
                self._clients[addr] = state
            state.last_clock_sync = time.monotonic()
            state.clock_syncs += 1
            self._stats.clock_syncs += 1
        self._send_control(PacketType.CLOCK_SYNC, response.encode(), addr,
                           timestamp_ms=now_ms)
        logger.debug(
            "[ControlServer] clock_sync from %s: t1=%d, t2=%d (drift=%+dms)",
            addr, payload.t1_ms, now_ms, now_ms - payload.t1_ms,
        )

    # ── 송신 ──

    def _send_control(
        self,
        packet_type: PacketType,
        payload: bytes,
        addr: tuple,
        timestamp_ms: int = 0,
    ) -> None:
        if self._send_sock is None:
            logger.warning("[ControlServer] 송신 소켓 없음 (stop된 상태?)")
            return
        if timestamp_ms == 0:
            timestamp_ms = int(time.time() * 1000)
        if packet_type == PacketType.HANDSHAKE:
            seq = self._handshake_seq
            self._handshake_seq = (seq + 1) % (2**32)
        elif packet_type == PacketType.CLOCK_SYNC:
            seq = self._clock_seq
            self._clock_seq = (seq + 1) % (2**32)
        else:
            seq = 0
        header = PacketHeader(
            packet_type=packet_type,
            sequence_num=seq,
            timestamp_ms=timestamp_ms,
            payload_len=len(payload),
        )
        packet = header.encode() + payload
        try:
            self._send_sock.sendto(packet, addr)
        except OSError as e:
            logger.error("[ControlServer] 송신 실패 → %s: %s", addr, e)


__all__ = ["ControlServer", "ClientState", "ControlStats"]
