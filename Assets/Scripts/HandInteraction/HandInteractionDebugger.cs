// HandInteractionDebugger.cs - 손 인터랙션 상태를 화면에 표시하는 진단 스크립트
// 씬의 아무 오브젝트에 붙이면 화면 좌측 상단에 디버그 정보가 표시됩니다.
// 빌드 테스트 후 비활성화하거나 삭제하세요.

using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;
using System.Text;
using System.Collections.Generic;

public class HandInteractionDebugger : MonoBehaviour
{
    private StringBuilder _sb = new StringBuilder();
    private GUIStyle _style;
    private XRHandSubsystem _handSubsystem;
    private float _checkInterval = 0.5f;
    private float _nextCheck = 0f;
    private string _cachedInfo = "초기화 중...";

    private Collider _buttonCollider;

    // Start에서 캐싱
    private XRPokeInteractor[] _cachedPokeInteractors;
    private Transform _xrOriginTransform;

    void Start()
    {
        // Start Button의 Collider 찾기
        var loader = FindObjectOfType<HandTriggerSceneLoader>();
        if (loader != null)
            _buttonCollider = loader.GetComponent<Collider>();

        // Poke Interactor 캐싱
        _cachedPokeInteractors = FindObjectsOfType<XRPokeInteractor>();

        // XR Origin Transform 캐싱
        var xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin != null)
        {
            _xrOriginTransform = xrOrigin.CameraFloorOffsetObject != null
                ? xrOrigin.CameraFloorOffsetObject.transform
                : xrOrigin.transform;
        }

        Debug.Log("[HandDebug] HandInteractionDebugger 시작됨");
    }

    // Hand Joint의 로컬 좌표를 월드 좌표로 변환
    private Vector3 JointPoseToWorldPosition(Pose localPose)
    {
        if (_xrOriginTransform != null)
            return _xrOriginTransform.TransformPoint(localPose.position);
        return localPose.position;
    }

    void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + _checkInterval;

        _sb.Clear();
        _sb.AppendLine("=== Hand Touch Debug ===");

        CheckHandSubsystem();
        CheckDistanceToButton();

        _cachedInfo = _sb.ToString();
        Debug.Log($"[HandDebug] {_cachedInfo}");
    }

    private void CheckHandSubsystem()
    {
        if (_handSubsystem == null || !_handSubsystem.running)
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            _handSubsystem = subsystems.Count > 0 ? subsystems[0] : null;
        }

        if (_handSubsystem != null && _handSubsystem.running)
        {
            _sb.AppendLine($"HandSubsystem: OK");
            _sb.AppendLine($"  L tracked={_handSubsystem.leftHand.isTracked}");
            _sb.AppendLine($"  R tracked={_handSubsystem.rightHand.isTracked}");
        }
        else
        {
            _sb.AppendLine("HandSubsystem: 없음!");
        }
    }

    private void CheckDistanceToButton()
    {
        if (_buttonCollider == null)
        {
            _sb.AppendLine("Button: 못 찾음!");
            return;
        }

        Bounds bounds = _buttonCollider.bounds;
        _sb.AppendLine($"Button: center={bounds.center:F2} size={bounds.size:F2}");
        _sb.AppendLine($"  rot={_buttonCollider.transform.eulerAngles:F1}");

        // Poke Interactor 거리 체크 (캐싱된 배열 사용)
        if (_cachedPokeInteractors == null)
            _cachedPokeInteractors = FindObjectsOfType<XRPokeInteractor>();

        _sb.AppendLine($"Poke: {_cachedPokeInteractors.Length}개");
        foreach (var poke in _cachedPokeInteractors)
        {
            if (poke == null) continue;
            Vector3 pos = poke.transform.position;
            // ClosestPoint 기반 거리 (회전 반영)
            Vector3 closest = _buttonCollider.ClosestPoint(pos);
            float dist = Vector3.Distance(pos, closest);
            bool inside = dist < 0.001f; // ClosestPoint == pos이면 내부
            bool insideExpanded = dist <= 0.05f;
            _sb.AppendLine($"  {poke.name}: d={dist:F3} in={inside} exp={insideExpanded}");
        }

        // Hand joint 거리 체크 (월드 좌표 변환 적용)
        if (_handSubsystem != null)
        {
            CheckJointDistance(_handSubsystem.leftHand, "L");
            CheckJointDistance(_handSubsystem.rightHand, "R");
        }
    }

    private void CheckJointDistance(XRHand hand, string label)
    {
        if (!hand.isTracked) return;

        var joint = hand.GetJoint(XRHandJointID.IndexTip);
        if (joint.TryGetPose(out Pose pose))
        {
            Vector3 worldPos = JointPoseToWorldPosition(pose);
            // ClosestPoint 기반 거리 (회전 반영)
            Vector3 closest = _buttonCollider.ClosestPoint(worldPos);
            float dist = Vector3.Distance(worldPos, closest);
            bool inside = dist < 0.001f;
            _sb.AppendLine($"  {label} IndexTip: d={dist:F3} in={inside} pos={worldPos:F2}");
        }
    }

    void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = 20;
            _style.normal.textColor = Color.yellow;
            _style.fontStyle = FontStyle.Bold;

            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.7f));
            bgTex.Apply();
            _style.normal.background = bgTex;
            _style.padding = new RectOffset(10, 10, 5, 5);
        }

        GUI.Label(new Rect(10, 10, 800, 500), _cachedInfo, _style);
    }
}
