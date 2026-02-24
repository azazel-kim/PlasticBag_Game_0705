using UnityEngine;

public class Plasticbagsound : MonoBehaviour
{
    private AudioSource audioSource;

    // 인스펙터에서 할당할 오디오 클립들
    public AudioClip lightTouchSound;    // 약한 충돌 시 재생될 소리 (기존 소리 또는 새로운 약한 소리)
    public AudioClip mediumImpactSound;   // 중간 세기 충돌 시 재생될 소리 (Light pounding sound)
    public AudioClip strongImpactSound;   // 강한 세기 충돌 시 재생될 소리 (Strong pounding sound)

    // 충돌 세기 임계값 (인스펙터에서 조절 가능)
    // 이 값들은 테스트를 통해 적절히 조절해야 합니다.
    public float mediumImpactThreshold = 2f; // 이 값 이상이면 중간 충돌로 간주
    public float strongImpactThreshold = 10f; // 이 값 이상이면 강한 충돌로 간주

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        // AudioSource 컴포넌트가 없다면 추가하고, 기본 설정을 할 수 있습니다.
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            // 예: audioSource.playOnAwake = false;
        }
    }

    // Update는 현재 사용하지 않으므로 비워두거나 삭제해도 됩니다.
    // void Update()
    // {
    // }

    private void OnCollisionEnter(Collision collision)
    {
        if (audioSource == null) return; // AudioSource가 없으면 아무것도 하지 않음

        // 충돌 상대 속도의 크기로 충격의 세기를 판단합니다.
        // collision.impulse.magnitude / Time.fixedDeltaTime; 를 사용할 수도 있습니다. (더 물리 기반)
        float impactForce = collision.relativeVelocity.magnitude;

        AudioClip clipToPlay = null;

        // 충돌 세기에 따라 재생할 클립 결정
        if (impactForce >= strongImpactThreshold && strongImpactSound != null)
        {
            clipToPlay = strongImpactSound;
            Debug.Log("Strong impact detected. Force: " + impactForce);
        }
        else if (impactForce >= mediumImpactThreshold && mediumImpactSound != null)
        {
            clipToPlay = mediumImpactSound;
            Debug.Log("Medium impact detected. Force: " + impactForce);
        }
        else if (impactForce > 0 && lightTouchSound != null) // 0보다 큰 경우에만 약한 충돌로 간주 (아주 미세한 접촉은 무시)
        {
            clipToPlay = lightTouchSound;
            Debug.Log("Light impact detected. Force: " + impactForce);
        }

        // 선택된 클립이 있고, AudioSource가 준비되었다면 소리 재생
        if (clipToPlay != null)
        {
            // PlayOneShot은 현재 재생 중인 소리를 멈추지 않고 새 소리를 중첩해서 재생합니다.
            // 짧은 효과음에 적합합니다.
            audioSource.PlayOneShot(clipToPlay);

            // 만약 이전 소리를 멈추고 새 소리만 재생하고 싶다면 아래와 같이 사용합니다.
            // audioSource.clip = clipToPlay;
            // audioSource.Play();
        }
    }
}
