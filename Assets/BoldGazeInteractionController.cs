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
    [Tooltip("이 시간(초) 동안의 평균을 계산합니다")]
    public float calculationInterval = 3f;
    public LayerMask targetLayer;

    [Header("시선 감지 정확도 설정")]
    [Tooltip("시선 감지 영역의 반지름")]
    public float gazeRadius = 0.1f;

    // 슬라이딩 윈도우 방식: 매 프레임의 히트 여부를 기록
    private float[] _frameSamples;   // 1=히트, 0=미스
    private float[] _frameDeltas;    // 각 샘플의 deltaTime
    private int _sampleIndex = 0;
    private int _sampleCount = 0;
    private int _maxSamples = 300;   // 최대 ~5초분 (60fps 기준)

    private float _displayPercentage = 100f;
    private GameObject lastHitGazeTarget = null;

    void Start()
    {
        objectNameText.text = "";
        percentageText.text = "100%";
        progressBarFill.fillAmount = 1f;

        _frameSamples = new float[_maxSamples];
        _frameDeltas = new float[_maxSamples];
        _displayPercentage = 100f;
    }

    void Update()
    {
        bool isHit = false;
        RaycastHit hit;

        if (Physics.SphereCast(transform.position, gazeRadius, transform.forward, out hit, float.MaxValue, targetLayer))
        {
            isHit = true;
            GameObject currentHitObject = hit.collider.gameObject;

            if (currentHitObject != lastHitGazeTarget)
            {
                objectNameText.text = currentHitObject.name;
                lastHitGazeTarget = currentHitObject;
            }
        }

        // 링 버퍼에 현재 프레임 기록
        _frameSamples[_sampleIndex] = isHit ? 1f : 0f;
        _frameDeltas[_sampleIndex] = Time.deltaTime;
        _sampleIndex = (_sampleIndex + 1) % _maxSamples;
        if (_sampleCount < _maxSamples) _sampleCount++;

        // calculationInterval 이내의 샘플로 가중 평균 계산
        float totalTime = 0f;
        float hitTime = 0f;
        int idx = _sampleIndex - 1;

        for (int i = 0; i < _sampleCount; i++)
        {
            if (idx < 0) idx += _maxSamples;
            float dt = _frameDeltas[idx];

            if (totalTime + dt > calculationInterval)
            {
                // 남은 시간만큼만 사용
                float remaining = calculationInterval - totalTime;
                hitTime += _frameSamples[idx] * remaining;
                totalTime = calculationInterval;
                break;
            }

            hitTime += _frameSamples[idx] * dt;
            totalTime += dt;
            idx--;
        }

        // % 계산
        float targetPercentage = (totalTime > 0.01f) ? (hitTime / totalTime) * 100f : _displayPercentage;

        // 부드러운 전환
        _displayPercentage = Mathf.Lerp(_displayPercentage, targetPercentage, Time.deltaTime * 5f);

        float fill = Mathf.Clamp01(_displayPercentage / 100f);
        float mappedFill = 0.1f + fill * 0.9f;

        progressBarFill.fillAmount = mappedFill;
        percentageText.text = $"{Mathf.RoundToInt(_displayPercentage)}%";
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, gazeRadius);
    }
}
