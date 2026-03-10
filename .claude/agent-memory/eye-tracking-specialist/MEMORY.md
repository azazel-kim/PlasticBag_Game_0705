# Eye Tracking Specialist - Project Memory

## 핵심 아키텍처 패턴

### Eye Tracking 데이터 소스 (두 가지 경로)
1. **ARFaceManager 경로** (AvatarMirror, CreatureGaze):
   - `ARFaceManager.trackables` 이터레이션 → `face.TryGetAndroidOpenXRFaceTrackingStates()`
   - `AndroidOpenXRFaceTrackingStates.LeftEyePoseValid / RightEyePoseValid` 플래그 체크 필수
   - `face.leftEye.rotation`, `face.rightEye.rotation` 으로 월드 공간 회전 획득
   - Head-relative 변환: `invHeadRot * eyeWorldRotation`
   - CS0618 warning: `#pragma warning disable CS0618` 필요 (AXR 1.1.0-pre.1 마이그레이션 이슈)

2. **XRI GazeInteractor 경로** (GazeAndPinch):
   - `Singleton.Instance.OriginManager.EnableGazeInteraction = true`로 활성화
   - `XRGazeInteractor` + `firstHoverEntered` 이벤트로 오브젝트 선택
   - `GazeInputManager`가 eye tracking 미지원 시 head tracking 폴백 처리

### 좌표계 주의사항
- CreatureGaze: `eyeWS * invHeadRot` (월드→로컬 순서 다름)
- AvatarMirror: `invHeadRot * eyeWS` (역순)
- 두 코드 모두 실제 동작하지만 좌표계 해석 방식 차이 존재 — 새 코드는 AvatarMirror 패턴 따를 것

### AndroidXROriginManager 주요 프로퍼티
- `EnableFaceManager` → ARSession 자동 갱신 (UpdateSessionEnabled 내부 호출)
- `EnableGazeInteraction` → GazeInteractionObjects[] 일괄 활성화
- `FoveationController.FoveationLevel` → Foveated Rendering 레벨 (float)

### 필터링 시스템
- `ExponentialFilter`: float, Vector3, Quaternion 모두 지원
- `FilterEnum.No / Exponential` 선택
- `ExponentialFilterSettingsV3`: Weight(0.01~0.99), InitializeOnFirstUpdate
- GazePosition에만 적용됨 (GazeRotation은 미필터링)

### EyeCreature 90Hz 가정 패턴
- 지연 큐 크기: `(int)(eyeTrackDelay / (1/90f))` — 90Hz 하드코딩
- 좌우 눈 각각 큐 유지: `_previousEyeRotationsLeft[]`, `_previousEyeRotationsRight[]`
- 시선 추적 속도 클램핑: `_maxAngleFromForward` 각도 제한

### AvatarEyeData 구조체
- `GazePosition`, `GazeRotation`, `LeftEyeRotation`, `RightEyeRotation`
- `GazeDirection`, `LeftEyeDirection`, `RightEyeDirection` (Quaternion → Vector3 계산 프로퍼티)
- `GazeRotation` = `Quaternion.Slerp(LeftEyeRotation, RightEyeRotation, 0.5f)` (합성 시선)

### IAvatarInput 인터페이스
- `AvatarInputPerception`이 구현체 (static 프로퍼티로 전역 접근)
- 멀티 트래킹: Head + Eye + Face + Hand + Body 통합

## 주요 파일 경로 (AndroidXR Samples 참조)
- `Assets/AndroidXRUnitySamples/AvatarMirror/Scripts/Input/EyeTracking/AvatarEyeData.cs`
- `Assets/AndroidXRUnitySamples/AvatarMirror/Scripts/Input/EyeTracking/AvatarEyeTracking.cs`
- `Assets/AndroidXRUnitySamples/AvatarMirror/Scripts/Input/AvatarInputPerception.cs`
- `Assets/AndroidXRUnitySamples/AvatarMirror/Scripts/Input/IAvatarInput.cs`
- `Assets/AndroidXRUnitySamples/AvatarMirror/Scripts/Utils/ExponentialFilter.cs`
- `Assets/AndroidXRUnitySamples/CreatureGaze/Scripts/CreatureGaze.cs`
- `Assets/AndroidXRUnitySamples/CreatureGaze/Scripts/EyeCreature.cs`
- `Assets/AndroidXRUnitySamples/GazeAndPinch/Scripts/GazeAndPinch.cs`
- `Assets/AndroidXRUnitySamples/GazeAndPinch/Scripts/CatapultController.cs`
- `Assets/AndroidXRUnitySamples/Common/Scripts/AndroidXROriginManager.cs`

## PlasticBag 프로젝트 Eye Tracking 파일
- `Assets/Scripts/GazeInteractionController.cs`
- `Assets/Scripts/BoldGazeInteractionController.cs`
- `Assets/Scripts/EyeTrackingRaycaster.cs`
- `Assets/Scripts/GazeFrequencyController.cs`

## FaceTracking과의 관계
- Eye Tracking은 ARFaceManager 경로에서 Face Tracking과 동일한 파이프라인 공유
- `XRFaceParameterIndices`: EyesClosedL/R, EyesLookDownL/R, EyesLookLeftL/R 등 눈 관련 블렌드쉐이프 포함

## Exergame 활용 영역

### 재활
- Smooth Pursuit 훈련 (움직이는 타겟 안구 추적)
- Fixation Training (주시 지속 → 집중력/인지 평가)
- 시야 결손 스크리닝 (시선 도달 영역 매핑)
- EEG + Eye Tracking 동기화 → P300 인지 부하 측정

### 피트니스
- 핸즈프리 UI 제어 (운동 중 시선 인터랙션)
- Blink Rate 피로 감지 → 운동 강도 자동 조절
- 시선 유도 운동 게임 (시선 타겟 → 몸 움직이기)

### 바이오피드백
- 시선 안정성 지표 (GSI) → 정신적 피로 수치화
- 실시간 Blink Rate (집중↓ vs 피로↑)
- EEG Alpha/Theta + 시선 안정성 상관관계

## 구현 로드맵 (난이도별)

### 쉬움 (1~3일)
1. Gaze Reticle 표시
2. Dwell-time UI 선택
3. 아바타 눈 리타게팅
4. Blink Rate 측정 (EyesClosedL/R 카운팅)

### 보통 (1~2주)
5. Smooth Pursuit 훈련 씬
6. Fixation 감지 (I-VT 알고리즘)
7. 시선 Heatmap 수집
8. 시선 + Blink + EEG 피로 지수 (LinkBand2 융합)
9. 동적 Foveated Rendering 연동

### 어려움 (2~4주)
10. 시선 경로 분석 대시보드 (Saccade/Fixation 분류)
11. EEG + Eye Tracking 인지 부하 지표
12. 시선 기반 적응형 난이도 AI
13. 양안 불일치 감지 (Vergence 스크리닝)

## API 경로 선택 가이드
| 용도 | API 경로 |
|------|---------|
| 정밀 양안 회전 (재활, Vergence, 아바타) | ARFaceManager |
| UI 상호작용 (메뉴, 버튼, 핸즈프리) | XRI GazeInteractor |

## 주의사항
- `AvatarEyeTracking`은 UNITY_EDITOR에서 자동 비활성화
- ARFaceManager trackingState 버그: 항상 None → `TryGetAndroidOpenXRFaceTrackingStates()` 우회 사용
- Singleton.Instance.OriginManager.EnableFaceManager = true 필수 (CreatureGaze.Start 참조)
