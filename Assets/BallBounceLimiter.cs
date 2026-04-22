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
    [Header("손 충돌 튕김 (봉지 HandBounceResponder 대응)")]
    [Tooltip("이 속도 이상으로 손이 움직일 때만 튕김 발생 (m/s). 아래면 공이 안 튀어 → 잡기 가능")]
    public float minBounceHandSpeed = 0.5f;

    [Tooltip("손 속도 → 튕김 힘 배수")]
    public float bounceRestitution = 1.2f;

    [Tooltip("튕김 힘 최소값 (살짝 스쳐도 최소 이만큼)")]
    public float impulseMin = 0.012f;

    [Tooltip("튕김 힘 최대값")]
    public float impulseMax = 0.12f;

    [Range(0f, 1f)]
    [Tooltip("튕김 방향의 위쪽 가중치 (0=직선반사, 1=완전수직)")]
    public float upwardBias = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("중력 상쇄 비율 (0=그대로, 0.32=32% 부양)")]
    public float gravityOffset = 0.32f;

    [Tooltip("연속 충돌 쿨다운 (초) — 한 번 튕긴 직후 같은 손에 다시 반응 방지")]
    public float bounceCooldown = 0.08f;

    [Header("바운스 제한")]
    [Tooltip("공의 최대 선형 속도 (m/s)")]
    public float maxLinearSpeed = 3.0f;

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
    private BallTwoHandedGrab _grab;
    private bool _destroyScheduled = false;
    private float _lastBounceTime = -1f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _grab = GetComponent<BallTwoHandedGrab>();
    }

    // 매 FixedUpdate: 속도 상한 + 중력 상쇄 (봉지 gravityOffset 대응)
    void FixedUpdate()
    {
        if (_rb == null || _rb.isKinematic) return;

        if (gravityOffset > 0f && _rb.useGravity)
            _rb.AddForce(-Physics.gravity * gravityOffset * _rb.mass, ForceMode.Force);

        if (maxLinearSpeed > 0f && _rb.linearVelocity.magnitude > maxLinearSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxLinearSpeed;
    }

    // 손 콜라이더 여부 (HandColliderSetup가 태그 "PlayerHand" 부여)
    private bool IsHandCollider(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.CompareTag("PlayerHand")) return true;
        if (obj.name.StartsWith("HandCol_")) return true;
        if (obj.GetComponentInParent<HandColliderSetup>() != null) return true;
        return false;
    }

    // 봉지와 유사한 방식으로 손 충돌 시 위쪽 편향 + 속도 기반 impulse를 재부여
    private void ApplyHandBounce(GameObject handObj, Vector3 contactPoint, Vector3 contactNormal)
    {
        if (_rb == null || _rb.isKinematic) return;
        // 잡힌 상태면 튕기기 차단 (grab이 우선)
        if (_grab != null && _grab.IsGrabbed) return;
        if (Time.time - _lastBounceTime < bounceCooldown) return;

        float handSpeed = HandBounceResponder.TryGetPalmSpeed(handObj);
        if (handSpeed < 0f)
            handSpeed = HandColliderSetup.GetColliderSpeed(handObj.GetInstanceID());
        if (handSpeed < 0f) handSpeed = 0f;

        // 빠른 타격일 때만 튕김 발동. 느린 접근(= 잡기 시도)은 그냥 통과
        if (handSpeed < minBounceHandSpeed) return;

        _lastBounceTime = Time.time;

        Vector3 direction = (transform.position - contactPoint).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = contactNormal;
        direction = (direction + Vector3.up * upwardBias).normalized;

        float impulse = _rb.mass * handSpeed * bounceRestitution;
        impulse = Mathf.Clamp(impulse, impulseMin, impulseMax);

        // 기존 속도 초기화 후 봉지식 impulse 재적용 (natural push로 인한 고속 비행 억제)
        _rb.linearVelocity = Vector3.zero;
        _rb.AddForce(direction * impulse, ForceMode.Impulse);
    }

    void OnCollisionEnter(Collision collision)
    {
        // 손 충돌 → 봉지와 유사하게 위쪽 편향 + impulse 재부여
        if (IsHandCollider(collision.gameObject) && collision.contacts.Length > 0)
        {
            var c = collision.contacts[0];
            ApplyHandBounce(collision.gameObject, c.point, c.normal);
            return;
        }

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
