---
name: LinkBand2 프로젝트 통합 컨텍스트
description: XR Exergame에서 LinkBand2 EEG를 FusedDataFrame 30Hz로 통합하는 목표와 배치 전략
type: project
---

## 목표
LinkBand2 EEG 데이터를 FusedDataFrame에 30Hz로 통합. 멀티모달 센서 융합 파이프라인의 일부.

## 권장 배치 전략
**옵션 B: 갤럭시 폴드 Android SDK 앱 → WiFi/UDP → PC/Galaxy XR**

Why: Galaxy XR에서 직접 BLE 시 게임 프레임레이트 영향 우려, Android SDK가 유일하게 실용적인 옵션.
웹 SDK는 문서 미비로 현재 사용 불가.

## 핵심 제약
- Android SDK 최소 요구: API 34 (Android 14) — Galaxy XR 지원 여부 확인 필요
- EEG는 SDK상 2채널 노출 (하드웨어 스펙은 6ch — 추가 확인 필요)
- 샘플링레이트 차이: EEG 250Hz → FusedDataFrame 30Hz 다운샘플링 필요
