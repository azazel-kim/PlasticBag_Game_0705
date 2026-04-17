# PROJECT_CONTEXT.md — DXP Lab XR Exergame Master Reference

> **PURPOSE**: Authoritative single-source-of-truth for all AI agents working on this project.
> All agents should read this file at session start before any other action.
> **Last updated**: 2026-04-17
> **Maintained by**: Cowork (Claude) + Project Lead (dxplabku307a@gmail.com)

---

## SECTION 0: ONE-LINE SUMMARY

Samsung Galaxy XR 기반의 비닐봉지 Exergame을 플랫폼으로 활용하여, 시니어/발달장애/ADHD 등 디지털소외계층의 재활운동을 지원하는 **멀티모달 몰입도 자동탐지 + 적응형 피드백 시스템**을 연구·개발한다. 얼굴표정(IR블렌드셰이프→FLAME→DAiSEE), 모션(MediaPipe/X-Sens), EEG(LinkBand2), 아이트래킹을 실시간 융합하여 engagement를 근실시간 추론하고 게임에 반영한다.

---

## SECTION 1: PROJECT IDENTITY

| 항목 | 값 |
|------|-----|
| 프로젝트명 | PlasticBag_Game (DXP Lab) |
| 소속 | DXP Lab, 경희대 |
| 연락처 | dxplabku307a@gmail.com |
| Git 리포 | github.com/azazel-kim/PlasticBag_Game_0705 |
| 활성 브랜치 | `samsung-xr` |
| 타겟 학회 | IEEE VR / CHI / IEEE TAFFC / IJHCS |
| 연구 목적 | 디지털소외계층(시니어·발달장애·ADHD·경계성지능) 재활운동 Exergame + 몰입도 검증 |

---

## SECTION 2: HARDWARE ECOSYSTEM

```
┌─────────────────────────────────────────────────────────────────┐
│                   HARDWARE STACK (2026-04)                     │
├──────────────────┬────────────────────────────────────────────┤
│ Samsung Galaxy XR│ 메인 XR 헤드셋 ($1,799)                     │
│  SM_I610         │ Snapdragon XR2+ Gen2                       │
│                  │ 내장 IR카메라 → 68 FACS 블렌드셰이프 @72Hz  │
│                  │ Eye Tracking @72Hz (좌우 회전+깜박임)       │
│                  │ OpenXR + Google XR Extensions              │
├──────────────────┼────────────────────────────────────────────┤
│ RTX 4080 PC      │ 메인 개발/훈련 환경 (16GB VRAM)             │
│  (Windows)       │ CNN+LSTM batch16, Mixed Precision(AMP)     │
│                  │ Samsung T9 1TB NVMe 권장                   │
├──────────────────┼────────────────────────────────────────────┤
│ M1 MacBook Pro   │ 보조 개발 (32GB RAM)                        │
│                  │ 전처리·프로토타입·시각화                     │
├──────────────────┼────────────────────────────────────────────┤
│ LinkBand2 EEG    │ 뇌파밴드 (기보유) — 6ch EEG @256Hz         │
│                  │ BLE 연결, PPG @25Hz, ACC @60Hz             │
│                  │ Ground Truth 검증 도구                      │
├──────────────────┼────────────────────────────────────────────┤
│ OBSBOT Tiny2     │ 외부 RGB 카메라 (AI 자동추적)               │
│                  │ MediaPipe BlazePose 33랜드마크 @30Hz        │
│                  │ libdev_v2.1.0_8 SDK 포함됨                  │
├──────────────────┼────────────────────────────────────────────┤
│ X-Sens IMU       │ 상체 관절 가동범위 평가용                    │
│                  │ 60~120Hz, 가속도+자이로+자기장+쿼터니언     │
│                  │ 재활 치료사 협업 측정 도구                   │
├──────────────────┼────────────────────────────────────────────┤
│ (선택) Nanoclaw  │ Edge AI 추론 시스템 (EDGE AI)              │
│                  │ 멀티에이전트 LLM API 연결 예정              │
└──────────────────┴────────────────────────────────────────────┘
```

---

## SECTION 3: SOFTWARE STACK

### Unity Project (현재 구현 상태)

| 항목 | 값 |
|------|-----|
| Unity 버전 | 6000.1.17f1 (Unity 6) |
| 렌더 파이프라인 | URP 17.1.0 |
| 패키지명 | com.DXPLap.PlasticBagGame |
| 타겟 플랫폼 | Android XR (API Level 29+, ARM64, IL2CPP, Vulkan) |
| XR SDK | com.unity.xr.openxr 1.15.1 + com.unity.xr.androidxr-openxr 1.0.0 |
| 손 추적 | com.unity.xr.hands 1.7.3 |
| XR Interaction | com.unity.xr.interaction.toolkit 3.3.1 |
| Google XR | com.google.xr.extensions (android-xr-unity-package v1.2.0) |
| 빌드 | Builds/PlasticBagGame_SamsungXR_1.2.2_20260409_040452.apk |

### 핵심 C# 스크립트 (Assets/)

| 파일 | 책임 |
|------|------|
| `HandBounceResponder.cs` | 손 충돌감지, 바운스, 색상변경, 7회→팝 이벤트 |
| `PlasticbagSpawner.cs` | 5초간격 스폰, 최대10개 관리 |
| `PlasticBag.cs` | 바닥충돌→파괴+스포너알림 |
| `ScoreManager.cs` | 점수계산+UI업데이트 |
| `ScoreFollowCamera.cs` | Lazy Follow UI |
| `GazeInteractionController.cs` | 시선추적 % 바 + UI위치 관리 |
| `EyeTrackingRaycaster.cs` | OpenXR 시선 레이캐스트 |
| `BGMManager.cs` | 배경음악 랜덤 셔플 |
| `Plasticbagsound.cs` | 충돌강도별 사운드 |
| `SceneSwitcher.cs` | CanvasGroup 기반 씬전환 페이드 |
| `AndroidXRPassthroughSetup.cs` | Android XR 패스스루 배경 |
| `Scripts/GazeFrequencyController.cs` | 타겟별 시선 체류시간 추적 |

### 씬 구성

| 씬 | 설명 | 상태 |
|----|------|------|
| `1_Start_Scene_with_Analyze` | 시작화면+분석 | 활성 |
| `3-1_PlasticBagPlay_with_Analyze` | 메인 게임플레이+분석 | 활성 (메인) |

### AI/ML 스택 (Python 사이드)

| SW | 용도 |
|----|------|
| Python + PyTorch | 모델 훈련/추론 |
| OpenFace 2.0 | DAiSEE AU 강도 추출 |
| FLAME PyTorch | 3DMM 파라미터 매핑 (flame.is.tue.mpg.de) |
| MediaPipe BlazePose | 외부카메라 전신 포즈 33랜드마크 |
| Whisper + SBERT | 선택적 — RTA 음성분석 |

---

## SECTION 4: RESEARCH PIPELINE (6 TASKS)

### 핵심 기술 문제: 도메인 갭

```
DAiSEE 소스 도메인          →    Galaxy XR 타겟 도메인
──────────────────────────────────────────────────────
RGB 풀페이스 이미지               IR 부분얼굴 → 68 FACS 블렌드셰이프 float벡터
전체 얼굴 가시                    눈주변+하안면 (HMD로 나머지 가림)
자연광/실내광                     적외선(IR)
원본 프레임                       68D 블렌드셰이프 + 3영역 신뢰도(0~1)
```

**해결 경로**: 블렌드셰이프 → FLAME 3DMM → 풀페이스 복원 → DAiSEE 사전훈련 모델

### Research Novelty

```
[Georgescu 2020]  하반부만으로 FER → 상반부 정보 없어 한계
[OFERA 2026]      블렌드셰이프→FLAME→3D Gaussian 아바타 복원 (렌더링 목적)
[본 연구]          블렌드셰이프→FLAME→3D 풀페이스 복원→감정/몰입도 인식  ← 미발표 영역
                  + Galaxy XR 눈/눈썹 데이터로 Georgescu 한계 극복
```

### TASK 1: 데이터 준비 및 AU 추출

```
입력:  DAiSEE 데이터셋 (9,068 비디오, 112명, 25시간, ~2.7M 프레임)
작업:  비디오→프레임 추출 → OpenFace 2.0 AU강도 CSV → FLAME 50D 변환 → 5-Fold 분할
출력:  flame_params.csv + labels.csv + splits.json
레이블: 4감정×4강도 (Boredom/Engagement/Confusion/Frustration × VeryLow~VeryHigh)
```

### TASK 2: 사전 훈련 모델 학습

```
입력:  TASK 1 출력
모델:  FLAME 50D → MLP(512→256→128→4×4) 또는 Temporal Transformer  [전략 B ⭐ 권장]
검증:  5-Fold 학생수준 교차검증
목표:  Engagement AUC > 0.70
벤치마크 SOTA: ViBED-Net 73.43% (2025)
출력:  pretrained_model.pt + metrics.json
비교전략: A=FLAME→2D렌더→CNN(보조), B=FLAME파라미터→MLP직접(메인), C=하이브리드(확장)
```

### TASK 3: Unity 데이터 수집 파이프라인

```
입력:  Unity 6 + Samsung Galaxy XR SDK
작업:  XRFaceTrackingFeature 활성화 + 블렌드셰이프 로깅
       Python MediaPipe↔Unity UDP/WebSocket 통신
       멀티모달 타임스탬프 동기화 설계
출력:  데이터 수집 파이프라인 (Unity↔Python)
API:   XRFaceTrackingFeature → 68 블렌드셰이프 + 3영역신뢰도
       SkinnedMeshRenderer.SetBlendShapeWeight()
```

### TASK 4: BDA 매핑 레이어

```
입력:  파일럿 캘리브레이션 데이터 (HMD 비착용 풀페이스 + HMD 착용 블렌드셰이프)
작업:  BDA(분포정렬, OFERA 차용): 블렌드셰이프 68D → FLAME 50D 매핑 MLP
       개인별 중립 FLAME 모델 구축 (HMD 비착용 촬영)
검증:  MSE + 시각적 비교
출력:  bda_mapper.pt
```

### TASK 5: 파인튜닝 + 멀티모달 융합

```
입력:  pretrained_model.pt + bda_mapper.pt + 파일럿 데이터 (N=15)
작업:  End-to-End 파인튜닝 (블렌드셰이프→BDA→FLAME→분류기)
       Hybrid Fusion 모델 구축
       근실시간 추론 서버 (Unity↔Python)
출력:  finetuned_model.pt + fusion_model.pt + 추론서버
```

### TASK 6: 본 실험 + 논문

```
입력:  전체 파이프라인 + N=25~30 참가자
설계:  Cohen's d=0.8, power 80% → N≈26
       세션: 캘리브레이션(HMD비착용) → 게임플레이 30~45분 → IEQ설문
분석:  AUC, paired t-test, Cohen's d
출력:  논문 (IEEE VR/CHI) + 코드 + 데이터셋
```

---

## SECTION 5: MULTIMODAL FUSION ARCHITECTURE

```
[Galaxy XR 블렌드셰이프 68D @72Hz]  → AU/감정 분류 ──┐
[Galaxy XR 아이트래킹 @72Hz]        → 시선 패턴    ──┤
[Galaxy XR 눈깜박임 @72Hz]          → 각성도       ──┤
[외부카메라 MediaPipe 33점 @30Hz]   → 동작 활성도  ──┼→ Hybrid Fusion → Engagement Score
[LinkBand2 EEG 6ch @256Hz]          → 인지부하/각성──┤   → Game Adaptive System
[X-Sens IMU @60-120Hz]              → 관절가동범위 ──┤
[IEQ 설문 Likert 7점]               → Ground Truth ──┘

출력 주기: FusedDataFrame @30Hz
MaxDrift: 100ms
RingBuffer: 512슬롯/센서
```

### 센서별 스펙

| 센서 | 주기 | 지연 | 데이터 |
|------|------|------|--------|
| EEG (LinkBand2) | ~256~500Hz | 20-50ms(BLE) | 6ch 전압(µV)+전극상태 |
| PPG (LinkBand2) | ~25Hz | 20-50ms | 적외선/적색광 |
| ACC (LinkBand2) | ~60Hz | 20-50ms | 3축 가속도 |
| Camera (OBSBOT) | ~30Hz | 50-100ms | JPEG+포즈추정 |
| X-Sens IMU | ~60-120Hz | 5-20ms | 가속도+자이로+자기장+쿼터니언 |
| Eye Tracking | ~72Hz | <10ms | 좌우눈회전+시선방향+깜박임 |

### 기대 성능

| 구성 | 예상 AUC |
|------|---------|
| 얼굴표정 단독 | 0.70~0.80 |
| + 아이트래킹 | 0.74~0.85 |
| + EEG | 0.78~0.89 |
| 전체 Hybrid Fusion | 0.78~0.92 |

---

## SECTION 6: EXPERIMENT DESIGN

### 참가자 계획

| 단계 | N | 목적 |
|------|---|------|
| Phase 1 (기존) | 112명 | DAiSEE 사전훈련 |
| Phase 2 파일럿 | 15명 | 매핑검증·파이프라인검증 |
| Phase 3 본실험 | 25~30명 | Cohen's d=0.8, power 80% |

### 세션 구조 (참가자당)

1. 캘리브레이션: HMD 비착용 풀페이스 촬영 → 개인 FLAME 모델
2. XR Exergame 플레이: 30~45분 (전모달 동시수집)
3. 사후: IEQ 설문(Likert 7점) + RTA(선택)

### 데이터량 추정 (파일럿)

- 블렌드셰이프: 15명 × 30분 × 72Hz = ~1,944,000 프레임
- 바디 포즈: 15명 × 30분 × 30Hz = ~810,000 프레임
- EEG: 15명 × 30분 × 256Hz = ~6,912,000 샘플

---

## SECTION 7: AGENT SYSTEM

### 14 Sub-Agents (모두 .claude/agents/ 에 정의됨)

| 에이전트 | 모델 | 담당 | 우선순위 |
|---------|------|------|---------|
| `xr-exergame-director` | opus | 총괄 디렉터, 아키텍처 결정 | - |
| `linkband2-sensor-specialist` | sonnet | LinkBand2 BLE 통신, EEG/PPG/ACC | 1 |
| `multi-sensor-fusion-engineer` | opus | FusedDataFrame, TimeSyncAligner, RingBuffer | 2 |
| `xr-platform-integrator` | sonnet | OpenXR 추상화, 멀티플랫폼 빌드 | 3 |
| `samsung-xr-device-specialist` | sonnet | Samsung XR API, Face/Eye/Body Tracking | 4 |
| `exergame-fitness-system` | sonnet | 운동분석, 적응형 난이도 AI파이프라인 | 5 |
| `gameplay-physics-engineer` | sonnet | 물리시뮬, 스폰로직, 게임메카닉 | 6 |
| `eye-tracking-specialist` | sonnet | Gaze분석, 시선빈도, Foveated Rendering | 7 |
| `camera-motion-analyst` | haiku | Camera2 API, X-Sens IMU, 동작분석 | 8 |
| `body-tracking-exercise` | sonnet | MediaPipe 바디트래킹, 칼로리추정 | 9 |
| `xr-performance-optimizer` | sonnet | 프레임레이트, GPU/CPU 최적화 | 10 |
| `xr-ux-engineer` | sonnet | Spatial UI, 접근성, 씬전환 UX | 11 |
| `audio-haptics-engineer` | haiku | Spatial Audio, 충돌사운드, 햅틱 | 12 |
| `build-deploy-engineer` | haiku | APK 빌드, CI/CD, 스토어 배포 | 13 |

### 데이터 흐름 (센서→AI→게임)

```
LinkBand2(EEG/PPG/ACC)────┐
OBSBOT Camera+MediaPipe───┤
X-Sens IMU────────────────┤→ multi-sensor-fusion-engineer
Samsung XR EyeTracking────┤   → FusedDataFrame @30Hz
Samsung XR BlendShapes────┘        │
                                   ↓
                          exergame-fitness-system
                              → Engagement Score
                              → BDA+FLAME→감정분류
                              → Adaptive Difficulty
                                   │
                                   ↓
                          xr-exergame-director
                              → 게임 파라미터 조정
                              → Unity GameManager
```

---

## SECTION 8: CURRENT BUILD STATUS

### 완료된 기능 ✅

- Samsung Galaxy XR APK 빌드 성공 (v1.2.2, 2026-04-09)
- 손 추적 기반 비닐봉지 충돌·바운스·팝 게임플레이
- OpenXR Eye Tracking (시선빈도 % 바 UI)
- Android XR Passthrough 배경
- 점수 시스템 + Lazy Follow UI
- BGM 랜덤 셔플 (4곡, Mixkit 로열티프리)
- 충돌 강도별 3단계 사운드
- CanvasGroup 씬 전환 페이드
- GitHub Actions CI 빌드 (`samsung-xr-build.yml`)

### 진행 중 / 미구현 🔧

- BLE LinkBand2 EEG 실시간 수신 (Unity)
- MediaPipe↔Unity UDP 브릿지
- XRFaceTrackingFeature 블렌드셰이프 로깅/저장
- DAiSEE 전처리 파이프라인 (Python)
- FLAME 3DMM 변환 레이어
- 멀티모달 FusedDataFrame 시스템
- 근실시간 추론 서버 (Python↔Unity)
- 적응형 난이도 조절 시스템
- 실험 데이터 기록 (CSV/JSON) 시스템

---

## SECTION 9: FILE STRUCTURE (KEY PATHS)

```
PlasticBag_Game_0705/
├── Assets/
│   ├── HandBounceResponder.cs          # 핵심 게임플레이
│   ├── PlasticbagSpawner.cs
│   ├── PlasticBag.cs
│   ├── GazeInteractionController.cs    # Eye Tracking
│   ├── EyeTrackingRaycaster.cs
│   ├── AndroidXRPassthroughSetup.cs    # Passthrough
│   ├── Scripts/
│   │   ├── GazeFrequencyController.cs
│   │   ├── GazeFrequencyUIFactory.cs
│   │   └── GazeFrequencyUIPanel.cs
│   ├── Editor/
│   │   ├── BuildSamsungXR.cs
│   │   ├── SamsungXRBuilder.cs
│   │   └── DiagnosePassthrough.cs
│   └── Android/AndroidManifest.xml
├── Packages/manifest.json              # 패키지 의존성
├── libdev_v2.1.0_8/                    # OBSBOT SDK
│   └── OBSBOT_Sample/main.cpp
├── Builds/
│   └── PlasticBagGame_SamsungXR_1.2.2_20260409_040452.apk
├── .claude/
│   ├── agents/                         # 14개 서브에이전트
│   ├── commands/                       # 슬래시 커맨드
│   └── skills/                         # 코드 규칙
├── PROJECT_CONTEXT.md                  # ← 이 파일
├── XR_Exergame_Immersion_Agent_Spec.md # 연구 설계 상세
├── XR_Exergame_SubAgents_Guide.md      # 에이전트 설정 가이드
├── galaxy_xr_hand_interaction_guide.md # 손추적 구현 가이드
└── hand_Interaction_ai_agent_prompt.md # AI 에이전트 프롬프트 모음
```

---

## SECTION 10: KEY REFERENCES

```
[1] Gupta et al. (2016). DAiSEE. arXiv:1609.01885
[2] OFERA (2026). Blendshape→3D Gaussian for VR. arXiv:2602.01748
[3] Georgescu et al. (2020). FER Under VR Occlusion. IEEE:9232653
[4] Mind-to-Face (2025). EEG→3D Face. arXiv:2512.04313
[5] 3D Gaussian Blendshapes. SIGGRAPH 2024. arXiv:2404.19398
[6] Li et al. (2017). FLAME: Faces Learned with Articulated Model
[7] Santoni et al. (2023). CNN Engagement in DAiSEE. IJACSA
[8] ViBED-Net (2025). SOTA 73.43% DAiSEE. arXiv:2510.18016
[9] Brown & Cairns (2004). Game Immersion Investigation. CHI
```

---

## SECTION 11: CODING CONVENTIONS

- **언어**: 한국어 소통, 기술 API/클래스명은 영어
- **Unity 버전**: 6000.1.17f1 — Unity 6 전용 API 사용
- **C# 패턴**: MonoBehaviour 최소화, SerializeField+private, GetComponent 캐싱
- **물리**: FixedUpdate 전용, Object Pooling 적용
- **XR 프레임 예산**: 11ms/frame (90fps) 엄수
- **데이터 기록**: 타임스탬프 포함, 비동기 저장 (메인스레드 블로킹 금지)
- **브랜치**: feature/기능이름 → samsung-xr PR, force push 절대 금지

---

## SECTION 12: AGENT QUICK-START PROTOCOL

AI 에이전트가 새 세션에서 작업 시작 시:

```
1. 이 파일(PROJECT_CONTEXT.md) 읽기
2. XR_Exergame_Immersion_Agent_Spec.md 섹션 해당부분 확인
3. 관련 .claude/agents/{agent-name}.md 읽기
4. 현재 구현 상태(Section 8) 확인 후 작업 범위 파악
5. 작업 전 TODO 체크리스트 생성
```

---

*이 파일은 프로젝트 상태 변화에 따라 지속 업데이트해야 합니다.*
*Section 8 (Build Status)는 주요 마일스톤 달성 시마다 갱신.*
