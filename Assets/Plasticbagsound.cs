using UnityEngine;
using System.Collections.Generic;

public class Plasticbagsound : MonoBehaviour
{
    private AudioSource audioSource;

    public AudioClip lightTouchSound;
    public AudioClip mediumImpactSound;
    public AudioClip strongImpactSound;

    public float mediumImpactThreshold = 0.4f;
    public float strongImpactThreshold = 1.5f;

    // 강도별 볼륨: Samsung XR 스피커 특성상 약한 충돌 가청성 확보 위해 light 비율을 medium보다 높게 설정.
    [Range(0f, 3f)]
    public float lightVolume = 1.6f;
    [Range(0f, 3f)]
    public float mediumVolume = 1.0f;
    [Range(0f, 10f)]
    public float strongVolume = 3.5f;

    // GameFeelController / GameFeelPreset이 단일 볼륨 배수로 제어하던 기존 API.
    // 새 구조에서는 "강타 기준 볼륨"으로 해석하고 약/중을 같은 비율로 스케일한다.
    // 비율: light 1.6 / medium 1.0 / strong 3.5 → light=strong×0.457, medium=strong×0.286
    // (약한 충돌 가청성 보장: 0.5 → 0.75 → 0.80 → 1.6  / light가 medium보다 크게 들리는 구조)
    public float volumeMultiplier
    {
        get { return strongVolume; }
        set
        {
            strongVolume = value;
            mediumVolume = value * (1.0f / 3.5f);
            lightVolume = value * (1.6f / 3.5f);
        }
    }

    private float lastPlayTime = -1f;
    private float soundCooldown = 0.15f;

    // 손 접촉 중복 방지용 HashSet (HandBounceResponder와 동일 패턴).
    // 여러 손가락 콜라이더가 동시에 닿아도 첫 진입에만 사운드 재생.
    private HashSet<GameObject> _activeHandTouches = new HashSet<GameObject>();

    private bool IsHandObj(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.CompareTag("PlayerHand")) return true;
        if (obj.name.Contains("Poke Interactor")) return true;
        if (obj.name.StartsWith("HandCol_")) return true;
        if (obj.GetComponentInParent<HandColliderSetup>() != null) return true;
        return false;
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // AudioSource 볼륨을 최대로
        audioSource.volume = 1f;
        audioSource.spatialBlend = 0f; // 2D 사운드로 확실히 들리게
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (audioSource == null) return;

        bool isHand = IsHandObj(collision.gameObject);
        if (isHand)
        {
            bool wasEmpty = _activeHandTouches.Count == 0;
            _activeHandTouches.Add(collision.gameObject);
            // 이미 다른 손 콜라이더 붙어있으면 소리 스킵
            if (!wasEmpty) return;
        }

        if (Time.time - lastPlayTime < soundCooldown) return;

        float palmSpeed = HandBounceResponder.TryGetPalmSpeed(collision.gameObject);
        float impactForce = palmSpeed >= 0f ? palmSpeed : collision.relativeVelocity.magnitude;
        string source = palmSpeed >= 0f ? $"{collision.gameObject.name}(palm)" : collision.gameObject.name;

        Debug.Log($"[PlasticBagSound] OnCollisionEnter: {source}, force={impactForce:F2}");
        PlaySoundByForce(impactForce);
    }

    private void OnCollisionExit(Collision collision)
    {
        _activeHandTouches.Remove(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (audioSource == null) return;

        bool isHand = IsHandObj(other.gameObject);
        if (isHand)
        {
            bool wasEmpty = _activeHandTouches.Count == 0;
            _activeHandTouches.Add(other.gameObject);
            if (!wasEmpty) return;
        }

        if (Time.time - lastPlayTime < soundCooldown) return;

        float palmSpeed = HandBounceResponder.TryGetPalmSpeed(other.gameObject);
        float impactForce;
        string source;
        if (palmSpeed >= 0f)
        {
            impactForce = palmSpeed;
            source = $"{other.gameObject.name}(palm)";
        }
        else
        {
            impactForce = 0f;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) impactForce = rb.linearVelocity.magnitude;
            source = other.gameObject.name;
        }

        Debug.Log($"[PlasticBagSound] OnTriggerEnter: {source}, force={impactForce:F2}");
        PlaySoundByForce(impactForce);
    }

    private void OnTriggerExit(Collider other)
    {
        _activeHandTouches.Remove(other.gameObject);
    }

    private void PlaySoundByForce(float impactForce)
    {
        AudioClip clipToPlay = null;
        float volume = lightVolume;
        string level = "light";

        if (impactForce >= strongImpactThreshold && strongImpactSound != null)
        {
            clipToPlay = strongImpactSound;
            volume = strongVolume;
            level = "strong";
        }
        else if (impactForce >= mediumImpactThreshold && mediumImpactSound != null)
        {
            clipToPlay = mediumImpactSound;
            volume = mediumVolume;
            level = "medium";
        }
        else if (lightTouchSound != null)
        {
            clipToPlay = lightTouchSound;
            volume = lightVolume;
            level = "light";
        }

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay, volume);
            lastPlayTime = Time.time;
            Debug.Log($"[PlasticBagSound] {level} force={impactForce:F2} vol={volume:F2} clip={clipToPlay.name}");
        }
        else
        {
            Debug.LogWarning("[PlasticBagSound] No clip assigned!");
        }
    }
}
