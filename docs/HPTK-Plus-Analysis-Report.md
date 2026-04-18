---
tokens: 8000
last_updated: 2026-04-18
topics: [HPTK-Plus, Hand Physics, XR Exergame, Rehabilitation]
---

# HPTK-Plus 분석 보고서 및 XR Exergame 적용 전략

## 1. 라이브러리 개요

HPTK-Plus(Hand Physics Toolkit Plus)는 VR 환경에서 손-오브젝트 물리 상호작용을 구현하는 오픈소스 Unity 라이브러리입니다.

| 항목 | 내용 |
|------|------|
| 저장소 | https://github.com/louspawn/HPTK-Plus.git |
| 로컬 경로 | /Users/user/Projects/Unity/Hand Interaction with Physics/ |
| 라이선스 | MIT |
| 패키지명 | com.jorgejgnz.hptk (v0.7.0) |
| 최소 Unity | 2020.3+ (ArticulationBody: 2022+) |
| 아키텍처 | MVC 패턴 (Model-View-Controller) |

## 2. 핵심 시스템 분석

### 2.1 물리 엔진 레이어 (Pheasy)

Pheasy는 HPTK의 핵심 물리 관리자입니다.

| 기능 | 설명 | Exergame 활용 |
|------|------|--------------|
| ArticulationBody 지원 | Unity 2022+ 최신 물리 시스템. 관절 체인 안정성 우수 | Unity 6에 최적. 현재 Rigidbody 기반 대체 가능 |
| TargetConstraint | 물리 기반 위치/회전 추적. 손이 목표 위치를 부드럽게 따라감 | 손이 봉지를 관통하지 않고 자연스럽게 밀어냄 |
| 속도 제한 | maxLinearVelocity, maxAngularVelocity로 물리 폭발 방지 | 손 빠르게 휘둘러도 안정적 |
| 충돌 관리 | 손가락 간, 손가락-손바닥 간 충돌 무시 설정 | 자기 충돌로 인한 떨림 방지 |
| 텔레포트 | TeleportToDestination()으로 즉시 위치 이동 | 트래킹 끊김 복구 |

### 2.2 손 물리 애니메이션 (ArticulationBodyFollower)

손의 각 관절(19개 뼈)에 ArticulationBody를 배치하고, 트래킹 데이터를 물리적으로 따라가게 합니다.

| 축 | 제어 방식 | 용도 |
|----|-----------|------|
| X/Y/Z 위치 | Prismatic Joint | 손 위치 이동 |
| Y/X/Z 회전 | Revolute/Spherical Joint | 손가락 구부림, 손목 회전 |
| Root | 직접 Quaternion 적용 | 손목 뼈 최상위 |

핵심 기술: FixAngleJump()로 각도 불연속 방지 (350도 -> 10도 점프 방지)

### 2.3 접촉 감지 (ContactDetection)

| 감지 방식 | 설명 | 적합도 |
|-----------|------|--------|
| Triggers | OnTriggerEnter/Stay/Exit 사용 | 성능 우수, 정밀도 낮음 |
| OverlapSphere | Physics.OverlapSphere로 구 범위 감지 | 정밀도 높음, 비용 높음 |

3단계 상태:
1. isEntered - 손이 감지 범위에 진입
2. isTouched - 손이 오브젝트에 접촉
3. isGrasped - 손이 오브젝트를 잡고 있음

### 2.4 제스처 감지 (GestureDetection)

| 제스처 | 설명 | 재활 운동 활용 |
|--------|------|--------------|
| Grasp | 손가락 오므리기 | 악력 운동, 물건 잡기 |
| Fist | 주먹 쥐기 | 근력 측정, 반복 운동 |
| Custom | 사용자 정의 제스처 | 특정 재활 동작 인식 |

### 2.5 입력 추상화 (InputDataProvider)

| SDK | 지원 여부 | Samsung Galaxy XR 호환 |
|-----|-----------|----------------------|
| Meta Quest Android | 지원 | X (Meta 전용) |
| Leap Motion | 지원 | X (별도 장비) |
| OpenXR Hand Tracking | 미지원 (추가 필요) | 추가 구현 필요 |

## 3. 현재 Exergame과의 차이점

| 항목 | 현재 Exergame | HPTK-Plus |
|------|--------------|-----------|
| 손 물리 | Poke Interactor SphereCollider 1개 | 19개 관절 ArticulationBody 체인 |
| 충돌 감지 | OnTriggerEnter (단일 포인트) | 다중 관절 ContactDetection |
| 물리 반응 | AddForce Impulse (즉시) | ArticulationBody Drive (부드러움) |
| 손 관통 | 발생함 (위치 보정으로 임시 해결) | ArticulationBody가 자동 방지 |
| 제스처 | 핀치만 (XR Hands 직접) | Grasp, Fist, Custom 확장 가능 |
| 설정 | 코드 상수 + Inspector | ScriptableObject Configuration |

## 4. 적용 전략

### Phase 1: 핵심 물리 레이어 이식 (1-2주)

HPTK의 Pheasy + ArticulationBodyFollower를 Exergame에 통합합니다.

| 작업 | 설명 |
|------|------|
| Pheasy 이식 | 물리 관리자를 프로젝트에 추가 |
| ArticulationBody 손 구성 | XR Origin의 Hand에 ArticulationBody 체인 구성 |
| OpenXR InputDataProvider | Samsung Galaxy XR용 InputDataProvider 신규 작성 |
| 충돌 레이어 설정 | Hand/Object/Ground 레이어 분리 |

### Phase 2: 접촉/제스처 시스템 통합 (1주)

| 작업 | 설명 |
|------|------|
| ContactDetection 적용 | 봉지 프리팹에 ContactDetection 추가 |
| GestureDetection 적용 | 재활 운동 제스처 감지 (쥐기, 펴기, 회전) |
| HandBounceResponder 교체 | HPTK 물리 기반 충돌 반응으로 교체 |

### Phase 3: 재활 운동 특화 (1-2주)

| 작업 | 설명 |
|------|------|
| 저항 시뮬레이션 | ArticulationBody Drive의 Stiffness/Damping으로 저항감 구현 |
| ROM 측정 | 관절 각도 실시간 측정 (Range of Motion) |
| 운동 강도 분석 | 손 속도, 악력, 반복 횟수 자동 기록 |
| GameFeel 연동 | 프리셋으로 물리 파라미터 일괄 전환 |

## 5. 기술적 과제

| 과제 | 난이도 | 해결 방안 |
|------|--------|-----------|
| OpenXR InputDataProvider 작성 | 중 | XR Hands API 관절 데이터 -> HPTK 19-bone 매핑 |
| ArticulationBody 성능 | 중 | 양손 38개 관절 -> FakeColliders 옵션으로 경량화 |
| 기존 HandBounceResponder와 공존 | 하 | 점진적 교체 (Phase 1에서는 병행) |
| Samsung Galaxy XR 호환 | 중 | XR_EXT_hand_tracking 확장 사용 |

## 6. 재활 운동 적용 시나리오

### 시나리오 A: 악력 재활

1. 봉지를 HPTK Grasp로 잡음
2. Drive Stiffness로 저항감 조절 (쉬움 -> 어려움)
3. isGrasped 유지 시간 측정
4. 반복 횟수 + 힘 데이터 CSV 기록

### 시나리오 B: 손가락 ROM 재활

1. 각 손가락 ArticulationBody 관절 각도 실시간 측정
2. 목표 각도까지 구부리기/펴기 운동
3. 진행도 시각화 (GazeFrequencyUI 패턴 재사용)

### 시나리오 C: 손 민첩성 훈련

1. 봉지를 다양한 속도/방향으로 쳐서 맞추기
2. ContactDetection으로 접촉 위치/힘 분석
3. 반응 시간 + 정확도 측정

## 7. 파일 구조 매핑

| HPTK-Plus 원본 | Exergame 적용 위치 |
|----------------|-------------------|
| Runtime/Physics/Pheasy.cs | Assets/Runtime/Physics/Pheasy.cs |
| Runtime/Physics/ArticulationBodyFollower.cs | Assets/Runtime/Physics/ArticulationBodyFollower.cs |
| Runtime/Modules/Part/ContactDetection/ | Assets/Runtime/Interaction/ContactDetection/ |
| Runtime/Modules/Hand/GestureDetection/ | Assets/Runtime/Interaction/GestureDetection/ |
| Runtime/Input/InputDataProvider.cs | Assets/Runtime/Input/OpenXRInputDataProvider.cs (신규) |
| Runtime/Physics/CustomJointDrive.cs | Assets/Runtime/Physics/CustomJointDrive.cs |

## 8. 결론

HPTK-Plus는 현재 Exergame의 손 물리 인터랙션 문제(관통, 불안정한 충돌, 단일 포인트 감지)를 근본적으로 해결할 수 있습니다. ArticulationBody 기반 물리 시스템은 Unity 6과 완벽 호환되며, MVC 아키텍처로 기존 코드와 점진적 통합이 가능합니다. Samsung Galaxy XR용 OpenXR InputDataProvider만 새로 작성하면 나머지 시스템은 그대로 활용 가능합니다.
