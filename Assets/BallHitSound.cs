using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공이 손과 접촉할 때 비닐 공 타격 사운드를 재생합니다.
///
/// 트리거 진입 기반 (공의 non-trigger 콜라이더가 사실상 비활성이므로 OnTriggerEnter로 감지).
///  - 약한 충돌 (palm 속도 < weakStrongThreshold): weakVolume (기본 0.1 = 원본 10%)
///  - 강한 충돌 (palm 속도 >= weakStrongThreshold): strongVolume (기본 0.4 = 원본 40%)
///
/// Inspector:
///  - hitSound에 freesound_community-fotballplast-92316.mp3를 할당
///  - 볼륨 / 임계값은 튜닝 가능
/// </summary>
[DisallowMultipleComponent]
public class BallHitSound : MonoBehaviour
{
    [Header("사운드 소스")]
    [Tooltip("비닐 공 타격 사운드 (예: freesound_community-fotballplast-92316.mp3)")]
    public AudioClip hitSound;

    [Header("볼륨 (원본 대비, 1 초과 시 증폭)")]
    [Range(0f, 5f)]
    [Tooltip("약한 충돌 볼륨. 1 초과 시 PlayOneShot이 증폭 재생 (Samsung XR 스피커 가청성 확보용)")]
    public float weakVolume = 0.1f;

    [Range(0f, 5f)]
    [Tooltip("강한 충돌 볼륨. 1 초과 시 증폭")]
    public float strongVolume = 1.0f;

    [Header("약/강 판정")]
    [Tooltip("palm 속도가 이 값 이상이면 '강한 충돌'로 판정 (m/s)")]
    public float weakStrongThreshold = 0.35f;

    [Header("소리 재생 안 할 조건")]
    [Tooltip("palm 속도가 이 값 미만이면 소리 안 남 (정지/매우 느린 접근)")]
    public float minSpeedForSound = 0.12f;

    [Tooltip("양손 잡기 진행 중(IsGrabbed=true)이면 소리 안 남")]
    public bool suppressWhenGrabbed = true;

    [Header("재생 제어")]
    [Tooltip("연속 재생 방지 쿨다운 (초)")]
    public float cooldown = 0.08f;

    [Tooltip("2D 사운드 (0) ↔ 3D 공간 사운드 (1)")]
    [Range(0f, 1f)]
    public float spatialBlend = 0f;

    // ─── 내부 ───
    private AudioSource _src;
    private BallTwoHandedGrab _grab;
    private float _lastPlay = -1f;
    // 중복 접촉 억제용 (봉지 Plasticbagsound와 동일 패턴)
    private readonly HashSet<GameObject> _activeTouches = new HashSet<GameObject>();

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        if (_src == null) _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = spatialBlend;
        _grab = GetComponent<BallTwoHandedGrab>();
    }

    private bool IsHand(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.CompareTag("PlayerHand")) return true;
        if (obj.name.StartsWith("HandCol_")) return true;
        if (obj.name.Contains("Poke Interactor")) return true;
        if (obj.GetComponentInParent<HandColliderSetup>() != null) return true;
        return false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hitSound == null || _src == null) return;
        if (!IsHand(other.gameObject)) return;

        // 양손 잡기 중이면 소리 스킵
        if (suppressWhenGrabbed && _grab != null && _grab.IsGrabbed) return;

        bool wasEmpty = _activeTouches.Count == 0;
        _activeTouches.Add(other.gameObject);
        // 이미 다른 손 콜라이더가 접촉 중이면 소리 중복 재생 억제
        if (!wasEmpty) return;

        if (Time.time - _lastPlay < cooldown) return;

        // 손 속도 (palm 대표 속도 우선, 없으면 해당 콜라이더 속도)
        float handSpeed = HandBounceResponder.TryGetPalmSpeed(other.gameObject);
        if (handSpeed < 0f) handSpeed = HandColliderSetup.GetColliderSpeed(other.gameObject.GetInstanceID());
        if (handSpeed < 0f) handSpeed = 0f;

        // 정지/매우 느린 손은 소리 없음
        if (handSpeed < minSpeedForSound) return;

        _lastPlay = Time.time;

        bool isStrong = handSpeed >= weakStrongThreshold;
        float vol = isStrong ? strongVolume : weakVolume;

        _src.pitch = 1f;
        _src.PlayOneShot(hitSound, vol);
        // 로그: 튜닝용 — 실제 수치 확인
        Debug.Log($"[BallHit] handSpeed={handSpeed:F3} m/s {(isStrong?"STRONG":"weak")} vol={vol:F3}");
    }

    void OnTriggerExit(Collider other)
    {
        _activeTouches.Remove(other.gameObject);
    }
}
