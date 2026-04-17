# UDP Protocol Specification v1.0

> XR Exergame 멀티모달 센서 융합 시스템 통신 프로토콜
> 작성일: 2026-04-17 | 대상: PC Bridge <-> Galaxy XR Unity

---

## 1. 개요

Galaxy XR(Unity Runtime)과 RTX 4080 PC(Python Bridge) 간의 실시간 센서 데이터 교환을 위한 UDP 프로토콜.
PC에서 외부 센서(LinkBand2, MediaPipe, X-Sens) 데이터를 융합하여 FusedDataFrame을 생성하고,
UDP로 Galaxy XR Unity에 전송함.

```
Galaxy XR (Android)                    RTX 4080 PC (Windows)
┌─────────────────┐                    ┌─────────────────────┐
│ Unity Runtime   │◄── UDP 9001 ──────│ FusedDataFrame @30Hz│
│                 │◄── UDP 9002 ──────│ Engagement @10Hz   │
│                 │◄──► UDP 9003 ────►│ Control (Handshake)│
└─────────────────┘                    └─────────────────────┘
```

---

## 2. 포트 할당

| 포트 | 방향 | 데이터 | 주기 |
|------|------|--------|------|
| **9001** | PC → Galaxy XR | FusedDataFrame | 30Hz (33ms) |
| **9002** | PC → Galaxy XR | Engagement Score | 10Hz (100ms) |
| **9003** | 양방향 | Control (Handshake, Heartbeat, ClockSync) | 필요 시 |

---

## 3. 패킷 구조

### 3.1 공통 헤더 (20 bytes)

```
Offset  Size  Type      Field         설명
──────  ────  ────────  ──────────    ──────────────────────
0       4     char[4]   Magic         "FUSE" (0x46555345)
4       1     uint8     Version       프로토콜 버전 (현재 0x01)
5       1     uint8     PacketType    패킷 유형 (아래 표 참조)
6       4     uint32    SequenceNum   패킷 일련번호 (오버플로우 시 순환)
10      8     int64     TimestampMs   Unix 밀리초 타임스탬프
18      2     uint16    PayloadLength 페이로드 바이트 수 (헤더 제외)
──────  ────
합계    20 bytes
```

### 3.2 PacketType 정의

| 값 | 이름 | 방향 | 설명 |
|----|------|------|------|
| `0x01` | FusedFrame | PC→XR | 30Hz 융합 센서 데이터 |
| `0x02` | EngagementScore | PC→XR | 10Hz 몰입도 점수 |
| `0x10` | Handshake | 양방향 | 세션 시작 시 연결 확인 |
| `0x11` | Heartbeat | 양방향 | 3초 주기 생존 확인 |
| `0x12` | ClockSync | 양방향 | 시간 동기화 요청/응답 |

### 3.3 바이트 순서

- **Little-endian** 통일 (x86/ARM64 네이티브)

---

## 4. 페이로드 상세

### 4.1 FusedFrame (PacketType=0x01)

전체 페이로드 구조 (가변 길이, ValidSensorFlags로 어떤 센서가 포함되었는지 표시):

```
Offset  Size    Field              설명
──────  ────    ─────              ──────────────────
0       4       FrameNumber        uint32 프레임 번호
4       1       ValidSensorFlags   비트마스크 (어떤 센서 유효한지)
5       ~       SensorPayloads     유효한 센서 데이터만 순서대로
```

#### ValidSensorFlags 비트마스크

| Bit | 센서 | 설명 |
|-----|------|------|
| 0 (0x01) | BlendShape | Galaxy XR IR 얼굴 68D |
| 1 (0x02) | EyeTracking | OpenXR 시선 추적 |
| 2 (0x04) | EEG | LinkBand2 6ch 뇌파 |
| 3 (0x08) | PPG | LinkBand2 심박 |
| 4 (0x10) | ACC | LinkBand2 가속도 |
| 5 (0x20) | Pose | MediaPipe 33점 포즈 |
| 6 (0x40) | IMU | X-Sens 관성센서 |

#### 센서별 페이로드 크기

| 센서 | 필드 | 크기 (bytes) |
|------|------|-------------|
| **BlendShape** | float32[68] weights + float32[3] confidence | 284 |
| **EyeTracking** | float32[4] leftQuat + float32[4] rightQuat + uint8 blink | 33 |
| **EEG** | float32[6] channels(uV) + uint8[6] contactQuality | 30 |
| **PPG** | float32 ir + float32 red | 8 |
| **ACC** | float32[3] xyz | 12 |
| **Pose** | float32[132] (33 landmarks x 4: x,y,z,visibility) | 528 |
| **IMU** | float32[4] quat + float32[3] acc + float32[3] gyro + float32[3] mag | 52 |

**최대 페이로드**: 5 (헤더) + 284 + 33 + 30 + 8 + 12 + 528 + 52 = **952 bytes**
**최대 패킷**: 20 (헤더) + 952 = **972 bytes** (MTU 1500 이내, 단편화 없음)

### 4.2 EngagementScore (PacketType=0x02)

```
Offset  Size    Field              설명
──────  ────    ─────              ──────────────────
0       4       SessionId          uint32 세션 ID
4       4       EngagementLevel    float32 (0.0~1.0)
8       4       BoredomLevel       float32 (0.0~1.0)
12      4       ConfusionLevel     float32 (0.0~1.0)
16      4       FrustrationLevel   float32 (0.0~1.0)
20      4       Confidence         float32 추론 신뢰도
24      1       ModelVersion       uint8 모델 버전
```

페이로드: **25 bytes** | 패킷: **45 bytes**

### 4.3 Handshake (PacketType=0x10)

```
Offset  Size    Field              설명
──────  ────    ─────              ──────────────────
0       4       SessionId          uint32 새 세션 ID
4       1       Role               0x01=PC, 0x02=HMD
5       8       LocalClockMs       int64 로컬 시계
13      1       SensorCount        uint8 활성 센서 수
14      1       SensorFlags        uint8 활성 센서 비트마스크
```

### 4.4 Heartbeat (PacketType=0x11)

```
Offset  Size    Field              설명
──────  ────    ─────              ──────────────────
0       4       SessionId          uint32 세션 ID
4       4       UptimeSeconds      uint32 가동 시간
8       1       Status             0x00=OK, 0x01=Warning, 0xFF=Error
```

### 4.5 ClockSync (PacketType=0x12)

```
Offset  Size    Field              설명
──────  ────    ─────              ──────────────────
0       1       Phase              0x01=Request, 0x02=Response
1       8       T1                 int64 요청 전송 시각
9       8       T2                 int64 수신 시각 (응답에만)
17      8       T3                 int64 응답 전송 시각 (응답에만)
```

Clock offset 계산: `offset = ((T2 - T1) + (T3 - T4)) / 2`
(T4 = 응답 수신 시각, 수신측에서 계산)

---

## 5. 시간 동기화 전략

| 항목 | 값 |
|------|-----|
| 마스터 클록 | PC UTC (NTP 동기화 전제) |
| 동기화 시점 | 세션 시작 + 5분 주기 + drift>50ms 감지 시 |
| 센서별 고정 오프셋 | BLE: 35ms, Camera: 75ms, IMU: 12ms, Eye: 5ms, BlendShape: 8ms |
| MaxDrift | 100ms (초과 시 해당 슬롯 drop + 경고 로그) |
| 보간 | MaxDrift 초과 빈발 시 Linear Interpolation |

---

## 6. 에러 처리

| 상황 | 처리 |
|------|------|
| 3초 이상 Heartbeat 미수신 | 연결 끊김 경고 UI 표시 |
| 패킷 Magic 불일치 | 패킷 폐기 (로그 기록) |
| SequenceNum 갭 발생 | 패킷 손실 카운터 증가 |
| ValidSensorFlags = 0x00 | 빈 프레임 — 게임은 마지막 유효 데이터 유지 |
| 페이로드 크기 불일치 | 패킷 폐기 + 경고 로그 |

---

## 7. 성능 예산

| 항목 | 값 |
|------|-----|
| FusedFrame 대역폭 | ~972 bytes x 30Hz = **~29 KB/s** |
| Engagement 대역폭 | ~45 bytes x 10Hz = **~0.45 KB/s** |
| Control 대역폭 | 무시 가능 (간헐적) |
| 총 대역폭 | **< 30 KB/s** (Wi-Fi 6에서 무시 가능) |
| 직렬화 지연 | < 0.1ms (버퍼 재사용, GC 없음) |

---

## 8. 구현 파일 매핑

| 파일 | 언어 | 역할 |
|------|------|------|
| `Assets/Runtime/Fusion/UdpProtocol.cs` | C# | 패킷 인코딩/디코딩 |
| `Assets/Runtime/Fusion/FusedDataFrame.cs` | C# | DTO 구조체 |
| `python/bridge/udp_protocol.py` | Python | 패킷 송/수신 |
| `python/bridge/fused_data_frame.py` | Python | DTO 미러 |

---

*이 문서는 v1.0이며, 센서 추가/변경 시 업데이트 필요.*
