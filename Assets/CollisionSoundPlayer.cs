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
        // --- 로그 추가: OnCollisionEnter 함수 호출 확인 및 충돌 상대 정보 ---
        Debug.Log(gameObject.name + " 와(과) " + collision.gameObject.name + " (태그: " + collision.gameObject.tag + ") 충돌 발생!");

        // 쿨다운 시간 확인
        if (Time.time < lastSoundPlayTime + cooldownTime)
        {
            // --- 로그 추가: 쿨다운으로 인해 소리 재생 건너뜀 ---
            Debug.Log(gameObject.name + ": 쿨다운 시간 미경과로 소리 재생 건너뜀.");
            return;
        }

        // 충돌 세기 확인
        float impactForce = collision.relativeVelocity.magnitude;
        // --- 로그 추가: 충돌 세기 정보 ---
        Debug.Log(gameObject.name + ": 충돌 세기 = " + impactForce);

        if (impactForce < minImpactForceForSound)
        {
            // --- 로그 추가: 충돌 세기 미달로 소리 재생 건너뜀 ---
            Debug.Log(gameObject.name + ": 충돌 세기 미달(" + impactForce + " < " + minImpactForceForSound + ")로 소리 재생 건너뜀.");
            return;
        }

        AudioClip clipToPlay = null;

        if (collision.gameObject.CompareTag("Plasticbag"))
        {
            clipToPlay = plasticToPlasticSound;
            // --- 로그 추가: Plasticbag과 충돌 감지 ---
            Debug.Log(gameObject.name + ": Plasticbag과 충돌하여 plasticToPlasticSound 선택됨.");
        }
        else if (collision.gameObject.CompareTag("PlayerHand"))
        {
            clipToPlay = handToPlasticSound;
            // --- 로그 추가: PlayerHand와 충돌 감지 ---
            Debug.Log(gameObject.name + ": PlayerHand와 충돌하여 handToPlasticSound 선택됨.");
        }
        else
        {
            // --- 로그 추가: 정의되지 않은 태그와 충돌 ---
            Debug.Log(gameObject.name + ": 정의되지 않은 태그(" + collision.gameObject.tag + ")와 충돌하여 재생할 소리 없음.");
        }


        if (clipToPlay != null && audioSource != null)
        {
            audioSource.PlayOneShot(clipToPlay);
            lastSoundPlayTime = Time.time;
            // --- 로그 추가: 소리 재생 ---
            Debug.Log(gameObject.name + ": " + clipToPlay.name + " 소리 재생됨.");
        }
        else if (clipToPlay == null)
        {
            // --- 로그 추가: 재생할 오디오 클립이 없음 ---
            Debug.Log(gameObject.name + ": 재생할 오디오 클립이 할당되지 않음 (clipToPlay is null).");
        }
        else if (audioSource == null)
        {
            // --- 로그 추가: AudioSource가 없음 ---
            Debug.Log(gameObject.name + ": AudioSource가 할당되지 않음 (audioSource is null).");
        }
    }
}