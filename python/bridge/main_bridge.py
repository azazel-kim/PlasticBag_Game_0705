"""
main_bridge — BridgeOrchestrator CLI 엔트리포인트

사용 예시::

    # Mock 4종으로 로컬호스트에 30Hz 송신 (Galaxy XR 없이 self-loopback 검증용)
    python3 -m bridge.main_bridge --target-ip 127.0.0.1 --mock all --duration 10

    # 실제 Galaxy XR IP로 송신
    python3 -m bridge.main_bridge --target-ip 192.168.0.45 --mock eeg,ppg,acc,imu

    # 통계를 파일로 저장
    python3 -m bridge.main_bridge --target-ip 127.0.0.1 --mock all --stats-out /tmp/stats.json
"""

from __future__ import annotations

import argparse
import json
import logging
import signal
import sys
import time
from pathlib import Path
from typing import Optional

from .control_server import ControlServer
from .linkband_receiver import LinkBandReceiver
from .mock_sensors import (
    MockAccSource,
    MockEegSource,
    MockImuSource,
    MockPpgSource,
)
from .orchestrator import BridgeOrchestrator
from .sensor_source import SensorSource
from .session_logger import LoggerConfig, SessionLogger
from .udp_protocol import UdpSender

logger = logging.getLogger("main_bridge")


_MOCK_KEYS = {"eeg", "ppg", "acc", "imu"}


def _parse_mock_arg(value: str) -> set[str]:
    """'--mock all' 또는 '--mock eeg,ppg' 파싱."""
    value = value.strip().lower()
    if value in ("all", "*"):
        return set(_MOCK_KEYS)
    if value in ("none", ""):
        return set()
    out: set[str] = set()
    for piece in value.split(","):
        piece = piece.strip()
        if piece in _MOCK_KEYS:
            out.add(piece)
        elif piece:
            raise ValueError(f"알 수 없는 mock 이름: {piece}")
    return out


def _build_mock_sources(mocks: set[str]) -> list[SensorSource]:
    sources: list[SensorSource] = []
    if "eeg" in mocks:
        sources.append(MockEegSource(sampling_hz=250.0, num_channels=2))
    if "ppg" in mocks:
        sources.append(MockPpgSource(sampling_hz=50.0, bpm=72.0))
    if "acc" in mocks:
        sources.append(MockAccSource(sampling_hz=25.0))
    if "imu" in mocks:
        sources.append(MockImuSource(sampling_hz=60.0))
    return sources


def _setup_logging(verbose: bool) -> None:
    level = logging.DEBUG if verbose else logging.INFO
    logging.basicConfig(
        level=level,
        format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
        datefmt="%H:%M:%S",
    )


def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="XR Exergame PC Bridge Orchestrator",
    )
    parser.add_argument(
        "--target-ip",
        required=True,
        help="Galaxy XR Unity의 IP 주소 (self-loop 시 127.0.0.1)",
    )
    parser.add_argument(
        "--mock",
        default="all",
        help="Mock 센서 지정: all / eeg,ppg,acc,imu / none (기본: all)",
    )
    parser.add_argument(
        "--rate",
        type=float,
        default=30.0,
        help="프레임 송신 주기 Hz (기본: 30)",
    )
    parser.add_argument(
        "--heartbeat",
        type=float,
        default=1.0,
        help="Heartbeat 주기 Hz, 0이면 비활성 (기본: 1)",
    )
    parser.add_argument(
        "--duration",
        type=float,
        default=0.0,
        help="실행 시간(초), 0이면 Ctrl+C까지 무한 (기본: 0)",
    )
    parser.add_argument(
        "--stats-out",
        type=Path,
        default=None,
        help="종료 시 통계 JSON을 쓸 경로 (기본: 미저장)",
    )
    parser.add_argument(
        "--with-linkband",
        action="store_true",
        help="LinkBandReceiver 활성화 (스마트폰→PC UDP 9010 수신)",
    )
    parser.add_argument(
        "--linkband-port",
        type=int,
        default=9010,
        help="LinkBand 수신 포트 (기본: 9010)",
    )
    parser.add_argument(
        "--with-control",
        action="store_true",
        help="ControlServer 활성화 (9003 Handshake/Heartbeat/ClockSync)",
    )
    parser.add_argument(
        "--log-dir",
        type=Path,
        default=None,
        help="SessionLogger 로그 디렉토리 (미지정 시 로깅 비활성)",
    )
    parser.add_argument(
        "--verbose", "-v",
        action="store_true",
        help="DEBUG 로그 활성화",
    )
    args = parser.parse_args(argv)

    _setup_logging(args.verbose)

    try:
        mocks = _parse_mock_arg(args.mock)
    except ValueError as e:
        logger.error("%s", e)
        return 2

    sources = _build_mock_sources(mocks)

    # 선택: 실제 LinkBand(스마트폰) 수신기 추가 — EEG/PPG/ACC Source 3개 제공
    linkband: Optional[LinkBandReceiver] = None
    if args.with_linkband:
        linkband = LinkBandReceiver(bind_port=args.linkband_port)
        linkband.start()
        sources.append(linkband.eeg_source())
        sources.append(linkband.ppg_source())
        sources.append(linkband.acc_source())

    if not sources:
        logger.error("등록 가능한 소스가 없습니다 (mocks=%s)", mocks)
        return 2

    # 선택: 세션 로거
    session_logger: Optional[SessionLogger] = None
    on_frame = None
    if args.log_dir is not None:
        session_logger = SessionLogger(
            log_dir=args.log_dir,
            config=LoggerConfig(),
        )
        session_logger.start(meta={"target_ip": args.target_ip,
                                   "rate_hz": args.rate,
                                   "mocks": sorted(mocks),
                                   "with_linkband": bool(args.with_linkband)})
        on_frame = session_logger.log_frame

    # 선택: 제어 서버 (Handshake/Heartbeat/ClockSync)
    control: Optional[ControlServer] = None
    if args.with_control:
        control = ControlServer()
        control.start()

    sender = UdpSender(target_ip=args.target_ip)
    orchestrator = BridgeOrchestrator(
        sender=sender,
        fusion_rate_hz=args.rate,
        heartbeat_hz=args.heartbeat,
        on_frame=on_frame,
    )
    for src in sources:
        orchestrator.add_source(src)

    # SIGINT/SIGTERM 핸들링 — 정상 종료
    stop_flag = {"done": False}

    def _handle_signal(signum, frame):  # noqa: ARG001
        logger.info("시그널 %s 수신 — 종료 절차 시작", signum)
        stop_flag["done"] = True

    signal.signal(signal.SIGINT, _handle_signal)
    signal.signal(signal.SIGTERM, _handle_signal)

    orchestrator.start()

    # 메인 스레드는 duration 만큼 대기하며 1초 간격으로 통계 로깅
    started = time.monotonic()
    try:
        while not stop_flag["done"]:
            time.sleep(1.0)
            stats = orchestrator.stats
            logger.info(
                "진행: sent=%d, rate=%.2fHz, skipped=%d, errors=%d, hits=%s",
                stats["frames_sent"], stats["send_rate_hz"],
                stats["frames_skipped"], stats["send_errors"], stats["source_hits"],
            )
            if args.duration > 0 and (time.monotonic() - started) >= args.duration:
                break
    finally:
        orchestrator.stop()
        if linkband is not None:
            linkband.stop()
        if control is not None:
            control.stop()
        if session_logger is not None:
            session_logger.stop()
        final_stats = orchestrator.stats
        logger.info("최종 통계: %s", final_stats)
        if args.stats_out is not None:
            args.stats_out.parent.mkdir(parents=True, exist_ok=True)
            args.stats_out.write_text(json.dumps(final_stats, ensure_ascii=False, indent=2))
            logger.info("통계 저장: %s", args.stats_out)

    return 0


if __name__ == "__main__":
    sys.exit(main())
