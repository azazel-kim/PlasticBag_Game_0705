// EyeGazeRaycaster.cs
// OpenXR Eye Gaze Interaction을 사용하여 시선을 추적합니다.
// Samsung Galaxy XR (Android XR) 대응 - Unity 6
// Google.XR.Extensions를 사용하여 Eye Tracking 퍼미션을 런타임에 요청합니다.
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;

[RequireComponent(typeof(LineRenderer))]
public class EyeGazeRaycaster : MonoBehaviour
{
    [Header("필수 연결 항목")]
    [Tooltip("XR Origin > Camera Offset > Main Camera 오브젝트를 여기에 끌어다 놓으세요.")]
    public Transform eyeGazeAnchor;

    [Tooltip("포인터의 부모 오브젝트(GazePointer)를 여기에 끌어다 놓으세요.")]
    public GameObject gazePointer;

    [Tooltip("머티리얼을 변경할 자식 오브젝트(Sphere)를 여기에 끌어다 놓으세요.")]
    public Renderer targetRenderer;

    [Header("머티리얼 설정")]
    public Material onHitMaterial;
    public Material offHitMaterial;

    [Header("기타 설정")]
    public float maxGazeDistance = 10.0f;

    private InputAction _eyeGazePositionAction;
    private InputAction _eyeGazeRotationAction;
    private InputAction _eyeGazeTrackingStateAction;

    private LineRenderer gazeLineRenderer;
    private bool _eyeTrackingActive;
    private bool _permissionRequested;
    private int _frameCount;
    private Vector3 _lastValidPos;
    private Quaternion _lastValidRot = Quaternion.identity;

    /// <summary>
    /// 마지막 Physics.Raycast hit 결과. 외부에서 시선이 어떤 Collider에 맞았는지 확인용.
    /// hit가 없으면 null.
    /// </summary>
    public Collider LastHitCollider { get; private set; }

    void Start()
    {
        gazeLineRenderer = GetComponent<LineRenderer>();

        if (eyeGazeAnchor == null || gazePointer == null || targetRenderer == null || onHitMaterial == null || offHitMaterial == null)
        {
            Debug.LogError("EyeGazeRaycaster의 Inspector 창에서 모든 항목을 연결해주세요!");
            this.enabled = false;
            return;
        }

        // Eye Tracking 퍼미션은 HandTrackingPermissionRequester가 순차적으로 처리함
        // (중복 요청 시 Android가 자동 거부하므로 여기서는 요청하지 않음)

        // OpenXR Eye Gaze Input Actions 설정
        _eyeGazePositionAction = new InputAction("EyeGazePosition", binding: "<EyeGaze>/pose/position");
        _eyeGazeRotationAction = new InputAction("EyeGazeRotation", binding: "<EyeGaze>/pose/rotation");
        _eyeGazeTrackingStateAction = new InputAction("EyeGazeTrackingState", binding: "<EyeGaze>/pose/trackingState");

        _eyeGazePositionAction.Enable();
        _eyeGazeRotationAction.Enable();
        _eyeGazeTrackingStateAction.Enable();

        Debug.Log("[EyeGaze] Input Actions 초기화 완료 (position, rotation, trackingState). 디바이스 연결 대기 중...");

        // OpenXR 세션 시작 후 디바이스가 등록될 때까지 폴링
        StartCoroutine(WaitForEyeGazeDevice());
    }

    /// <summary>
    /// Google.XR.Extensions를 사용하여 Eye Tracking 퍼미션을 요청합니다.
    /// Android XR에서 Eye Tracking을 사용하려면 런타임 퍼미션이 필요합니다.
    /// </summary>
    private void RequestEyeTrackingPermission()
    {
#if !UNITY_EDITOR
        // Android 런타임 퍼미션 요청
        string eyeTrackingPermission = "android.permission.EYE_TRACKING";

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(eyeTrackingPermission))
        {
            Debug.Log("[EyeGaze] Eye Tracking 퍼미션 요청 중...");
            UnityEngine.Android.Permission.RequestUserPermission(eyeTrackingPermission);
            _permissionRequested = true;
        }
        else
        {
            Debug.Log("[EyeGaze] Eye Tracking 퍼미션 이미 허용됨");
            _permissionRequested = true;
        }
#else
        Debug.Log("[EyeGaze] 에디터 모드: 퍼미션 요청 스킵");
        _permissionRequested = true;
#endif
    }

    private IEnumerator WaitForEyeGazeDevice()
    {
        // 퍼미션 승인 대기
        yield return new WaitForSeconds(1.0f);

        float timeout = 15f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            // 방법 1: InputSystem에서 EyeGaze 디바이스 확인
            var eyeDevice = InputSystem.GetDevice("EyeGaze");
            if (eyeDevice != null)
            {
                Debug.Log($"[EyeGaze] InputSystem 디바이스 발견! {eyeDevice.displayName} (대기시간: {elapsed}초)");
                _eyeTrackingActive = true;
                yield break;
            }

            // 방법 2: XR InputDevice API에서 Eye Tracking 디바이스 확인
            var inputDevices = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.EyeTracking, inputDevices);
            if (inputDevices.Count > 0)
            {
                Debug.Log($"[EyeGaze] XR Eye Tracking 디바이스 발견! {inputDevices[0].name} (대기시간: {elapsed}초)");
                _eyeTrackingActive = true;
                yield break;
            }

            // 방법 3: trackingState로 확인
            int trackingState = _eyeGazeTrackingStateAction.ReadValue<int>();
            if (trackingState > 0)
            {
                Debug.Log($"[EyeGaze] trackingState={trackingState} 감지! (대기시간: {elapsed}초)");
                _eyeTrackingActive = true;
                yield break;
            }

            // 방법 4: Input Action에서 데이터가 오는지 확인
            Quaternion rot = _eyeGazeRotationAction.ReadValue<Quaternion>();
            Vector3 pos = _eyeGazePositionAction.ReadValue<Vector3>();
            if (IsValidGazeData(pos, rot))
            {
                Debug.Log($"[EyeGaze] 유효한 Eye Gaze 데이터 수신! pos={pos} rot={rot.eulerAngles} (대기시간: {elapsed}초)");
                _eyeTrackingActive = true;
                yield break;
            }

#if !UNITY_EDITOR
            // 퍼미션 상태 확인
            bool hasPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.EYE_TRACKING");
            Debug.Log($"[EyeGaze] 대기 중... ({elapsed}s) trackingState={trackingState} pos={pos} rot={rot} permission={hasPermission}");
#else
            Debug.Log($"[EyeGaze] 대기 중... ({elapsed}s) trackingState={trackingState} pos={pos} rot={rot}");
#endif
        }

        Debug.LogWarning("[EyeGaze] 타임아웃: Eye Gaze 디바이스를 찾지 못했습니다. 헤드 트래킹으로 동작합니다.");
    }

    void OnDestroy()
    {
        _eyeGazePositionAction?.Disable();
        _eyeGazeRotationAction?.Disable();
        _eyeGazeTrackingStateAction?.Disable();
    }

    /// <summary>
    /// Eye Gaze 데이터가 유효한지 확인합니다.
    /// </summary>
    private bool IsValidGazeData(Vector3 pos, Quaternion rot)
    {
        // Quaternion(0,0,0,0)은 유효하지 않음
        if (rot.x == 0 && rot.y == 0 && rot.z == 0 && rot.w == 0)
            return false;

        // Quaternion.identity(0,0,0,1)만 있고 position도 0이면 데이터 없음
        bool isIdentity = Mathf.Approximately(rot.x, 0) && Mathf.Approximately(rot.y, 0)
                       && Mathf.Approximately(rot.z, 0) && Mathf.Approximately(rot.w, 1);
        bool isZeroPos = pos.sqrMagnitude < 0.0001f;

        if (isIdentity && isZeroPos)
            return false;

        return true;
    }

    void Update()
    {
        _frameCount++;
        Ray gazeRay;

        // Eye Gaze 데이터 읽기
        Vector3 eyePos = _eyeGazePositionAction.ReadValue<Vector3>();
        Quaternion eyeRot = _eyeGazeRotationAction.ReadValue<Quaternion>();
        int trackingState = _eyeGazeTrackingStateAction.ReadValue<int>();

        bool hasValidData = IsValidGazeData(eyePos, eyeRot) && trackingState > 0;

        // 디버그 로그 (매 5초마다)
        if (_frameCount % 360 == 1)
        {
            Debug.Log($"[EyeGaze] pos={eyePos:F3} rot={eyeRot} trackingState={trackingState} valid={hasValidData} active={_eyeTrackingActive}");
        }

        if (hasValidData)
        {
            _eyeTrackingActive = true;
            _lastValidPos = eyePos;
            _lastValidRot = eyeRot;

            Vector3 eyeDir = eyeRot * Vector3.forward;
            gazeRay = new Ray(eyePos, eyeDir);
        }
        else if (_eyeTrackingActive && IsValidGazeData(eyePos, eyeRot))
        {
            // trackingState가 0이어도 유효 데이터가 있으면 사용
            _lastValidPos = eyePos;
            _lastValidRot = eyeRot;

            Vector3 eyeDir = eyeRot * Vector3.forward;
            gazeRay = new Ray(eyePos, eyeDir);
        }
        else
        {
            // Fallback: 헤드 트래킹
            gazeRay = new Ray(eyeGazeAnchor.position, eyeGazeAnchor.forward);
        }

        // Line Renderer 시작점
        gazeLineRenderer.SetPosition(0, gazeRay.origin);

        // QueryTriggerInteraction.Collide: Trigger Collider도 감지 (패널 버튼용)
        if (Physics.Raycast(gazeRay, out RaycastHit hit, maxGazeDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            gazePointer.SetActive(true);
            gazePointer.transform.position = hit.point;
            gazeLineRenderer.SetPosition(1, hit.point);
            targetRenderer.material = onHitMaterial;
            LastHitCollider = hit.collider;
        }
        else
        {
            gazePointer.SetActive(true);
            gazePointer.transform.position = gazeRay.origin + gazeRay.direction * maxGazeDistance;
            gazeLineRenderer.SetPosition(1, gazeRay.origin + gazeRay.direction * maxGazeDistance);
            targetRenderer.material = offHitMaterial;
            LastHitCollider = null;
        }
    }
}
