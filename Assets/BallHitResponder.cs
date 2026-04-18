using UnityEngine;

/// <summary>
/// 공은 생성 시 빨강 또는 파랑 중 하나가 랜덤하게 지정됩니다.
/// (이전의 7단계 히트 색상 변화는 제거)
/// </summary>
public class BallHitResponder : MonoBehaviour
{
    [Header("공 색상 (빨강/파랑 중 랜덤)")]
    [Tooltip("빨강 색")]
    public Color redColor = new Color(1.0f, 0.15f, 0.15f);

    [Tooltip("파랑 색")]
    public Color blueColor = new Color(0.2f, 0.4f, 1.0f);

    // 선택된 색상 (외부에서 읽기 — 점수 매칭 로직 등에 활용 가능)
    public Color AssignedColor { get; private set; }
    public bool IsRed { get; private set; }

    private Material _matInstance;

    void Start()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
            _matInstance = renderer.material;

        // 랜덤 색상 선택
        IsRed = Random.value < 0.5f;
        AssignedColor = IsRed ? redColor : blueColor;
        ApplyColor(AssignedColor);
    }

    private void ApplyColor(Color color)
    {
        if (_matInstance == null) return;
        _matInstance.SetColor("_BaseColor", color);  // URP Lit
        _matInstance.color = color;                   // 레거시 호환
    }
}
