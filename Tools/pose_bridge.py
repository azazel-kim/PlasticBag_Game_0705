#!/usr/bin/env python3
"""
pose_bridge.py — OBSBOT 카메라 → MediaPipe Pose → FUSE UDP 브릿지
OBSBOT Tiny 2 Lite에서 영상을 캡처하고 MediaPipe PoseLandmarker로
33개 랜드마크를 추출하여 FUSE 프로토콜(UDP port 9001)로 Unity에 전송합니다.

사용법:
    python3 pose_bridge.py                    # 기본 설정 (OBSBOT 자동 감지)
    python3 pose_bridge.py --camera 0         # 카메라 인덱스 지정
    python3 pose_bridge.py --host 192.168.0.5 # Unity가 다른 PC에 있을 때
    python3 pose_bridge.py --show             # 미리보기 창 표시

필요 모델 (같은 폴더에 위치):
    pose_landmarker_lite.task
"""

import argparse
import os
import struct
import socket
import time
import sys

import cv2
# MediaPipe Tasks API (0.10.x)
import mediapipe as mp
from mediapipe.tasks.python import BaseOptions
from mediapipe.tasks.python.vision import (
    PoseLandmarker,
    PoseLandmarkerOptions,
    RunningMode,
)

# =============================================================================
# FUSE 프로토콜 상수 (UdpProtocol.cs와 동일)
# =============================================================================
FUSE_MAGIC = b"FUSE"              # 매직 바이트 (4 bytes)
FUSE_VERSION = 0x01               # 프로토콜 버전
PACKET_TYPE_FUSED_FRAME = 0x01    # FusedDataFrame 패킷 타입
FUSED_FRAME_PORT = 9001           # Unity 수신 포트
VALID_SENSOR_POSE = 0x20          # ValidSensorFlags.Pose = 1 << 5

# MediaPipe Pose 랜드마크 수
LANDMARK_COUNT = 33
FLOATS_PER_LANDMARK = 4           # x, y, z, visibility


def build_fuse_packet(landmarks, confidence, frame_number, timestamp_ms):
    """
    FusedDataFrame을 FUSE 프로토콜 패킷으로 조립합니다.

    패킷 구조:
      [FUSE 헤더 20B] + [페이로드]

    헤더:
      Magic(4) + Version(1) + PacketType(1) + SeqNum(4) + Timestamp(8) + PayloadLen(2)

    페이로드 (FusedDataFrame.ToBytes() 호환):
      TimestampMs(8) + FrameNumber(4) + ValidSensors(1)
      + Landmarks(132 floats = 528B) + Confidence(4B)
    """
    # --- 페이로드 조립 ---
    payload = bytearray()

    # TimestampMs (8 bytes, Little-Endian)
    payload += struct.pack("<q", timestamp_ms)

    # FrameNumber (4 bytes, Little-Endian)
    payload += struct.pack("<I", frame_number)

    # ValidSensorFlags (1 byte) — Pose만 유효
    payload += struct.pack("<B", VALID_SENSOR_POSE)

    # PoseData: 33 landmarks × 4 floats (x, y, z, visibility) = 132 floats
    for lm in landmarks:
        payload += struct.pack("<ffff", lm[0], lm[1], lm[2], lm[3])

    # PoseData.Confidence (1 float)
    payload += struct.pack("<f", confidence)

    payload_bytes = bytes(payload)
    payload_len = len(payload_bytes)

    # --- FUSE 헤더 조립 (20 bytes) ---
    header = bytearray()
    header += FUSE_MAGIC                                    # Magic (4B)
    header += struct.pack("<B", FUSE_VERSION)               # Version (1B)
    header += struct.pack("<B", PACKET_TYPE_FUSED_FRAME)    # PacketType (1B)
    header += struct.pack("<I", frame_number)               # SeqNum (4B)
    header += struct.pack("<q", timestamp_ms)               # Timestamp (8B)
    header += struct.pack("<H", payload_len)                # PayloadLen (2B)

    return bytes(header) + payload_bytes


def find_obsbot_camera():
    """OBSBOT 카메라 인덱스를 자동 감지합니다."""
    for idx in range(5):
        cap = cv2.VideoCapture(idx)
        if cap.isOpened():
            ret, _ = cap.read()
            cap.release()
            if ret:
                return idx
    return 0


# MediaPipe Pose 연결선 정의 (시각화용)
POSE_CONNECTIONS = [
    # 얼굴
    (0, 1), (1, 2), (2, 3), (3, 7),
    (0, 4), (4, 5), (5, 6), (6, 8),
    (9, 10),
    # 몸통
    (11, 12), (11, 23), (12, 24), (23, 24),
    # 왼팔
    (11, 13), (13, 15), (15, 17), (15, 19), (15, 21), (17, 19),
    # 오른팔
    (12, 14), (14, 16), (16, 18), (16, 20), (16, 22), (18, 20),
    # 왼다리
    (23, 25), (25, 27), (27, 29), (27, 31), (29, 31),
    # 오른다리
    (24, 26), (26, 28), (28, 30), (28, 32), (30, 32),
]


def draw_landmarks_on_frame(frame, landmarks):
    """OpenCV로 랜드마크와 연결선을 직접 그립니다."""
    h, w, _ = frame.shape

    # 랜드마크 점 그리기
    points = []
    for lm in landmarks:
        x, y, z, vis = lm
        px, py = int(x * w), int(y * h)
        points.append((px, py))
        color = (0, 255, 0) if vis > 0.5 else (0, 0, 255)
        cv2.circle(frame, (px, py), 3, color, -1)

    # 연결선 그리기
    for a, b in POSE_CONNECTIONS:
        if landmarks[a][3] > 0.5 and landmarks[b][3] > 0.5:
            cv2.line(frame, points[a], points[b], (0, 180, 255), 2)


def main():
    parser = argparse.ArgumentParser(description="OBSBOT → MediaPipe Pose → FUSE UDP 브릿지")
    parser.add_argument("--camera", type=int, default=None, help="카메라 인덱스 (기본: 자동 감지)")
    parser.add_argument("--host", type=str, default="127.0.0.1", help="Unity UDP 수신 호스트 (기본: localhost)")
    parser.add_argument("--port", type=int, default=FUSED_FRAME_PORT, help=f"UDP 포트 (기본: {FUSED_FRAME_PORT})")
    parser.add_argument("--show", action="store_true", help="미리보기 창 표시")
    parser.add_argument("--fps", type=int, default=30, help="목표 FPS (기본: 30)")
    parser.add_argument("--confidence", type=float, default=0.5, help="최소 감지 신뢰도 (기본: 0.5)")
    args = parser.parse_args()

    # --- 모델 파일 확인 ---
    script_dir = os.path.dirname(os.path.abspath(__file__))
    model_path = os.path.join(script_dir, "pose_landmarker_lite.task")
    if not os.path.exists(model_path):
        print(f"[PoseBridge] 오류: 모델 파일을 찾을 수 없습니다: {model_path}", file=sys.stderr)
        print(f"[PoseBridge] 다운로드: curl -L -o {model_path} https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/latest/pose_landmarker_lite.task")
        sys.exit(1)

    # --- 카메라 초기화 ---
    camera_idx = args.camera if args.camera is not None else find_obsbot_camera()
    print(f"[PoseBridge] 카메라 인덱스: {camera_idx}")

    cap = cv2.VideoCapture(camera_idx)
    if not cap.isOpened():
        print(f"[PoseBridge] 오류: 카메라 {camera_idx}를 열 수 없습니다.", file=sys.stderr)
        sys.exit(1)

    # 해상도/FPS 설정 (OBSBOT Tiny 2 Lite 기본)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
    cap.set(cv2.CAP_PROP_FPS, args.fps)

    actual_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    actual_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    actual_fps = cap.get(cv2.CAP_PROP_FPS)
    print(f"[PoseBridge] 해상도: {actual_w}x{actual_h}, FPS: {actual_fps}")

    # --- MediaPipe PoseLandmarker 초기화 (VIDEO 모드) ---
    options = PoseLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=model_path),
        running_mode=RunningMode.VIDEO,
        num_poses=1,
        min_pose_detection_confidence=args.confidence,
        min_tracking_confidence=args.confidence,
    )
    landmarker = PoseLandmarker.create_from_options(options)
    print(f"[PoseBridge] PoseLandmarker 초기화 완료 (model: pose_landmarker_lite)")

    # --- UDP 소켓 초기화 ---
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    dest = (args.host, args.port)
    print(f"[PoseBridge] UDP 전송 대상: {args.host}:{args.port}")

    # --- 메인 루프 ---
    frame_number = 0
    frame_interval = 1.0 / args.fps
    last_frame_time = 0.0
    fps_counter = 0
    fps_timer = time.time()

    print(f"[PoseBridge] 포즈 트래킹 시작 (종료: Ctrl+C 또는 'q' 키)")

    try:
        while True:
            now = time.time()

            # FPS 제한
            if now - last_frame_time < frame_interval:
                time.sleep(0.001)
                continue
            last_frame_time = now

            ret, frame = cap.read()
            if not ret:
                print("[PoseBridge] 프레임 읽기 실패, 재시도...", file=sys.stderr)
                time.sleep(0.1)
                continue

            # BGR → RGB 변환 후 MediaPipe Image로 래핑
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)

            # MediaPipe 포즈 추정 (VIDEO 모드 — 타임스탬프 필요)
            timestamp_ms = int(now * 1000)
            results = landmarker.detect_for_video(mp_image, timestamp_ms)

            if results.pose_landmarks and len(results.pose_landmarks) > 0:
                # 첫 번째 포즈의 33개 랜드마크 추출
                pose_lms = results.pose_landmarks[0]
                landmarks = []
                for lm in pose_lms:
                    landmarks.append((lm.x, lm.y, lm.z, lm.visibility))

                # 전체 신뢰도 = 모든 랜드마크 visibility 평균
                avg_confidence = sum(lm[3] for lm in landmarks) / len(landmarks)

                # FUSE 패킷 조립 및 전송
                packet = build_fuse_packet(landmarks, avg_confidence, frame_number, timestamp_ms)
                sock.sendto(packet, dest)

                # 미리보기 그리기
                if args.show:
                    draw_landmarks_on_frame(frame, landmarks)
            else:
                # 포즈 미감지 — 빈 랜드마크 전송
                landmarks = [(0.0, 0.0, 0.0, 0.0)] * LANDMARK_COUNT
                packet = build_fuse_packet(landmarks, 0.0, frame_number, timestamp_ms)
                sock.sendto(packet, dest)

            frame_number += 1
            fps_counter += 1

            # 1초마다 FPS 출력
            if now - fps_timer >= 1.0:
                detected = "O" if (results.pose_landmarks and len(results.pose_landmarks) > 0) else "X"
                print(f"[PoseBridge] FPS: {fps_counter} | 포즈 감지: {detected} | 프레임: {frame_number}", end="\r")
                fps_counter = 0
                fps_timer = now

            # 미리보기 표시
            if args.show:
                has_pose = results.pose_landmarks and len(results.pose_landmarks) > 0
                status = "POSE DETECTED" if has_pose else "NO POSE"
                color = (0, 255, 0) if has_pose else (0, 0, 255)
                cv2.putText(frame, status, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, color, 2)
                cv2.putText(frame, f"Frame: {frame_number}", (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 1)
                cv2.imshow("PoseBridge - OBSBOT", frame)

                if cv2.waitKey(1) & 0xFF == ord("q"):
                    print("\n[PoseBridge] 'q' 키로 종료")
                    break

    except KeyboardInterrupt:
        print("\n[PoseBridge] Ctrl+C 종료")

    finally:
        cap.release()
        sock.close()
        landmarker.close()
        if args.show:
            cv2.destroyAllWindows()
        print(f"[PoseBridge] 총 {frame_number} 프레임 전송 완료")


if __name__ == "__main__":
    main()
