"""
test_a_leg — Phase 2 A-leg 통합 테스트

검증 대상:
    1. LinkBandReceiver — 스마트폰을 시뮬레이션한 파셜 FusedDataFrame 송신을
       수신하여 EEG/PPG/ACC 슬롯으로 노출
    2. ControlServer — Handshake 응답, Heartbeat 추적, ClockSync 왕복
    3. SessionLogger — FusedDataFrame을 CSV에 기록, meta.json 생성

모두 로컬호스트에서 self-loop으로 검증.
"""

from __future__ import annotations

import socket
import sys
import tempfile
import time
from pathlib import Path

from .fused_data_frame import (
    ACCData,
    EEGData,
    FusedDataFrame,
    PPGData,
    SensorFlags,
)
from .linkband_receiver import LinkBandReceiver, LINKBAND_INGEST_PORT
from .control_server import ControlServer
from .session_logger import SessionLogger, LoggerConfig
from .udp_protocol import (
    CONTROL_PORT,
    PacketHeader,
    PacketType,
    UdpSender,
)


def test_linkband_receiver_partial_frame() -> bool:
    """스마트폰 Mock: EEG|PPG|ACC만 set된 파셜 프레임 1개 송신 → facade로 조회."""
    # 포트 충돌 회피
    port = LINKBAND_INGEST_PORT + 50
    recv = LinkBandReceiver(bind_ip="127.0.0.1", bind_port=port, stale_ms=2000)
    recv.start()
    try:
        # 파셜 프레임 조립
        frame = FusedDataFrame(
            eeg=EEGData(channels=[1.0, 2.0, 0.0, 0.0, 0.0, 0.0],
                        contact_quality=[255, 255, 0, 0, 0, 0]),
            ppg=PPGData(ir=123.0, red=456.0),
            acc=ACCData(x=0.1, y=0.2, z=0.3),
            valid_sensors=SensorFlags.EEG | SensorFlags.PPG | SensorFlags.ACC,
            timestamp_ms=int(time.time() * 1000),
        )
        # UdpSender는 기본 FUSED_FRAME_PORT(9001)로 보내므로,
        # 직접 소켓으로 커스텀 포트에 송신
        payload = frame.to_bytes()
        header = PacketHeader(
            packet_type=PacketType.FUSED_FRAME,
            sequence_num=0,
            timestamp_ms=frame.timestamp_ms,
            payload_len=len(payload),
        )
        packet = header.encode() + payload
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.sendto(packet, ("127.0.0.1", port))
        sock.close()

        # 수신 대기
        time.sleep(0.2)

        eeg = recv.get_latest_eeg()
        ppg = recv.get_latest_ppg()
        acc = recv.get_latest_acc()
        if eeg is None or ppg is None or acc is None:
            print(f"[FAIL] 파셜 프레임 수신 실패: eeg={eeg}, ppg={ppg}, acc={acc}")
            return False
        if abs(eeg.channels[0] - 1.0) > 1e-6 or abs(ppg.ir - 123.0) > 1e-6:
            print("[FAIL] 수신 값 불일치")
            return False

        # facade 동작 검증
        eeg_src = recv.eeg_source()
        latest = eeg_src.get_latest()
        if latest is None or abs(latest.channels[1] - 2.0) > 1e-6:
            print("[FAIL] eeg_source facade 조회 실패")
            return False

        print("[PASS] LinkBandReceiver 파셜 프레임 수신 통과")
        return True
    finally:
        recv.stop()


def test_control_server_handshake_heartbeat_clocksync() -> bool:
    """Handshake 응답, Heartbeat 카운트, ClockSync 왕복 검증."""
    # 포트 충돌 회피
    port = CONTROL_PORT + 50
    server = ControlServer(bind_ip="127.0.0.1", bind_port=port)
    server.start()
    try:
        # 클라이언트: UdpSender(control_port=테스트 포트)
        sender = UdpSender(
            target_ip="127.0.0.1",
            control_port=port,
        )
        sender.send_handshake()
        sender.send_heartbeat()
        sender.send_heartbeat()
        t1 = sender.send_clock_sync_request()

        # 서버 처리 대기
        time.sleep(0.3)

        stats = server.stats
        if stats["handshakes"] != 1:
            print(f"[FAIL] handshake 카운트 불일치: {stats['handshakes']}")
            return False
        if stats["heartbeats"] != 2:
            print(f"[FAIL] heartbeat 카운트 불일치: {stats['heartbeats']}")
            return False
        if stats["clock_syncs"] != 1:
            print(f"[FAIL] clock_sync 카운트 불일치: {stats['clock_syncs']}")
            return False
        alive = server.list_alive_clients()
        if not alive:
            print("[FAIL] 생존 클라이언트 없음")
            return False

        print(f"[PASS] ControlServer 통과 (t1={t1}, alive={alive})")
        return True
    finally:
        server.stop()


def test_session_logger_writes_csv() -> bool:
    """SessionLogger에 10프레임 log_frame → CSV 확인."""
    with tempfile.TemporaryDirectory() as tmpdir:
        log_dir = Path(tmpdir)
        logger = SessionLogger(
            log_dir=log_dir,
            session_id="test_session",
            config=LoggerConfig(
                log_eeg=True, log_ppg=True, log_acc=True, log_imu=False,
                log_pose=False, log_blendshape=False, flush_every_n=5,
            ),
        )
        logger.start(meta={"test": True})
        for i in range(10):
            frame = FusedDataFrame(
                eeg=EEGData(channels=[float(i)] * 6, contact_quality=[255] * 6),
                ppg=PPGData(ir=float(i * 100), red=float(i * 90)),
                acc=ACCData(x=0.0, y=-1.0, z=0.0),
                valid_sensors=SensorFlags.EEG | SensorFlags.PPG | SensorFlags.ACC,
                timestamp_ms=1_700_000_000_000 + i,
            )
            logger.log_frame(frame)
        # writer thread가 drain하도록 대기
        time.sleep(0.3)
        logger.stop()

        session_dir = log_dir / "test_session"
        meta = session_dir / "meta.json"
        fused = session_dir / "fused_frames.csv"
        eeg_csv = session_dir / "eeg.csv"

        if not meta.exists() or not fused.exists() or not eeg_csv.exists():
            print("[FAIL] 예상 파일 누락")
            return False

        fused_lines = fused.read_text().strip().splitlines()
        # header + 10 rows
        if len(fused_lines) != 11:
            print(f"[FAIL] fused_frames.csv 행 수 불일치: {len(fused_lines)} (기대 11)")
            return False
        eeg_lines = eeg_csv.read_text().strip().splitlines()
        if len(eeg_lines) != 11:
            print(f"[FAIL] eeg.csv 행 수 불일치: {len(eeg_lines)}")
            return False

        print(f"[PASS] SessionLogger 10프레임 CSV 기록 통과 ({session_dir})")
        return True


def main() -> int:
    results = [
        ("LinkBandReceiver partial frame", test_linkband_receiver_partial_frame()),
        ("ControlServer handshake/heartbeat/clocksync", test_control_server_handshake_heartbeat_clocksync()),
        ("SessionLogger CSV write", test_session_logger_writes_csv()),
    ]
    print("\n=== Phase 2 A-leg 테스트 요약 ===")
    all_pass = True
    for name, passed in results:
        mark = "PASS" if passed else "FAIL"
        print(f"  [{mark}] {name}")
        all_pass = all_pass and passed
    return 0 if all_pass else 1


if __name__ == "__main__":
    sys.exit(main())
