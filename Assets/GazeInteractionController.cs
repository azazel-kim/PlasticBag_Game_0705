using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GazeInteractionController : MonoBehaviour
{
    [Header("오브젝트 이름 표시 UI")]
    public TextMeshProUGUI objectNameText;

    [Header("시선 빈도 표시 UI")]
    public Image progressBarFill;
    public TextMeshProUGUI percentageText;

    [Header("시선 측정 설정")]
    public float calculationInterval = 10f;
    
    // [수정 1] LayerMask 변수 추가
    [Tooltip("시선으로 감지할 오브젝트들의 레이어를 선택해주세요.")]
    public LayerMask targetLayer;

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
        // [수정 2] Physics.Raycast에 LayerMask를 추가하여 오직 해당 레이어만 감지하도록 함
        // float.MaxValue는 사정거리를 무한대로 설정하는 것을 의미합니다.
        if (Physics.Raycast(transform.position, transform.forward, out hit, float.MaxValue, targetLayer))
        {
            GameObject currentHitObject = hit.collider.gameObject;

            // 이제 여기에 들어오는 오브젝트는 무조건 GazeTargetLayer에 속한 것들이므로,
            // CompareTag를 굳이 다시 할 필요가 없습니다. (하지만 안전을 위해 남겨두는 것도 좋습니다)
            if (currentHitObject != lastHitGazeTarget)
            {
                objectNameText.text = currentHitObject.name;
                lastHitGazeTarget = currentHitObject;
            }
            
            // 시선 시간 누적
            gazeDurationOnTarget += Time.deltaTime;
        }
        
        // 10초 주기 업데이트 로직은 그대로 유지
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
}