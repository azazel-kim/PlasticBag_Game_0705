using UnityEngine;

/// <summary>
/// 공 양손 grab — **거리 기반 감지**로 Unity 물리 트리거 이벤트와 무관하게 동작합니다.
///
/// 핵심 설계:
///  - 손 루트(Left Hand/Right Hand Transform) ↔ 공 중심 거리를 매 FixedUpdate에서 측정
///  - `pinchGuardDistance` 이내: 공 즉시 isKinematic=true (물리 핀치 원천 차단)
///  - `grabDistance` 이내 & 양손: 공을 양손 중점으로 MovePosition
///  - 양손 모두 벗어나면 isKinematic=false → 중력 낙하 재개
///
/// 트리거 콜라이더 이벤트에 의존하지 않으므로 ball non-trigger 물리 collider가
/// grab trigger보다 크더라도 문제 없음.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BallTwoHandedGrab : MonoBehaviour
{
    [Header("거리 기반 감지 (손 루트 ↔ 공 중심)")]
    [Tooltip("이 거리 이내에 손이 들어오면 공 kinematic 전환 (물리 pinch 차단용)")]
    public float pinchGuardDistance = 0.23f;

    [Tooltip("이 거리 이내에 양손 모두 있으면 grab 시작")]
    public float grabDistance = 0.235f;

    [Header("잡힘 동작")]
    [Tooltip("양손 중점 이동 스무딩 (0=즉시, 높을수록 지연)")]
    [Range(0f, 0.9f)]
    public float grabSmoothing = 0.15f;

    [Header("디버그")]
    public bool verboseLog = false;

    private Rigidbody _rb;
    private Transform _leftHandRoot;
    private Transform _rightHandRoot;
    private bool _isGrabbed = false;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void EnsureHandRefs()
    {
        if (_leftHandRoot == null)
        {
            var go = GameObject.Find("Left Hand");
            if (go != null) _leftHandRoot = go.transform;
        }
        if (_rightHandRoot == null)
        {
            var go = GameObject.Find("Right Hand");
            if (go != null) _rightHandRoot = go.transform;
        }
    }

    void FixedUpdate()
    {
        EnsureHandRefs();
        if (_leftHandRoot == null || _rightHandRoot == null) return;

        Vector3 ballPos = transform.position;
        float leftDist = Vector3.Distance(ballPos, _leftHandRoot.position);
        float rightDist = Vector3.Distance(ballPos, _rightHandRoot.position);

        // 두 조건은 독립적으로 판정 (pinch < grab 또는 pinch > grab 어느 순서든 안전)
        bool anyInPinch = (leftDist < pinchGuardDistance) || (rightDist < pinchGuardDistance);
        bool bothInGrab = (leftDist < grabDistance) && (rightDist < grabDistance);
        bool needKinematic = anyInPinch || bothInGrab;

        // Kinematic 상태 관리
        if (needKinematic)
        {
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.isKinematic = true;
                if (verboseLog) Debug.Log($"[BallGrab] kinematic ON (left={leftDist:F2}, right={rightDist:F2}, pinch={anyInPinch}, grab={bothInGrab})");
            }
        }
        else
        {
            if (_rb != null && _rb.isKinematic)
            {
                _rb.isKinematic = false;
                if (verboseLog) Debug.Log("[BallGrab] release → dynamic");
            }
            _isGrabbed = false;
        }

        // Grab 이동: 양손 모두 grab 거리 안이면 중점으로 이동
        if (bothInGrab)
        {
            if (!_isGrabbed)
            {
                _isGrabbed = true;
                if (verboseLog) Debug.Log("[BallGrab] 양손 grab 시작");
            }
            Vector3 mid = (_leftHandRoot.position + _rightHandRoot.position) * 0.5f;
            Vector3 target = Vector3.Lerp(ballPos, mid, 1f - grabSmoothing);
            if (_rb != null && _rb.isKinematic)
                _rb.MovePosition(target);
            else
                transform.position = target;
        }
        else if (_isGrabbed)
        {
            _isGrabbed = false;
        }
        // else: anyInPinch만 있는 경우 → kinematic이므로 제자리 고정
    }
}
