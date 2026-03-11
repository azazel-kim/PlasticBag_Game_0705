using UnityEngine;
using UnityEngine.XR.OpenXR;
using Google.XR.Extensions;

/// <summary>
/// Android XR 패스쓰루 초기화 스크립트.
/// 씬의 아무 GameObject에 추가하면 자동으로 패스쓰루를 설정합니다.
/// </summary>
public class AndroidXRPassthroughSetup : MonoBehaviour
{
    [Tooltip("패스쓰루 활성화 여부")]
    [SerializeField] private bool enablePassthrough = true;

    private XREnvironmentBlendModeFeature _blendFeature;
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            // 카메라 배경을 투명하게 설정 (패스쓰루가 보이려면 필수)
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
    }

    private void Start()
    {
        if (!enablePassthrough)
            return;

        _blendFeature = OpenXRSettings.Instance?.GetFeature<XREnvironmentBlendModeFeature>();

        if (_blendFeature == null)
        {
            Debug.LogError("[Passthrough] XREnvironmentBlendModeFeature를 찾을 수 없습니다. " +
                "OpenXR Settings에서 Environment Blend Mode를 활성화하세요.");
            return;
        }

        if (!_blendFeature.enabled)
        {
            Debug.LogError("[Passthrough] Environment Blend Mode Feature가 비활성화 상태입니다.");
            return;
        }

        // AlphaBlend 모드로 설정하여 패스쓰루 활성화
        _blendFeature.RequestedEnvironmentBlendMode =
            UnityEngine.XR.OpenXR.NativeTypes.XrEnvironmentBlendMode.AlphaBlend;

        Debug.Log("[Passthrough] 패스쓰루 모드(AlphaBlend) 활성화 완료");
    }

    private void OnDestroy()
    {
        // 씬 전환 시 Opaque로 복원이 필요한 경우 여기서 처리
    }
}
