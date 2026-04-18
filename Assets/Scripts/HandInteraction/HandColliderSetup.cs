using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

/// <summary>
/// 손의 여러 관절(손바닥, 손가락 끝)에 SphereCollider를 배치하여
/// 플라스틱백과의 충돌 감지를 안정적으로 만듭니다.
/// 기존 Poke Interactor(검지 끝)에 더해 중지, 약지, 엄지, 손바닥까지 충돌 가능.
///
/// XR Origin > Camera Offset > Left Hand / Right Hand에 붙여주세요.
/// </summary>
public class HandColliderSetup : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("왼손이면 Left, 오른손이면 Right")]
    public Handedness handedness = Handedness.Left;

    [Tooltip("각 관절 Collider의 반지름 (미터)")]
    public float colliderRadius = 0.025f;

    [Tooltip("손바닥 Collider의 반지름 (미터)")]
    public float palmColliderRadius = 0.05f;

    // 추적할 관절 목록
    private static readonly XRHandJointID[] TrackedJoints = new[]
    {
        XRHandJointID.MiddleTip,    // 중지 끝
        XRHandJointID.RingTip,      // 약지 끝
        XRHandJointID.ThumbTip,     // 엄지 끝
        XRHandJointID.Palm,         // 손바닥 중심
    };

    private XRHandSubsystem _handSubsystem;
    private Dictionary<XRHandJointID, GameObject> _colliderObjects = new Dictionary<XRHandJointID, GameObject>();
    private bool _initialized = false;

    // 매 프레임 각 콜라이더의 속도를 추적 (HandBounceResponder에서 참조)
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

        // 각 관절에 Collider 오브젝트 생성
        foreach (var jointId in TrackedJoints)
        {
            var obj = new GameObject($"HandCol_{handedness}_{jointId}");
            obj.transform.SetParent(transform);
            obj.tag = "PlayerHand"; // HandBounceResponder가 감지하는 태그

            var col = obj.AddComponent<SphereCollider>();
            col.radius = (jointId == XRHandJointID.Palm) ? palmColliderRadius : colliderRadius;
            col.isTrigger = true; // Trigger로 설정 — OnTriggerEnter 감지

            var rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _colliderObjects[jointId] = obj;
        }

        _initialized = true;
        Debug.Log($"[HandCollider] {handedness} 초기화 완료. {_colliderObjects.Count}개 관절 Collider 생성.");
    }

    void Update()
    {
        if (!_initialized || _handSubsystem == null || !_handSubsystem.running) return;

        XRHand hand = (handedness == Handedness.Left)
            ? _handSubsystem.leftHand
            : _handSubsystem.rightHand;

        if (!hand.isTracked) return;

        // 각 관절 위치에 Collider 오브젝트 이동 + 속도 계산
        float dt = Time.deltaTime;
        foreach (var kvp in _colliderObjects)
        {
            var joint = hand.GetJoint(kvp.Key);
            if (joint.TryGetPose(out Pose pose))
            {
                // 매 프레임 속도 계산
                int id = kvp.Value.GetInstanceID();
                if (_prevPositions.TryGetValue(kvp.Key, out Vector3 prevPos) && dt > 0.0001f)
                {
                    float speed = (pose.position - prevPos).magnitude / dt;
                    _colliderSpeeds[id] = speed;
                }
                _prevPositions[kvp.Key] = pose.position;

                kvp.Value.transform.position = pose.position;
                kvp.Value.transform.rotation = pose.rotation;
                kvp.Value.SetActive(true);
            }
            else
            {
                kvp.Value.SetActive(false);
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
    }
}
