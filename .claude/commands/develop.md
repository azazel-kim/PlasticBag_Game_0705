---
description: "서브에이전트를 활용하여 기능을 구현하는 개발 커맨드. skills와 agents를 참고하여 클린 아키텍처 기반으로 코드를 작성합니다."
---

# /develop 커맨드

사용자가 요청한 기능을 서브에이전트를 활용하여 체계적으로 구현합니다.

## 실행 절차

### Step 1: Skills 참고
구현 전에 반드시 아래 파일들을 읽고 규칙을 따르세요:
- `.claude/skills/code.rules.md` — 코드 작성 규칙 (한국어 주석, 비전공자 친화적 설명 등)
- `.claude/agents/xr-exergame-director.md` — 프로젝트 총괄 아키텍처 및 코드 표준

### Step 2: 프로젝트 현황 파악
- `.claude/agent_config.json` 파일을 읽어 현재 프로젝트 구성을 파악하세요.
- 기존 코드베이스를 탐색하여 관련된 기존 시스템을 확인하세요.

### Step 3: 서브에이전트를 활용한 구현
요청된 기능의 성격에 따라 적절한 서브에이전트를 활용하세요:

| 기능 영역 | 에이전트 | 커맨드 |
|-----------|---------|--------|
| 프로젝트 총괄/아키텍처 | xr-exergame-director | /director |
| XR 멀티플랫폼 통합 | xr-platform-integrator | /platform |
| Eye Tracking & Gaze | eye-tracking-specialist | /eye |
| 바디/핸드 트래킹 | body-tracking-exercise | /body |
| 물리/게임플레이 | gameplay-physics-engineer | /physics |
| 공간 UI/UX | xr-ux-engineer | /ux |
| 성능 최적화 | xr-performance-optimizer | /optimizer |
| 오디오/햅틱 | audio-haptics-engineer | /audio |
| 빌드/배포 | build-deploy-engineer | /build |
| LinkBand2 뇌파 BLE | linkband2-sensor-specialist | /linkband |
| 멀티센서 융합 | multi-sensor-fusion-engineer | /fusion |
| 운동 분석/난이도 | exergame-fitness-system | /exergame |
| Samsung XR 디바이스 | samsung-xr-device-specialist | /samsung |
| 카메라/X-Sens IMU | camera-motion-analyst | /camera |

### Step 4: 코드 구현
- **skills의 코드 작성 규칙을 반드시 준수하세요**
- 비전공자도 이해할 수 있는 한국어 주석을 포함하세요
- Unity 에셋의 메뉴나 properties 설정 방법도 주석으로 안내하세요
- 전문용어, 라이브러리, 키워드에 대한 설명 주석을 추가하세요

### Step 5: 검증 및 리뷰
- 구현된 코드가 기존 시스템과 호환되는지 확인
- xr-exergame-director의 Quality Checklist로 자기 검증 수행

## 사용 예시

```
/develop eye-tracking 기반 UI 선택 시스템 구현해줘
/develop 뇌파 데이터 수신 모듈 만들어줘
/develop 게임 난이도 자동 조절 시스템 추가해줘
/develop Samsung XR용 패스스루 설정 구현해줘
```

## 구현 시 반드시 참고할 사항

$ARGUMENTS
