using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// 공 잡기 — **공 트리거 콜라이더 진입 기반** 양손 감싸기.
///
/// 동작:
///  - 공에는 2개의 SphereCollider: 물리(non-trigger, 10cm) + 트리거(isTrigger, 12.5cm = 비주얼 표면)
///  - 손 조인트 콜라이더가 공 트리거 영역(=비주얼 안쪽)에 들어오면 "공 안에 있음"
///  - 양손 중 적어도 하나씩의 조인트가 **동시에** 공 트리거 내부면 grab 시작
///  - 공은 grab 중 dynamic 유지 + 스프링 힘으로 양손 최근접 점 중점으로 끌어당김
///  - 한 손이라도 나가면 release + 손 속도 이어받아 throw
///
/// 거리 기반보다 트래킹 편차에 강건: "실제로 안에 있느냐"로 판정하므로 왼/오른쪽 대칭성 확보.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BallTwoHandedGrab : MonoBehaviour
{
    [Header("스프링 잡기 (dynamic 유지)")]
    [Tooltip("양손 중점으로 끌어당기는 강성")]
    public float grabStiffness = 600f;

    [Tooltip("스프링 감쇠")]
    public float grabDamping = 40f;

    [Tooltip("release 시 공에 이어받을 손 속도 비율")]
    public float releaseVelocityScale = 1.0f;

    [Header("디버그")]
    public bool verboseLog = false;

    // ─── 외부 ───
    public bool IsGrabbed => _isGrabbed;

    // ─── 내부 ───
    private Rigidbody _rb;
    private bool _isGrabbed = false;
    private bool _savedUseGravity = true;
    private Vector3 _lastGrabPoint;
    private Vector3 _handVelocity;
    private float _diagLogTimer = 0f;

    // 트리거에 진입한 손 콜라이더 집합 (손 별로 구분)
    private readonly HashSet<Collider> _leftInside = new HashSet<Collider>();
    private readonly HashSet<Collider> _rightInside = new HashSet<Collider>();

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // 콜라이더가 왼손 / 오른손 / 해당 없음 판정
    private static int ClassifyHand(Collider other)
    {
        // 1) 이름 prefix로 판정 (가장 빠름)
        string n = other.gameObject.name;
        if (n.StartsWith("HandCol_Left_")) return 1;
        if (n.StartsWith("HandCol_Right_")) return 2;

        // 2) 부모 계층의 HandColliderSetup handedness로 판정
        var setup = other.GetComponentInParent<HandColliderSetup>();
        if (setup != null)
        {
            if (setup.handedness == Handedness.Left) return 1;
            if (setup.handedness == Handedness.Right) return 2;
        }

        // 3) Left Hand / Right Hand GameObject 하위 판정
        Transform t = other.transform;
        while (t != null)
        {
            if (t.name == "Left Hand") return 1;
            if (t.name == "Right Hand") return 2;
            t = t.parent;
        }
        return 0;
    }

    void OnTriggerEnter(Collider other)
    {
        int side = ClassifyHand(other);
        if (side == 1) _leftInside.Add(other);
        else if (side == 2) _rightInside.Add(other);
    }

    void OnTriggerExit(Collider other)
    {
        int side = ClassifyHand(other);
        if (side == 1) _leftInside.Remove(other);
        else if (side == 2) _rightInside.Remove(other);
    }

    // 손 측 콜라이더 중 공에서 가장 가까운 점의 월드 좌표 반환
    private Vector3 GetNearestPoint(HashSet<Collider> set, Vector3 ballPos, out bool any)
    {
        any = false;
        float minDist = float.MaxValue;
        Vector3 best = ballPos;
        foreach (var c in set)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;

            // [중요] c.bounds.center(손 콜라이더 내부 중심)가 아니라
            // c.ClosestPoint(공 쪽으로 가장 가까운 손 콜라이더 표면점)를 사용
            //  → 공이 손 내부로 파고들지 않고 손 표면과 맞물리는 지점에 안착
            //  → 스프링 힘 vs 물리 충돌 간 싸움 해소
            Vector3 p = c.ClosestPoint(ballPos);
            float d = Vector3.Distance(ballPos, p);
            if (d < minDist) { minDist = d; best = p; any = true; }
        }
        return best;
    }

    void FixedUpdate()
    {
        // null 제거 (콜라이더 파괴 등)
        _leftInside.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);
        _rightInside.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);

        bool bothHands = _leftInside.Count > 0 && _rightInside.Count > 0;

        if (verboseLog)
        {
            _diagLogTimer += Time.fixedDeltaTime;
            if (_diagLogTimer >= 1.0f)
            {
                _diagLogTimer = 0f;
                Debug.Log($"[BallGrab-DIAG] L_inside={_leftInside.Count} R_inside={_rightInside.Count} grabbed={_isGrabbed} vel={_rb.linearVelocity.magnitude:F2}");
            }
        }

        if (bothHands)
        {
            Vector3 ballPos = transform.position;
            Vector3 lp = GetNearestPoint(_leftInside, ballPos, out _);
            Vector3 rp = GetNearestPoint(_rightInside, ballPos, out _);
            Vector3 mid = (lp + rp) * 0.5f;
            if (!_isGrabbed) StartGrab(mid);
            else UpdateGrab(mid);
        }
        else
        {
            if (_isGrabbed) ReleaseGrab();
        }
    }

    private void StartGrab(Vector3 target)
    {
        _isGrabbed = true;
        _savedUseGravity = _rb.useGravity;
        _rb.useGravity = false;
        _lastGrabPoint = target;
        _handVelocity = Vector3.zero;
        if (verboseLog) Debug.Log($"[BallGrab] START at {target.ToString("F3")}");
    }

    private void UpdateGrab(Vector3 target)
    {
        float dt = Time.fixedDeltaTime;
        Vector3 ballPos = transform.position;

        if (dt > 0.0001f)
            _handVelocity = (target - _lastGrabPoint) / dt;
        _lastGrabPoint = target;

        Vector3 posError = target - ballPos;
        Vector3 force = posError * grabStiffness - _rb.linearVelocity * grabDamping;
        _rb.AddForce(force, ForceMode.Acceleration);
    }

    private void ReleaseGrab()
    {
        if (verboseLog) Debug.Log($"[BallGrab] RELEASE (handVel={_handVelocity.magnitude:F2} m/s)");
        _isGrabbed = false;
        if (_rb != null)
        {
            _rb.useGravity = _savedUseGravity;
            _rb.linearVelocity = _rb.linearVelocity + _handVelocity * (releaseVelocityScale - 1f);
        }
        _handVelocity = Vector3.zero;
    }
}
