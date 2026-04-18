using UnityEngine;

/// <summary>
/// 공이 Ground(바닥)에 닿을 때 바운스 높이를 제한합니다.
/// - 최대 바운스 높이: 10cm (수직 속도 상한)
/// - 바운스마다 높이 25% 감소 (공기 저항 시뮬레이션 — 속도 × sqrt(0.75) ≈ 0.866)
/// Rigidbody의 기본 물리 바운스를 무시하고 OnCollisionEnter에서 속도를 직접 제어.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BallBounceLimiter : MonoBehaviour
{
    [Header("바운스 제한")]
    [Tooltip("바닥에서 최대 바운스 높이 (m). 0 = 바운스 없음 (바닥에 안착)")]
    public float maxBounceHeight = 0f;

    [Tooltip("매 바운스마다 높이 감소 비율 (0.25 = 25% 감소, 즉 다음 바운스는 75%)")]
    [Range(0f, 1f)]
    public float bounceDecayPerHit = 1f;  // 1.0 = 완전 흡수 (maxBounceHeight=0과 중복 안전장치)

    [Header("감지 대상")]
    [Tooltip("바닥으로 간주할 태그 (Ground tag가 있는 오브젝트만)")]
    public string groundTag = "Ground";

    [Tooltip("태그가 없어도 이름이 'Ground'면 바닥으로 간주")]
    public bool matchNameGround = true;

    [Header("자동 파괴")]
    [Tooltip("Ground 닿으면 파괴할지 (false면 땅에 남아 있음)")]
    public bool destroyOnGround = false;

    [Tooltip("BallBox 내부(Floor) 닿으면 파괴할지. 현재는 BoxScoreZone이 처리하므로 false 기본값")]
    public bool destroyOnBoxFloor = false;

    [Tooltip("Ground 파괴 지연 (초)")]
    public float groundDestroyDelay = 0.6f;

    [Tooltip("Box Floor 파괴 지연 (초)")]
    public float boxDestroyDelay = 0.5f;

    [Header("점수")]
    [Tooltip("공이 Box에 들어갔을 때 추가 점수")]
    public int boxScore = 10;

    [HideInInspector]
    public PlasticbagSpawner spawner;  // 스포너가 자기 연결용

    private Rigidbody _rb;
    private bool _destroyScheduled = false;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        bool isGround = IsGround(collision.gameObject);
        bool isBoxFloor = IsBoxFloor(collision.gameObject);

        // Ground에서 바운스 속도 제한
        if (isGround && _rb != null)
        {
            Vector3 v = _rb.linearVelocity;
            float maxBounceSpeed = Mathf.Sqrt(2f * 9.81f * maxBounceHeight);

            if (v.y < 0f)
            {
                // 높이 h ∝ v² 이므로 높이 25% 감소 = v × sqrt(0.75)
                float retainFactor = Mathf.Sqrt(1f - bounceDecayPerHit);
                float bounceSpeed = Mathf.Min(-v.y * retainFactor, maxBounceSpeed);
                v.y = bounceSpeed;
                _rb.linearVelocity = v;
            }
            else if (v.y > maxBounceSpeed)
            {
                v.y = maxBounceSpeed;
                _rb.linearVelocity = v;
            }
        }

        // 파괴 처리 (다음 공 스폰 유도)
        if (_destroyScheduled) return;

        if (isGround && destroyOnGround)
        {
            ScheduleDestroy(groundDestroyDelay, addBoxScore: false);
        }
        else if (isBoxFloor && destroyOnBoxFloor)
        {
            ScheduleDestroy(boxDestroyDelay, addBoxScore: true);
        }
    }

    private void ScheduleDestroy(float delay, bool addBoxScore)
    {
        _destroyScheduled = true;

        // 상자 진입 시 점수 추가
        if (addBoxScore && boxScore > 0 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(boxScore, "Box");
        }

        // 스포너에 알려 카운트 감소
        if (spawner != null) spawner.OnBagDestroyed(gameObject);
        Destroy(gameObject, delay);
    }

    private bool IsGround(GameObject obj)
    {
        if (obj == null) return false;
        if (!string.IsNullOrEmpty(groundTag) && obj.CompareTag(groundTag)) return true;
        if (matchNameGround && obj.name == "Ground") return true;
        return false;
    }

    // BallBox/BallBox_Right의 Floor 자식인지 확인
    private bool IsBoxFloor(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.name != "Floor") return false;
        var parent = obj.transform.parent;
        if (parent == null) return false;
        return parent.name.StartsWith("BallBox");
    }
}
