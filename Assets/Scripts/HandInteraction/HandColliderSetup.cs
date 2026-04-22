using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

/// <summary>
/// 손의 19개 관절(HPTK 호환 범위)에 Trigger + Physics 콜라이더 두 세트를 배치합니다.
///
/// 하이브리드 구조:
///  - Trigger SphereCollider  (isTrigger=true)
///      → 기존 OnTriggerEnter 기반 Bounce/Sound 로직용 (HandBounceResponder, Plasticbagsound)
///  - Physics SphereCollider  (isTrigger=false)
///      → 봉지·벽을 실제로 밀어내 관통 방지 (XRIT 패턴 참고, Kinematic Rigidbody + MovePosition)
///  - Kinematic Rigidbody 1개 (공용)
///      → FixedUpdate에서 rb.MovePosition/MoveRotation으로 물리 반영
///
/// XR Origin > Camera Offset > Left Hand / Right Hand에 붙여주세요.
/// </summary>
public class HandColliderSetup : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("왼손이면 Left, 오른손이면 Right")]
    public Handedness handedness = Handedness.Left;

    [Header("Trigger 콜라이더 (터치 감지)")]
    [Tooltip("일반 관절 Trigger 반지름 (m) — 터치 감지용")]
    public float triggerRadius = 0.012f;

    [Tooltip("Palm/Wrist Trigger 반지름 (m) — 손바닥은 크게")]
    public float palmTriggerRadius = 0.035f;

    [Header("Physics 콜라이더 (봉지 밀어냄)")]
    [Tooltip("물리 레이어를 활성화할지. false이면 기존 Trigger만 동작")]
    public bool enablePhysicsColliders = true;

    [Tooltip("일반 관절 Physics 반지름 (m) — Trigger보다 작게 해서 Trigger 먼저 발동")]
    public float physicsRadius = 0.009f;

    [Tooltip("Palm/Wrist Physics 반지름 (m)")]
    public float palmPhysicsRadius = 0.03f;

    [Header("디버그 가시화 (19 콜라이더를 VR 내에서 보이게)")]
    [Tooltip("true이면 각 joint에 작은 구 mesh를 렌더링")]
    public bool showDebugVisuals = true;

    [Tooltip("가시화 구의 크기 배율 (1.0 = physics 반지름과 동일)")]
    public float debugVisualScale = 1.0f;

    [Tooltip("일반 joint 색상")]
    public Color debugJointColor = new Color(0f, 1f, 1f, 0.6f);

    [Tooltip("Palm/Wrist 색상")]
    public Color debugPalmColor = new Color(1f, 0.8f, 0f, 0.6f);

    // HPTK 호환 19-bone 중 실제 XR Hand에 존재하는 관절 19개 (synthesized forearm 제외).
    // 각 관절에서 Trigger + Physics 콜라이더 쌍을 생성합니다.
    private static readonly XRHandJointID[] TrackedJoints = new[]
    {
        // 루트
        XRHandJointID.Wrist,
        XRHandJointID.Palm,

        // Thumb 4개
        XRHandJointID.ThumbMetacarpal,
        XRHandJointID.ThumbProximal,
        XRHandJointID.ThumbDistal,
        XRHandJointID.ThumbTip,

        // Index 4개 (Proximal/Intermediate/Distal/Tip)
        XRHandJointID.IndexProximal,
        XRHandJointID.IndexIntermediate,
        XRHandJointID.IndexDistal,
        XRHandJointID.IndexTip,

        // Middle 4개
        XRHandJointID.MiddleProximal,
        XRHandJointID.MiddleIntermediate,
        XRHandJointID.MiddleDistal,
        XRHandJointID.MiddleTip,

        // Ring 4개
        XRHandJointID.RingProximal,
        XRHandJointID.RingIntermediate,
        XRHandJointID.RingDistal,
        XRHandJointID.RingTip,

        // Little(pinky) 4개 — 원래 HPTK는 Metacarpal 포함하지만 Tip이 게임플레이에 더 중요
        XRHandJointID.LittleProximal,
        XRHandJointID.LittleIntermediate,
        XRHandJointID.LittleDistal,
        XRHandJointID.LittleTip,
    };

    private XRHandSubsystem _handSubsystem;
    private Dictionary<XRHandJointID, GameObject> _colliderObjects = new Dictionary<XRHandJointID, GameObject>();
    private Dictionary<XRHandJointID, Rigidbody> _colliderRigidbodies = new Dictionary<XRHandJointID, Rigidbody>();
    private bool _initialized = false;

    // 매 프레임 각 콜라이더의 속도를 추적 (HandBounceResponder / Plasticbagsound에서 참조)
    private static Dictionary<int, float> _colliderSpeeds = new Dictionary<int, float>();
    private Dictionary<XRHandJointID, Vector3> _prevPositions = new Dictionary<XRHandJointID, Vector3>();

    /// <summary>
    /// 특정 콜라이더 GameObject의 현재 이동 속도 (m/s)를 반환합니다.
    /// HandBounceResponder에서 충돌 시 호출합니다.
    /// </summary>
    public static float GetColliderSpeed(int instanceId)
    {
        return _colliderSpeeds.TryGetValue(instanceId, out float speed) ? speed : 0f;
    }

    void Start()
    {
        // 약간 딜레이 후 초기화 (XR 서브시스템 준비 대기)
        Invoke(nameof(Initialize), 1.0f);
    }

    private void Initialize()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count == 0)
        {
            Debug.LogWarning($"[HandCollider] XRHandSubsystem 없음. 1초 후 재시도.");
            Invoke(nameof(Initialize), 1.0f);
            return;
        }

        _handSubsystem = subsystems[0];

        // 각 관절에 Collider 오브젝트 생성 (Trigger + Physics 두 세트)
        foreach (var jointId in TrackedJoints)
        {
            bool isPalmOrWrist = jointId == XRHandJointID.Palm || jointId == XRHandJointID.Wrist;

            var obj = new GameObject($"HandCol_{handedness}_{jointId}");
            obj.transform.SetParent(transform);
            obj.tag = "PlayerHand"; // HandBounceResponder가 감지하는 태그

            // ─── Trigger SphereCollider (터치 감지용) ───
            var trigCol = obj.AddComponent<SphereCollider>();
            trigCol.radius = isPalmOrWrist ? palmTriggerRadius : triggerRadius;
            trigCol.isTrigger = true;

            // ─── Physics SphereCollider (봉지 밀어냄용) ───
            if (enablePhysicsColliders)
            {
                var physCol = obj.AddComponent<SphereCollider>();
                physCol.radius = isPalmOrWrist ? palmPhysicsRadius : physicsRadius;
                physCol.isTrigger = false;
            }

            // ─── Kinematic Rigidbody (공용) ───
            var rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            // Interpolation: FixedUpdate보다 Update에서 더 부드럽게 보임
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Kinematic rigidbody는 ContinuousSpeculative가 적합 (봉지 같은 dynamic rigidbody와 정확한 충돌)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // ─── 디버그 가시화 (선택) ───
            if (showDebugVisuals)
            {
                AttachDebugVisual(obj, isPalmOrWrist);
            }

            _colliderObjects[jointId] = obj;
            _colliderRigidbodies[jointId] = rb;
        }

        _initialized = true;
        int physCount = enablePhysicsColliders ? _colliderObjects.Count : 0;
        Debug.Log($"[HandCollider] {handedness} 초기화 완료. Trigger {_colliderObjects.Count}개 + Physics {physCount}개.");
    }

    // 매 프레임 Update: 위치 데이터 수집 + 속도 계산 (물리 이동은 FixedUpdate에서).
    void Update()
    {
        if (!_initialized || _handSubsystem == null || !_handSubsystem.running) return;

        XRHand hand = (handedness == Handedness.Left)
            ? _handSubsystem.leftHand
            : _handSubsystem.rightHand;

        if (!hand.isTracked) return;

        float dt = Time.deltaTime;
        foreach (var kvp in _colliderObjects)
        {
            var joint = hand.GetJoint(kvp.Key);
            if (joint.TryGetPose(out Pose pose))
            {
                // 속도 계산
                int id = kvp.Value.GetInstanceID();
                if (_prevPositions.TryGetValue(kvp.Key, out Vector3 prevPos) && dt > 0.0001f)
                {
                    float speed = (pose.position - prevPos).magnitude / dt;
                    _colliderSpeeds[id] = speed;
                }
                _prevPositions[kvp.Key] = pose.position;

                kvp.Value.SetActive(true);
            }
            else
            {
                kvp.Value.SetActive(false);
            }
        }
    }

    // FixedUpdate: Kinematic Rigidbody.MovePosition/MoveRotation으로 물리 밀어냄 유도.
    // (transform.position 직접 대입은 physics 영향 없음. MovePosition이 핵심)
    void FixedUpdate()
    {
        if (!_initialized || _handSubsystem == null || !_handSubsystem.running) return;

        XRHand hand = (handedness == Handedness.Left)
            ? _handSubsystem.leftHand
            : _handSubsystem.rightHand;

        if (!hand.isTracked) return;

        foreach (var kvp in _colliderRigidbodies)
        {
            var joint = hand.GetJoint(kvp.Key);
            if (joint.TryGetPose(out Pose pose) && kvp.Value != null)
            {
                kvp.Value.MovePosition(pose.position);
                kvp.Value.MoveRotation(pose.rotation);
            }
        }
    }

    void OnDestroy()
    {
        foreach (var obj in _colliderObjects.Values)
        {
            if (obj != null) Destroy(obj);
        }
        _colliderObjects.Clear();
        _colliderRigidbodies.Clear();
        if (_debugMatJoint != null) Destroy(_debugMatJoint);
        if (_debugMatPalm != null) Destroy(_debugMatPalm);
    }

    // ───── 디버그 가시화 ─────
    private Material _debugMatJoint;
    private Material _debugMatPalm;

    private void AttachDebugVisual(GameObject parent, bool isPalmOrWrist)
    {
        // 머티리얼 1회 생성 (양쪽 손 공유)
        if (_debugMatJoint == null) _debugMatJoint = CreateDebugMat(debugJointColor);
        if (_debugMatPalm == null) _debugMatPalm = CreateDebugMat(debugPalmColor);

        // 자식 GameObject에 Sphere Mesh 부착 (Collider 없음)
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "DebugVisual";
        var visualCol = visual.GetComponent<Collider>();
        if (visualCol != null) Destroy(visualCol);
        visual.transform.SetParent(parent.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // 크기: physics 반경의 2배(지름) * 배율
        float r = isPalmOrWrist ? palmPhysicsRadius : physicsRadius;
        float diameter = r * 2f * debugVisualScale;
        visual.transform.localScale = Vector3.one * diameter;

        // 머티리얼 교체
        var r2 = visual.GetComponent<Renderer>();
        if (r2 != null)
        {
            r2.sharedMaterial = isPalmOrWrist ? _debugMatPalm : _debugMatJoint;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
        }
    }

    private static Material CreateDebugMat(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh);
        m.name = "AutoGenerated_HandColliderDebug";
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        // 반투명 처리 (URP Unlit)
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1); // Transparent
            m.SetFloat("_Blend", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        return m;
    }
}
