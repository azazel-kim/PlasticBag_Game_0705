---
description: "서브에이전트를 활용하여 기능을 구현하는 개발 커맨드. 기존 skills와 agents를 참고하여 클린 아키텍처 기반으로 코드를 작성합니다."
---

# /develop 커맨드

사용자가 요청한 기능을 서브에이전트를 활용하여 체계적으로 구현합니다.

## 실행 절차

### Step 1: Skills 참고
구현 전에 반드시 아래 파일들을 읽고 규칙을 따르세요:
- `.claude/skills/code.rules.md` — 코드 작성 규칙 (한국어 주석, 비전공자 친화적 설명 등)
- `.claude/agents/unity-senior-architect.md` — 클린 아키텍처 원칙, 코드 표준, 네이밍 컨벤션
- `.claude/agents/project-planner-director.md` — 프로젝트 컨텍스트 및 전략적 방향

### Step 2: 프로젝트 현황 파악
- `PlasticBag_Game_Project_Analysis.md` 파일을 읽어 현재 프로젝트 진행 상황을 파악하세요.
- 기존 코드베이스를 탐색하여 관련된 기존 시스템을 확인하세요.

### Step 3: 서브에이전트를 활용한 구현
요청된 기능의 성격에 따라 적절한 서브에이전트를 활용하세요:

1. **아키텍처 설계가 필요한 경우**: `unity-senior-architect` 에이전트를 Task 도구로 실행하여 클린 아키텍처 기반 설계를 먼저 수행
2. **프로젝트 방향 검토가 필요한 경우**: `project-planner-director` 에이전트를 Task 도구로 실행하여 전략적 맥락 확인
3. **코드 탐색이 필요한 경우**: Explore 에이전트를 사용하여 기존 코드베이스를 조사
4. **구현 계획이 필요한 경우**: Plan 에이전트를 사용하여 구현 전략 수립

### Step 4: 코드 구현
- skills의 코드 작성 규칙을 반드시 준수하세요
- 비전공자도 이해할 수 있는 한국어 주석을 포함하세요
- Unity 에셋의 메뉴나 properties 설정 방법도 주석으로 안내하세요
- 클린 아키텍처 레이어(Domain → Application → Infrastructure → Presentation)를 따르세요

### Step 5: 검증 및 리뷰
- 구현된 코드가 기존 시스템과 호환되는지 확인
- unity-senior-architect의 Quality Checklist로 자기 검증 수행
- 필요시 프로젝트 분석 문서(`PlasticBag_Game_Project_Analysis.md`) 업데이트

## 사용 예시

```
/develop eye-tracking 기반 UI 선택 시스템 구현해줘
/develop 뇌파 데이터 수신 모듈 만들어줘
/develop 게임 난이도 자동 조절 시스템 추가해줘
```

## 구현 시 반드시 참고할 사항

$ARGUMENTS
