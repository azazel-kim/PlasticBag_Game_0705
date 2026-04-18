using UnityEngine;
using TMPro;
using System.Collections.Generic;

// 손과 충돌 시 플라스틱백을 튕기고, 터치 횟수별 색상 변경 + 7회 터치 시 팝
public class HandBounceResponder : MonoBehaviour
{
    [Header("튕김 튜닝")]
    [Range(1f, 10f)]
    [Tooltip("손 속도 → 튕김 힘 배수 (클수록 세게 튕김)")]
    public float bounceRestitution = 3.9f;

    [Range(0.001f, 0.1f)]
    [Tooltip("튕김 힘 최소값 (살짝 스쳐도 최소 이만큼)")]
    public float impulseMin = 0.012f;

    [Range(0.1f, 2f)]
    [Tooltip("튕김 힘 최대값 (너무 멀리 날아가지 않도록 제한)")]
    public float impulseMax = 0.4f;

    [Range(0f, 1f)]
    [Tooltip("튕김 방향의 위쪽 가중치 (0=직선반사, 1=완전수직)")]
    public float upwardBias = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("중력 상쇄 비율 (0=전역중력 그대로, 0.32=32% 부양 ≈ 낙하속도 20% 감속)")]
    public float gravityOffset = 0.32f;

    [Header("비닐봉지 투명도")]
    [Range(0f, 1f)]
    [Tooltip("비닐봉지의 기본 투명도 (0=완전투명, 1=불투명)")]
    public float bagAlpha = 0.7f;

    [Header("터치별 색상 (7단계)")]
    [Tooltip("7회 터치 시 봉지가 터짐")]
    public int maxHits = 7;

    private Rigidbody rb;
    private float _lastCollisionTime = -1f;
    private float _cooldown = 0.1f;
    private Material _matInstance;
    private bool _isPopped = false;
    private bool _blendSwitched = false;

    // "현재 봉지에 붙어있는 손 콜라이더" 집합.
    // 손가락 19개가 동시에 붙을 수 있으므로 HashSet으로 관리.
    // Count==0 → Count>0 전환이 "새 히트 이벤트".
    private HashSet<GameObject> _activeHandTouches = new HashSet<GameObject>();

    // 점수 시스템 연동
    private int _hitCount = 0;
    public int HitCount => _hitCount;

    // 1회 회색 → 2회 노랑 → 3회 녹색 → 4회 청색 → 5회 보라 → 6회 주황 → 7회 빨강
    private static readonly Color[] HitColors = new Color[]
    {
        new Color(0.45f, 0.45f, 0.45f),  // 1회: 회색
        new Color(1.0f, 0.92f, 0.016f),  // 2회: 노랑
        new Color(0.2f, 0.8f, 0.2f),     // 3회: 녹색
        new Color(0.2f, 0.4f, 1.0f),     // 4회: 청색
        new Color(0.6f, 0.2f, 0.8f),     // 5회: 보라
        new Color(1.0f, 0.5f, 0.0f),     // 6회: 주황
        new Color(1.0f, 0.15f, 0.15f),   // 7회: 빨강
    };

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
            _matInstance = renderer.material;
    }

    void FixedUpdate()
    {
        // 중력 상쇄: gravityOffset만큼 전역 중력을 상쇄하여 둥실 뜨는 느낌 부여
        // 0 = 전역 중력 그대로 적용, 0.15 = 중력의 15%를 상쇄, 1.0 = 완전 무중력
        if (rb != null && !_isPopped)
            rb.AddForce(-Physics.gravity * gravityOffset * rb.mass, ForceMode.Force);
    }

    // 손 여부 판정 (공용)
    private bool IsHandCollider(GameObject obj, Rigidbody rbRef)
    {
        if (obj == null) return false;
        if (obj.CompareTag("PlayerHand")) return true;
        if (rbRef != null && rbRef.gameObject.CompareTag("PlayerHand")) return true;
        if (obj.name.Contains("Poke Interactor")) return true;
        if (obj.name.StartsWith("HandCol_")) return true;
        // 부모 계층에 HandColliderSetup 있으면 손 하위로 간주
        if (obj.GetComponentInParent<HandColliderSetup>() != null) return true;
        return false;
    }

    // 새 히트 이벤트 등록 (Count 0 → >0 전환일 때만 호출)
    private bool TryRegisterHit()
    {
        if (_isPopped) return false;
        if (Time.time - _lastCollisionTime < _cooldown) return false;

        _hitCount++;
        _lastCollisionTime = Time.time;
        HandleHit();
        return !_isPopped;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_isPopped || rb == null) return;
        if (!IsHandCollider(collision.gameObject, collision.rigidbody)) return;

        bool wasEmpty = _activeHandTouches.Count == 0;
        _activeHandTouches.Add(collision.gameObject);

        // 이미 다른 손 콜라이더가 붙어있으면 색/사운드 히트 건너뛰기 (떨어졌다 다시 닿을 때까지)
        if (!wasEmpty) return;

        if (TryRegisterHit())
        {
            float palmSpeed = TryGetPalmSpeed(collision.gameObject);
            float handSpeed = palmSpeed >= 0f ? palmSpeed : collision.relativeVelocity.magnitude;
            ApplyBounce(collision.contacts[0].point, collision.contacts[0].normal, handSpeed, collision.gameObject.name);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        _activeHandTouches.Remove(collision.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isPopped || rb == null) return;
        if (!IsHandCollider(other.gameObject, other.attachedRigidbody)) return;

        bool wasEmpty = _activeHandTouches.Count == 0;
        _activeHandTouches.Add(other.gameObject);

        if (!wasEmpty) return;

        if (!TryRegisterHit()) return;

        Vector3 direction = (transform.position - other.transform.position).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.up;
        direction = (direction + Vector3.up * upwardBias).normalized;

        // 팜 속도를 손 전체 대표 속도로 사용 (지터 억제).
        float palmSpeed = TryGetPalmSpeed(other.gameObject);
        float handSpeed = palmSpeed >= 0f
            ? palmSpeed
            : HandColliderSetup.GetColliderSpeed(other.gameObject.GetInstanceID());

        // 손 속도 반영: 속도가 빠를수록 세게 튕김
        float impulse = rb.mass * handSpeed * bounceRestitution;
        impulse = Mathf.Clamp(impulse, impulseMin, impulseMax);

        rb.linearVelocity = Vector3.zero;
        float pushDist = Mathf.Lerp(0.005f, 0.03f, Mathf.Clamp01(handSpeed / 3f));
        transform.position += direction * pushDist;
        rb.AddForce(direction * impulse, ForceMode.Impulse);

        Debug.Log($"[Bounce] hand={other.name}, palmSpeed={handSpeed:F2}, impulse={impulse:F4}, push={pushDist:F3}");
    }

    void OnTriggerExit(Collider other)
    {
        _activeHandTouches.Remove(other.gameObject);
    }

    // 충돌한 콜라이더로부터 "그 손의 Palm 속도"를 추출합니다.
    // 반환값이 음수면 매핑 실패 (HandColliderSetup 관리 외 오브젝트) → 호출자가 폴백 처리.
    // Palm을 손 전체 대표 속도로 쓰는 이유:
    //   - 손가락 tip은 XR 추적 지터로 단기 속도 스파이크 발생
    //   - Palm은 손 중심이라 상대적으로 안정적
    //   - "손을 얼마나 빠르게 휘둘렀냐"라는 물리적 직관과 일치
    public static float TryGetPalmSpeed(GameObject colliderObj)
    {
        if (colliderObj == null) return -1f;

        // Case 1: 이름이 "HandCol_Left_*" / "HandCol_Right_*" → 형제 Palm 콜라이더 탐색
        if (colliderObj.name.StartsWith("HandCol_"))
        {
            int firstUs = colliderObj.name.IndexOf('_');
            int secondUs = colliderObj.name.IndexOf('_', firstUs + 1);
            if (firstUs >= 0 && secondUs > firstUs && colliderObj.transform.parent != null)
            {
                string handPart = colliderObj.name.Substring(firstUs + 1, secondUs - firstUs - 1);
                Transform palm = colliderObj.transform.parent.Find($"HandCol_{handPart}_Palm");
                if (palm != null)
                    return HandColliderSetup.GetColliderSpeed(palm.gameObject.GetInstanceID());
            }
        }

        // Case 2: 부모 계층에 HandColliderSetup 있음 (Poke Interactor 등 XR Hand 자식)
        var setup = colliderObj.GetComponentInParent<HandColliderSetup>();
        if (setup != null)
        {
            Transform palm = setup.transform.Find($"HandCol_{setup.handedness}_Palm");
            if (palm != null)
                return HandColliderSetup.GetColliderSpeed(palm.gameObject.GetInstanceID());
        }

        return -1f;
    }

    private void HandleHit()
    {
        // 점수 시스템에 알림
        ScoreManager.Instance?.OnBagHit(_hitCount);

        if (_hitCount >= maxHits)
        {
            // 7회: 빨간색 적용 후 팝
            ApplyColor(HitColors[HitColors.Length - 1]);
            PopBag();
        }
        else
        {
            // 첫 터치부터 노란색 시작 (회색 건너뜀)
            // hit 1→index 1(노랑), hit 2→index 2(녹색)...
            int colorIndex = Mathf.Clamp(_hitCount, 0, HitColors.Length - 1);
            ApplyColor(HitColors[colorIndex]);
        }
    }

    // 첫 터치 시 Alpha Blend로 전환 + 텍스처 제거 → 색상이 바로 보임
    private void ApplyColor(Color color)
    {
        if (_matInstance == null) return;

        // 첫 터치: Multiply+텍스처 → Alpha Blend+흰색 텍스처로 전환
        if (!_blendSwitched)
        {
            _blendSwitched = true;
            _matInstance.SetFloat("_Surface", 1);  // Transparent
            _matInstance.SetFloat("_Blend", 0);    // Alpha blend
            _matInstance.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _matInstance.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _matInstance.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            _matInstance.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _matInstance.SetFloat("_ZWrite", 0);
            _matInstance.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _matInstance.EnableKeyword("_ALPHABLEND_ON");
            _matInstance.DisableKeyword("_ALPHAMULTIPLY_ON");
            _matInstance.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _matInstance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // 텍스처를 흰색으로 교체 → _BaseColor가 그대로 표면 색상이 됨
            _matInstance.SetTexture("_BaseMap", Texture2D.whiteTexture);
            _matInstance.SetTexture("_MainTex", Texture2D.whiteTexture);
        }

        Color c = new Color(color.r, color.g, color.b, bagAlpha);
        _matInstance.SetColor("_BaseColor", c);
        _matInstance.SetColor("_Color", c);
    }

    private void PopBag()
    {
        _isPopped = true;
        Vector3 bagPos = transform.position;

        // 1) 스포너 알림을 가장 먼저 (새 봉지 생성 보장)
        var bagScript = GetComponent<PlasticBag>();
        if (bagScript != null && bagScript.spawner != null)
            bagScript.spawner.OnBagDestroyed(gameObject);

        // 2) 이펙트 (실패해도 봉지는 반드시 파괴)
        try
        {
            SpawnScatterPieces(bagPos);
            ShowScorePopup(bagPos);
            PlayPopChime(bagPos);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PopBag] 이펙트 오류: {e.Message}");
        }

        Destroy(gameObject);
    }

    // 작은 구체를 흩뿌리는 팝 이펙트
    private void SpawnScatterPieces(Vector3 pos)
    {
        Color pieceColor = HitColors[HitColors.Length - 1]; // 빨강
        int count = 10;

        // URP용 Unlit 머티리얼 생성 (한번만)
        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpUnlit == null) urpUnlit = Shader.Find("Unlit/Color");
        Material pieceMat = new Material(urpUnlit);
        pieceMat.SetColor("_BaseColor", pieceColor);
        pieceMat.SetColor("_Color", pieceColor);

        for (int i = 0; i < count; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            piece.name = "PopPiece";
            piece.transform.position = pos + Random.insideUnitSphere * 0.05f;
            piece.transform.localScale = Vector3.one * Random.Range(0.02f, 0.04f); // 2~4cm

            // URP 머티리얼 적용
            var rend = piece.GetComponent<Renderer>();
            if (rend != null)
                rend.material = pieceMat;

            // 충돌 제거 + 물리
            Destroy(piece.GetComponent<Collider>());
            var pieceRb = piece.AddComponent<Rigidbody>();
            pieceRb.mass = 0.005f;
            pieceRb.useGravity = true;
            pieceRb.linearVelocity = Random.insideUnitSphere * 0.4f + Vector3.up * 0.25f;
            pieceRb.angularVelocity = Random.insideUnitSphere * 5f;

            Destroy(piece, 1.5f);
        }
    }

    private void ShowScorePopup(Vector3 pos)
    {
        GameObject popupObj = new GameObject("ScorePopup");
        popupObj.transform.position = pos + Vector3.up * 0.05f;

        TextMeshPro tmp = popupObj.AddComponent<TextMeshPro>();
        tmp.text = $"+{_hitCount * 5}";
        tmp.fontSize = 0.84f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0f, 0.9f, 1f, 1f); // 형광 파란색
        tmp.fontStyle = TMPro.FontStyles.Bold;

        Camera cam = Camera.main;
        if (cam != null)
            popupObj.transform.rotation = Quaternion.LookRotation(popupObj.transform.position - cam.transform.position);

        Destroy(popupObj, 1.5f);
    }

    // 하프 아르페지오 사운드 생성 (디리링~ 연속음)
    private void PlayPopChime(Vector3 pos)
    {
        int sampleRate = 22050;
        float duration = 1.2f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        // 하프 아르페지오: C5→E5→G5→C6→E6 순차 재생
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f };
        float noteDelay = 0.08f; // 각 음 사이 간격 (초)

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            for (int n = 0; n < notes.Length; n++)
            {
                float noteStart = n * noteDelay;
                float noteT = t - noteStart;
                if (noteT < 0f) continue;

                // 각 음의 감쇠 (하프처럼 천천히 줄어듦)
                float envelope = Mathf.Exp(-noteT * 3f) * Mathf.Max(0f, 1f - noteT * 0.5f);
                // 하프 음색: 기본음 + 2배음 + 3배음
                float wave = Mathf.Sin(2f * Mathf.PI * notes[n] * noteT) * 0.25f
                           + Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * noteT) * 0.1f
                           + Mathf.Sin(2f * Mathf.PI * notes[n] * 3f * noteT) * 0.05f;
                sample += wave * envelope;
            }

            data[i] = Mathf.Clamp(sample, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("HarpArpeggio", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        AudioSource.PlayClipAtPoint(clip, pos, 1.5f);
    }

    // 비닐봉지 바운스: 둥실둥실 가볍게 튕기는 느낌
    // handSpeed: 팜(또는 폴백) 속도 m/s — 호출자가 매핑해서 전달.
    private void ApplyBounce(Vector3 contactPoint, Vector3 contactNormal, float handSpeed, string sourceName)
    {
        Vector3 direction = (transform.position - contactPoint).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = contactNormal;

        // upwardBias: 위쪽 가중치 — 둥실 위로 뜨는 방향 보정
        direction = (direction + Vector3.up * upwardBias).normalized;

        // bounceRestitution: 손 속도 × 질량 × 반발계수 = 최종 튕김 힘
        float impulse = rb.mass * handSpeed * bounceRestitution;
        impulse = Mathf.Clamp(impulse, impulseMin, impulseMax);

        rb.AddForce(direction * impulse, ForceMode.Impulse);

        Debug.Log($"[Bounce] hand={sourceName}, palmSpeed={handSpeed:F2}, impulse={impulse:F4} (collision-path)");
    }
}
