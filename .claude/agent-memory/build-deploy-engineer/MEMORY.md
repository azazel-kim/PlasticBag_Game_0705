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

## 식별된 주요 이슈 및 개선사항

### 1. Target SDK 업그레이드 필요
- 현재: API 32
- 권장: API 34-35 (Google Play 정책, 2024)
- 영향: 앱 스토어 심사, 보안, 성능

### 2. Graphics API 설정 확인 불가
- ProjectSettings.asset에서 androidGraphicsAPIs 직접 설정 미확인
- Vulkan 명시 설정 필요 (Samsung XR는 Vulkan 권장)

### 3. 빌드 스크립트 부재
- Assets/Editor 폴더에 커스텀 빌드 스크립트 없음
- Samsung XR/OpenXR 빌드 자동화 필요

### 4. XRManagementSettings 구성 확인
- EditorBuildSettings.asset에서는 OpenXR 로더 참조만 확인
- 런타임 Loader 활성화 상태 명시적 확인 필요

### 5. 씬 구성
- 빌드 씬: 1_Start_Scene_with_Analyze.unity (활성)
- 2번째 씬 3-1_PlasticBagPlay_with_Analyze.unity (비활성)
- Analyze 관련 코드 제거 검토 필요 (릴리스 빌드)

## 다음 단계
1. Target SDK 업데이트 (32 → 34+)
2. Graphics API 설정 명시 (Vulkan 우선)
3. 빌드 자동화 스크립트 작성
4. XR Loader 런타임 활성화 검증
5. Analyze 씬 리소스 정리
