"""
MediaPipe BlazePose 브릿지 — OBSBOT Tiny2/웹캠 → UDP 포즈 전송

OBSBOT Tiny2(또는 기본 웹캠)에서 프레임을 캡처하여 MediaPipe BlazePose로
33개 전신 랜드마크를 추출하고, UDP 9001로 FusedDataFrame의 pose 필드에
담아 전송하는 브릿지.

설치:
    pip install mediapipe opencv-python numpy

사용 예시::

    from bridge.mediapipe_bridge import MediaPipeBridge

    bridge = MediaPipeBridge(target_ip="192.168.1.50", camera_idx=0, gui=True)
    bridge.start()
    # ... 약 10초 실행 ...
    stats = bridge.stop()
    print(stats)
"""

from __future__ import annotations

import logging
import threading
import time
from dataclasses import dataclass
from typing import Optional

try:
    import cv2
    import mediapipe as mp
    import numpy as np
except ImportError as e:
    raise ImportError(
        "MediaPipe 브릿지에 필요한 패키지가 없음. 설치:\n"
        "  pip install mediapipe opencv-python numpy"
    ) from e

from .fused_data_frame import FusedDataFrame, PoseData, SensorFlags
from .udp_protocol import UdpSender

logger = logging.getLogger(__name__)


# ─────────────────────────────────────────────
# 설정 및 상수
# ─────────────────────────────────────────────

@dataclass
class MediaPipeConfig:
    """MediaPipe BlazePose 설정."""
    # 포즈 추정
    static_image_mode: bool = False
    model_complexity: int = 1  # 0=Lite, 1=Full, 2=Heavy
    smooth_landmarks: bool = True
    enable_segmentation: bool = False
    smooth_segmentation: bool = False
    min_detection_confidence: float = 0.7
    min_tracking_confidence: float = 0.7

    # 카메라
    camera_idx: int = 0
    target_width: int = 640
    target_height: int = 480
    target_fps: int = 30

    # UI
    show_gui: bool = True
    gui_scale: float = 0.5


# ─────────────────────────────────────────────
# MediaPipe 브릿지
# ─────────────────────────────────────────────

class MediaPipeBridge:
    """OBSBOT Tiny2/웹캠 → MediaPipe → UDP 포즈 전송 브릿지.

    매개변수:
        target_ip: Galaxy XR 디바이스 IP 주소
        camera_idx: 웹캠 인덱스 (기본 0 = 기본 카메라)
        gui: 시각화 창 표시 여부 (기본 True)
        config: MediaPipeConfig 객체 (None이면 기본 설정 사용)

    라이프사이클:
        bridge = MediaPipeBridge(target_ip="192.168.1.50", gui=True)
        bridge.start()
        # ... 약 10초 실행 ...
        stats = bridge.stop()
    """

    def __init__(
        self,
        target_ip: str,
        camera_idx: int = 0,
        gui: bool = True,
        config: Optional[MediaPipeConfig] = None,
    ) -> None:
        self._target_ip = target_ip
        self._config = config or MediaPipeConfig(
            camera_idx=camera_idx,
            show_gui=gui,
        )

        # 카메라 및 MediaPipe 초기화
        self._cap: Optional[cv2.VideoCapture] = None
        self._mp_pose: Optional[mp.solutions.pose.Pose] = None

        # UDP 송신기
        self._sender: Optional[UdpSender] = None

        # 실행 제어
        self._running = threading.Event()
        self._thread: Optional[threading.Thread] = None

        # 통계
        self._frame_count = 0
        self._pose_failures = 0
        self._udp_failures = 0
        self._start_time = 0.0

        logger.info(
            "[MediaPipeBridge] 초기화 — target=%s, camera=%d, gui=%s",
            target_ip, camera_idx, gui,
        )

    def start(self) -> None:
        """카메라 및 포즈 추정 스레드 시작."""
        if self._running.is_set():
            logger.warning("[MediaPipeBridge] 이미 실행 중")
            return

        # 카메라 초기화
        self._cap = cv2.VideoCapture(self._config.camera_idx)
        if not self._cap.isOpened():
            raise RuntimeError(
                f"웹캠 초기화 실패 (인덱스 {self._config.camera_idx}). "
                "카메라가 연결되어 있는지 확인하세요."
            )

        # 카메라 설정
        self._cap.set(cv2.CAP_PROP_FRAME_WIDTH, self._config.target_width)
        self._cap.set(cv2.CAP_PROP_FRAME_HEIGHT, self._config.target_height)
        self._cap.set(cv2.CAP_PROP_FPS, self._config.target_fps)

        # MediaPipe Pose 초기화
        self._mp_pose = mp.solutions.pose.Pose(
            static_image_mode=self._config.static_image_mode,
            model_complexity=self._config.model_complexity,
            smooth_landmarks=self._config.smooth_landmarks,
            enable_segmentation=self._config.enable_segmentation,
            smooth_segmentation=self._config.smooth_segmentation,
            min_detection_confidence=self._config.min_detection_confidence,
            min_tracking_confidence=self._config.min_tracking_confidence,
        )

        # UDP 송신기 초기화
        self._sender = UdpSender(target_ip=self._target_ip)

        # 실행 및 스레드 시작
        self._running.set()
        self._start_time = time.time()
        self._thread = threading.Thread(
            target=self._process_loop,
            name="MediaPipeBridge",
            daemon=True,
        )
        self._thread.start()
        logger.info("[MediaPipeBridge] 시작")

    def stop(self) -> dict:
        """프로세스 스레드 중지 및 통계 반환."""
        self._running.clear()

        if self._thread is not None:
            self._thread.join(timeout=5.0)
            self._thread = None

        # 리소스 정리
        if self._cap is not None:
            self._cap.release()
            self._cap = None

        if self._mp_pose is not None:
            self._mp_pose.close()
            self._mp_pose = None

        if self._sender is not None:
            self._sender.close()
            self._sender = None

        cv2.destroyAllWindows()

        elapsed = time.time() - self._start_time
        stats = {
            "elapsed_sec": elapsed,
            "total_frames": self._frame_count,
            "pose_failures": self._pose_failures,
            "udp_failures": self._udp_failures,
            "effective_fps": self._frame_count / elapsed if elapsed > 0 else 0,
        }

        logger.info(
            "[MediaPipeBridge] 중지 — %d 프레임, %.1f FPS, "
            "포즈 실패 %d, UDP 실패 %d",
            self._frame_count,
            stats["effective_fps"],
            self._pose_failures,
            self._udp_failures,
        )

        return stats

    # ── 내부 ──

    def _process_loop(self) -> None:
        """카메라 입력 → 포즈 추정 → UDP 전송 루프."""
        last_valid_landmarks = [0.0] * 132  # 33 landmarks × 4 values

        while self._running.is_set():
            try:
                ret, frame = self._cap.read()
                if not ret or frame is None:
                    logger.warning("[MediaPipeBridge] 프레임 캡처 실패")
                    continue

                # BGR → RGB 변환
                rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)

                # MediaPipe 포즈 추정
                results = self._mp_pose.process(rgb_frame)

                if results.pose_landmarks is None:
                    self._pose_failures += 1
                    landmarks = last_valid_landmarks
                else:
                    # 33개 랜드마크 × [x, y, z, visibility] 추출
                    # x, y는 0.0~1.0 정규화 좌표, z는 깊이(상대값), visibility는 신뢰도
                    landmarks = []
                    for lm in results.pose_landmarks:
                        landmarks.extend([lm.x, lm.y, lm.z, lm.visibility])

                    # 유효한 포즈 저장 (다음 실패 시 사용)
                    last_valid_landmarks = landmarks

                # FusedDataFrame 구성 및 전송
                frame_data = FusedDataFrame(
                    pose=PoseData(landmarks=landmarks),
                    valid_sensors=SensorFlags.POSE,
                    timestamp_ms=int(time.time() * 1000),
                )

                try:
                    self._sender.send_fused_frame(frame_data)
                except Exception as e:
                    self._udp_failures += 1
                    logger.debug("[MediaPipeBridge] UDP 전송 실패: %s", e)

                # 시각화 (GUI 활성화 시)
                if self._config.show_gui:
                    self._draw_frame(frame, results)

                self._frame_count += 1

                # 프레임 레이트 제어
                frame_time = 1.0 / self._config.target_fps
                time.sleep(frame_time * 0.8)  # 약간의 여유 시간

            except Exception as e:
                logger.error("[MediaPipeBridge] 루프 에러: %s", e)
                continue

    def _draw_frame(self, frame: np.ndarray, results: mp.solutions.pose.PoseLandmarkList) -> None:
        """프레임에 랜드마크 그리고 표시."""
        h, w = frame.shape[:2]

        # 랜드마크 그리기
        if results.pose_landmarks is not None:
            # 주요 연결 (팔, 다리, 척추)
            connections = [
                # 척추
                (0, 1), (1, 2), (2, 3), (3, 7),
                # 좌측 팔
                (5, 7), (7, 9), (9, 11), (11, 13), (13, 15),
                # 우측 팔
                (6, 8), (8, 10), (10, 12), (12, 14), (14, 16),
                # 좌측 다리
                (23, 25), (25, 27), (27, 29), (29, 31),
                # 우측 다리
                (24, 26), (26, 28), (28, 30), (30, 32),
                # 골반
                (23, 24),
            ]

            for start, end in connections:
                if start < len(results.pose_landmarks) and end < len(results.pose_landmarks):
                    p1 = results.pose_landmarks[start]
                    p2 = results.pose_landmarks[end]

                    x1, y1 = int(p1.x * w), int(p1.y * h)
                    x2, y2 = int(p2.x * w), int(p2.y * h)

                    # 신뢰도에 따라 색상 결정
                    confidence = (p1.visibility + p2.visibility) / 2
                    color = (0, 255, 0) if confidence > 0.5 else (0, 165, 255)

                    cv2.line(frame, (x1, y1), (x2, y2), color, 2)

            # 랜드마크 점 그리기
            for i, lm in enumerate(results.pose_landmarks):
                x, y = int(lm.x * w), int(lm.y * h)
                color = (0, 255, 0) if lm.visibility > 0.5 else (0, 0, 255)
                cv2.circle(frame, (x, y), 3, color, -1)

        # 통계 텍스트
        cv2.putText(
            frame,
            f"Frames: {self._frame_count} | "
            f"FPS: {self._frame_count / max(time.time() - self._start_time, 0.1):.1f}",
            (10, 30),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.6,
            (0, 255, 0),
            2,
        )

        # 비율 조정 및 표시
        scale = self._config.gui_scale
        small_frame = cv2.resize(
            frame,
            (int(w * scale), int(h * scale)),
        )
        cv2.imshow("MediaPipe Bridge", small_frame)

        # ESC 키로 종료 가능
        key = cv2.waitKey(1) & 0xFF
        if key == 27:  # ESC
            logger.info("[MediaPipeBridge] GUI에서 ESC 입력 — 종료 요청")
            self._running.clear()


# ─────────────────────────────────────────────
# CLI 인터페이스
# ─────────────────────────────────────────────

def main() -> None:
    """커맨드라인 실행 예시."""
    import argparse

    parser = argparse.ArgumentParser(
        description="MediaPipe BlazePose → UDP 포즈 전송 브릿지",
    )
    parser.add_argument(
        "--target-ip",
        default="192.168.1.50",
        help="Galaxy XR 디바이스 IP (기본 192.168.1.50)",
    )
    parser.add_argument(
        "--camera",
        type=int,
        default=0,
        help="웹캠 인덱스 (기본 0)",
    )
    parser.add_argument(
        "--no-gui",
        action="store_true",
        help="시각화 창 끄기",
    )
    parser.add_argument(
        "--duration",
        type=int,
        default=10,
        help="실행 시간 (초, 기본 10)",
    )

    args = parser.parse_args()

    # 로깅 설정
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
    )

    # 브릿지 실행
    bridge = MediaPipeBridge(
        target_ip=args.target_ip,
        camera_idx=args.camera,
        gui=not args.no_gui,
    )

    try:
        bridge.start()
        logger.info("브릿지 실행 중... %d초 후 종료됩니다.", args.duration)
        time.sleep(args.duration)
    except KeyboardInterrupt:
        logger.info("사용자 중단 신호(Ctrl+C) 감지")
    finally:
        stats = bridge.stop()
        print("\n=== 최종 통계 ===")
        print(f"경과 시간: {stats['elapsed_sec']:.2f}초")
        print(f"총 프레임: {stats['total_frames']}")
        print(f"유효 FPS: {stats['effective_fps']:.2f}")
        print(f"포즈 추정 실패: {stats['pose_failures']}")
        print(f"UDP 전송 실패: {stats['udp_failures']}")


if __name__ == "__main__":
    main()
