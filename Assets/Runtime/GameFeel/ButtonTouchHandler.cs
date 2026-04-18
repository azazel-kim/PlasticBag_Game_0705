using UnityEngine;

/// <summary>
/// 3D 버튼에 붙이는 핸드 터치(Poke) 핸들러.
/// Poke Interactor가 Trigger 영역에 진입하면 action을 즉시 실행합니다.
/// 플라스틱백의 OnTriggerEnter와 동일한 감지 방식입니다.
/// </summary>
public class ButtonTouchHandler : MonoBehaviour
{
    // GameFeelDebugPanel에서 설정
    [HideInInspector] public System.Action action;
    [HideInInspector] public string label;

    // 중복 실행 방지 쿨타임
    private float _lastTriggerTime = -1f;
    private float _cooldown = 1.0f;

    // 시각 피드백
    private Renderer _renderer;
    private Color _originalColor;

    void Start()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            _originalColor = _renderer.material.GetColor("_BaseColor");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time - _lastTriggerTime < _cooldown) return;

        // Poke Interactor 또는 PlayerHand 태그 감지
        bool isHand = other.CompareTag("PlayerHand");
        if (!isHand && other.attachedRigidbody != null)
            isHand = other.attachedRigidbody.gameObject.CompareTag("PlayerHand");

        // Poke Interactor 이름으로도 감지 (태그가 없는 경우)
        if (!isHand)
            isHand = other.gameObject.name.Contains("Poke");

        if (!isHand) return;

        _lastTriggerTime = Time.time;

        // 시각 피드백: 잠깐 초록색
        if (_renderer != null)
        {
            _renderer.material.SetColor("_BaseColor", new Color(0.1f, 0.9f, 0.3f, 1f));
            _renderer.material.SetColor("_Color", new Color(0.1f, 0.9f, 0.3f, 1f));
        }

        Debug.Log($"[ButtonTouch] Touched: {label} by {other.gameObject.name}");
        action?.Invoke();

        // 0.3초 후 원래 색상 복원
        Invoke(nameof(ResetColor), 0.3f);
    }

    private void ResetColor()
    {
        if (_renderer != null)
        {
            _renderer.material.SetColor("_BaseColor", _originalColor);
            _renderer.material.SetColor("_Color", _originalColor);
        }
    }
}
