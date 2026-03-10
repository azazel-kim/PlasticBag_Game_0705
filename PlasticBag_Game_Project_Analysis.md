# 🎮 PlasticBag Game — 프로젝트 분석 문서

> Unity 프로젝트: `W:\1_DXP_Projects\Unity\PlasticBag_Game_0705`
> 작성일: 2026-02-24

---

## 1. 기본 정보

| 항목                | 내용                                            |
| ------------------- | ----------------------------------------------- |
| **Unity 버전**      | 2022.3.40f1 (LTS)                               |
| **타겟 플랫폼**     | Android (Meta Quest VR 헤드셋)                  |
| **렌더 파이프라인** | URP (Universal Render Pipeline)                 |
| **프로젝트명**      | PlasticBag_Game_0705 (2024년 7월 5일 기준 버전) |

---

## 2. 프로젝트 개요

**Meta Quest VR 헤드셋용 비닐봉지(PlasticBag) 인터랙션 게임**입니다.
시선 추적(Eye Tracking)을 핵심 인터랙션 수단으로 사용하며, **장애인을 위한 게임(Game for Disabled)** 컨셉을 포함합니다.
플레이어는 손이나 시선으로 공중에서 떨어지는 비닐봉지와 상호작용하며, 시선 데이터가 실시간으로 수집/시각화됩니다.

---

## 3. 씬(Scene) 구성

| 씬 이름                           | 역할                                                            |
| --------------------------------- | --------------------------------------------------------------- |
| `1_Start_Scene`                   | 게임 시작 화면 — 회전하는 오브젝트에 손을 대면 게임 씬으로 전환 |
| `1_Start_Scene_with_Analyze`      | 시선 분석 UI가 포함된 시작 화면                                 |
| `3_1_PlasticBagPlay`              | 메인 게임플레이 — 비닐봉지 스폰/충돌                            |
| `3-1_PlasticBagPlay_with_Analyze` | 시선 분석 UI가 포함된 게임플레이 씬                             |
| `EyeTracking`                     | 아이트래킹 기능 테스트 씬                                       |
| `EyeTracking_calculator`          | 시선 빈도 계산기 테스트 씬                                      |
| `GeminiAI`                        | Gemini AI 연동 테스트 씬                                        |
| `Passthrough IK`                  | Meta Passthrough + 풀바디 IK 씬                                 |
| `SampleScene`                     | 개발용 샘플/프로토타입 씬                                       |
| `UI_making_test`                  | UI 구성 테스트 씬                                               |

---

## 4. 핵심 스크립트

### 4-1. 게임 로직

| 스크립트                    | 역할                                                                                                                                       |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `PlasticBag.cs`             | 비닐봉지 오브젝트. `Ground` 태그에 닿으면 자신을 삭제하고 스포너에게 새 봉지 생성을 요청                                                   |
| `PlasticbagSpawner.cs`      | 설정된 반경 내 랜덤 위치에 비닐봉지를 스폰. 최초 실행 시 첫 봉지를 생성                                                                    |
| `CollisionSoundPlayer.cs`   | 충돌 사운드 재생 — `Plasticbag`↔`Plasticbag`, `PlayerHand`↔`Plasticbag` 태그 기반으로 다른 사운드 재생. 쿨다운 및 최소 충돌 강도 설정 가능 |
| `AutoDestroy.cs`            | 일정 시간 후 오브젝트 자동 삭제                                                                                                            |
| `DestroyOnGroundContact.cs` | 바닥 접촉 시 오브젝트 삭제                                                                                                                 |
| `Plasticbagsound.cs`        | 비닐봉지 사운드 추가 처리                                                                                                                  |

### 4-2. 씬 관리

| 스크립트           | 역할                                                                                                                           |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------ |
| `SceneSwitcher.cs` | OVRScreenFade로 페이드 효과를 주며 씬 전환. 시작 씬에서 플레이어 손이 Trigger에 닿으면 게임 씬으로 이동. 환영 음성도 반복 재생 |

### 4-3. 시선 추적 (Eye Tracking)

| 스크립트                             | 역할                                                                                 |
| ------------------------------------ | ------------------------------------------------------------------------------------ |
| `EyeTrackingRaycaster.cs`            | 시선 레이캐스트 처리 — 카메라 포워드 방향으로 레이를 발사하여 시선 대상 감지         |
| `GazeInteractionController.cs`       | 여러 오브젝트의 시선 빈도를 동시 측정 후 UI에 백분율로 표시 (구형, Raycast 기반)     |
| `BoldGazeInteractionController.cs`   | 개선된 시선 컨트롤러. `SphereCast`로 감지 정확도 향상 + 프로그레스 바 UI 표시 (신형) |
| `Scripts/GazeFrequencyController.cs` | 시선 빈도 계산기 — UI 패널 동적 생성 포함                                            |
| `Scripts/GazeFrequencyUIFactory.cs`  | 시선 빈도 UI 패널 팩토리                                                             |
| `Scripts/GazeFrequencyUIPanel.cs`    | 시선 빈도 UI 패널 컴포넌트                                                           |

---

## 5. 주요 패키지 & SDK

| 패키지                      | 버전   | 용도                                   |
| --------------------------- | ------ | -------------------------------------- |
| `com.meta.xr.sdk.all`       | 77.0.0 | Meta XR SDK (Quest 하드웨어 전체 접근) |
| `com.meta.movement`         | latest | Meta Body/Face Tracking, IK            |
| `com.avaturn.core`          | v0.3.2 | Avaturn 아바타 SDK                     |
| `com.unity.xr.openxr`       | 1.11.0 | OpenXR 지원                            |
| `com.unity.xr.management`   | 4.5.1  | XR 플랫폼 관리                         |
| `com.quaza.unitymcp`        | latest | Unity MCP — AI 에이전트(Gemini) 연동   |
| `com.unity.textmeshpro`     | 3.0.6  | 고품질 UI 텍스트                       |
| `com.unity.visualscripting` | 1.9.4  | 비주얼 스크립팅                        |
| `com.atteneder.gltfast`     | v5.0.5 | GLB/glTF 3D 모델 로드                  |

---

## 6. 에셋 구성

### 사운드

| 파일                                          | 용도                |
| --------------------------------------------- | ------------------- |
| `Plastic bag Hand Collision sound.mp3`        | 손-비닐 충돌음      |
| `Plastic bag Strong/light pounding sound.mp3` | 강/약 두드림 사운드 |
| `Plastic bag to Plasticbag collision.mp3`     | 비닐-비닐 충돌음    |
| `Plastic bag touching sound.mp3`              | 비닐 터치 사운드    |
| `Welcome.mp3` / `인사쏭.mp3`                  | 시작 씬 환영 사운드 |
| `RotateWind.wav`                              | 회전 바람 효과음    |

### 3D 모델 & 아바타

- `JHKim_AVATAR_v2412.fbx` / `JHKim_AVATAR_v241220.fbx` — 커스텀 인체 아바타 (Avaturn)
- `model.glb` / `model (1).glb` — GLB 포맷 3D 모델
- `garbage_bag_min.prefab` — 비닐봉지 메인 프리팹
- `StartButton.fbx` — 시작 버튼 3D 모델

### 시선 추적 (EyeGaze 폴더)

- `DysonSphere.fbx` — 시선 포인터용 구체 모델
- `EyeGazeSystem.prefab` / `EyeGazeAnchor.prefab` — 시선 시스템 프리팹
- 시선 On/Off 머티리얼 포함

---

## 7. 빌드 산출물 (APK 히스토리)

| APK 파일                                | 크기  | 내용                 |
| --------------------------------------- | ----- | -------------------- |
| `PlasticBagGame.apk`                    | 115MB | 메인 게임 빌드       |
| `PlasticGame_0708.apk`                  | 115MB | 0708 버전            |
| `PlasticBag_with_Eyetracking.apk`       | 85MB  | 아이트래킹 추가 버전 |
| `GeminiControl.apk`                     | 79MB  | Gemini AI 제어 버전  |
| `GamePlayUI.apk` / `GamePlayUITest.apk` | 82MB  | UI 테스트 버전       |
| `EyecountTest.apk`                      | 116MB | 시선 카운팅 테스트   |
| `EyetrackingTest.apk`                   | 103MB | 아이트래킹 테스트    |
| `UITest.apk`                            | 79MB  | UI 단독 테스트       |

---

## 8. 게임플레이 흐름

```
[1_Start_Scene]
  └─ 회전하는 오브젝트(SceneSwitcher) + 환영 음성 반복 재생
  └─ 플레이어 손이 Trigger와 접촉
        ↓ OVRScreenFade (페이드 아웃)

[3_1_PlasticBagPlay]
  └─ PlasticbagSpawner: 공중 랜덤 위치에 비닐봉지 스폰
  └─ 플레이어가 손/시선으로 상호작용
  └─ CollisionSoundPlayer: 충돌 시 적절한 사운드 재생
  └─ PlasticBag.OnCollisionEnter: 바닥 접촉 → 자신 삭제 + 새 봉지 스폰

[선택: with_Analyze 버전]
  └─ BoldGazeInteractionController: 시선 대상 및 빈도를 실시간 UI에 표시
```

---

## 9. 특이사항 & 참고

> [!NOTE]
> **장애인 접근성 컨셉**: `Game_for_Disabled` 서브 폴더가 별도로 존재하며, 손 움직임 없이 시선만으로 조작 가능한 게임을 지향합니다.

> [!NOTE]
> **Gemini AI 연동**: `GeminiAI.unity` 씬과 `GeminiControl.apk` 빌드가 존재합니다. `com.quaza.unitymcp` 패키지로 AI 에이전트와 Unity를 직접 연동하는 기능을 시도했습니다.

> [!NOTE]
> **Avaturn 아바타**: `JHKim_AVATAR` 파일명에서 알 수 있듯 실제 인물(김JH)을 스캔한 개인 아바타를 사용합니다. `model.glb`도 Avaturn을 통해 생성된 모델로 추정됩니다.

> [!NOTE]
> **시선 시스템 이중화**: `GazeInteractionController`(구형, Raycast)와 `BoldGazeInteractionController`(신형, SphereCast)가 공존합니다. 신형이 더 정확하므로 신형 사용을 권장합니다.

> [!NOTE]
> **APK 파일 정리 필요**: 루트 폴더에 개발 과정에서 생성한 APK 파일 8개(약 880MB)가 남아 있습니다. 불필요한 APK는 정리하면 저장 공간을 절약할 수 있습니다.
