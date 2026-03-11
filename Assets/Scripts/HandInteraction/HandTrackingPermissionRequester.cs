// HandTrackingPermissionRequester.cs
// Android XR에서 Hand Tracking 퍼미션을 런타임에 요청합니다.
// 씬의 아무 오브젝트에 붙이면 앱 시작 시 퍼미션을 요청합니다.

using UnityEngine;
using System.Collections;

public class HandTrackingPermissionRequester : MonoBehaviour
{
    private bool _permissionGranted = false;

    void Start()
    {
        RequestHandTrackingPermission();
    }

    private void RequestHandTrackingPermission()
    {
#if !UNITY_EDITOR
        string handTrackingPermission = "android.permission.HAND_TRACKING";

        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(handTrackingPermission))
        {
            Debug.Log("[HandTracking] Hand Tracking 퍼미션 이미 허용됨");
            _permissionGranted = true;
        }
        else
        {
            Debug.Log("[HandTracking] Hand Tracking 퍼미션 요청 중...");
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += OnPermissionGranted;
            callbacks.PermissionDenied += OnPermissionDenied;
            UnityEngine.Android.Permission.RequestUserPermission(handTrackingPermission, callbacks);
        }
#else
        Debug.Log("[HandTracking] 에디터 모드: 퍼미션 요청 스킵");
        _permissionGranted = true;
#endif
    }

    private void OnPermissionGranted(string permission)
    {
        Debug.Log($"[HandTracking] 퍼미션 허용됨: {permission}");
        _permissionGranted = true;
    }

    private void OnPermissionDenied(string permission)
    {
        Debug.LogWarning($"[HandTracking] 퍼미션 거부됨: {permission} — 손 추적이 작동하지 않습니다.");
    }
}
