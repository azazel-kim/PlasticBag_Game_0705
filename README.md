# PlasticBag Game - VR Exergame

Samsung Galaxy XR용 VR 비닐봉지 운동 게임 프로젝트입니다.  
손으로 비닐봉지를 쳐서 터뜨리는 인터랙션 기반의 운동 게임입니다.

## 프로젝트 개요

| 항목 | 내용 |
|------|------|
| Unity 버전 | 6000.1.17f1 (Unity 6) |
| 렌더 파이프라인 | URP 17.1.0 |
| 타겟 디바이스 | Samsung Galaxy XR (SM_I610) |
| 플랫폼 | Android XR |
| XR SDK | OpenXR + Google XR Extensions |
| Eye Tracking | OpenXR EyeGazeInteraction |
| 패키지명 | com.DXPLap.PlasticBagGame |
| 활성 브랜치 | samsung-xr |

## 게임 플레이

- 비닐봉지가 5초마다 생성되어 공중에 떠다님 (최대 10개)
- 손으로 터치하면 봉지가 바운스되며 색상이 변함
  - 1회: 노랑 → 2회: 녹색 → 3회: 청색 → 4회: 보라 → 5회: 주황 → 6회: 빨강
- 7회 터치 시 봉지가 터지며 점수 획득 + 사운드 + 팝 이펙트
- 바닥에 닿으면 봉지 소멸, 새 봉지 자동 생성
- 배경음악 4곡 랜덤 셔플 반복 재생 (Mixkit 로열티프리)

## 주요 기능

- **Hand Tracking**: 손 추적 기반 충돌 감지 (SphereCollider)
- **Eye Tracking**: OpenXR 시선 추적 + 시선 빈도 % 바 표시
- **점수 시스템**: 터치당 점수 획득, 화면 상단 Lazy Follow UI
- **색상 변화**: 터치 횟수별 실시간 머티리얼 색상 변경 (URP Alpha Blend)
- **물리 시뮬레이션**: 비닐봉지 바운스 (반발계수 기반), 감소 중력
- **Passthrough**: Android XR 패스스루 배경

## 씬 구성

| 씬 | 설명 |
|----|------|
| `1_Start_Scene_with_Analyze` | 시작 화면 |
| `3-1_PlasticBagPlay_with_Analyze` | 메인 게임 플레이 (현재 활성) |

## 핵심 스크립트

| 파일 | 역할 |
|------|------|
| `HandBounceResponder.cs` | 손 충돌 감지, 바운스, 색상 변경, 7회 팝 |
| `PlasticbagSpawner.cs` | 봉지 생성 관리 (5초 간격, 최대 10개) |
| `PlasticBag.cs` | 봉지 바닥 충돌 시 파괴 + 스포너 알림 |
| `ScoreManager.cs` | 점수 계산 및 UI 업데이트 |
| `ScoreFollowCamera.cs` | 점수 UI Lazy Follow (시야 고정) |
| `GazeInteractionController.cs` | 시선 추적 % 바 + UI 위치 관리 |
| `EyeTrackingRaycaster.cs` | OpenXR 시선 레이캐스트 |
| `BGMManager.cs` | 배경음악 랜덤 셔플 재생 |
| `Plasticbagsound.cs` | 충돌 강도별 사운드 재생 |
| `SceneSwitcher.cs` | CanvasGroup 기반 씬 전환 페이드 |

## 빌드 방법

1. Unity에서 프로젝트 열기
2. 메뉴 > `Build` > `Build Samsung XR APK`
3. APK가 `Builds/` 폴더에 생성됨
4. 헤드셋 설치: `adb install -r [APK파일경로]`

---

## 협업 가이드

### 필수 준비물

- Git 설치 (https://git-scm.com)
- Unity Hub + Unity 6000.1.17f1 (Unity 6)
- Unity 설치 시 모듈 추가: Android Build Support, Android XR
- (Samsung Galaxy XR 헤드셋이 있으면) USB 케이블 + ADB

### Step 1. 프로젝트 다운로드

```bash
git clone -b samsung-xr https://github.com/azazel-kim/PlasticBag_Game_0705.git
```

> `-b samsung-xr`을 반드시 붙여야 합니다. 없으면 구버전(main)이 받아집니다.

### Step 2. Unity에서 프로젝트 열기

- Unity Hub > Add > Add project from disk > clone한 폴더 선택
- 버전이 다르다고 표시되면 6000.1.17f1로 변경
- 처음 열 때 패키지 임포트에 5~10분 소요될 수 있음

### Step 3. 작업 시작

- samsung-xr 브랜치에 직접 push하지 마세요 (원본 작업자가 사용 중, Branch Protection 설정됨)
- 반드시 자신의 브랜치를 만들어서 작업하세요:

```bash
git checkout -b feature/새기능이름
```

### 작업 후 저장

```bash
git status                        # 변경된 파일 확인
git add 변경한파일들                # 파일 추가
git commit -m "작업 내용 설명"      # 커밋
git push                          # 원격 저장소에 업로드
```

### 주의사항

- **main 브랜치는 사용하지 마세요** (구버전 백업이며 samsung-xr과 병합 불가)
- **git push --force 절대 사용 금지**
- **samsung-xr 브랜치는 Branch Protection이 설정되어 있어 직접 push가 차단됩니다**
- 큰 작업 전에는 반드시 커밋해서 되돌릴 수 있게 해두세요
- 작업 시작 전 `git pull`로 최신 상태를 받아오세요
