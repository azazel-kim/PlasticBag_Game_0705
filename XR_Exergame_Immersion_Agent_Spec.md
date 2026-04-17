# XR Exergame 몰입도 자동 탐지 시스템 — AI Agent 작업 명세서

> **용도:** AI 에이전트가 순차적으로 작업을 수행하기 위한 프로젝트 컨텍스트 문서
> **최종 갱신:** 2026-04-16
> **타겟 학회:** IEEE VR / CHI / IEEE TAFFC / IJHCS

---

## 0. 프로젝트 한 줄 요약

Samsung Galaxy XR의 내부 IR 센서(68 블렌드셰이프)로 HMD 착용 중 가려진 상반부 얼굴을 FLAME 3DMM으로 복원하여 풀페이스를 생성하고, DAiSEE 데이터셋으로 사전 훈련된 비전 모델에 입력하여 감정/몰입도를 근실시간 분류한 뒤 XR Exergame에 반영하는 시스템.

---

## 1. 핵심 문제: 도메인 갭

| 항목 | DAiSEE (소스) | Galaxy XR (타겟) |
|------|--------------|-----------------|
| 입력 | RGB 풀페이스 이미지 | IR 부분얼굴 → 68 FACS 블렌드셰이프 float벡터 |
| 가시영역 | 전체 얼굴 | 눈주변 + 하안면 (HMD로 분리) |
| 조명 | 자연광/실내광 | 적외선(IR) |
| 출력 | 원본 프레임 | 68D 블렌드셰이프 + 3영역 신뢰도(0~1) |

**결론:** 직접 전이 불가 → 블렌드셰이프→FLAME→3D 풀페이스 복원 경로 필요.

---

## 2. 연구 Novelty (Research Gap)

```
[Georgescu 2020] 하반부만으로 FER → 상반부 정보 없어 한계
[OFERA 2026]     블렌드셰이프→FLAME→3D Gaussian 아바타 복원 (렌더링 목적)
[본 연구]        블렌드셰이프→FLAME→3D 풀페이스 복원→감정/몰입도 인식 ← 미발표 영역
                 + Galaxy XR 눈/눈썹 데이터로 Georgescu 한계 극복
```

**선행연구 참조:**
- Georgescu et al. (2020): https://ieeexplore.ieee.org/document/9232653
- OFERA (2026.02, 한국 NST/IITP): https://arxiv.org/html/2602.01748v1
- Mind-to-Face (2025.12): https://arxiv.org/html/2512.04313
- 3D Gaussian Blendshapes (SIGGRAPH 2024): https://arxiv.org/html/2404.19398v2
- DAiSEE 원본: https://arxiv.org/abs/1609.01885

---

## 3. 기술 파이프라인 (3 Stages)

### Stage 1: 사전 훈련 (Pre-training)
- **입력:** DAiSEE 9,068 비디오 (112명, 25시간, ~2.7M 프레임)
- **처리:** OpenFace 2.0으로 AU 강도 추출 → FLAME 파라미터 변환
- **모델:** EfficientNetV2 또는 MLP/Temporal Transformer
- **레이블:** 4감정×4강도 (Boredom, Engagement, Confusion, Frustration × VeryLow~VeryHigh)
- **벤치마크 SOTA:** 73.43% (ViBED-Net, 2025)

### Stage 2: 블렌드셰이프→FLAME 매핑
- **매핑:** Galaxy XR 68 블렌드셰이프 → BDA(분포 정렬, OFERA 차용) → FLAME 표현 파라미터(50D)
- **차별점:** 눈/눈썹 블렌드셰이프가 FLAME 상안면 표현을 정밀 구동
- **캘리브레이션:** 참가자별 HMD 비착용 풀페이스 촬영 → 개인 중립 FLAME 모델 구축

### Stage 3: 3D 풀페이스 복원 + 감정 추론
- **복원:** FLAME 파라미터 → 3D 풀페이스 메시 렌더링
- **추론:** 복원 풀페이스 → Stage1 사전 훈련 모델 → 4감정 분류
- **추론 속도:** 근실시간 (수초 딜레이 허용) → 게임에 반영

---

## 4. 파인튜닝 전략 (비전 모델, LLM 아님)

> ⚠️ 본 프로젝트는 **LLM 파인튜닝이 아닌 비전 모델/도메인 적응(Domain Adaptation)**임.
> 논문 표현: "Transfer Learning 기반 도메인 적응" 사용.

| 전략 | 방식 | 장점 | 단점 | 권장도 |
|------|------|------|------|--------|
| **A** | FLAME→2D 렌더링→CNN 파인튜닝 | DAiSEE 모델 재활용 | 3D→2D 정보손실 | 보조 |
| **B (권장)** | FLAME 파라미터(50D)→MLP/Transformer 직접 분류 | 렌더링 불필요, 근실시간 최적 | 별도 분류기 학습 | ⭐ 메인 |
| **C** | FLAME + 기하학적 특징(Deformation Gradient) 하이브리드 | 최고 성능 기대 | 복잡도↑ | 확장 |

**권장 경로:** B로 시작 → 검증 후 C로 확장.

**B 전략 상세 절차:**
1. DAiSEE 비디오 → OpenFace AU → FLAME 50D 파라미터
2. FLAME 50D → MLP/Temporal Transformer → 4감정 분류 학습
3. Galaxy XR 블렌드셰이프 → BDA → FLAME → 동일 분류기
4. 자체 수집 데이터로 BDA + 분류 Head 파인튜닝

---

## 5. 다중 모달 데이터 융합

```
[Galaxy XR 블렌드셰이프 68D @72Hz] → AU/감정 분류 ──┐
[Galaxy XR 아이트래킹 @72Hz]       → 시선 패턴    ──┤
[Galaxy XR 눈 깜박임 @72Hz]        → 각성도       ──┤
[외부카메라 MediaPipe 33점 @30Hz]  → 동작 활성도  ──┼→ Hybrid Fusion → 몰입도
[Looxid Link EEG 6ch @256Hz]       → 인지부하/각성──┤
[IEQ 설문 Likert 7점]              → Ground Truth ──┘
```

**융합 방식:** Hybrid Fusion 권장 (관련 모달끼리 중간 융합 → 최종 통합)
**기대 성능:** 단일 AUC 0.70~0.85 → 융합 시 AUC 0.78~0.92

---

## 6. 하드웨어/소프트웨어 스택

### 필수 하드웨어
| 기기 | 용도 | 비고 |
|------|------|------|
| Samsung Galaxy XR ($1,799) | 얼굴 트래킹, 아이트래킹, 아바타 | Snapdragon XR2+ Gen2, OpenXR |
| 외부 RGB 웹캠 (Logitech C920+) | 전신 바디 트래킹 (MediaPipe) | 또는 Depth 카메라 |
| RTX 4080 PC (16GB VRAM) | 훈련/추론/Unity 개발 | 메인 개발환경 |
| M1 MacBook 32GB | 전처리/프로토타입/시각화 | 보조 개발환경 |
| Looxid Link 2.0 EEG (기보유) | Ground Truth 검증 | 6ch EEG |

### 소프트웨어 스택
| SW | 용도 | 참고 |
|----|------|------|
| Unity 6 + Android XR Extensions | XR 개발, Face/Eye/Body Tracking | [dev docs](https://developer.android.com/develop/xr/unity) |
| OpenFace 2.0 | DAiSEE AU 추출 | [github](https://github.com/TadasBaltrusaitis/OpenFace) |
| MediaPipe BlazePose | 외부 카메라 전신 포즈 (33 랜드마크) | [github](https://github.com/google/mediapipe) |
| Python + PyTorch | 모델 훈련 | scikit-learn, FLAME PyTorch 구현 |
| FLAME PyTorch | 3DMM 파라미터 매핑 | [flame.is.tue.mpg.de](https://flame.is.tue.mpg.de) |

### 핵심 API 경로 (Unity)
- `XRFaceTrackingFeature` → 68 블렌드셰이프 벡터 + 3영역 신뢰도
- `Android XR Extensions for Unity` → face/eye/body tracking
- `SkinnedMeshRenderer.SetBlendShapeWeight()` → 아바타 매핑
- UDP/WebSocket → Python MediaPipe↔Unity 통신

---

## 7. 아바타 연동 설계

### 얼굴 (Galaxy XR → 아바타)
1. `XRFaceTrackingFeature` 활성화 → 블렌드셰이프 수신
2. 블렌드셰이프 → SkinnedMeshRenderer 매핑
3. 영역별 신뢰도로 저신뢰 구간 필터링
4. 아이트래킹 → 눈 bone 직접 매핑
5. 눈 깜박임 → 별도 파라미터 분리
6. 혀 움직임 (최신 OpenXR 확장)

### 바디 (외부 카메라 → 아바타)
1. Python MediaPipe BlazePose → 33개 3D 랜드마크
2. UDP → Unity 전송
3. Avatar Configuration Tool → bone 매핑
4. IK(Inverse Kinematics) 포즈 보정

**참고 프로젝트:**
- https://github.com/skill-diver/Unity_MediaPipe_Action_Tracking
- https://github.com/ju1ce/Mediapipe-VR-Fullbody-Tracking

---

## 8. 실험 설계

### 참가자 수
| 단계 | N | 데이터 소스 | 근거 |
|------|---|------------|------|
| Phase 1 사전훈련 | 112 (기존) | DAiSEE | 공개 데이터셋 |
| Phase 2 파일럿 | **15명** | 자체수집 Galaxy XR | 매핑 검증, 파이프라인 검증 |
| Phase 3 본실험 | **25~30명** | 자체수집 전체 모달 | Cohen's d=0.8, power 80% → N≈26 |

### 세션 구조 (참가자당)
1. 캘리브레이션: HMD 비착용 풀페이스 촬영 (FLAME 모델링)
2. XR Exergame 플레이: 30~45분 (모든 모달 동시 수집)
3. 사후: IEQ 설문 + RTA(회고적 Think-Aloud, 선택)

### 데이터량 추정
- 파일럿 15명 × 30분 × 72Hz = ~1,944,000 블렌드셰이프 프레임
- 바디 포즈: 15명 × 30분 × 30Hz = ~810,000 프레임

---

## 9. 실행 로드맵 (총 15~22주)

| # | 작업 | 기간 | 입력 | 출력 |
|---|------|------|------|------|
| 1 | DAiSEE 다운로드 + OpenFace AU 추출 + FLAME 변환 + 사전훈련 | 2~3주 | DAiSEE 데이터셋 | AU→감정 분류 모델 |
| 2 | Unity 6 프로젝트 세팅 + Galaxy XR Face Tracking API + MediaPipe 연동 | 2~3주 | SDK/패키지 | 데이터 수집 파이프라인 |
| 3 | 파일럿 실험 (N=15) + 파이프라인 검증 | 2~3주 | 참가자 | 블렌드셰이프+EEG+포즈 데이터 |
| 4 | BDA + FLAME 매핑 파인튜닝 + 모델 검증 | 2~3주 | 파일럿 데이터 | 도메인 적응 모델 |
| 5 | 본 실험 (N=25~30) + 다중모달 융합 모델 | 3~4주 | 참가자 | 최종 몰입도 예측 모델 |
| 6 | 결과 분석 + 논문 작성 | 4~6주 | 전체 결과 | 논문 (IEEE VR/CHI) |

---

## 10. LLM 활용 지점 (선택적 확장)

본 파이프라인은 비전 모델 기반이나, Think-Aloud 분석에서 실제 LLM 활용 가능:
- RTA 수행 → Whisper 전사 → SBERT 임베딩 → 몰입도 발화 자동 분류
- 비전 모델 결과와 삼각측량(triangulation)으로 교차 검증
- IEEE VR 2026 포스터(음성 기반 VR 질문지)와 연결되는 확장

---

## 11. 컴퓨팅 환경

### RTX 4080 (메인)
- CNN+LSTM: batch 16, ~20h 학습, 80~83% 정확도
- Mixed Precision(AMP) 사용 권장
- 권장 저장장치: Samsung T9 1TB NVMe (2,000MB/s)

### M1 MacBook 32GB (보조)
- ResNet-50 프로토타입: batch 8~12, ~40h, 75~78%
- 전처리/시각화/평가용

### 하이브리드 워크플로우
1. M1: 전처리+프로토타입 (2~3일)
2. RTX 4080: 메인 학습 (1~2일)
3. M1: 평가+시각화 (1일)
→ **총 4~6일, 80~83% 정확도**

---

## 12. 에이전트 작업 체크리스트

각 단계를 순차적으로 실행할 것. 이전 단계의 출력이 다음 단계의 입력이 됨.

### TASK 1: 데이터 준비 및 AU 추출
```
입력: DAiSEE 데이터셋 다운로드 URL
작업: 
  1. 비디오 다운로드 및 프레임 추출
  2. OpenFace 2.0으로 프레임별 AU 강도 추출 (CSV)
  3. AU CSV → FLAME 50D 파라미터 변환
  4. 학습/검증/테스트 분할 (학생 수준 5-Fold)
출력: flame_params.csv + labels.csv + splits.json
```

### TASK 2: 사전 훈련 모델 학습
```
입력: TASK 1 출력
작업:
  1. FLAME 50D → MLP (512→256→128→4×4) 또는 Temporal Transformer
  2. 4감정 × 4강도 multi-label 분류
  3. 5-Fold 학생 수준 교차검증
  4. AUC, F1, Accuracy 평가
출력: pretrained_model.pt + metrics.json
벤치마크: Engagement AUC > 0.70 목표
```

### TASK 3: Unity 프로젝트 세팅
```
입력: Unity 6, Android XR SDK
작업:
  1. Unity 프로젝트 생성 (Android XR 타겟)
  2. Android XR Extensions 설치 (face tracking)
  3. XRFaceTrackingFeature 활성화 + 블렌드셰이프 로깅
  4. MediaPipe Python→Unity UDP 통신 구축
  5. 데이터 동기화 타임스탬프 설계
출력: Unity 프로젝트 + 데이터 수집 파이프라인
```

### TASK 4: BDA 매핑 레이어 구축
```
입력: Galaxy XR 블렌드셰이프 캘리브레이션 데이터
작업:
  1. HMD 비착용 풀페이스 → FLAME 파라미터 추출 (Ground Truth)
  2. 동시에 HMD 착용 → Galaxy XR 블렌드셰이프 수집
  3. 블렌드셰이프 68D → FLAME 50D 매핑 MLP 학습 (BDA)
  4. 매핑 정확도 검증 (MSE, 시각적 비교)
출력: bda_mapper.pt
```

### TASK 5: 파인튜닝 및 통합
```
입력: pretrained_model.pt + bda_mapper.pt + 파일럿 데이터
작업:
  1. Galaxy XR 블렌드셰이프 → BDA → FLAME → 분류기 End-to-End 파인튜닝
  2. 다중 모달 융합 모델 (Hybrid Fusion)
  3. 근실시간 추론 파이프라인 (Unity↔Python)
출력: finetuned_model.pt + fusion_model.pt + 추론 서버
```

### TASK 6: 본 실험 및 논문
```
입력: 전체 파이프라인 + N=25~30 참가자
작업:
  1. 데이터 수집 (전체 모달)
  2. 최종 모델 학습 + 평가
  3. 통계 분석 (AUC, paired t-test, Cohen's d)
  4. 논문 작성 (IEEE VR / CHI 포맷)
출력: 논문 + 코드 리포지토리 + 데이터셋
```

---

## 13. 핵심 참고문헌 (Essential References)

1. Gupta et al. (2016). DAiSEE. arXiv:1609.01885
2. OFERA (2026). Blendshape-driven 3D Gaussian for VR. arXiv:2602.01748
3. Georgescu et al. (2020). FER Under Partial Occlusion from VR. IEEE
4. Mind-to-Face (2025). EEG→3D Face. arXiv:2512.04313
5. Ma et al. (2024). 3D Gaussian Blendshapes. SIGGRAPH 2024
6. Li et al. (2017). FLAME: Faces Learned with an Articulated Model
7. Santoni et al. (2023). CNN Engagement Detection in DAiSEE. IJACSA
8. ViBED-Net (2025). SOTA 73.43% on DAiSEE. arXiv:2510.18016
9. Brown & Cairns (2004). Grounded Investigation of Game Immersion. CHI
