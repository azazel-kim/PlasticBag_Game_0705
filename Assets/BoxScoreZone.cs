using UnityEngine;

/// <summary>
/// 상자 내부에 Trigger BoxCollider로 배치해서 공이 진입하면 +N점 처리.
/// Kinematic 상태의 공도 Trigger 이벤트는 확실히 발동하므로 BallBounceLimiter의
/// OnCollisionEnter 방식보다 신뢰도 높습니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BoxScoreZone : MonoBehaviour
{
    [Header("점수")]
    public int scoreValue = 10;

    [Tooltip("공이 진입 후 파괴되는 지연 (초) — 시각적 확인 시간")]
    public float destroyDelay = 0.4f;

    [Header("디버그")]
    public bool verboseLog = true;

    // 중복 점수 방지용 공 목록
    private System.Collections.Generic.HashSet<GameObject> _alreadyScored
        = new System.Collections.Generic.HashSet<GameObject>();

    void Awake()
    {
        // Collider가 trigger인지 강제 설정
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[BoxScoreZone] {name}: Collider를 isTrigger=true로 자동 설정");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // 공인지 확인: BallBounceLimiter 컴포넌트가 있는지
        var limiter = other.GetComponentInParent<BallBounceLimiter>();
        if (limiter == null) return;

        GameObject ballRoot = limiter.gameObject;
        if (_alreadyScored.Contains(ballRoot)) return;
        _alreadyScored.Add(ballRoot);

        // 점수 추가
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(scoreValue, $"Box({gameObject.name})");

        if (verboseLog)
            Debug.Log($"[BoxScoreZone] 공 {ballRoot.name} 진입 → +{scoreValue}점");

        // 스포너에 알림 (카운트 감소 → 다음 공 스폰 가능)
        if (limiter.spawner != null)
            limiter.spawner.OnBagDestroyed(ballRoot);

        Destroy(ballRoot, destroyDelay);
    }
}
