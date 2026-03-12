// DisableRayInteractors.cs - 손에서 나오는 광선(Ray)과 막대기를 완전히 비활성화하는 스크립트
// XR Origin에 붙이면 시작 시 Near-Far Interactor 게임오브젝트 자체를 꺼버립니다.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DisableRayInteractors : MonoBehaviour
{
    void Start()
    {
        // 약간 딜레이 후 실행 (XR 초기화 완료 대기)
        Invoke(nameof(DisableAll), 0.5f);
    }

    private void DisableAll()
    {
        // NearFarInteractor가 붙은 게임오브젝트 자체를 비활성화
        var interactors = GetComponentsInChildren<NearFarInteractor>(true);
        foreach (var interactor in interactors)
        {
            interactor.gameObject.SetActive(false);
            Debug.Log($"[DisableRay] {interactor.gameObject.name}: 게임오브젝트 비활성화");
        }

        // 혹시 남은 LineRenderer도 전부 끄기
        var lineRenderers = GetComponentsInChildren<LineRenderer>(true);
        foreach (var lr in lineRenderers)
        {
            lr.enabled = false;
            Debug.Log($"[DisableRay] {lr.gameObject.name}: LineRenderer 비활성화");
        }

        Debug.Log($"[DisableRay] NearFar: {interactors.Length}개 비활성화, LineRenderer: {lineRenderers.Length}개 비활성화");
    }
}
