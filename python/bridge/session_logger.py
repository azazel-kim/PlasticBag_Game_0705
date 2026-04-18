"""
SessionLogger — FusedDataFrame을 CSV로 기록하는 비동기 로거

동작:
    - 메인 스레드에서 `log_frame(frame)` 호출 → ConcurrentQueue에 push
    - 백그라운드 writer 스레드가 큐에서 pop → CSV에 append
    - 세션 시작 시 메타데이터(JSON)를 별도 파일로 기록
    - 종료 시 큐 drain 후 파일 닫기

BlendShapeLogger.cs의 패턴(ConcurrentQueue + background thread)을 재사용.

파일 레이아웃:
    <log_dir>/session_<YYYYMMDD_HHMMSS>/
        meta.json          — 세션 메타(시작/종료 시각, 설정)
        fused_frames.csv   — 각 프레임 한 줄 (timestamp_ms + valid_sensors + 요약 값)
        eeg.csv            — EEG 채널별 상세 (있을 때만)
        ppg.csv, acc.csv, imu.csv, pose.csv, blendshape.csv

주의:
    - 대용량·고차원 데이터(BlendShape 68D, Pose 132D)는 선택적으로만 기록.
    - 모든 CSV는 UTF-8, 헤더 포함.
"""

from __future__ import annotations

import csv
import json
import logging
import queue
import threading
import time
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Optional

from .fused_data_frame import FusedDataFrame, SensorFlags

logger = logging.getLogger(__name__)


@dataclass
class LoggerConfig:
    """SessionLogger 옵션."""
    log_eeg: bool = True
    log_ppg: bool = True
    log_acc: bool = True
    log_imu: bool = True
    log_pose: bool = False          # 132 floats — 큼
    log_blendshape: bool = False    # 68 floats — 큼
    flush_every_n: int = 30         # 30 프레임(=1초)마다 flush


@dataclass
class SessionStats:
    """세션 통계."""
    frames_queued: int = 0
    frames_written: int = 0
    drops: int = 0
    started_at: float = 0.0
    finished_at: float = 0.0
    meta: dict = field(default_factory=dict)


class SessionLogger:
    """비동기 CSV 세션 로거.

    매개변수:
        log_dir: 상위 디렉토리 — 세션별 하위 폴더가 자동 생성됨
        session_id: 세션 식별자 (미지정 시 타임스탬프)
        config: LoggerConfig
        queue_maxsize: 큐 최대 크기 (0 = 무제한). 초과 시 drop 집계.
    """

    def __init__(
        self,
        log_dir: Path,
        session_id: Optional[str] = None,
        config: Optional[LoggerConfig] = None,
        queue_maxsize: int = 0,
    ) -> None:
        self._config = config or LoggerConfig()
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        self._session_id = session_id or f"session_{timestamp}"
        self._session_dir = Path(log_dir) / self._session_id
        self._queue: queue.Queue[Optional[FusedDataFrame]] = queue.Queue(maxsize=queue_maxsize)

        self._thread: Optional[threading.Thread] = None
        self._running = False
        self._stats = SessionStats()

        # 파일 핸들 — start() 에서 open
        self._files: dict[str, object] = {}
        self._writers: dict[str, csv.writer] = {}

        logger.info("[SessionLogger] 초기화 — %s", self._session_dir)

    # ── 라이프사이클 ──

    def start(self, meta: Optional[dict] = None) -> None:
        if self._running:
            return
        self._session_dir.mkdir(parents=True, exist_ok=True)
        self._stats = SessionStats()
        self._stats.started_at = time.time()
        self._stats.meta = dict(meta or {})

        # 파일 오픈 + 헤더 기록
        self._open_files()

        # meta.json
        meta_path = self._session_dir / "meta.json"
        meta_path.write_text(
            json.dumps(
                {
                    "session_id": self._session_id,
                    "started_at_iso": datetime.now().isoformat(timespec="seconds"),
                    "started_at_ms": int(self._stats.started_at * 1000),
                    "config": self._config.__dict__,
                    "meta": self._stats.meta,
                },
                ensure_ascii=False,
                indent=2,
            )
        )

        self._running = True
        self._thread = threading.Thread(
            target=self._writer_loop,
            name="SessionLogger",
            daemon=True,
        )
        self._thread.start()
        logger.info("[SessionLogger] 시작 — %s", self._session_dir)

    def stop(self) -> None:
        if not self._running:
            return
        self._running = False
        # 종료 sentinel
        try:
            self._queue.put_nowait(None)
        except queue.Full:
            pass
        if self._thread is not None:
            self._thread.join(timeout=5.0)
            self._thread = None

        # 파일 flush/close
        for fh in self._files.values():
            try:
                fh.flush()      # type: ignore[attr-defined]
                fh.close()      # type: ignore[attr-defined]
            except Exception as e:
                logger.error("[SessionLogger] 파일 close 에러: %s", e)
        self._files.clear()
        self._writers.clear()

        # meta.json 갱신 — 종료 시각
        self._stats.finished_at = time.time()
        meta_path = self._session_dir / "meta.json"
        try:
            meta = json.loads(meta_path.read_text())
            meta["finished_at_iso"] = datetime.now().isoformat(timespec="seconds")
            meta["finished_at_ms"] = int(self._stats.finished_at * 1000)
            meta["frames_written"] = self._stats.frames_written
            meta["frames_queued"] = self._stats.frames_queued
            meta["drops"] = self._stats.drops
            meta_path.write_text(json.dumps(meta, ensure_ascii=False, indent=2))
        except Exception as e:
            logger.error("[SessionLogger] meta.json 갱신 실패: %s", e)

        logger.info(
            "[SessionLogger] 종료 — %d 프레임 기록, %d drop",
            self._stats.frames_written, self._stats.drops,
        )

    @property
    def is_running(self) -> bool:
        return self._running

    @property
    def session_dir(self) -> Path:
        return self._session_dir

    @property
    def stats(self) -> dict:
        return {
            "frames_queued": self._stats.frames_queued,
            "frames_written": self._stats.frames_written,
            "drops": self._stats.drops,
            "queue_size": self._queue.qsize(),
        }

    # ── 로그 입력 ──

    def log_frame(self, frame: FusedDataFrame) -> None:
        """프레임을 큐에 push — 메인 스레드에서 호출."""
        if not self._running:
            return
        try:
            self._queue.put_nowait(frame)
            self._stats.frames_queued += 1
        except queue.Full:
            self._stats.drops += 1

    # ── 내부 writer 루프 ──

    def _open_files(self) -> None:
        """각 CSV 파일을 열고 헤더 라인을 기록."""
        # fused_frames.csv: 모든 프레임의 요약
        self._open_csv("fused", "fused_frames.csv",
                       ["timestamp_ms", "valid_sensors", "sensor_names"])
        if self._config.log_eeg:
            self._open_csv("eeg", "eeg.csv",
                           ["timestamp_ms",
                            "ch0", "ch1", "ch2", "ch3", "ch4", "ch5",
                            "q0", "q1", "q2", "q3", "q4", "q5"])
        if self._config.log_ppg:
            self._open_csv("ppg", "ppg.csv", ["timestamp_ms", "ir", "red"])
        if self._config.log_acc:
            self._open_csv("acc", "acc.csv", ["timestamp_ms", "x", "y", "z"])
        if self._config.log_imu:
            self._open_csv("imu", "imu.csv",
                           ["timestamp_ms",
                            "qx", "qy", "qz", "qw",
                            "ax", "ay", "az",
                            "gx", "gy", "gz",
                            "mx", "my", "mz"])
        if self._config.log_pose:
            cols = ["timestamp_ms"]
            for i in range(33):
                cols.extend([f"p{i}_x", f"p{i}_y", f"p{i}_z", f"p{i}_v"])
            self._open_csv("pose", "pose.csv", cols)
        if self._config.log_blendshape:
            cols = ["timestamp_ms"] + [f"bs{i}" for i in range(68)] + \
                   ["conf_all", "conf_l", "conf_r"]
            self._open_csv("blendshape", "blendshape.csv", cols)

    def _open_csv(self, key: str, filename: str, header: list[str]) -> None:
        path = self._session_dir / filename
        fh = path.open("w", encoding="utf-8", newline="")
        writer = csv.writer(fh)
        writer.writerow(header)
        self._files[key] = fh
        self._writers[key] = writer

    def _writer_loop(self) -> None:
        count_since_flush = 0
        while self._running or not self._queue.empty():
            try:
                frame = self._queue.get(timeout=0.2)
            except queue.Empty:
                continue
            if frame is None:
                break
            try:
                self._write_frame(frame)
                self._stats.frames_written += 1
                count_since_flush += 1
                if count_since_flush >= self._config.flush_every_n:
                    self._flush_all()
                    count_since_flush = 0
            except Exception as e:
                logger.error("[SessionLogger] write 에러: %s", e)
        self._flush_all()

    def _flush_all(self) -> None:
        for fh in self._files.values():
            try:
                fh.flush()  # type: ignore[attr-defined]
            except Exception:
                pass

    def _write_frame(self, frame: FusedDataFrame) -> None:
        ts = frame.timestamp_ms
        flags = int(frame.valid_sensors)
        self._writers["fused"].writerow(
            [ts, flags, "|".join(frame.sensor_names())]
        )
        if self._config.log_eeg and frame.valid_sensors & SensorFlags.EEG and frame.eeg is not None:
            self._writers["eeg"].writerow(
                [ts] + list(frame.eeg.channels) + list(frame.eeg.contact_quality)
            )
        if self._config.log_ppg and frame.valid_sensors & SensorFlags.PPG and frame.ppg is not None:
            self._writers["ppg"].writerow([ts, frame.ppg.ir, frame.ppg.red])
        if self._config.log_acc and frame.valid_sensors & SensorFlags.ACC and frame.acc is not None:
            self._writers["acc"].writerow([ts, frame.acc.x, frame.acc.y, frame.acc.z])
        if self._config.log_imu and frame.valid_sensors & SensorFlags.IMU and frame.imu is not None:
            imu = frame.imu
            self._writers["imu"].writerow(
                [ts] + list(imu.quaternion) + list(imu.acceleration)
                + list(imu.gyroscope) + list(imu.magnetometer)
            )
        if self._config.log_pose and frame.valid_sensors & SensorFlags.POSE and frame.pose is not None:
            self._writers["pose"].writerow([ts] + list(frame.pose.landmarks))
        if self._config.log_blendshape and frame.valid_sensors & SensorFlags.BLENDSHAPE and frame.blendshape is not None:
            self._writers["blendshape"].writerow(
                [ts] + list(frame.blendshape.weights) + list(frame.blendshape.confidence)
            )


__all__ = ["SessionLogger", "LoggerConfig", "SessionStats"]
