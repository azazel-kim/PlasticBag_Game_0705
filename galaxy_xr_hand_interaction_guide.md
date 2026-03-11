# Galaxy XR Hand Interaction - 완벽 가이드

> **목표**: Unity 6에서 삼성 갤럭시 XR을 사용하여 손 감지로 오브젝트와 충돌 시 다음 씬으로 전환하기

---

## 📋 목차

1. [필수 준비물](#필수-준비물)
2. [Unity 프로젝트 설정](#unity-프로젝트-설정)
3. [XR 패키지 설치](#xr-패키지-설치)
4. [손 추적 설정](#손-추적-설정)
5. [충돌 감지 오브젝트 생성](#충돌-감지-오브젝트-생성)
6. [씬 전환 스크립트 작성](#씬-전환-스크립트-작성)
7. [빌드 설정](#빌드-설정)
8. [테스트 및 배포](#테스트-및-배포)
9. [문제 해결](#문제-해결)

---

## 필수 준비물

### 하드웨어
- ✅ 삼성 갤럭시 XR 헤드셋
- ✅ 개발용 PC (Windows 10/11 권장)
- ✅ USB-C 케이블 (디바이스 연결용)

### 소프트웨어
- ✅ Unity 6 (2023.3 이상)
- ✅ Android Studio (최신 버전)
- ✅ JDK 17 이상
- ✅ Android SDK (API Level 29 이상)

### 지식 수준
- 🟢 Unity 기본 UI 사용법
- 🟢 간단한 오브젝트 배치 경험
- 🟡 C# 기본 문법 (복사/붙여넣기 가능)

---

## Unity 프로젝트 설정

### Step 1: 새 프로젝트 생성

1. **Unity Hub 실행**
2. **"New project" 클릭**
3. **템플릿 선택**:
   - `3D (URP)` 템플릿 선택
   - 또는 `VR` 템플릿 선택 (XR 설정이 미리 되어 있음)
4. **프로젝트 이름**: `GalaxyXR_HandInteraction`
5. **위치 선택** 후 **"Create project"** 클릭

### Step 2: 프로젝트 설정 변경

1. **Edit → Project Settings** 메뉴 열기
2. **Player** 섹션 선택
3. **Other Settings** 확장
4. 다음 설정 변경:
   - **Color Space**: `Linear`
   - **Graphics API**: `Vulkan` (최상단에 위치하도록)
   - **Minimum API Level**: `Android 10.0 (API Level 29)`
   - **Target API Level**: `Automatic (highest installed)`
   - **Scripting Backend**: `IL2CPP`
   - **Target Architectures**: `ARM64` 체크

5. **XR Plug-in Management** 섹션 선택
   - Android 탭 클릭
   - **OpenXR** 체크

---

## XR 패키지 설치

### Step 3: Package Manager에서 패키지 설치

1. **Window → Package Manager** 열기
2. **좌측 상단 "+"** 클릭
3. **"Add package by name..."** 선택
4. 다음 패키지들을 하나씩 설치:

#### 필수 패키지 목록:

```
com.unity.xr.openxr
com.unity.xr.hands
com.unity.xr.interaction.toolkit
com.unity.xr.androidxr-openxr
```

#### 설치 방법 (각 패키지마다 반복):

1. Package Manager에서 **"+ → Add package by name..."**
2. 패키지 이름 입력 (예: `com.unity.xr.openxr`)
3. **"Add"** 클릭
4. 설치 완료 대기

### Step 4: XR Interaction Toolkit 샘플 설치

1. Package Manager에서 **"XR Interaction Toolkit"** 선택
2. **Samples** 탭 클릭
3. **"Starter Assets"** 찾아서 **Import** 클릭
4. **"Hands Interaction Demo"** 찾아서 **Import** 클릭

---

## 손 추적 설정

### Step 5: XR Origin 설정

1. **Hierarchy 창**에서 우클릭
2. **XR → XR Origin (Action-based)** 선택
3. 기본 Main Camera 삭제 (XR Origin이 자체 카메라 포함)

### Step 6: XR Hands 컴포넌트 추가

#### XR Origin 설정:

1. **Hierarchy**에서 **XR Origin** 선택
2. **Inspector**에서 **Add Component** 클릭
3. 다음 컴포넌트들 추가:
   - `XR Hands Subsystem Manager`
   - `Hand Tracking Events` (선택사항)

#### 손 Prefab 추가:

1. **Project 창**에서 다음 경로로 이동:
   ```
   Assets/Samples/XR Hands/[버전]/Hands Interaction Demo/Prefabs/
   ```

2. **Left Hand Interaction** Prefab을 **Hierarchy**의 **XR Origin** 하위로 드래그

3. **Right Hand Interaction** Prefab을 **XR Origin** 하위로 드래그

### Step 7: 손 Collider 설정

각 손 Prefab에 Collider 추가:

1. **Hierarchy**에서 **Left Hand Interaction** 선택
2. **Inspector**에서 **Add Component** 클릭
3. **Sphere Collider** 추가
4. Collider 설정:
   - **Is Trigger**: ✅ 체크
   - **Radius**: `0.05`
   - **Center**: `(0, 0, 0)`

5. **Rigidbody** 추가:
   - **Use Gravity**: ❌ 체크 해제
   - **Is Kinematic**: ✅ 체크

6. **Right Hand Interaction**에도 동일하게 반복

---

## 충돌 감지 오브젝트 생성

### Step 8: 씬 2개 생성

#### 첫 번째 씬 (현재 씬):

1. **File → Save As...** 클릭
2. 씬 이름: `Scene1_HandInteraction`
3. **Scenes** 폴더에 저장

#### 두 번째 씬 (전환될 씬):

1. **File → New Scene** 클릭
2. **Basic (Built-in)** 선택
3. **File → Save As...** 클릭
4. 씬 이름: `Scene2_Next`
5. **Scenes** 폴더에 저장

6. **Scene1_HandInteraction** 다시 열기 (더블클릭)

### Step 9: 충돌 감지용 트리거 오브젝트 생성

1. **Hierarchy**에서 우클릭 → **3D Object → Cube** 선택
2. 이름을 `TriggerCube`로 변경
3. **Inspector**에서 설정:
   - **Position**: `(0, 1.5, 2)` (플레이어 앞 2미터)
   - **Scale**: `(0.5, 0.5, 0.5)`

4. **Box Collider** 설정:
   - **Is Trigger**: ✅ 체크

5. 시각적으로 보이게 Material 추가:
   - **Project 창** 우클릭 → **Create → Material**
   - 이름: `TriggerMaterial`
   - **Albedo** 색상: 빨간색 또는 원하는 색
   - Material을 TriggerCube에 드래그

---

## 씬 전환 스크립트 작성

### Step 10: C# 스크립트 생성

1. **Project 창**에서 우클릭 → **Create → Folder**
2. 폴더 이름: `Scripts`
3. **Scripts 폴더** 우클릭 → **Create → C# Script**
4. 스크립트 이름: `HandTriggerSceneLoader`

### Step 11: 스크립트 작성

**HandTriggerSceneLoader.cs** 파일을 더블클릭하여 열고, 다음 코드를 **전체 복사/붙여넣기**:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 손이나 XR 컨트롤러가 트리거 영역에 진입하면 다음 씬을 로드합니다.
/// </summary>
public class HandTriggerSceneLoader : MonoBehaviour
{
    [Header("씬 전환 설정")]
    [Tooltip("로드할 씬의 이름을 입력하세요")]
    public string nextSceneName = "Scene2_Next";

    [Tooltip("씬 전환 지연 시간 (초)")]
    public float delayBeforeLoad = 0.5f;

    [Header("디버그")]
    [Tooltip("콘솔에 디버그 메시지 출력")]
    public bool showDebugMessages = true;

    private bool hasTriggered = false;

    /// <summary>
    /// 트리거 영역에 오브젝트가 진입했을 때 호출됩니다.
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        // 이미 트리거되었으면 무시 (중복 방지)
        if (hasTriggered) return;

        // 디버그 메시지 출력
        if (showDebugMessages)
        {
            Debug.Log($"트리거 감지: {other.gameObject.name}");
        }

        // 손 또는 XR 관련 오브젝트인지 확인
        if (IsHandOrXRObject(other))
        {
            hasTriggered = true;

            if (showDebugMessages)
            {
                Debug.Log($"씬 전환 시작: {nextSceneName}");
            }

            // 지연 후 씬 로드
            Invoke(nameof(LoadNextScene), delayBeforeLoad);
        }
    }

    /// <summary>
    /// 오브젝트가 손 또는 XR 관련 오브젝트인지 확인합니다.
    /// </summary>
    bool IsHandOrXRObject(Collider other)
    {
        // 이름으로 확인
        string objName = other.gameObject.name.ToLower();
        if (objName.Contains("hand") ||
            objName.Contains("controller") ||
            objName.Contains("xr"))
        {
            return true;
        }

        // 태그로 확인 (선택사항)
        if (other.CompareTag("Hand") ||
            other.CompareTag("Player") ||
            other.CompareTag("XR"))
        {
            return true;
        }

        // 부모 오브젝트 이름 확인
        Transform parent = other.transform.parent;
        if (parent != null)
        {
            string parentName = parent.name.ToLower();
            if (parentName.Contains("hand") ||
                parentName.Contains("xr origin"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 다음 씬을 로드합니다.
    /// </summary>
    void LoadNextScene()
    {
        // 씬 이름이 비어있는지 확인
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("씬 이름이 설정되지 않았습니다!");
            return;
        }

        // 씬이 빌드 설정에 포함되어 있는지 확인
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError($"씬 '{nextSceneName}'이(가) 빌드 설정에 없습니다!");
            Debug.LogError("File → Build Settings에서 씬을 추가하세요.");
        }
    }

    /// <summary>
    /// 에디터에서 트리거 영역을 시각화합니다.
    /// </summary>
    void OnDrawGizmos()
    {
        // 트리거가 활성화되지 않았을 때는 초록색
        Gizmos.color = hasTriggered ? Color.red : Color.green;

        // Collider가 있으면 그 크기대로 표시
        BoxCollider boxCol = GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCol.center, boxCol.size);
        }
    }
}
```

### Step 12: 스크립트를 오브젝트에 연결

1. **HandTriggerSceneLoader.cs** 스크립트를 **TriggerCube**에 드래그
2. **Inspector**에서 스크립트 설정 확인:
   - **Next Scene Name**: `Scene2_Next`
   - **Delay Before Load**: `0.5`
   - **Show Debug Messages**: ✅ 체크

---

## 빌드 설정

### Step 13: Build Settings에 씬 추가

1. **File → Build Settings** 열기
2. **"Add Open Scenes"** 클릭하여 현재 씬 추가
3. **Scene2_Next** 씬 열기 (더블클릭)
4. 다시 **File → Build Settings** 열기
5. **"Add Open Scenes"** 클릭

**결과**: 두 씬이 모두 "Scenes In Build" 목록에 표시됨

### Step 14: Android 빌드 설정

1. **Platform** 목록에서 **Android** 선택
2. **Switch Platform** 클릭 (시간이 걸릴 수 있음)
3. **Player Settings** 클릭
4. **Publishing Settings** → **Build** 섹션에서:
   - **Custom Main Manifest** 체크
   - **Custom Main Gradle Template** 체크

5. **AndroidManifest.xml** 열기:
   ```
   Assets/Plugins/Android/AndroidManifest.xml
   ```

6. `<manifest>` 태그 안에 다음 권한 추가:

```xml
<!-- 손 추적 권한 -->
<uses-permission android:name="com.oculus.permission.HAND_TRACKING" />
<uses-feature android:name="oculus.software.handtracking" android:required="false" />

<!-- XR 기능 -->
<uses-feature android:name="android.hardware.vr.headtracking" android:required="true" />
```

---

## 테스트 및 배포

### Step 15: Unity 에디터에서 테스트

1. **Scene1_HandInteraction** 씬 열기
2. **Play 버튼** 클릭
3. **Scene 뷰**에서 XR Origin과 TriggerCube 위치 확인
4. **Console 창** 열기 (Window → General → Console)
5. 테스트 시 콘솔 메시지 확인

> **참고**: 에디터에서는 손 추적이 시뮬레이션되지 않으므로, TriggerCube를 직접 손 오브젝트와 겹치게 이동시켜 테스트합니다.

### Step 16: 갤럭시 XR에 배포

#### 개발자 모드 활성화:

1. 갤럭시 XR 헤드셋 착용
2. **Settings → About** 메뉴로 이동
3. **Build Number** 7번 연속 탭
4. **Developer Options** 활성화
5. **USB Debugging** 활성화

#### 빌드 및 실행:

1. USB-C 케이블로 헤드셋을 PC에 연결
2. Unity에서 **File → Build Settings**
3. **Build And Run** 클릭
4. APK 저장 위치 선택
5. 빌드 완료 대기 (5-10분 소요)
6. 자동으로 헤드셋에 설치 및 실행

---

## 문제 해결

### 문제 1: 손이 감지되지 않음

**해결 방법**:
1. **Project Settings → XR Plug-in Management → OpenXR** 확인
2. **Hand Tracking Subsystem** 활성화 확인
3. AndroidManifest.xml에 손 추적 권한 추가 확인
4. 헤드셋에서 손 추적 기능 활성화 확인

### 문제 2: 씬이 전환되지 않음

**해결 방법**:
1. **Build Settings**에 두 씬이 모두 추가되어 있는지 확인
2. **Console 창**에서 에러 메시지 확인
3. 스크립트의 `nextSceneName`이 정확한지 확인 (대소문자 구분)
4. TriggerCube의 **Is Trigger** 체크 확인

### 문제 3: 충돌이 감지되지 않음

**해결 방법**:
1. 손 오브젝트에 **Collider**와 **Rigidbody** 있는지 확인
2. TriggerCube의 **Box Collider → Is Trigger** 체크 확인
3. **손 오브젝트의 Layer**와 **TriggerCube의 Layer** 충돌 설정 확인:
   - **Edit → Project Settings → Physics**
   - **Layer Collision Matrix** 확인

### 문제 4: 빌드 실패

**해결 방법**:
1. **JDK 경로** 확인:
   - **Edit → Preferences → External Tools**
   - JDK 경로가 올바른지 확인
2. **Android SDK** 경로 확인
3. **Gradle** 빌드 오류 시:
   - Unity 재시작
   - **Library** 폴더 삭제 후 재생성

---

## 추가 기능 아이디어

### 시각적 피드백 추가

트리거 오브젝트가 활성화될 때 색상 변경:

```csharp
void OnTriggerEnter(Collider other)
{
    if (IsHandOrXRObject(other))
    {
        // Material 색상 변경
        GetComponent<Renderer>().material.color = Color.green;

        hasTriggered = true;
        Invoke(nameof(LoadNextScene), delayBeforeLoad);
    }
}
```

### 사운드 효과 추가

트리거 시 소리 재생:

```csharp
[Header("오디오")]
public AudioClip triggerSound;
private AudioSource audioSource;

void Start()
{
    audioSource = gameObject.AddComponent<AudioSource>();
}

void OnTriggerEnter(Collider other)
{
    if (IsHandOrXRObject(other))
    {
        if (triggerSound != null)
        {
            audioSource.PlayOneShot(triggerSound);
        }

        hasTriggered = true;
        Invoke(nameof(LoadNextScene), delayBeforeLoad);
    }
}
```

### 페이드 효과 추가

씬 전환 시 부드러운 페이드 효과:

```csharp
using System.Collections;

IEnumerator FadeAndLoadScene()
{
    // 페이드 아웃 효과
    float fadeTime = 1f;
    float timer = 0;

    CanvasGroup fadeCanvas = FindObjectOfType<CanvasGroup>();

    while (timer < fadeTime)
    {
        timer += Time.deltaTime;
        fadeCanvas.alpha = Mathf.Lerp(0, 1, timer / fadeTime);
        yield return null;
    }

    SceneManager.LoadScene(nextSceneName);
}
```

---

## 참고 자료

### 공식 문서
- [Android XR for Unity - Android Developers](https://developer.android.com/develop/xr/unity)
- [Unity XR Hands Package](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.xr.hands.html)
- [Unity XR Interaction Toolkit](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.5/manual/index.html)
- [Hand Tracking - Unity OpenXR Android XR](https://docs.unity3d.com/Packages/com.unity.xr.androidxr-openxr@1.1/manual/features/hand-tracking.html)

### 튜토리얼
- [Creating an interactive VR scene with Android XR - Unity Learn](https://learn.unity.com/course/android-xr-with-unity/tutorial/creating-interactive-vr-scene)
- [Getting started with Unity and Android XR - Android Developers Blog](https://android-developers.googleblog.com/2025/10/getting-started-with-unity-and-android.html)

### 코드 예제
- [Unity XR Interaction Toolkit Examples - GitHub](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples)
- [Unity SceneManager API](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.html)

---

## 요약 체크리스트

완성까지 필요한 모든 단계:

- [ ] Unity 6 프로젝트 생성 (3D URP 템플릿)
- [ ] XR 패키지 4개 설치
- [ ] XR Origin 설정
- [ ] 손 Prefab 추가 및 Collider 설정
- [ ] 2개 씬 생성 (Scene1, Scene2)
- [ ] TriggerCube 오브젝트 생성
- [ ] HandTriggerSceneLoader.cs 스크립트 작성
- [ ] 스크립트를 TriggerCube에 연결
- [ ] Build Settings에 씬 추가
- [ ] AndroidManifest.xml 권한 추가
- [ ] 갤럭시 XR에 빌드 및 테스트

---

## 마치며

이 가이드를 완료하셨다면 축하합니다! 🎉

이제 Unity 6에서 삼성 갤럭시 XR의 손 추적 기능을 활용하여 인터랙티브한 경험을 만드는 기본기를 습득하셨습니다.

**다음 단계로 도전해보세요**:
- 여러 개의 트리거 오브젝트 추가
- 손 제스처 인식 추가
- UI 버튼과 상호작용
- 오브젝트 잡기 및 던지기 구현
- 멀티플레이어 기능 추가

궁금한 점이 있으면 Unity Learn 튜토리얼과 공식 문서를 참고하세요!
