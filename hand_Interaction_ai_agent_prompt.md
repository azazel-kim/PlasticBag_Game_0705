# AI 에이전트용 프롬프트

Unity 6 + 삼성 갤럭시 XR 손 추적 프로젝트를 위한 AI 에이전트 지시 프롬프트입니다.

---

## 🤖 기본 프로젝트 설정 프롬프트

```
Unity 6에서 삼성 갤럭시 XR용 손 추적 프로젝트를 생성해주세요.

요구사항:
1. 3D URP 템플릿 사용
2. 다음 패키지 설치:
   - com.unity.xr.openxr
   - com.unity.xr.hands
   - com.unity.xr.interaction.toolkit
   - com.unity.xr.androidxr-openxr
3. Android 빌드 설정:
   - Platform: Android
   - Graphics API: Vulkan
   - Minimum API Level: 29
   - Scripting Backend: IL2CPP
   - Target Architecture: ARM64
4. XR Plug-in Management에서 OpenXR 활성화

설정 완료 후 체크리스트를 제공해주세요.
```

---

## 🎯 손 추적 설정 프롬프트

```
Unity 프로젝트에 XR 손 추적 기능을 설정해주세요.

작업 내용:
1. XR Origin (Action-based) 생성
2. XR Hands Subsystem Manager 추가
3. Left Hand Interaction과 Right Hand Interaction Prefab 추가
4. 각 손에 다음 컴포넌트 추가:
   - Sphere Collider (Is Trigger: true, Radius: 0.05)
   - Rigidbody (Use Gravity: false, Is Kinematic: true)

완료 후 Hierarchy 구조를 보여주세요.
```

---

## 📝 C# 스크립트 생성 프롬프트

```
Unity에서 손 충돌 감지 시 씬을 전환하는 C# 스크립트를 작성해주세요.

기능 요구사항:
1. 손 또는 XR 오브젝트가 트리거 영역에 진입하면 씬 전환
2. 다음 기능 포함:
   - 씬 이름을 Inspector에서 설정 가능
   - 전환 전 지연 시간 설정 가능
   - 디버그 메시지 옵션
   - 중복 트리거 방지
   - 손/XR 오브젝트 자동 감지 (이름, 태그, 부모 오브젝트)
3. 에러 처리:
   - 씬이 빌드 설정에 없을 경우 경고
   - 씬 이름이 비어있을 경우 경고
4. 에디터에서 트리거 영역 시각화 (Gizmos 사용)

스크립트 이름: HandTriggerSceneLoader
네임스페이스: UnityEngine.SceneManagement 사용

주석을 한국어로 상세하게 작성해주세요.
```

---

## 🎨 트리거 오브젝트 생성 프롬프트

```
Unity 씬에 손 충돌 감지용 트리거 오브젝트를 생성해주세요.

설정:
1. 3D Cube 오브젝트 생성
2. 이름: TriggerCube
3. Transform:
   - Position: (0, 1.5, 2)
   - Scale: (0.5, 0.5, 0.5)
4. Box Collider:
   - Is Trigger: true
5. Material:
   - 빨간색 또는 눈에 띄는 색상
6. HandTriggerSceneLoader 스크립트 추가:
   - Next Scene Name: "Scene2_Next"
   - Delay Before Load: 0.5
   - Show Debug Messages: true

완료 후 Inspector 설정 스크린샷을 제공해주세요.
```

---

## 🏗️ 씬 구조 설정 프롬프트

```
Unity 프로젝트에 두 개의 씬을 생성하고 빌드 설정에 추가해주세요.

작업 내용:
1. 첫 번째 씬 (Scene1_HandInteraction):
   - XR Origin 포함
   - Left/Right Hand Interaction
   - TriggerCube (HandTriggerSceneLoader 스크립트 연결)
   - 바닥 Plane
   - 조명

2. 두 번째 씬 (Scene2_Next):
   - 기본 카메라
   - 텍스트 UI: "씬 전환 성공!"
   - 바닥 Plane
   - 조명

3. Build Settings:
   - 두 씬 모두 "Scenes In Build"에 추가
   - 순서: Scene1_HandInteraction (0), Scene2_Next (1)

완료 후 Build Settings 스크린샷을 제공해주세요.
```

---

## 📱 Android 빌드 설정 프롬프트

```
Unity 프로젝트를 삼성 갤럭시 XR용 Android APK로 빌드 설정해주세요.

작업 내용:
1. Platform을 Android로 전환
2. Player Settings:
   - Company Name: [회사명]
   - Product Name: GalaxyXR_HandInteraction
   - Package Name: com.[회사명].galaxyxr
   - Version: 1.0
   - Bundle Version Code: 1

3. Other Settings:
   - Color Space: Linear
   - Graphics API: Vulkan (최우선)
   - Minimum API Level: Android 10.0 (API 29)
   - Target API Level: Automatic
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64 only

4. Publishing Settings:
   - Create new Keystore (개발용)
   - Custom Main Manifest 활성화

5. AndroidManifest.xml에 손 추적 권한 추가:
   ```xml
   <uses-permission android:name="com.oculus.permission.HAND_TRACKING" />
   <uses-feature android:name="oculus.software.handtracking" android:required="false" />
   <uses-feature android:name="android.hardware.vr.headtracking" android:required="true" />
   ```

완료 후 빌드 준비 상태를 확인해주세요.
```

---

## 🐛 디버깅 지원 프롬프트

```
Unity XR 손 추적 프로젝트에서 다음 문제를 해결해주세요:

문제: [문제 설명]

확인 사항:
1. Console에 표시된 에러 메시지
2. 손 오브젝트의 Collider와 Rigidbody 설정
3. TriggerCube의 Is Trigger 설정
4. Build Settings에 씬 포함 여부
5. AndroidManifest.xml 권한 설정
6. XR Plug-in Management 활성화 상태
7. Layer Collision Matrix 설정

문제 진단 및 해결 방법을 단계별로 제시해주세요.
```

---

## 🎨 시각적 피드백 추가 프롬프트

```
HandTriggerSceneLoader 스크립트에 시각적 피드백을 추가해주세요.

기능 추가:
1. 트리거 진입 시 오브젝트 색상 변경:
   - 기본: 빨간색
   - 트리거 활성화: 초록색

2. 파티클 효과 추가:
   - 트리거 진입 시 파티클 시스템 재생

3. 스케일 애니메이션:
   - 트리거 진입 시 오브젝트가 커졌다 작아지는 효과

4. Inspector에서 설정 가능한 옵션:
   - 활성화 색상
   - 파티클 프리팹 참조
   - 애니메이션 지속 시간

기존 스크립트를 수정하고, 주석을 추가해주세요.
```

---

## 🔊 오디오 피드백 추가 프롬프트

```
HandTriggerSceneLoader 스크립트에 사운드 효과를 추가해주세요.

기능 추가:
1. 트리거 진입 시 사운드 재생
2. AudioSource 컴포넌트 자동 추가
3. Inspector에서 AudioClip 할당 가능
4. 볼륨 조절 옵션
5. 사운드 재생 후 씬 전환 (타이밍 조정)

오디오 없이도 작동하도록 null 체크 포함.
기존 스크립트에 통합하고, 주석을 추가해주세요.
```

---

## 🌟 페이드 효과 추가 프롬프트

```
씬 전환 시 부드러운 페이드 효과를 추가해주세요.

구현 방법:
1. Canvas와 Panel (검은색, 전체 화면) 생성
2. CanvasGroup 컴포넌트로 알파값 제어
3. Coroutine을 사용한 페이드 아웃/인 효과
4. 페이드 지속 시간 Inspector에서 설정 가능

작업 내용:
- SceneFadeManager.cs 스크립트 작성
- HandTriggerSceneLoader에 통합
- Scene1과 Scene2 모두에 적용

DontDestroyOnLoad를 사용하여 씬 전환 시에도 유지되도록 구현해주세요.
```

---

## 🧪 테스트 시나리오 생성 프롬프트

```
Unity XR 손 추적 프로젝트의 테스트 시나리오를 작성해주세요.

포함 내용:
1. Unity 에디터 내 테스트:
   - Play Mode에서 동작 확인
   - Console 메시지 확인
   - Gizmos 시각화 확인

2. 디바이스 테스트:
   - 갤럭시 XR에서 손 추적 정상 작동 확인
   - 트리거 오브젝트와 충돌 시 씬 전환 확인
   - 여러 번 반복 테스트

3. 엣지 케이스:
   - 손을 매우 빠르게 움직일 때
   - 양손 동시에 트리거 진입
   - 씬이 빌드 설정에 없을 때

각 테스트의 예상 결과와 실패 시 해결 방법을 포함해주세요.
```

---

## 📚 문서화 프롬프트

```
Unity XR 손 추적 프로젝트의 기술 문서를 작성해주세요.

문서 구조:
1. 프로젝트 개요
   - 목적 및 기능
   - 기술 스택
   - 타겟 플랫폼

2. 아키텍처
   - Hierarchy 구조
   - 스크립트 설명
   - 데이터 흐름

3. 설정 가이드
   - 환경 설정
   - 패키지 설치
   - 빌드 설정

4. 사용 방법
   - 씬 구성
   - 스크립트 사용법
   - 커스터마이징 방법

5. 트러블슈팅
   - 일반적인 문제
   - 해결 방법
   - FAQ

Markdown 형식으로 작성하고, 코드 예제와 스크린샷 위치 표시를 포함해주세요.
```

---

## 🔄 프로젝트 확장 프롬프트

```
기존 Unity XR 손 추적 프로젝트를 다음 기능으로 확장해주세요:

추가 기능:
1. 여러 개의 트리거 오브젝트:
   - 각각 다른 씬으로 전환
   - 색상으로 구분
   - 레벨 선택 UI

2. 손 제스처 인식:
   - 주먹 쥐기
   - 손가락 펴기
   - 특정 제스처로 씬 전환

3. 진행 상황 저장:
   - PlayerPrefs로 마지막 씬 저장
   - 재시작 시 이어하기 기능

4. UI 메뉴:
   - 씬 선택 메뉴
   - 설정 패널
   - 손으로 버튼 클릭

각 기능의 구현 방법과 필요한 스크립트를 제공해주세요.
```

---

## 🎯 최적화 프롬프트

```
Unity XR 손 추적 프로젝트의 성능을 최적화해주세요.

최적화 영역:
1. 스크립트 최적화:
   - Update 대신 이벤트 사용
   - 불필요한 GetComponent 제거
   - 오브젝트 풀링 적용

2. 렌더링 최적화:
   - LOD (Level of Detail) 설정
   - Occlusion Culling
   - Baked Lighting

3. 메모리 최적화:
   - 텍스처 압축
   - 메시 최적화
   - 에셋 번들

4. 프로파일링:
   - Unity Profiler로 병목 지점 확인
   - 프레임 드롭 원인 분석

각 최적화 항목의 적용 방법과 예상 성능 향상을 제시해주세요.
```

---

## 📦 완성 프로젝트 체크리스트 프롬프트

```
Unity XR 손 추적 프로젝트가 배포 준비되었는지 확인해주세요.

체크리스트:
✅ 기능 완성도
- [ ] 손 추적 정상 작동
- [ ] 씬 전환 정상 작동
- [ ] 모든 씬 빌드 설정에 포함
- [ ] 에러 없이 빌드 가능

✅ 성능
- [ ] 60 FPS 이상 유지
- [ ] 메모리 사용량 적정
- [ ] 배터리 소모 확인

✅ UX
- [ ] 직관적인 인터랙션
- [ ] 시각적 피드백 제공
- [ ] 사운드 피드백 제공

✅ 문서화
- [ ] README 작성
- [ ] 코드 주석 완료
- [ ] 사용 가이드 작성

✅ 테스트
- [ ] 에디터 테스트 완료
- [ ] 디바이스 테스트 완료
- [ ] 엣지 케이스 테스트 완료

각 항목의 상태를 확인하고, 미완료 항목의 해결 방법을 제시해주세요.
```

---

## 💡 사용 팁

### AI 에이전트에게 효과적으로 지시하는 방법:

1. **구체적으로 요청하기**:
   - ❌ "손 추적 만들어줘"
   - ✅ "Unity 6에서 XR Hands 패키지를 사용하여 손 추적을 설정하고, Sphere Collider를 추가해줘"

2. **단계별로 나누기**:
   - 복잡한 작업은 여러 프롬프트로 분할
   - 각 단계의 완료 확인 후 다음 단계 진행

3. **컨텍스트 제공하기**:
   - 프로젝트 상태 공유
   - 현재까지 완료된 작업 명시
   - 발생한 에러 메시지 포함

4. **검증 요청하기**:
   - 코드 작성 후 리뷰 요청
   - 설정 완료 후 체크리스트 확인
   - 예상되는 문제점 질문

5. **문서화 요청하기**:
   - 주석 한국어로 작성 요청
   - README 생성 요청
   - 사용 예제 포함 요청

---

## 🔗 관련 리소스

이 프롬프트들은 다음 공식 문서를 기반으로 작성되었습니다:

- [Android XR for Unity - Android Developers](https://developer.android.com/develop/xr/unity)
- [Unity XR Hands Package](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.1/manual/index.html)
- [Unity XR Interaction Toolkit](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.5/manual/index.html)
- [Unity SceneManager API](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.html)

---

## 📝 프롬프트 커스터마이징

프로젝트 요구사항에 맞게 프롬프트를 수정하여 사용하세요:

1. **[회사명]** → 실제 회사명으로 변경
2. **씬 이름** → 프로젝트에 맞는 씬 이름으로 변경
3. **기능 요구사항** → 추가하고 싶은 기능으로 변경
4. **에러 메시지** → 실제 발생한 에러로 변경

이 프롬프트들을 Claude, ChatGPT, Gemini 등 다양한 AI 에이전트에서 활용할 수 있습니다.
