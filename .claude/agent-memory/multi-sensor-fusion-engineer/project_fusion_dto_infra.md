---
name: Fusion DTO Infrastructure Created
description: FusedDataFrame, RingBuffer, TimeSyncAligner, UdpProtocol 4개 핵심 파일을 Assets/Runtime/Fusion/에 생성 완료 (2026-04-17)
type: project
---

## 센서 융합 인프라 0단계 완료 (2026-04-17)

4개 핵심 파일을 `Assets/Runtime/Fusion/`에 생성:

| 파일 | 라인수 | 역할 |
|------|--------|------|
| FusedDataFrame.cs | 615 | 6종 센서 + IEQ 융합 DTO, ToBytes/FromBytes UDP 직렬화 |
| RingBuffer.cs | 305 | Thread-safe 제네릭 순환 버퍼, GetNearest(이진탐색) |
| TimeSyncAligner.cs | 334 | NTP-like 클럭 동기화, 센서별 지연 보정, 드리프트 모니터링 |
| UdpProtocol.cs | 425 | "FUSE" 매직 헤더, Encode/Decode, ClockSync 핸드셰이크 |

**Why:** 이후 모든 센서 모듈(LinkBand2, OBSBOT, X-Sens)과 SensorFusionManager가 이 인터페이스를 따르게 됨.

**How to apply:** 센서 연동 작업 시 반드시 이 DTO 규격에 맞춰 데이터를 Push하고, UDP 통신은 UdpProtocol 헬퍼를 사용할 것.

### 설계 결정 사항
- namespace: `XRExergame.Fusion`
- ValidSensorFlags: 7비트 비트마스크 (BlendShape~IMU)
- UDP 포트: 9001(FusedFrame), 9002(Engagement), 9003(Control)
- 패킷 헤더: 20 bytes (Magic 4 + Ver 1 + Type 1 + Seq 4 + Timestamp 8 + PayloadLen 2)
- RingBuffer: 기본 512 슬롯, maxDrift 100ms
- 센서 지연 기본값: BlendShape/Eye 5ms, EEG/PPG/ACC 35ms, Pose 75ms, IMU 12ms
