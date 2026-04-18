using UnityEngine;

/// <summary>
/// 씬에 1개 배치. 프리셋을 선택하면 HandBounceResponder + Plasticbagsound + Physics.gravity를
/// 일괄 적용합��다. 외부 시스템(난이도, 센서)에서 ApplyPreset() 또는 BlendPresets()를 호출하여
/// 런타���에 게임 느낌을 전환할 수 있습니다.
/// </summary>
public class GameFeelController : MonoBehaviour
{
    // ── 싱글톤 ──
    public static GameFeelController Instance { get; private set; }

    [Header("프리셋 목록")]
    [Tooltip("비교 실험할 프리셋들을 등록합니다")]
    public GameFeelPreset[] presets;

    [Header("현재 프리셋")]
    [Tooltip("현재 활성 ��리셋 인덱스")]
    public int currentPresetIndex = 0;

    [Header("블렌드 (보간 모드)")]
    [Tooltip("true면 두 프리셋 사이를 blendRatio로 보간합니다")]
    public bool useBlend = false;

    [Tooltip("보간 대상 프리셋 A 인덱스")]
    public int blendIndexA = 0;

    [Tooltip("보간 대상 프리셋 B 인덱스")]
    public int blendIndexB = 1;

    [Range(0f, 1f)]
    [Tooltip("0=A 프리셋, 1=B 프리셋, 0.5=중간")]
    public float blendRatio = 0f;

    // 보간 결과를 담는 임시 프리셋 (런타임 생성)
    private GameFeelPreset _blendedPreset;

    // 씬 내 대상 컴포넌트 캐시
    private HandBounceResponder[] _bouncers;
    private Plasticbagsound[] _sounds;

    // 현재 적용된 프리셋 이름 (디버그 패널용)
    public string CurrentPresetName => GetActivePreset()?.displayName ?? "None";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 보간 결과 저장용 임시 ScriptableObject
        _blendedPreset = ScriptableObject.CreateInstance<GameFeelPreset>();
        _blendedPreset.displayName = "Blended";
    }

    void Start()
    {
        RefreshTargets();
        Debug.Log($"[GameFeel] 초기화: {_bouncers.Length}개 Bouncer, {_sounds.Length}개 Sound, {(presets != null ? presets.Length : 0)}개 프리셋");

        // 초기 프리셋 적용
        if (presets != null && presets.Length > 0)
            ApplyPreset(currentPresetIndex);
        else
            Debug.LogWarning("[GameFeel] 프리셋이 비어있습니다. Inspector에서 프리셋을 등록하세요.");
    }

    /// <summary>
    /// 씬 내 HandBounceResponder, Plasticbagsound를 다시 검색합니다.
    /// 봉지가 스폰/파괴될 때 호출하면 새 봉지에도 적용됩니다.
    /// </summary>
    public void RefreshTargets()
    {
        _bouncers = FindObjectsByType<HandBounceResponder>(FindObjectsSortMode.None);
        _sounds = FindObjectsByType<Plasticbagsound>(FindObjectsSortMode.None);
    }

    /// <summary>
    /// 인덱스로 프리셋을 즉시 적용합니다.
    /// </summary>
    public void ApplyPreset(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return;

        useBlend = false;
        currentPresetIndex = index;
        Apply(presets[index]);

        Debug.Log($"[GameFeel] 프리셋 적용: {presets[index].displayName}");
    }

    /// <summary>
    /// 두 프리셋 사이를 ratio로 보간하여 적용합니다.
    /// 플레이어 피로도, 심박수 등 연속 값으로 호출합니다.
    /// ratio: 0=A 프리셋, 1=B 프리셋
    /// </summary>
    public void BlendPresets(int indexA, int indexB, float ratio)
    {
        if (presets == null) return;
        if (indexA < 0 || indexA >= presets.Length) return;
        if (indexB < 0 || indexB >= presets.Length) return;

        useBlend = true;
        blendIndexA = indexA;
        blendIndexB = indexB;
        blendRatio = ratio;

        GameFeelPreset.Lerp(presets[indexA], presets[indexB], ratio, _blendedPreset);
        Apply(_blendedPreset);
    }

    /// <summary>
    /// 현재 활성 프리셋을 반환합니다.
    /// </summary>
    public GameFeelPreset GetActivePreset()
    {
        if (useBlend) return _blendedPreset;
        if (presets != null && currentPresetIndex >= 0 && currentPresetIndex < presets.Length)
            return presets[currentPresetIndex];
        return null;
    }

    /// <summary>
    /// 프리셋 값을 실제 컴포넌트에 적용하는 핵심 함수입니다.
    /// </summary>
    private void Apply(GameFeelPreset p)
    {
        if (p == null) return;

        // ── 1) 전역 중력 적용 ──
        Physics.gravity = new Vector3(0f, p.gravityY, 0f);

        // 대상 새로고침 (스폰된 봉지가 늘어날 수 있으므로)
        RefreshTargets();

        // ── 2) HandBounceResponder 파라미터 적용 ──
        foreach (var b in _bouncers)
        {
            if (b == null) continue;

            b.bounceRestitution = p.bounceRestitution;
            b.impulseMin = p.impulseMin;
            b.impulseMax = p.impulseMax;
            b.upwardBias = p.upwardBias;
            b.gravityOffset = p.gravityOffset;

            // Rigidbody 낙하 파라미터
            var rb = b.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearDamping = p.linearDrag;
                rb.mass = p.mass;
            }
        }

        // ── 3) Plasticbagsound 파라미터 적용 ──
        foreach (var s in _sounds)
        {
            if (s == null) continue;

            s.mediumImpactThreshold = p.mediumImpactThreshold;
            s.strongImpactThreshold = p.strongImpactThreshold;
            s.volumeMultiplier = p.volumeMultiplier;
        }
    }
}
