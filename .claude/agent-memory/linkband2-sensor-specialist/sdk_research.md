---
name: LinkBand SDK 리서치 결과
description: Android SDK 스펙, 웹 SDK 한계, Gradle 의존성 — 2026-04-17 조사
type: project
---

## Android SDK (io.github.looxidlabs:SDK-Android:1.0.1)

- EEG: **2채널**, 250Hz (주의: LinkBand2 스펙 시트는 6ch/256Hz — SDK 노출은 2ch)
- PPG: 50Hz (적외선+적색)
- ACC: 3축, 25Hz
- BLE 자동 스캔/연결/재연결 내장
- 데이터 형식: StateFlow (Kotlin), CSV/JSON 저장 지원
- 최소 Android: **API 34 (Android 14.0)**
- Java 17 필수
- 라이선스: 미확인 (리포 확인 필요)
- Demo: https://github.com/LooxidLabs/Android-LinkBandDemoApp

**Why:** 구현 시 Android API 34 제약이 핵심 — Galaxy XR(SM_I610) 지원 여부 확인 필요
**How to apply:** Gradle 의존성: `io.github.looxidlabs:SDK-Android:1.0.1`, JDK 17 환경 필수

## 웹 SDK (sdk.linkband.store)

- 페이지 내용 거의 없음 — 실질적 문서 미공개 상태
- 원시 EEG 제공 여부 불명확
- 기존 Looxid Link(구형) SDK는 Link Core PC앱 경유 WebSocket — LinkBand2 동일 구조 추정
- 공개 REST/WebSocket API 문서 없음 → 직접 문의 또는 베타 접근 필요

**Why:** 웹 SDK는 현재 활용 불가 수준 — 배치 전략에서 제외 권고
