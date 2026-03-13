// HandVisualController.cs - 가상 손의 시각적 옵션을 제어하는 스크립트
// XR Origin 하위의 Hand Tracking 오브젝트에 적용합니다.
// 3가지 모드: 아웃라인, 어두운 손, 투명 (사용자 손만 보임)

using UnityEngine;
using UnityEngine.XR.Hands;

public class HandVisualController : MonoBehaviour
{
    /// <summary>
    /// 가상 손의 표시 모드
    /// </summary>
    public enum HandVisualMode
    {
        Outline,    // 손의 아웃라인만 보이는 모드
        DarkHand,   // 어두운 색의 손으로 표시
        Invisible   // 가상 손을 숨기고 사용자의 실제 손만 보이게 함
    }

    [Header("가상 손 모드 설정")]
    [Tooltip("가상 손의 표시 모드를 선택합니다")]
    public HandVisualMode visualMode = HandVisualMode.Outline;

    [Header("아웃라인 모드 설정")]
    [Tooltip("아웃라인 색상")]
    public Color outlineColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
    [Tooltip("아웃라인 두께")]
    [Range(0.001f, 0.01f)]
    public float outlineWidth = 0.003f;

    [Header("어두운 손 모드 설정")]
    [Tooltip("어두운 손 색상")]
    public Color darkHandColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);

    [Header("커스텀 머티리얼 (선택사항)")]
    [Tooltip("아웃라인용 머티리얼 (없으면 자동 생성)")]
    public Material outlineMaterial;
    [Tooltip("어두운 손용 머티리얼 (없으면 자동 생성)")]
    public Material darkHandMaterial;

    // 내부에서 자동 생성한 머티리얼
    private Material _autoOutlineMat;
    private Material _autoDarkMat;
    private Renderer[] _handRenderers;
    private Renderer[] _handVisualizerRenderers; // Hand Visualizer 오브젝트의 렌더러
    private Material[] _originalMaterials;
    private HandVisualMode _currentMode;

    void Start()
    {
        // 약간의 딜레이 후 초기화 (Hand 오브젝트가 생성된 뒤에 실행)
        Invoke(nameof(InitializeHandVisuals), 1.0f);
    }

    private bool _initialized = false;

    void InitializeHandVisuals()
    {
        CollectRenderers();
        CreateMaterials();
        ApplyVisualMode(visualMode);
        _initialized = true;

        // Hand Visualizer가 별도로 렌더링하는 기본 파란색 머티리얼도 수집
        CollectHandVisualizerRenderers();
    }

    /// <summary>
    /// Hand Visualizer 오브젝트 아래의 렌더러를 수집합니다.
    /// XR Hands 샘플의 HandVisualizer가 별도로 파란색 손을 그리는 문제 방지.
    /// </summary>
    private void CollectHandVisualizerRenderers()
    {
        var cameraOffset = transform.parent;
        if (cameraOffset == null) return;

        var handVisualizer = cameraOffset.GetComponentInChildren<UnityEngine.XR.Hands.Samples.VisualizerSample.HandVisualizer>(true);
        if (handVisualizer != null)
        {
            var renderers = handVisualizer.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                _handVisualizerRenderers = renderers;
                Debug.Log($"[HandVisual] Hand Visualizer에서 {renderers.Length}개 렌더러 발견, 머티리얼 덮어쓰기 시작");
            }
        }
    }

    // XRHandMeshController가 매 프레임 머티리얼을 덮어쓸 수 있으므로
    // LateUpdate에서 매 프레임 강제 적용
    void LateUpdate()
    {
        if (!_initialized) return;

        // 렌더러가 동적으로 생성되거나 파괴되었을 수 있으므로 체크 (XRHandMeshController 등 대응)
        if (_handRenderers == null || _handRenderers.Length == 0 || (_handRenderers.Length > 0 && _handRenderers[0] == null))
        {
            CollectRenderers();
        }

        if (_handRenderers == null || _handRenderers.Length == 0) return;

        Material mat = null;
        switch (_currentMode)
        {
            case HandVisualMode.Outline:
                mat = outlineMaterial != null ? outlineMaterial : _autoOutlineMat;
                break;
            case HandVisualMode.DarkHand:
                mat = darkHandMaterial != null ? darkHandMaterial : _autoDarkMat;
                break;
            case HandVisualMode.Invisible:
                SetRenderersVisible(false);
                return;
        }

        if (mat != null)
        {
            foreach (var r in _handRenderers)
            {
                if (r != null && r.sharedMaterial != mat)
                    r.material = mat;
            }

            // Hand Visualizer의 렌더러도 매 프레임 회색으로 강제 적용
            if (_handVisualizerRenderers == null || _handVisualizerRenderers.Length == 0 ||
                (_handVisualizerRenderers.Length > 0 && _handVisualizerRenderers[0] == null))
            {
                CollectHandVisualizerRenderers();
            }
            if (_handVisualizerRenderers != null)
            {
                foreach (var r in _handVisualizerRenderers)
                {
                    if (r != null && r.sharedMaterial != mat)
                        r.material = mat;
                }
            }
        }
    }

    /// <summary>
    /// 이 오브젝트와 자식에서 손 렌더러를 수집합니다.
    /// </summary>
    private void CollectRenderers()
    {
        var currentRenderers = GetComponentsInChildren<Renderer>(true);

        if (currentRenderers.Length > 0)
        {
            bool isNew = (_handRenderers == null || _handRenderers.Length != currentRenderers.Length);
            if (!isNew && _handRenderers.Length > 0)
            {
                isNew = _handRenderers[0] == null;
            }

            if (isNew)
            {
                _handRenderers = currentRenderers;
                // 원본 머티리얼 백업
                _originalMaterials = new Material[_handRenderers.Length];
                for (int i = 0; i < _handRenderers.Length; i++)
                {
                    if (_handRenderers[i].sharedMaterial != null)
                        _originalMaterials[i] = new Material(_handRenderers[i].sharedMaterial);
                }
                Debug.Log($"[HandVisual] {_handRenderers.Length}개의 손 렌더러를 동적으로 찾았습니다.");
                
                if (_initialized)
                {
                    ApplyVisualMode(_currentMode);
                }
            }
        }
        else
        {
            if (_handRenderers == null)
            {
                Debug.LogWarning("[HandVisual] 손 렌더러를 찾을 수 없습니다. 렌더러가 생성되기를 기다립니다.");
                _handRenderers = new Renderer[0];
            }
        }
    }

    /// <summary>
    /// 커스텀 머티리얼이 없으면 자동으로 생성합니다.
    /// Inspector에 저장된 색상값 대신 코드에서 정의한 회색을 강제 사용합니다.
    /// </summary>
    private void CreateMaterials()
    {
        // Inspector에 저장된 파란색 값을 코드에서 강제로 회색으로 덮어쓰기
        outlineColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        darkHandColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        // 아웃라인 머티리얼 (밝은 회색 + 흰색 Emission 테두리 느낌)
        if (outlineMaterial == null)
        {
            _autoOutlineMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (_autoOutlineMat != null)
            {
                _autoOutlineMat.name = "AutoGenerated_HandOutline";
                SetMaterialTransparent(_autoOutlineMat);
                _autoOutlineMat.color = outlineColor;
                _autoOutlineMat.SetColor("_BaseColor", outlineColor);
                // 흰색 Emission으로 가장자리가 밝게 보이는 효과
                _autoOutlineMat.EnableKeyword("_EMISSION");
                _autoOutlineMat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f, 1f) * 0.15f);
                _autoOutlineMat.SetFloat("_Smoothness", 0.3f);
            }
        }

        // 어두운 손 머티리얼
        if (darkHandMaterial == null)
        {
            _autoDarkMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (_autoDarkMat != null)
            {
                _autoDarkMat.name = "AutoGenerated_DarkHand";
                SetMaterialTransparent(_autoDarkMat);
                _autoDarkMat.color = darkHandColor;
                _autoDarkMat.SetColor("_BaseColor", darkHandColor);
            }
        }
    }

    /// <summary>
    /// URP Unlit 머티리얼을 투명 모드로 설정합니다.
    /// </summary>
    private void SetMaterialTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0);   // Alpha
        mat.SetFloat("_AlphaClip", 0);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    /// <summary>
    /// 가상 손의 표시 모드를 변경합니다. 런타임에서도 호출 가능합니다.
    /// </summary>
    public void ApplyVisualMode(HandVisualMode mode)
    {
        _currentMode = mode;

        if (_handRenderers == null || _handRenderers.Length == 0)
        {
            CollectRenderers();
            if (_handRenderers == null || _handRenderers.Length == 0) return;
        }

        switch (mode)
        {
            case HandVisualMode.Outline:
                SetRenderersVisible(true);
                Material outMat = outlineMaterial != null ? outlineMaterial : _autoOutlineMat;
                if (outMat != null)
                    ApplyMaterialToAll(outMat);
                Debug.Log("[HandVisual] 아웃라인 모드 적용");
                break;

            case HandVisualMode.DarkHand:
                SetRenderersVisible(true);
                Material darkMat = darkHandMaterial != null ? darkHandMaterial : _autoDarkMat;
                if (darkMat != null)
                    ApplyMaterialToAll(darkMat);
                Debug.Log("[HandVisual] 어두운 손 모드 적용");
                break;

            case HandVisualMode.Invisible:
                SetRenderersVisible(false);
                Debug.Log("[HandVisual] 투명 모드 적용 (실제 손만 보임)");
                break;
        }
    }

    /// <summary>
    /// 모든 손 렌더러에 머티리얼을 적용합니다.
    /// </summary>
    private void ApplyMaterialToAll(Material mat)
    {
        foreach (var renderer in _handRenderers)
        {
            if (renderer != null)
                renderer.material = mat;
        }
    }

    /// <summary>
    /// 손 렌더러의 보이기/숨기기를 설정합니다.
    /// </summary>
    private void SetRenderersVisible(bool visible)
    {
        foreach (var renderer in _handRenderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    /// <summary>
    /// 외부에서 모드를 숫자로 변경 (UI 버튼 등에서 사용 가능)
    /// 0 = Outline, 1 = DarkHand, 2 = Invisible
    /// </summary>
    public void SetMode(int modeIndex)
    {
        if (modeIndex >= 0 && modeIndex <= 2)
        {
            visualMode = (HandVisualMode)modeIndex;
            ApplyVisualMode(visualMode);
        }
    }

    /// <summary>
    /// 다음 모드로 순환합니다 (토글용).
    /// </summary>
    public void CycleMode()
    {
        int next = ((int)_currentMode + 1) % 3;
        SetMode(next);
    }

    void OnValidate()
    {
        // Inspector에서 값 변경 시 즉시 반영 (Play 모드일 때만)
        if (Application.isPlaying && _handRenderers != null && _handRenderers.Length > 0)
        {
            ApplyVisualMode(visualMode);
        }
    }

    void OnDestroy()
    {
        // 자동 생성한 머티리얼 정리
        if (_autoOutlineMat != null) Destroy(_autoOutlineMat);
        if (_autoDarkMat != null) Destroy(_autoDarkMat);
        if (_originalMaterials != null)
        {
            foreach (var mat in _originalMaterials)
                if (mat != null) Destroy(mat);
        }
    }
}
