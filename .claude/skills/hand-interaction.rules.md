# Hand Interaction Rules - Samsung Galaxy XR

## 플랫폼
- Samsung Galaxy XR (SM_I610, Android XR) + Unity 6000.1.17f1 + OpenXR
- Meta XR SDK 사용하지 않음 (OVR API 사용 금지)
- UnityPlayerGameActivity 사용 (UnityPlayerActivity 아님)
- 패스스루(Passthrough) 환경에서 동작

## 필수 패키지 (검증 완료)
- `com.unity.xr.hands` (1.7.3) — 손 관절 추적, XRHandSubsystem
- `com.unity.xr.interaction.toolkit` (3.3.1) — XR 인터랙션 (Poke, Direct, NearFar)
- `com.unity.xr.openxr` (1.15.1) — OpenXR 런타임
- `com.unity.xr.androidxr-openxr` (1.0.0) — Android XR 확장
- `com.google.xr.extensions` (v1.2.0) — Google XR Extensions (XRHandMeshFeature)

## OpenXR 활성화 필수 기능 (Android 탭)
- **Hand Tracking Subsystem** (`XR_EXT_hand_tracking`) → m_enabled: 1
- **Hand Interaction Profile** (`XR_EXT_hand_interaction`) → m_enabled: 1
- **Hand Interaction Poses** (`XR_EXT_hand_interaction`) → m_enabled: 1
- **Eye Gaze Interaction** (`XR_EXT_eye_gaze_interaction`) → m_enabled: 1
- **XR Environment Blend Mode** → m_enabled: 1, _requestMode: 1 (AlphaBlend)

## 검증된 XR Origin Hierarchy 구조
`XR Origin Hands (XR Rig).prefab` 기반 (XR Interaction Toolkit 3.3.1 Hands Interaction Demo 샘플)
```
XR Origin (XR Rig)                    [XROrigin, CharacterController, InputActionManager, XRInputModalityManager, XRGazeAssistance]
├── Camera Offset
│   ├── Main Camera                   [Camera(SolidColor, RGBA 0,0,0,0), AudioListener, TrackedPoseDriver, ARCameraManager]
│   ├── Gaze Interactor (비활성)      [XRGazeInteractor, GazeInputManager, TrackedPoseDriver]
│   ├── Gaze Stabilized (비활성)      [XRTransformStabilizer]
│   ├── Left Controller               [ControllerInputActionManager, XRInteractionGroup, HapticImpulsePlayer, TrackedPoseDriver]
│   ├── Left Controller Teleport Stabilized Origin
│   ├── Right Controller              [ControllerInputActionManager, XRInteractionGroup, HapticImpulsePlayer, TrackedPoseDriver]
│   ├── Right Controller Teleport Stabilized Origin
│   ├── Left Hand                     [XRInteractionGroup, PokeGestureDetector, AudioSource, SphereCollider(isTrigger), Rigidbody(kinematic), HandVisualController]
│   │   ├── Poke Interactor           [XRPokeInteractor, TrackedPoseDriver]
│   │   ├── Near-Far Interactor       [NearFarInteractor, InteractionAttachController]
│   │   ├── Aim Pose                  [TrackedPoseDriver]
│   │   ├── Pinch Point Stabilized    [PinchPointFollow, XRInteractorAffordanceStateProvider]
│   │   ├── Pinch Grab Pose           [TrackedPoseDriver]
│   │   ├── LeftHandQuestVisual (비활성)    [XRHandTrackingEvents, XRHandSkeletonDriver, XRHandMeshController]
│   │   └── LeftHandAndroidXRVisual (비활성) [XRHandTrackingEvents, XRHandSkeletonDriver, XRHandMeshController]
│   ├── Right Hand                    [XRInteractionGroup, PokeGestureDetector, AudioSource, SphereCollider(isTrigger), Rigidbody(kinematic), HandVisualController]
│   │   ├── Poke Interactor           [XRPokeInteractor, TrackedPoseDriver]
│   │   ├── Near-Far Interactor       [NearFarInteractor, InteractionAttachController]
│   │   ├── Aim Pose                  [TrackedPoseDriver]
│   │   ├── Pinch Point Stabilized    [PinchPointFollow, XRInteractorAffordanceStateProvider]
│   │   ├── Pinch Grab Pose           [TrackedPoseDriver]
│   │   ├── RightHandQuestVisual (비활성)    [XRHandTrackingEvents, XRHandSkeletonDriver, XRHandMeshController]
│   │   └── RightHandAndroidXRVisual (비활성) [XRHandTrackingEvents, XRHandSkeletonDriver, XRHandMeshController]
│   └── Hand Visualizer               [HandVisualizer]
├── Locomotion                        [LocomotionMediator, XRBodyTransformer]
└── Hands Smoothing Post Processor    [HandsOneEuroFilterPostProcessor]
```

참고: `XRInputModalityManager`가 런타임에서 플랫폼에 맞는 Visual(Quest vs AndroidXR)을 자동 활성화합니다.

## XR Interaction Toolkit 샘플 Import 필수
Package Manager에서 Import:
1. **XR Interaction Toolkit** → Samples → **"Starter Assets"** Import
2. **XR Interaction Toolkit** → Samples → **"Hands Interaction Demo"** Import
3. (선택) **XR Hands** → Samples → **"HandVisualizer"** Import

Prefab 경로 (3.3.1 버전):
```
Assets/Samples/XR Interaction Toolkit/3.3.1/Hands Interaction Demo/Prefabs/
├── XR Origin Hands (XR Rig).prefab      ← 전체 XR Origin (손 포함, 권장)
├── LeftHandAndroidXRVisual.prefab       ← Android XR(Samsung)용 왼손 비주얼
├── RightHandAndroidXRVisual.prefab      ← Android XR(Samsung)용 오른손 비주얼
├── HandInteractorAffordances.prefab     ← 인터랙션 피드백
└── HandPokeInteractorAffordances.prefab ← Poke 피드백

Assets/Samples/XR Hands/1.7.3/HandVisualizer/Prefabs/
├── Left Hand Tracking.prefab
├── Right Hand Tracking.prefab
└── Joint.prefab
```

## 씬 설정 절차 (UnityMCP 사용)

### 1단계: XR Origin 배치
```
// 기존 XR Origin 삭제 후 Prefab 인스턴스화
manage_gameobject(action="create", prefab_path="Assets/Samples/XR Interaction Toolkit/3.3.1/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab")
```

### 2단계: Main Camera 패스스루 설정
```
// Camera: clearFlags=2 (SolidColor), backgroundColor=(0,0,0,0)
manage_components(action="set_property", target="Main Camera", component_type="Camera", property="clearFlags", value=2)
manage_components(action="set_property", target="Main Camera", component_type="Camera", property="backgroundColor", value={"r":0,"g":0,"b":0,"a":0})
manage_components(action="add", target="Main Camera", component_type="ARCameraManager")
```

### 3단계: 손 충돌 설정 (OnTriggerEnter 기반)
Left Hand, Right Hand에:
- **SphereCollider**: isTrigger=true, radius=0.05
- **Rigidbody**: useGravity=false, isKinematic=true
- **HandVisualController**: 가상 손 시각 모드 제어

### 4단계: 인터랙션 대상 오브젝트
터치 대상 오브젝트(예: Start Button)에:
- **Collider** (Box/Sphere): isTrigger=true
- **HandTriggerSceneLoader.cs** 컴포넌트 추가
- Inspector에서 `sceneToLoad`, `fadeCanvasGroup` 설정

## 손 충돌 감지 방식
- **OnTriggerEnter** 기반 (Collider.isTrigger = true)
- 손 오브젝트 판별 순서:
  1. `CompareTag("PlayerHand")` 태그
  2. 이름에 "hand", "poke", "direct" 포함
  3. 부모 오브젝트 이름에 "hand" 포함
  4. XR Interactor 컴포넌트 보유 여부
- 양손 동시 트리거 → 중복 방지 (`_triggered` 플래그)

## 핵심 스크립트

### 1. HandTriggerSceneLoader.cs
위치: `Assets/Scripts/HandInteraction/HandTriggerSceneLoader.cs`
- 오브젝트 터치 시 씬 전환
- Inspector에서 씬 이름, 딜레이, 페이드 설정 가능
- CanvasGroup 기반 페이드 효과 (FadeCanvas 연결)
- 중복 트리거 방지
- 시각/오디오 피드백
- 에디터 Gizmos로 트리거 영역 시각화

### 2. HandVisualController.cs
위치: `Assets/Scripts/HandInteraction/HandVisualController.cs`
- 가상 손 시각 모드 3가지:
  - **Outline** (0): 반투명 아웃라인 (URP Unlit + Transparent)
  - **DarkHand** (1): 어두운 색 반투명 손
  - **Invisible** (2): 가상 손 숨김 (패스스루로 실제 손만 보임)
- `SetMode(int)`, `CycleMode()` 외부 호출 가능
- Inspector에서 색상, 머티리얼 커스터마이징
- Start()에서 1초 딜레이 후 초기화 (Hand 생성 대기)

### 3. SceneSwitcher.cs (기존, 레거시)
위치: `Assets/SceneSwitcher.cs`
- OnTriggerEnter + CanvasGroup 페이드 + 오디오
- HandTriggerSceneLoader로 대체됨

## 금지 사항 (Samsung XR에서 사용 불가)
- Meta OVR API (PokeInteractable, OVRHand, OVRSkeleton, OVRGrabber)
- OVRScreenFade (CanvasGroup 페이드 사용)
- UnityPlayerActivity (UnityPlayerGameActivity만 사용)
- Oculus 전용 권한 (`com.oculus.permission.HAND_TRACKING`)
- HDR 활성화 (패스스루와 충돌)

## 씬 전환 패턴
```csharp
// CanvasGroup 기반 페이드 → 씬 로드
fadeCanvasGroup.blocksRaycasts = true;
float elapsed = 0f;
while (elapsed < fadeDuration)
{
    elapsed += Time.deltaTime;
    fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
    yield return null;
}
SceneManager.LoadScene(sceneToLoad);
```

## Build/Deploy 체크리스트
- Build Settings > Scenes In Build에 모든 씬 포함
- Android Platform, IL2CPP, ARM64
- Minimum API Level: Android 10.0 (API 29)
- Graphics API: Vulkan
- Color Space: Linear
- OpenXR 활성화 (XR Plug-in Management > Android)
- Hand Tracking Subsystem 활성화
- Hand Interaction Profile 활성화
- APK 빌드 → ADB 수동 설치 (Build & Run은 Samsung XR에서 불안정)
- ADB 실행: `adb shell am start -n com.DXPLap.PlasticBagGame/com.unity3d.player.UnityPlayerGameActivity`

## 디버깅
- 손 감지 안 됨: OpenXR Hand Tracking 활성화 확인, Collider/Rigidbody 설정 확인
- 씬 전환 안 됨: Build Settings에 씬 포함 확인, 씬 이름 대소문자 확인
- 충돌 감지 안 됨: Layer Collision Matrix 확인 (Edit > Project Settings > Physics)
- 가상 손 안 보임: HandVisualController 모드 확인, XRInputModalityManager 확인
- AndroidXR Visual 안 보임: XRInputModalityManager가 자동으로 활성화하므로 수동 활성화 불필요
