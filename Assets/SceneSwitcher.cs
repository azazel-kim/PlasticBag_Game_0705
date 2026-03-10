// SceneSwitcher.cs - 플랫폼 독립적 Screen Fade + 씬 전환

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

    [Header("오디오 클립 (선택적 할당)")]
    public AudioClip rotationLoopSound;
    public AudioClip welcomeVoiceSound;
    public AudioClip touchSound;

    [Header("오디오 소스 (인스펙터에서 할당)")]
    public AudioSource rotationAudioSource;
    public AudioSource oneShotAudioSource;

    [Header("페이드 효과")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;

    [Header("씬 설정")]
    public string sceneToLoad = "PlasticBagPlay";

    private bool isInteractable = true;
    private Coroutine welcomeVoiceCoroutine;

    void Start()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        if (rotationAudioSource == null || oneShotAudioSource == null) return;
        rotationAudioSource.volume = rotationVolume;
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

        if (rotationAudioSource != null) rotationAudioSource.Stop();
        if (welcomeVoiceCoroutine != null) StopCoroutine(welcomeVoiceCoroutine);

        if (oneShotAudioSource != null && touchSound != null)
        {
            oneShotAudioSource.PlayOneShot(touchSound, touchVolume);
        }

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
        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning("fadeCanvasGroup이 할당되지 않았습니다. 바로 씬을 로드합니다.");
            SceneManager.LoadScene(sceneToLoad);
            yield break;
        }

        Debug.Log("페이드 아웃 시작.");
        fadeCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;

        Debug.Log("페이드 아웃 완료. 씬 로드: " + sceneToLoad);
        SceneManager.LoadScene(sceneToLoad);
    }
}