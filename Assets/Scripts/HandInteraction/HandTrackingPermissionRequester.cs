// HandTrackingPermissionRequester.cs
// Android XR에서 Hand Tracking 퍼미션을 런타임에 요청합니다.
// 씬의 아무 오브젝트에 붙이면 앱 시작 시 퍼미션을 요청합니다.

using UnityEngine;
using System.Collections;

/// <summary>
/// Android XR에서 Hand Tracking + Eye Tracking 퍼미션을 순차적으로 요청합니다.
/// 두 퍼미션을 동시에 요청하면 두 번째 다이얼로그가 무시되는 문제가 있어
/// 코루틴으로 순차 처리합니다.
/// </summary>
public class HandTrackingPermissionRequester : MonoBehaviour
{
    private bool _handPermissionGranted = false;
    private bool _eyePermissionGranted = false;
    private bool _handPermissionDone = false;

    void Start()
    {
        StartCoroutine(RequestPermissionsSequentially());
    }

    /// <summary>
    /// 핸드 트래킹 → 아이 트래킹 순서로 퍼미션을 순차 요청합니다.
    /// 첫 번째 퍼미션 응답이 돌아온 후 두 번째를 요청합니다.
    /// </summary>
    private IEnumerator RequestPermissionsSequentially()
    {
#if !UNITY_EDITOR
        // 1) 핸드 트래킹 퍼미션
        string handPermission = "android.permission.HAND_TRACKING";
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(handPermission))
        {
            Debug.Log("[Permission] Hand Tracking 이미 허용됨");
            _handPermissionGranted = true;
        }
        else
        {
            Debug.Log("[Permission] Hand Tracking 퍼미션 요청 중...");
            _handPermissionDone = false;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += (p) => { _handPermissionGranted = true; _handPermissionDone = true; Debug.Log($"[Permission] 허용됨: {p}"); };
            callbacks.PermissionDenied += (p) => { _handPermissionDone = true; Debug.LogWarning($"[Permission] 거부됨: {p}"); };
            callbacks.PermissionDeniedAndDontAskAgain += (p) => { _handPermissionDone = true; Debug.LogWarning($"[Permission] 영구 거부됨: {p}"); };
            UnityEngine.Android.Permission.RequestUserPermission(handPermission, callbacks);

            // 콜백이 돌아올 때까지 대기 (최대 30초)
            float timeout = 30f;
            while (!_handPermissionDone && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        // 퍼미션 다이얼로그 사이에 잠시 대기 (Android XR UI 안정화)
        yield return new WaitForSeconds(0.5f);

        // 2) 아이 트래킹 퍼미션
        string eyePermission = "android.permission.EYE_TRACKING";
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(eyePermission))
        {
            Debug.Log("[Permission] Eye Tracking 이미 허용됨");
            _eyePermissionGranted = true;
        }
        else
        {
            Debug.Log("[Permission] Eye Tracking 퍼미션 요청 중...");
            bool eyeDone = false;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += (p) => { _eyePermissionGranted = true; eyeDone = true; Debug.Log($"[Permission] 허용됨: {p}"); };
            callbacks.PermissionDenied += (p) => { eyeDone = true; Debug.LogWarning($"[Permission] 거부됨: {p}"); };
            callbacks.PermissionDeniedAndDontAskAgain += (p) => { eyeDone = true; Debug.LogWarning($"[Permission] 영구 거부됨: {p}"); };
            UnityEngine.Android.Permission.RequestUserPermission(eyePermission, callbacks);

            float timeout = 30f;
            while (!eyeDone && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        Debug.Log($"[Permission] 완료 — Hand={_handPermissionGranted}, Eye={_eyePermissionGranted}");
#else
        Debug.Log("[Permission] 에디터 모드: 퍼미션 요청 스킵");
        _handPermissionGranted = true;
        _eyePermissionGranted = true;
        yield return null;
#endif
    }
}
