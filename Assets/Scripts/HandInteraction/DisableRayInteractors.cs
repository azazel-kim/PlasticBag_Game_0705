// DisableRayInteractors.cs - 손에서 나오는 광선(Ray)을 비활성화하는 스크립트
// XR Origin에 붙이면 시작 시 NearFarInteractor와 관련 Line Visual을 꺼줍니다.
// 프리팹을 직접 수정하지 않아 샘플 업데이트와 충돌하지 않습니다.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

public class DisableRayInteractors : MonoBehaviour
{
    [Tooltip("NearFarInteractor의 Far 기능만 끌지, 컴포넌트 전체를 끌지 선택")]
    public bool disableEntireComponent = false;

    void Start()
    {
        DisableAllNearFarInteractors();
    }

    private void DisableAllNearFarInteractors()
    {
        // 이 오브젝트 하위의 모든 NearFarInteractor 검색
        var interactors = GetComponentsInChildren<NearFarInteractor>(true);

        foreach (var interactor in interactors)
        {
            if (disableEntireComponent)
            {
                // 컴포넌트 전체 비활성화
                interactor.enabled = false;
                Debug.Log($"[DisableRay] {interactor.gameObject.name}: NearFarInteractor 비활성화");
            }
            else
            {
                // Far Interaction만 비활성화 (Near/Poke는 유지)
                interactor.enableFarCasting = false;
                Debug.Log($"[DisableRay] {interactor.gameObject.name}: Far Casting 비활성화");
            }

            // Line Visual (광선 시각화) 비활성화
            var lineVisual = interactor.GetComponent<XRInteractorLineVisual>();
            if (lineVisual != null)
            {
                lineVisual.enabled = false;
                Debug.Log($"[DisableRay] {interactor.gameObject.name}: LineVisual 비활성화");
            }

            // LineRenderer도 비활성화
            var lineRenderer = interactor.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
                Debug.Log($"[DisableRay] {interactor.gameObject.name}: LineRenderer 비활성화");
            }
        }

        if (interactors.Length == 0)
            Debug.Log("[DisableRay] NearFarInteractor를 찾지 못했습니다.");
        else
            Debug.Log($"[DisableRay] 총 {interactors.Length}개 광선 비활성화 완료");
    }
}
