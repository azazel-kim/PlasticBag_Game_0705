// SceneFadeManager.cs - 씬 전환 시 페이드 아웃/인 효과를 관리하는 전역 매니저
// DontDestroyOnLoad로 씬이 바뀌어도 유지되며, 새 씬 로드 후 자동으로 페이드 인 합니다.
// 사용법: 씬에 FadeCanvas(CanvasGroup 포함)를 배치하고 이 스크립트를 추가하세요.

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneFadeManager : MonoBehaviour
{
    public static SceneFadeManager Instance { get; private set; }

    [Header("페이드 설정")]
    [Tooltip("페이드 효과 시간 (초)")]
    public float fadeDuration = 0.5f;

    [Tooltip("씬 로드 후 페이드 인 시작 전 대기 시간 (초)")]
    public float fadeInDelay = 0.1f;

    private CanvasGroup _canvasGroup;
    private Canvas _canvas;

    void Awake()
    {
        // 싱글톤: 이미 있으면 새로 만든 것을 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _canvasGroup = GetComponent<CanvasGroup>();
        _canvas = GetComponent<Canvas>();

        if (_canvasGroup == null)
        {
            Debug.LogError("[SceneFade] CanvasGroup이 없습니다!");
            return;
        }

        // 씬 로드 이벤트 등록
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // 첫 씬에서 페이드 인 실행
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            StartCoroutine(FadeIn());
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 새 씬이 로드되면 자동으로 페이드 인
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Canvas의 카메라 참조 갱신 (Overlay 모드면 불필요)
        if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Overlay 모드는 카메라 필요 없음
        }

        StartCoroutine(FadeIn());
    }

    /// <summary>
    /// 페이드 아웃 후 씬 로드. 외부에서 호출하세요.
    /// </summary>
    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    /// <summary>
    /// 페이드 아웃 (화면이 점점 어두워짐)
    /// </summary>
    public IEnumerator FadeOut()
    {
        if (_canvasGroup == null) yield break;

        _canvasGroup.blocksRaycasts = true;
        float elapsed = 0f;
        float startAlpha = _canvasGroup.alpha;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / fadeDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// 페이드 인 (어두운 화면이 점점 밝아짐)
    /// </summary>
    public IEnumerator FadeIn()
    {
        if (_canvasGroup == null) yield break;

        // 씬 로드 직후 잠깐 대기 (렌더링 안정화)
        if (fadeInDelay > 0)
            yield return new WaitForSeconds(fadeInDelay);

        _canvasGroup.alpha = 1f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 페이드 아웃 → 씬 로드 (페이드 인은 OnSceneLoaded에서 자동 실행)
    /// </summary>
    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        yield return FadeOut();

        Debug.Log($"[SceneFade] 씬 로드: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}
