using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoldGazeInteractionController : MonoBehaviour
{
    [Header("오브젝트 이름 표시 UI")]
    public TextMeshProUGUI objectNameText;

    [Header("시선 빈도 표시 UI")]
    public Image progressBarFill;
    public TextMeshProUGUI percentageText;

    [Header("시선 측정 설정")]
    public float calculationInterval = 10f;
    public LayerMask targetLayer;

    // [수정 1] SphereCast의 반지름(두께)을 설정할 변수 추가
    [Header("시선 감지 정확도 설정")]
    [Tooltip("시선 감지 영역의 반지름입니다. 움직이는 작은 타겟을 위해 0.1~0.2 정도로 설정하는 것을 추천합니다.")]
    public float gazeRadius = 0.1f;

    private float timer = 0f;
    private float gazeDurationOnTarget = 0f;
    private GameObject lastHitGazeTarget = null;

    void Start()
    {
        objectNameText.text = "";
        percentageText.text = "0%";
        progressBarFill.fillAmount = 0.1f;
    }

    void Update()
    {
        timer += Time.deltaTime;

        RaycastHit hit;
        
        // [수정 2] Physics.Raycast를 Physics.SphereCast로 교체
        // gazeRadius의 두께를 가진 구를 쏘아 감지 정확도를 높입니다.
        if (Physics.SphereCast(transform.position, gazeRadius, transform.forward, out hit, float.MaxValue, targetLayer))
        {
            GameObject currentHitObject = hit.collider.gameObject;

            if (currentHitObject != lastHitGazeTarget)
            {
                objectNameText.text = currentHitObject.name;
                lastHitGazeTarget = currentHitObject;
            }
            
            gazeDurationOnTarget += Time.deltaTime;
        }
        
        if (timer >= calculationInterval)
        {
            float percentage = (gazeDurationOnTarget / calculationInterval) * 100f;
            float fillValue = gazeDurationOnTarget / calculationInterval;
            float mappedFillValue = 0.1f + fillValue * 0.9f;

            progressBarFill.fillAmount = mappedFillValue;
            percentageText.text = $"{percentage:F0}%";

            timer = 0f;
            gazeDurationOnTarget = 0f;
        }
    }

    // (선택 사항) Scene 뷰에서 감지 영역을 시각적으로 확인하기 위한 Gizmo
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, gazeRadius);
    }
}