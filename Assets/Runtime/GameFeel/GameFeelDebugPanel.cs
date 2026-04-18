using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

/// <summary>
/// VR GameFeel 프리셋 선택 패널 (간소화 버전).
/// 프리셋 퀵 선택 + START 버튼만 표시.
/// 미세 조정은 Unity Inspector에서 프리셋 에셋(.asset)을 직접 수정.
/// </summary>
public class GameFeelDebugPanel : MonoBehaviour
{
    [Header("패널 설정")]
    public float distanceFromCamera = 0.5f;
    public float heightOffset = -0.15f;
    public bool showOnStart = true;

    private GameObject _panelRoot;
    private GameFeelController _controller;
    private EyeGazeRaycaster _gazeRaycaster;
    private XRHandSubsystem _handSubsystem;
    private bool _isVisible;
    private float _pinchThreshold = 0.02f;

    // 버튼: collider ID → action/visual
    private Dictionary<int, System.Action> _btnActions = new Dictionary<int, System.Action>();
    private Dictionary<int, GameObject> _btnQuads = new Dictionary<int, GameObject>();
    private Dictionary<int, Color> _btnColors = new Dictionary<int, Color>();
    private int _gazedBtnId = -1;
    private bool _prevPinch;

    public bool GameStarted { get; private set; } = false;

    void Start()
    {
        _controller = GameFeelController.Instance;
        if (_controller == null) return;

        _gazeRaycaster = FindFirstObjectByType<EyeGazeRaycaster>();
        BuildPanel();

        if (showOnStart)
        {
            Show();
            Debug.Log($"[GameFeelDebugPanel] Panel: {_btnActions.Count} buttons.");
        }
        else Hide();
    }

    void Update()
    {
        if (!_isVisible) return;

        bool pinch = DetectPinch();
        Collider gazedCol = _gazeRaycaster != null ? _gazeRaycaster.LastHitCollider : null;
        int gazedId = gazedCol != null ? gazedCol.GetInstanceID() : -1;

        // 아이게이즈로 하이라이트
        if (_btnActions.ContainsKey(gazedId))
        {
            if (gazedId != _gazedBtnId)
            {
                ResetHighlight(_gazedBtnId);
                _gazedBtnId = gazedId;
                SetHighlight(gazedId, true);
            }

            // 핀치 시작 → 실행
            if (pinch && !_prevPinch)
            {
                Flash(gazedId);
                _btnActions[gazedId]?.Invoke();
            }
        }
        else if (_gazedBtnId != -1)
        {
            ResetHighlight(_gazedBtnId);
            _gazedBtnId = -1;
        }

        _prevPinch = pinch;
    }

    // ── 패널 구성 ──

    private void BuildPanel()
    {
        _panelRoot = new GameObject("GameFeelPanel3D");
        _panelRoot.transform.SetParent(transform);

        int presetCount = _controller.presets != null ? _controller.presets.Length : 0;
        float btnW = 0.35f;
        float btnH = 0.05f;
        float gap = 0.015f;
        float totalH = (presetCount + 1) * (btnH + gap) + 0.08f;

        // 배경
        MakeQuad("BG", Vector3.zero,
            new Vector2(btnW + 0.08f, totalH + 0.02f),
            new Color(0.03f, 0.03f, 0.08f, 0.93f), false);

        float y = totalH / 2f - 0.04f;

        // 타이틀
        MakeText(new Vector3(0, y, -0.003f), "GAME FEEL", 0.007f, 60, Color.white);
        y -= 0.04f;

        // 프리셋 버튼들
        var presetDefs = new (string name, int index, Color color)[]
        {
            ("Gentle", 0, new Color(0.15f, 0.25f, 0.5f, 0.95f)),
            ("Bouncy", 1, new Color(0.5f, 0.3f, 0.15f, 0.95f)),
            ("Energetic", 2, new Color(0.5f, 0.15f, 0.15f, 0.95f)),
        };

        for (int i = 0; i < presetDefs.Length && i < presetCount; i++)
        {
            int idx = presetDefs[i].index;
            MakeButton(new Vector3(0, y, -0.003f), new Vector2(btnW, btnH),
                presetDefs[i].name, presetDefs[i].color,
                () => {
                    _controller.ApplyPreset(idx);
                    Debug.Log($"[GameFeel] Preset: {_controller.CurrentPresetName}");
                });
            y -= (btnH + gap);
        }

        // START 버튼
        y -= 0.01f;
        MakeButton(new Vector3(0, y, -0.003f), new Vector2(btnW, 0.06f),
            "START GAME", new Color(0.08f, 0.5f, 0.15f, 0.95f),
            () => {
                GameStarted = true;
                DestroyPanel();
                foreach (var s in FindObjectsByType<PlasticbagSpawner>(FindObjectsSortMode.None))
                    s.StartSpawning();
                LogAllValues();
                Debug.Log("[GameFeel] Game started!");
            });
    }

    private void LogAllValues()
    {
        var p = _controller.GetActivePreset();
        if (p == null) return;
        Debug.Log($"[GameFeel:Config] preset={p.displayName} " +
            $"bounce={p.bounceRestitution:F2} impMax={p.impulseMax:F3} " +
            $"upBias={p.upwardBias:F2} gravY={p.gravityY:F3} " +
            $"gravOff={p.gravityOffset:F2} vol={p.volumeMultiplier:F1}");
    }

    // ── 핀치 감지 ──

    private bool DetectPinch()
    {
        if (_handSubsystem == null || !_handSubsystem.running)
        {
            var subs = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subs);
            if (subs.Count > 0) _handSubsystem = subs[0];
        }
        if (_handSubsystem != null && _handSubsystem.running)
        {
            if (CheckPinch(_handSubsystem.leftHand)) return true;
            if (CheckPinch(_handSubsystem.rightHand)) return true;
        }
        return false;
    }

    private bool CheckPinch(XRHand hand)
    {
        if (!hand.isTracked) return false;
        var thumb = hand.GetJoint(XRHandJointID.ThumbTip);
        var index = hand.GetJoint(XRHandJointID.IndexTip);
        if (!thumb.TryGetPose(out Pose tp) || !index.TryGetPose(out Pose ip)) return false;
        return Vector3.Distance(tp.position, ip.position) < _pinchThreshold;
    }

    // ── 표시/숨김 ──

    public void Show()
    {
        _isVisible = true;
        if (_panelRoot != null) _panelRoot.SetActive(true);
        Camera cam = Camera.main;
        if (cam != null && _panelRoot != null)
        {
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0;
            fwd.Normalize();
            Vector3 pos = cam.transform.position + fwd * distanceFromCamera + Vector3.up * heightOffset;
            _panelRoot.transform.position = pos;
            _panelRoot.transform.rotation = Quaternion.LookRotation(fwd);
        }
    }

    public void Hide()
    {
        _isVisible = false;
        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    public void DestroyPanel()
    {
        _isVisible = false;
        if (_panelRoot != null) { Destroy(_panelRoot); _panelRoot = null; }
        _btnActions.Clear();
        _btnQuads.Clear();
        _btnColors.Clear();
    }

    // ── 버튼 ──

    private void MakeButton(Vector3 pos, Vector2 size, string label, Color color, System.Action action)
    {
        var quad = MakeQuad($"Btn_{label}", pos, size, color, true);
        MakeText(pos + new Vector3(0, 0, -0.002f), label, size.y * 0.12f, 45, Color.white);

        var col = quad.GetComponent<Collider>();
        if (col != null)
        {
            int id = col.GetInstanceID();
            _btnActions[id] = action;
            _btnQuads[id] = quad;
            _btnColors[id] = color;
        }
    }

    private void SetHighlight(int id, bool on)
    {
        if (!_btnQuads.ContainsKey(id)) return;
        var r = _btnQuads[id].GetComponent<Renderer>();
        if (r == null) return;
        Color c = on ? new Color(0.4f, 0.55f, 0.85f, 1f) : _btnColors[id];
        r.material.SetColor("_BaseColor", c);
        r.material.SetColor("_Color", c);
    }

    private void ResetHighlight(int id) => SetHighlight(id, false);

    private void Flash(int id)
    {
        if (!_btnQuads.ContainsKey(id)) return;
        var r = _btnQuads[id].GetComponent<Renderer>();
        if (r == null) return;
        r.material.SetColor("_BaseColor", new Color(0.1f, 0.9f, 0.3f, 1f));
        r.material.SetColor("_Color", new Color(0.1f, 0.9f, 0.3f, 1f));
    }

    // ── 3D UI ──

    private GameObject MakeQuad(string name, Vector3 localPos, Vector2 size, Color color, bool keepCollider)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = name;
        obj.transform.SetParent(_panelRoot.transform, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);

        if (!keepCollider)
            Destroy(obj.GetComponent<MeshCollider>());
        else
        {
            var col = obj.GetComponent<MeshCollider>();
            if (col != null) { col.convex = true; col.isTrigger = true; }
        }

        var rend = obj.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        rend.material = mat;
        return obj;
    }

    private void MakeText(Vector3 localPos, string text, float charSize, int fontSize, Color color)
    {
        var obj = new GameObject($"T_{text}");
        obj.transform.SetParent(_panelRoot.transform, false);
        obj.transform.localPosition = localPos;
        var tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = charSize;
        tm.fontSize = fontSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
    }
}
