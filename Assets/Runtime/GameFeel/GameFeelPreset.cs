using UnityEngine;

/// <summary>
/// 게임 느낌(Game Feel)을 정��하는 데이터 카드.
/// Project 창에서 Create > GameFeel > Preset 으로 생성 가능.
/// 하나의 프리셋 = 튕김 + 낙하 + 사운드의 조합.
/// </summary>
[CreateAssetMenu(fileName = "NewGameFeelPreset", menuName = "GameFeel/Preset")]
public class GameFeelPreset : ScriptableObject
{
    [Header("프리셋 정보")]
    [Tooltip("프리셋 이름 (VR 디버그 패널에 표시)")]
    public string displayName = "New Preset";

    [TextArea(1, 2)]
    [Tooltip("이 프리셋의 느낌 설명")]
    public string description = "";

    // ── 튕김 파라미터 (HandBounceResponder에 적용) ──

    [Header("튕김 튜닝")]
    [Range(1f, 10f)]
    [Tooltip("손 속도 × 질량 × 이 값 = 튕김 힘")]
    public float bounceRestitution = 3.9f;

    [Range(0.001f, 0.1f)]
    [Tooltip("살짝 스쳐도 최소 이만큼 튕김")]
    public float impulseMin = 0.012f;

    [Range(0.1f, 2f)]
    [Tooltip("최대 튕김 힘 (시야 밖 방지)")]
    public float impulseMax = 0.4f;

    [Range(0f, 1f)]
    [Tooltip("위쪽 가중치 (0=수평반사, 1=수직상승)")]
    public float upwardBias = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("중력 상쇄 비율 (0=중력 그대로, 1=무중력)")]
    public float gravityOffset = 0.15f;

    // ── 낙하 파라미터 (Physics + Rigidbody에 적용) ──

    [Header("낙하 튜닝")]
    [Range(-9.81f, 0f)]
    [Tooltip("전역 중력 Y값 (기본 -9.81, 현재 -0.132)")]
    public float gravityY = -0.132f;

    [Range(0f, 5f)]
    [Tooltip("공기 저항 (높을수록 천천히 떨어짐)")]
    public float linearDrag = 0f;

    [Range(0.01f, 1f)]
    [Tooltip("봉지 질량 (튕김 impulse 계산에 영향)")]
    public float mass = 0.1f;

    // ── 사운드 파라미터 (Plasticbagsound에 적용) ──

    [Header("사운드 튜닝")]
    [Range(0.1f, 5f)]
    [Tooltip("중강도 소리 시작 임계값 (relativeVelocity)")]
    public float mediumImpactThreshold = 2f;

    [Range(1f, 20f)]
    [Tooltip("강타 소리 시작 임계값")]
    public float strongImpactThreshold = 10f;

    [Range(1f, 10f)]
    [Tooltip("볼륨 배수")]
    public float volumeMultiplier = 3f;

    /// <summary>
    /// 두 프리셋 사이를 보간하여 새 값을 반환합니다.
    /// t=0이면 a, t=1이면 b, 0.5면 중간값.
    /// 플레이어 피로도 등 연속 값에 따라 부드럽게 전환할 때 사용합니다.
    /// </summary>
    public static GameFeelPreset Lerp(GameFeelPreset a, GameFeelPreset b, float t, GameFeelPreset result)
    {
        t = Mathf.Clamp01(t);

        // 튕김
        result.bounceRestitution = Mathf.Lerp(a.bounceRestitution, b.bounceRestitution, t);
        result.impulseMin = Mathf.Lerp(a.impulseMin, b.impulseMin, t);
        result.impulseMax = Mathf.Lerp(a.impulseMax, b.impulseMax, t);
        result.upwardBias = Mathf.Lerp(a.upwardBias, b.upwardBias, t);
        result.gravityOffset = Mathf.Lerp(a.gravityOffset, b.gravityOffset, t);

        // 낙하
        result.gravityY = Mathf.Lerp(a.gravityY, b.gravityY, t);
        result.linearDrag = Mathf.Lerp(a.linearDrag, b.linearDrag, t);
        result.mass = Mathf.Lerp(a.mass, b.mass, t);

        // 사운드
        result.mediumImpactThreshold = Mathf.Lerp(a.mediumImpactThreshold, b.mediumImpactThreshold, t);
        result.strongImpactThreshold = Mathf.Lerp(a.strongImpactThreshold, b.strongImpactThreshold, t);
        result.volumeMultiplier = Mathf.Lerp(a.volumeMultiplier, b.volumeMultiplier, t);

        return result;
    }
}
