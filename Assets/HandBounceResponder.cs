using UnityEngine;
using TMPro;

// 손과 충돌 시 플라스틱백을 튕기고, 터치 횟수별 색상 변경 + 7회 터치 시 팝
public class HandBounceResponder : MonoBehaviour
{
    [Tooltip("기본 튕김 힘")]
    public float bounceForce = 0f;

    [Tooltip("손 속도에 대한 민감도 배수")]
    public float velocitySensitivity = 0f;

    [Tooltip("최대 튕김 힘 (시야 밖으로 나가지 않도록 제한)")]
    public float maxBounceForce = 0f;

    [Header("비닐봉지 투명도")]
    [Range(0f, 1f)]
    [Tooltip("비닐봉지의 기본 투명도 (0=완전투명, 1=불투명)")]
    public float bagAlpha = 0.4f;

    [Header("터치별 색상 (7단계)")]
    [Tooltip("7회 터치 시 봉지가 터짐")]
    public int maxHits = 7;

    private Rigidbody rb;
    private float _lastCollisionTime = -1f;
    private float _cooldown = 0.1f;
    private Material _matInstance;
    private bool _isPopped = false;
    private bool _blendSwitched = false;

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
        // 추가 중력 -15% (기본 중력보다 가벼움 → 둥실 뜨는 느낌)
        if (rb != null && !_isPopped)
            rb.AddForce(-Physics.gravity * 0.15f * rb.mass, ForceMode.Force);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_isPopped || rb == null) return;
        if (Time.time - _lastCollisionTime < _cooldown) return;

        bool isHand = collision.gameObject.CompareTag("PlayerHand");
        if (!isHand && collision.rigidbody != null)
            isHand = collision.rigidbody.gameObject.CompareTag("PlayerHand");
        if (!isHand) return;

        _hitCount++;
        _lastCollisionTime = Time.time;
        HandleHit();
        if (!_isPopped)
            ApplyBounce(collision.contacts[0].point, collision.contacts[0].normal, collision.relativeVelocity);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isPopped || rb == null) return;
        if (Time.time - _lastCollisionTime < _cooldown) return;

        bool isHand = other.CompareTag("PlayerHand");
        if (!isHand && other.attachedRigidbody != null)
            isHand = other.attachedRigidbody.gameObject.CompareTag("PlayerHand");
        if (!isHand) return;

        _hitCount++;
        _lastCollisionTime = Time.time;
        HandleHit();

        if (!_isPopped)
        {
            Vector3 direction = (transform.position - other.transform.position).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.up;
            direction = (direction + Vector3.up * 0.35f).normalized;

            float impulse = Mathf.Clamp(rb.mass * 3.9f, 0.012f, 0.4f);
            rb.AddForce(direction * impulse, ForceMode.Impulse);
        }
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
    private void ApplyBounce(Vector3 contactPoint, Vector3 contactNormal, Vector3 relativeVelocity)
    {
        Vector3 direction = (transform.position - contactPoint).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = contactNormal;

        // 위쪽 35% 가중 — 둥실 위로 뜨는 느낌
        direction = (direction + Vector3.up * 0.35f).normalized;

        float handSpeed = relativeVelocity.magnitude;
        float restitution = 3.9f; // 3.0 * 1.3 = 30% 강화
        float impulse = rb.mass * handSpeed * restitution;
        impulse = Mathf.Clamp(impulse, 0.012f, 0.4f);

        rb.AddForce(direction * impulse, ForceMode.Impulse);
    }
}
