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
    [Tooltip("시선으로 감지할 오브젝트들의 레이어를 선택해주세요.")]
    public LayerMask targetLayer;

    [Header("거리 기반 감소 설정")]
    [Tooltip("손이 닿는 거리 (이 거리 안에서 감소 시작)")]
    public float armReachDistance = 1.0f;
    [Tooltip("시선이 대상에 없을 때 감소 속도 (초당 %)")]
    public float decayRateAtArmReach = 1.5f;
    [Tooltip("시선이 대상에 있을 때 회복 속도 (초당 %)")]
    public float recoveryRate = 5f;

    [Header("UI 위치 설정")]
    [Tooltip("눈 높이 기준 아래 오프셋 (미터)")]
    public float uiDownOffset = 0.30f;

    private float _displayPercentage = 100f;
    private GameObject lastHitGazeTarget = null;
    private Camera _mainCamera;
    private Canvas _uiCanvas;

    void Start()
    {
        objectNameText.text = "";
        percentageText.text = "100%";
        progressBarFill.fillAmount = 1f;
        _displayPercentage = 100f;
        _mainCamera = Camera.main;

        // % 바 UI의 Canvas를 찾아서 위치 조정용으로 저장
        if (progressBarFill != null)
            _uiCanvas = progressBarFill.GetComponentInParent<Canvas>();
    }

    void LateUpdate()
    {
        // % 바 UI를 카메라 기준 눈 높이 - 30cm 위치에 고정
        if (_uiCanvas != null && _mainCamera != null)
        {
            Transform cam = _mainCamera.transform;
            Vector3 targetPos = cam.position
                + cam.forward * 0.6f         // 앞쪽 60cm
                - cam.up * uiDownOffset;     // 아래 30cm
            _uiCanvas.transform.position = Vector3.Lerp(
                _uiCanvas.transform.position, targetPos, Time.deltaTime * 3f);
            _uiCanvas.transform.rotation = Quaternion.Slerp(
                _uiCanvas.transform.rotation, cam.rotation, Time.deltaTime * 3f);
        }
    }

    void Update()
    {
        bool isHit = false;
        float hitDistance = float.MaxValue;
        RaycastHit hit;

        if (Physics.Raycast(transform.position, transform.forward, out hit, float.MaxValue, targetLayer))
        {
            isHit = true;
            hitDistance = hit.distance;
            GameObject currentHitObject = hit.collider.gameObject;

            if (currentHitObject != lastHitGazeTarget)
            {
                objectNameText.text = currentHitObject.name;
                lastHitGazeTarget = currentHitObject;
            }
        }

        // 카메라에서 대상까지의 거리 계산 (시선이 안 맞으면 마지막 대상 위치 사용)
        float distanceToTarget = hitDistance;
        if (!isHit && lastHitGazeTarget != null)
        {
            distanceToTarget = Vector3.Distance(
                _mainCamera != null ? _mainCamera.transform.position : transform.position,
                lastHitGazeTarget.transform.position);
        }

        // 거리 비율 계산: 0 = 손 닿는 거리, 1+ = 멀리
        float distanceRatio = Mathf.Max(0f, distanceToTarget / armReachDistance);

        if (isHit)
        {
            // 시선이 대상에 있을 때: 점수 회복 (가까울수록 더 빠르게)
            float proximityBonus = 1f + Mathf.Max(0f, 1f - distanceRatio) * 2f;
            _displayPercentage += recoveryRate * proximityBonus * Time.deltaTime;
        }
        else
        {
            // 시선이 대상에 없을 때: 거리에 따라 다른 감소 속도
            // 멀수록 매우 느리게, 가까울수록 감소 시작
            float distanceMultiplier;
            if (distanceRatio > 2f)
            {
                // 팔 거리의 2배 이상 → 거의 안 떨어짐
                distanceMultiplier = 0.1f;
            }
            else if (distanceRatio > 1f)
            {
                // 팔 거리 ~ 2배 → 매우 느리게
                distanceMultiplier = Mathf.Lerp(0.5f, 0.1f, distanceRatio - 1f);
            }
            else
            {
                // 팔 거리 이내 → 감소 시작 (기존 대비 50% 느림)
                distanceMultiplier = Mathf.Lerp(1f, 0.5f, 1f - distanceRatio);
            }

            _displayPercentage -= decayRateAtArmReach * distanceMultiplier * Time.deltaTime;
        }

        _displayPercentage = Mathf.Clamp(_displayPercentage, 0f, 100f);

        float fill = Mathf.Clamp01(_displayPercentage / 100f);
        float mappedFill = 0.1f + fill * 0.9f;

        progressBarFill.fillAmount = mappedFill;
        percentageText.text = $"{Mathf.RoundToInt(_displayPercentage)}%";
    }
}
