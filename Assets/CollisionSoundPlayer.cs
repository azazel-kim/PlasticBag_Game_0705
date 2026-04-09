using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CollisionSoundPlayer : MonoBehaviour
{
    [Header("Audio Clips")]
    public AudioClip plasticToPlasticSound;
    public AudioClip handToPlasticSound;

    [Header("Collision Settings")]
    public float minImpactForceForSound = 0.1f;
    public float cooldownTime = 0.2f;

    private AudioSource audioSource;
    private float lastSoundPlayTime;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        lastSoundPlayTime = -cooldownTime;

        // --- 로그 추가: 스크립트 시작 확인 ---
        Debug.Log(gameObject.name + ": CollisionSoundPlayer 스크립트 시작됨. AudioSource 할당됨: " + (audioSource != null));
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Time.time < lastSoundPlayTime + cooldownTime) return;

        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce < minImpactForceForSound) return;

        PlayByTag(collision.gameObject.tag);
    }

    // 손의 Trigger Collider와 접촉 시 호출 (XRI 손은 isTrigger=true)
    void OnTriggerEnter(Collider other)
    {
        if (Time.time < lastSoundPlayTime + cooldownTime) return;

        PlayByTag(other.gameObject.tag);

        // 부모의 태그도 확인 (HandPhysicsCollider → Left Hand)
        if (other.attachedRigidbody != null)
        {
            PlayByTag(other.attachedRigidbody.gameObject.tag);
        }
    }

    // 태그에 따라 적절한 사운드 재생
    private void PlayByTag(string tag)
    {
        AudioClip clipToPlay = null;

        if (tag == "Plasticbag")
            clipToPlay = plasticToPlasticSound;
        else if (tag == "PlayerHand")
            clipToPlay = handToPlasticSound;

        if (clipToPlay != null && audioSource != null)
        {
            audioSource.PlayOneShot(clipToPlay);
            lastSoundPlayTime = Time.time;
        }
    }
}