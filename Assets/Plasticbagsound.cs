using UnityEngine;

public class Plasticbagsound : MonoBehaviour
{
    private AudioSource audioSource;

    public AudioClip lightTouchSound;
    public AudioClip mediumImpactSound;
    public AudioClip strongImpactSound;

    public float mediumImpactThreshold = 2f;
    public float strongImpactThreshold = 10f;

    // 볼륨 배수 (소리를 크게 하기 위함)
    [Range(1f, 10f)]
    public float volumeMultiplier = 3f;

    private float lastPlayTime = -1f;
    private float soundCooldown = 0.15f;

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
        if (Time.time - lastPlayTime < soundCooldown) return;

        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce < 0.01f) impactForce = mediumImpactThreshold;

        Debug.Log($"[PlasticBagSound] OnCollisionEnter: {collision.gameObject.name}, force={impactForce:F2}");
        PlaySoundByForce(impactForce);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (audioSource == null) return;
        if (Time.time - lastPlayTime < soundCooldown) return;

        Debug.Log($"[PlasticBagSound] OnTriggerEnter: {other.gameObject.name}");
        PlaySoundByForce(mediumImpactThreshold);
    }

    private void PlaySoundByForce(float impactForce)
    {
        AudioClip clipToPlay = null;

        if (impactForce >= strongImpactThreshold && strongImpactSound != null)
            clipToPlay = strongImpactSound;
        else if (impactForce >= mediumImpactThreshold && mediumImpactSound != null)
            clipToPlay = mediumImpactSound;
        else if (lightTouchSound != null)
            clipToPlay = lightTouchSound;

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay, volumeMultiplier);
            lastPlayTime = Time.time;
            Debug.Log($"[PlasticBagSound] Playing: {clipToPlay.name}, vol={volumeMultiplier}");
        }
        else
        {
            Debug.LogWarning("[PlasticBagSound] No clip assigned!");
        }
    }
}
