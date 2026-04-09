# Samsung XR (Android XR) 빌드 환경 설정 현황

## 프로젝트 기본 정보
- **Unity 버전**: 6000.1.17f1 (Unity 6)
- **타겟 플랫폼**: Samsung Galaxy XR (Android XR / Project Moohan)
- **앱 번들 ID**: com.DXPLap.PlasticBagGame
- **버전**: 1.1.0
- **스크립팅 백엔드**: IL2CPP (확인됨)
- **최소 Android SDK**: 32 / 타겟 SDK: 32
- **CPU 아키텍처**: ARM64 (AndroidTargetArchitectures: 2)

## 패키지 의존성 (Packages/manifest.json)
### XR/OpenXR 관련 핵심 패키지
- com.unity.xr.openxr: 1.15.1
- com.unity.xr.androidxr-openxr: 1.0.0 (Samsung Android XR 지원)
- com.google.xr.extensions: v1.2.0 (Android XR Extensions)
- com.unity.xr.management: 4.5.1
- com.unity.xr.hands: 1.7.0
- com.unity.xr.interaction.toolkit: 3.3.0
- com.unity.xr.arfoundation: 6.1.1

### 성능 최적화 패키지
- com.unity.adaptiveperformance: 5.1.6
- com.unity.adaptiveperformance.samsung.android: 5.1.0 (삼성 최적화)

### 렌더 파이프라인
- com.unity.render-pipelines.universal: 17.1.0 (URP 활용 중)
- com.unity.shadergraph: 17.1.0

## ProjectSettings 설정 상태
| 항목 | 현재값 | 최적화 필요 |
|------|--------|----------|
| Min SDK | 32 | OK (Android XR 최소 요구) |
| Target SDK | 32 | 권장: 34-35로 업데이트 |
| Stereo Rendering Path | 2 (Single Pass Instanced) | OK |
| Color Space | Linear | OK (모던 XR 표준) |
| Vsync Count | 0 | 주의: XR에서 높은 framerate 유지 필요 |
| useFlipModelSwapchain | 1 | OK (Android XR 표준) |
| androidUseSwappy | 1 | OK (프레임 동기화) |
| stripEngineCode | 1 | OK (APK 최적화) |

## XR 로더 설정
- **OpenXRLoader.asset**: 활성화됨 (주 XR 플랫폼)
- **OculusLoader.asset**: Meta Quest 호환성용
- **SimulationLoader.asset**: 에디터 테스트용

## AndroidManifest.xml 상태
- Samsung XR VR 모드로 설정됨 (android:name="com.samsung.android.vr.application.mode", android:value="vr_only")
- VR 헤드 트래킹 하드웨어 요구사항 선언됨
- Meta SDK 주입 차단 (tools:replace="android:enabled")
- Android XR 표준 인텐트 필터 사용

## 렌더 파이프라인
- **활성 파이프라인**: Universal Render Pipeline (URP)
- **Custom Render Pipeline**: guid: 22b4e5fc61c3d8c489566da84ab70ca0
- **기본 렌더링 경로**: 1 (Forward Rendering)
- **모바일 렌더링 경로**: 1 (Forward Rendering)
- **활성화된 기능**: ShaderGraph, RenderGraph (비활성)

## 빌드 스크립트 (확인됨)
### SamsungXRBuilder.cs
**위치**: `/Users/user/Projects/Unity/PlasticBag_Game_0705/Assets/Editor/SamsungXRBuilder.cs`
- **기능**: Development/Release APK 빌드 자동화
- **메뉴**: Build > Samsung XR - Development/Release APK
- **주요 기능**:
  - IL2CPP 검증
  - ARM64 아키텍처 확인
  - Bundle Version Code 자동 증가
  - Graphics API 확인
  - 씬 목록 검증

### BuildSamsungXR.cs
**위치**: `/Users/user/Projects/Unity/PlasticBag_Game_0705/Assets/Editor/BuildSamsungXR.cs`
- **기능**: APK 빌드 및 타임스탬프 추가
- **메뉴**: Build > Build Samsung XR APK
- **버전**: 1.2.2 (스크립트 내 하드코딩, 자동 증가 필요)

## 빌드 결과 (2026-04-01)
### Development APK 빌드
- **파일명**: PlasticBagGame_SamsungXR_Dev.apk
- **파일 크기**: 110 MB (설치 후 전체 빌드 크기: 1597.7MB)
- **빌드 시간**: ~7분 10초
- **Bundle Version Code**: 1 → 2 (자동 증가)
- **상태**: ✓ 성공

### ADB 설치 (R3KYB032YLY)
- **설치 상태**: ✓ 성공 ("Success")
- **패키지명**: com.DXPLap.PlasticBagGame
- **Activity**: com.unity3d.player.UnityPlayerGameActivity (정확한 이름)

## 디바이스 연결 정보
- **디바이스 모델**: Samsung Galaxy XR (SM_I610)
- **디바이스 ID**: R3KYB032YLY
- **ADB 경로**: `/Applications/Unity/Hub/Editor/6000.1.17f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`
- **연결 상태**: ✓ 항상 정상

## 식별된 주요 이슈 및 개선사항

### 1. Target SDK 업그레이드 필요
- 현재: API 32
- 권장: API 34-35 (Google Play 정책, 2024)
- 영향: 앱 스토어 심사, 보안, 성능

### 2. Activity 클래스 선택 (해결됨)
- **오류**: `com.unity3d.player.UnityPlayerActivity` 사용 시 Activity not found
- **해결**: `com.unity3d.player.UnityPlayerGameActivity` 사용 (Unity 6 표준)
- **확인**: `aapt dump badging` 명령어로 APK 내 Activity 검증
- **상태**: ✓ 완료

### 3. Unity 에디터 충돌 (해결됨)
- **원인**: 동일 프로젝트에서 2개 이상 Unity 인스턴스 실행 불가
- **해결**: `kill -TERM` 후 커맨드라인 빌드 실행
- **상태**: ✓ 완료

### 4. 씬 구성 (확인됨)
- 빌드 씬: 1_Start_Scene_with_Analyze.unity (활성)
- 2번째 씬 3-1_PlasticBagPlay_with_Analyze.unity (활성)
- Analyze 관련 코드 제거 검토 필요 (릴리스 빌드)

## 주요 트러블슈팅 패턴
### 빌드 프로세스
1. Unity 에디터 실행 중인 경우, 먼저 종료: `kill -TERM [PID]`
2. 커맨드라인 빌드 실행: `-executeMethod SamsungXRBuilder.BuildDevelopment`
3. IL2CPP 컴파일 + Gradle 빌드 단계 (약 7분 소요)
4. APK 생성 확인: `ls -lh Builds/*.apk`

### ADB 설치 및 실행
1. 디바이스 연결 확인: `adb devices`
2. APK 설치: `adb -s [DEVICE_ID] install -r [APK_PATH]`
3. Activity 확인 (올바른 이름 필수): `aapt dump badging [APK_FILE]`
4. 앱 실행: `adb -s [DEVICE_ID] shell am start -n [PACKAGE]/[ACTIVITY]`

## 다음 단계
1. Release APK 빌드 (Debug 심볼 제거)
2. Target SDK 업그레이드 (32 → 34+)
3. APK 크기 최적화 (Analyze APK 도구 사용)
4. Samsung XR Store 스토어 심사 준비
5. Performance Profiler로 성능 분석
