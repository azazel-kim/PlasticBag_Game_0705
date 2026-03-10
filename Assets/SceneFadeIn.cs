// SceneFadeIn.cs - 씬 로드 시 검은 화면에서 밝아지는 Fade In 효과
// 씬 2(3-1_PlasticBagPlay_with_Analyze)에 배치하여 사용

using UnityEngine;
using System.Collections;

public class SceneFadeIn : MonoBehaviour
{
    [Header("페이드 효과")]
    [Tooltip("Fade Canvas의 CanvasGroup을 할당하세요")]
    public CanvasGroup fadeCanvasGroup;

    [Tooltip("밝아지는 데 걸리는 시간 (초)")]
    public float fadeInDuration = 0.5f;

    [Tooltip("씬 시작 후 Fade In 시작까지 대기 시간 (초)")]
    public float delayBeforeFadeIn = 0.1f;

    void Start()
    {
        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning("[SceneFadeIn] fadeCanvasGroup이 할당되지 않았습니다.");
            return;
        }

        // 시작 시 완전히 검은 상태
        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.blocksRaycasts = true;

        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        // 씬 로드 직후 잠깐 대기 (렌더링 안정화)
        if (delayBeforeFadeIn > 0f)
        {
            yield return new WaitForSeconds(delayBeforeFadeIn);
        }

        Debug.Log("[SceneFadeIn] 페이드 인 시작.");

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;

        Debug.Log("[SceneFadeIn] 페이드 인 완료.");
    }
}
