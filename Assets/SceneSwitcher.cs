// SceneSwitcher.cs - OVRScreenFade를 사용하는 Passthrough 최종 버전

using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    [Header("회전 설정")]
    public float rotationSpeed = 150f;

    [Header("오디오 볼륨 (0.0 ~ 1.0)")]
    [Range(0, 1)] public float rotationVolume = 0.3f;
    [Range(0, 1)] public float welcomeVolume = 0.8f;
    [Range(0, 1)] public float touchVolume = 1.0f;

    [Header("오디오 클립 (오디오 파일)")]
    public AudioClip rotationLoopSound;
    public AudioClip welcomeVoiceSound;
    public AudioClip touchSound;

    [Header("오디오 소스 (인스펙터에서 할당)")]
    public AudioSource rotationAudioSource;
    public AudioSource oneShotAudioSource;

    // --- 여기가 바뀌었습니다! ---
    [Header("페이드 효과 (OVR)")]
    public OVRScreenFade screenFader; // OVRScreenFade 컴포넌트를 연결할 슬롯

    [Header("씬 설정")]
    public string sceneToLoad = "PlasticBagPlay";

    private bool isInteractable = true;
    private Coroutine welcomeVoiceCoroutine;

    void Start()
    {
        // 씬이 시작될 때 자동으로 밝아지는 것은 OVRScreenFade가 담당합니다.
        // (단, 이전 씬에서 어두워진 상태로 넘어왔을 경우)

        if (rotationAudioSource == null || oneShotAudioSource == null) { /*...*/ return; }
        rotationAudioSource.volume = rotationVolume;
        if (rotationLoopSound != null) { /*...*/ }
        if (welcomeVoiceSound != null) { welcomeVoiceCoroutine = StartCoroutine(PlayWelcomeVoiceRepeatedly()); }
    }

    void Update()
    {
        if (isInteractable) { transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime); }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isInteractable) return;
        isInteractable = false;

        // 모든 소리와 움직임을 멈춥니다.
        if (rotationAudioSource != null) rotationAudioSource.Stop();
        if (welcomeVoiceCoroutine != null) StopCoroutine(welcomeVoiceCoroutine);

        // 터치 소리를 재생합니다.
        if (oneShotAudioSource != null && touchSound != null)
        {
            oneShotAudioSource.PlayOneShot(touchSound, touchVolume);
        }

        // 페이드 아웃 및 씬 전환 코루틴을 시작합니다.
        StartCoroutine(FadeAndLoadScene());
    }

    IEnumerator PlayWelcomeVoiceRepeatedly()
    {
        yield return new WaitForSeconds(0.5f);
        while (true)
        {
            oneShotAudioSource.PlayOneShot(welcomeVoiceSound, welcomeVolume);
            yield return new WaitForSeconds(welcomeVoiceSound.length);
        }
    }

    IEnumerator FadeAndLoadScene()
    {
        // 1. screenFader가 할당되었는지 확인합니다.
        if (screenFader == null)
        {
            Debug.LogError("OVRScreenFade가 할당되지 않았습니다! 씬을 즉시 로드합니다.");
            SceneManager.LoadScene(sceneToLoad);
            yield break; // 코루틴 종료
        }

        // 2. 화면을 어둡게 합니다.
        Debug.Log("페이드 아웃 시작.");
        screenFader.FadeOut();

        // 3. 페이드 아웃이 끝날 때까지 기다립니다. (screenFader의 fadeTime 만큼)
        yield return new WaitForSeconds(screenFader.fadeTime);

        // 4. 씬을 로드합니다.
        Debug.Log("페이드 아웃 완료. 씬 로드: " + sceneToLoad);
        SceneManager.LoadScene(sceneToLoad);
    }

    // (Start 함수의 나머지 부분과 다른 코루틴들은 이전과 동일하게 유지됩니다)
    // void Start() { ... }
    // void Update() { ... }
    // IEnumerator PlayWelcomeVoiceRepeatedly() { ... }
}