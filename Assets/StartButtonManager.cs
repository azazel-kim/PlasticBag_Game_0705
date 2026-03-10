using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
// using Oculus.Interaction; // PokeInteractable 직접 참조하지 않으므로 필요 없을 수 있습니다.

public class StartButtonManager : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 50f; // Y축 회전 속도 (Inspector에서 조절 가능)

    [Header("Audio Settings")]
    public AudioClip rotationLoopSound; // 계속 회전할 때 반복 재생될 소리 파일
    public AudioClip welcomeVoiceSound; // 5초 간격으로 나올 음성 파일
    public float welcomeVoiceInterval = 5f; // 음성 재생 간격 (초)
    public AudioClip touchSound; // 버튼 터치(트리거) 시 재생될 소리 파일

    // 필요한 AudioSource 컴포넌트 (Inspector에서 이 스크립트가 붙은 오브젝트의 AudioSource를 연결)
    [Header("Audio Sources (Assign in Inspector)")]
    public AudioSource rotationAudioSource; // 회전 소리 재생용 AudioSource
    public AudioSource oneShotAudioSource; // 음성 및 터치 소리 재생용 AudioSource (PlayOneShot 사용)

    [Header("Scene Settings")]
    public string sceneToLoad = "YourNextSceneName"; // 터치 후 이동할 씬 이름 (Build Settings에 추가되어 있어야 함)

    // PokeInteractable 컴포넌트 참조는 이제 필요 없습니다.
    // InteractableUnityEventWrapper가 이벤트를 전달해 줍니다.
    // [Header("Interaction Settings")]
    // public PokeInteractable pokeInteractable;

    private bool isRotating = true; // 현재 회전 중인지 상태 변수
    private Coroutine welcomeVoiceCoroutine; // 음성 재생 코루틴 참조

    void Start()
    {
        Debug.Log("StartButtonManager: Start() 함수 시작.");

        // PokeInteractable 이벤트 연결 코드는 이제 필요 없습니다.
        // InteractableUnityEventWrapper 컴포넌트에서 Inspector로 연결합니다.
        /*
        if (pokeInteractable == null)
        {
            pokeInteractable = GetComponent<PokeInteractable>();
        }

        if (pokeInteractable != null)
        {
            pokeInteractable.WhenSelect += OnButtonPoked;
            Debug.Log("StartButtonManager: PokeInteractable 이벤트 연결 완료.");
        }
        else
        {
            Debug.LogError("StartButtonManager: PokeInteractable 컴포넌트가 할당되지 않았거나 찾을 수 없습니다!");
        }
        */

        // AudioSource 컴포넌트가 제대로 연결되었는지 확인
        if (rotationAudioSource == null)
        {
            Debug.LogError("StartButtonManager: 'Rotation Audio Source'가 Inspector에 할당되지 않았습니다!");
        }
        if (oneShotAudioSource == null)
        {
            Debug.LogError("StartButtonManager: 'One Shot Audio Source'가 Inspector에 할당되지 않았습니다!");
        }

        // 기능 3a: 회전하는 소리 계속 재생
        if (rotationAudioSource != null && rotationLoopSound != null)
        {
            rotationAudioSource.clip = rotationLoopSound; // 재생할 소리 파일 지정
            rotationAudioSource.loop = true; // 반복 재생 설정
            rotationAudioSource.Play(); // 소리 재생 시작
            Debug.Log("StartButtonManager: 회전 소리 재생 시작.");
        }
        else
        {
            Debug.LogWarning("StartButtonManager: 회전 소리 AudioSource 또는 AudioClip이 할당되지 않아 회전 소리가 재생되지 않습니다.");
        }

        // 기능 3b: "어서오세요!" 음성 5초 간격 재생
        if (oneShotAudioSource != null && welcomeVoiceSound != null)
        {
            // 음성 재생 코루틴 시작
            welcomeVoiceCoroutine = StartCoroutine(PlayWelcomeVoiceRepeatedly());
            Debug.Log("StartButtonManager: 음성 재생 코루틴 시작.");
        }
        else
        {
            Debug.LogWarning("StartButtonManager: 음성 AudioSource 또는 AudioClip이 할당되지 않아 음성 메시지가 재생되지 않습니다.");
        }

        // 씬 로드 준비 상태 로그
        Debug.Log($"StartButtonManager: 터치 시 '{sceneToLoad}' 씬으로 이동할 준비 완료.");
        // 참고: 실제 씬이 Build Settings에 추가되었는지는 이 코드에서 직접 확인하지 않으므로 사용자가 직접 확인해야 합니다.
    }

    // OnDestroy 함수는 이제 필요 없습니다.
    /*
    void OnDestroy()
    {
        if (pokeInteractable != null)
        {
            pokeInteractable.WhenSelect -= OnButtonPoked;
            Debug.Log("StartButtonManager: PokeInteractable 이벤트 연결 해제.");
        }
    }
    */

    void Update()
    {
        // 기능 1: y 축으로 계속해서 회전
        if (isRotating) // isRotating이 true일 때만 회전
        {
            // Time.deltaTime을 곱하여 모든 컴퓨터에서 동일한 속도로 회전하게 함
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }

    // PokeInteractable의 Select 이벤트가 발생했을 때 InteractableUnityEventWrapper에 의해 호출될 함수
    // 이 함수는 이제 public이어야 하며, UnityEvent에 연결하기 위해 매개변수가 없어도 됩니다.
    public void OnButtonPoked() // 매개변수 제거
    {
        // 어떤 Interactor에 의해 선택되었는지 로그 출력은 InteractableUnityEventWrapper에서 직접 제공하지 않습니다.
        // 필요하다면 다른 방법으로 Interactor 정보를 얻어야 하지만, 여기서는 단순화합니다.
        Debug.Log($"StartButtonManager: 버튼이 Poked(선택) 감지됨.");


        // 기능 2a: 회전 멈춤
        if (isRotating) // 이미 멈춘 상태가 아니라면 한 번만 실행
        {
            isRotating = false;
            Debug.Log("StartButtonManager: 회전 중지.");

            // 회전 소리 멈춤
            if (rotationAudioSource != null && rotationAudioSource.isPlaying)
            {
                rotationAudioSource.Stop();
                Debug.Log("StartButtonManager: 회전 소리 중지.");
            }

            // 음성 코루틴 멈춤
            if (welcomeVoiceCoroutine != null)
            {
                StopCoroutine(welcomeVoiceCoroutine);
                Debug.Log("StartButtonManager: 음성 재생 코루틴 중지.");
            }

            // 기능 4: 터치할 때 소리 재생
            if (oneShotAudioSource != null && touchSound != null)
            {
                oneShotAudioSource.PlayOneShot(touchSound); // 터치 소리 한 번 재생
                Debug.Log("StartButtonManager: 터치 소리 재생.");

                // 터치 소리가 끝날 때까지 잠시 기다린 후 씬 이동
                // 소리 길이를 가져와서 그 시간만큼 대기
                StartCoroutine(LoadSceneAfterSound(touchSound.length));
            }
            else
            {
                Debug.LogWarning("StartButtonManager: 터치 소리 AudioSource 또는 AudioClip이 할당되지 않았습니다. 씬을 즉시 로드합니다.");
                // 터치 소리가 없으면 바로 씬 이동
                LoadTargetScene();
            }
        }
    }


    // 기능 3b 구현을 위한 코루틴: 지정된 간격으로 음성 재생
    IEnumerator PlayWelcomeVoiceRepeatedly()
    {
        // 게임 시작 후 바로 첫 음성 재생 (선택 사항: 시작 시 바로 재생하고 싶지 않으면 이 yield return 줄을 제거)
        yield return new WaitForSeconds(1.0f); // 예: 시작 후 1초 대기

        while (true) // 무한 반복 (StopCoroutine이 호출될 때까지)
        {
            if (oneShotAudioSource != null && welcomeVoiceSound != null)
            {
                oneShotAudioSource.PlayOneShot(welcomeVoiceSound); // 음성 한 번 재생
                Debug.Log("StartButtonManager: '어서오세요!' 음성 재생.");
            }
            // AudioSource나 AudioClip이 없으면 Start()에서 이미 경고했으므로 여기서 또 할 필요는 없음

            // 지정된 간격만큼 대기
            yield return new WaitForSeconds(welcomeVoiceInterval);
        }
    }

    // 터치 소리가 끝날 때까지 기다린 후 씬을 로드하는 코루틴
    IEnumerator LoadSceneAfterSound(float delay)
    {
        // delay 시간만큼 대기
        yield return new WaitForSeconds(delay);

        // 씬 로드 함수 호출
        LoadTargetScene();
    }

    // 씬 로드를 처리하는 함수
    void LoadTargetScene()
    {
        // 기능 2b: 지정 Scene으로 넘어감
        if (!string.IsNullOrEmpty(sceneToLoad)) // 씬 이름이 비어있지 않은지 확인
        {
            // 씬 로드
            SceneManager.LoadScene(sceneToLoad);
            Debug.Log($"StartButtonManager: 씬 '{sceneToLoad}' 로드 시작.");
        }
        else
        {
            Debug.LogError("StartButtonManager: 로드할 씬 이름이 Inspector에 설정되지 않았습니다!");
        }
    }
}