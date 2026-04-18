# XR Control — Galaxy Fold LinkBand2 Bridge 앱

Samsung Galaxy Fold에서 LinkBand2 EEG/PPG/ACC 데이터를 받아 PC Bridge(MacBook)로 UDP로
송신하는 Android 14+ 앱입니다. PC는 다시 Galaxy XR로 FusedDataFrame을 30Hz로 전송합니다.

## 토폴로지

```
Galaxy Fold (LinkBand2 BLE)
        │ (FuseUdpSender — 파셜 FusedFrame, EEG|PPG|ACC)
        ▼  UDP 9010
MacBook PC Bridge (python/bridge/linkband_receiver.py)
        │
        ▼  UDP 9001 (FusedDataFrame 30Hz)
Galaxy XR (Unity Runtime)
```

## 현재 상태

- **Mock 모드 기본 동작**: LinkBand2 없이 사인파·화이트노이즈 시뮬레이션으로 E2E 검증 가능
- **실SDK 연동 TODO**: `data/linkband/LinkBandClient.kt`는 스텁. LooxidLabs SDK-Android AAR
  수령 후 `libs.versions.toml`에 의존성 추가하고 실구현 교체
- **ClockSync**: `ControlClient`로 PC ControlServer(9003)와 NTP 스타일 왕복 측정

## 빌드

Android Studio Hedgehog(2023.1.1) 이상에서 프로젝트 `Android_Control/`을 open. Gradle sync
후 Run → Device: Galaxy Fold (Android 14+).

```bash
cd Android_Control
./gradlew :app:installDebug  # Gradle wrapper는 Android Studio sync 시 자동 생성됨
```

## 단위 테스트 (JVM, 실기기 불필요)

```bash
./gradlew :app:testDebugUnitTest
```

`FuseUdpSenderTest`가 956 byte 고정 페이로드, 파셜 프레임 인코딩, valid_sensors 비트마스크를
검증합니다. Python `udp_protocol.py`와 바이트 호환성을 확인합니다.

## E2E 검증 시나리오

1. MacBook에서 수신기 가동:
   ```bash
   cd python
   python3 -m bridge.test_a_leg    # 파셜 프레임/Control/로거 검증
   # 또는 실시간 Bridge + Logger:
   python3 -m bridge.main_bridge --target-ip 127.0.0.1 --mock none \
       --with-linkband --with-control --log-dir ./logs
   ```
2. Android 앱 실행 → Settings 화면에서 PC IP를 MacBook IP로 설정
3. Main 화면 → "시작" 탭 → 30Hz로 파셜 프레임 송신
4. MacBook `logs/session_*/eeg.csv` 파일에 EEG 값 누적 확인

## 파일 구조

| 경로 | 역할 |
|---|---|
| `data/network/FuseProtocol.kt` | FUSE v1.0 상수 (Python [udp_protocol.py]와 동일 스펙) |
| `data/network/FuseUdpSender.kt` | 파셜 FusedFrame 인코더·송신기 |
| `data/network/ControlClient.kt` | 9003 ClockSync NTP 측정 |
| `data/mock/MockLinkBandClient.kt` | 사인파 기반 Mock SDK |
| `data/linkband/LinkBandClient.kt` | 실SDK 래퍼 (TODO 스텁) |
| `domain/source/SensorSource.kt` | real/mock 공통 인터페이스 |
| `data/prefs/SettingsRepository.kt` | DataStore 기반 설정 저장 |
| `service/StreamingService.kt` | Foreground Service (30Hz 송신 + Heartbeat) |
| `ui/screens/*` | Main, Connection, Waveform, SessionLog, Settings |
| `di/SourceModule.kt` | Hilt — SensorSource 주입 (Mock 기본) |
