using UnityEngine;
using HandPhysics.Core;

namespace HandPhysics.Core
{
    /// <summary>
    /// 모든 인터랙터블 오브젝트의 MonoBehaviour 베이스 클래스.
    ///
    /// IInteractable 인터페이스의 기본 구현을 제공하며,
    /// 공통 기능(Rigidbody 캐싱, 오디오 재생, 이벤트 로깅)을 포함합니다.
    ///
    /// 구체적인 인터랙션 로직은 서브클래스에서 override합니다:
    /// - BounceInteractable (Phase 2)
    /// - SqueezeInteractable (Phase 2)
    /// - GrabPlaceInteractable (Phase 3)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Header("인터랙터블 공통 설정")]

        [Tooltip("이 오브젝트가 파괴될 때 ScoreManager에 보고할 점수")]
        public int baseScore = 5;

        [Tooltip("디버그 로그 출력 여부")]
        public bool debugLog = false;

        // --- 캐시된 컴포넌트 ---

        /// <summary>캐시된 Rigidbody — 서브클래스에서 자유롭게 접근</summary>
        protected Rigidbody Rb { get; private set; }

        /// <summary>캐시된 AudioSource (있으면) — 서브클래스에서 사운드 재생용</summary>
        protected AudioSource Audio { get; private set; }

        // --- 양손 상태 추적 ---

        /// <summary>왼손이 현재 Grasped 상태인지</summary>
        protected bool IsLeftGrasping { get; private set; }

        /// <summary>오른손이 현재 Grasped 상태인지</summary>
        protected bool IsRightGrasping { get; private set; }

        /// <summary>양손 모두 Grasped인지</summary>
        protected bool IsBimanualGrasping => IsLeftGrasping && IsRightGrasping;

        /// <summary>최소 한 손이 Grasped인지</summary>
        protected bool IsAnyHandGrasping => IsLeftGrasping || IsRightGrasping;

        // 양손 grasp 시 저장되는 ContactInfo
        private ContactInfo _leftGraspInfo;
        private ContactInfo _rightGraspInfo;

        // --- IInteractable 구현 ---

        public abstract InteractionType Type { get; }

        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Audio = GetComponent<AudioSource>();
        }

        // 기본 구현: 서브클래스에서 필요한 메서드만 override
        public virtual void OnHandEnter(ContactInfo info)
        {
            if (debugLog) Debug.Log($"[{Type}] OnHandEnter: {info.HandSide} at {info.HandPart}");
        }

        public virtual void OnHandTouch(ContactInfo info)
        {
            if (debugLog) Debug.Log($"[{Type}] OnHandTouch: {info.HandSide}, speed={info.RelativeSpeed:F3}");
        }

        public virtual void OnHandGrasp(ContactInfo info)
        {
            if (debugLog) Debug.Log($"[{Type}] OnHandGrasp: {info.HandSide}");

            // 양손 상태 추적
            if (info.HandSide == HandSide.Left)
            {
                IsLeftGrasping = true;
                _leftGraspInfo = info;
            }
            else
            {
                IsRightGrasping = true;
                _rightGraspInfo = info;
            }

            // 양손 동시 grasp 감지
            if (IsBimanualGrasping)
            {
                OnBimanualGrasp(_leftGraspInfo, _rightGraspInfo);
            }
        }

        public virtual void OnHandUngrasp(ContactInfo info)
        {
            if (debugLog) Debug.Log($"[{Type}] OnHandUngrasp: {info.HandSide}");

            bool wasBimanual = IsBimanualGrasping;

            if (info.HandSide == HandSide.Left)
                IsLeftGrasping = false;
            else
                IsRightGrasping = false;

            // 양손 → 한 손으로 전환 시 알림
            if (wasBimanual)
            {
                OnBimanualUngrasp(info.HandSide);
            }
        }

        public virtual void OnHandExit(ContactInfo info)
        {
            if (debugLog) Debug.Log($"[{Type}] OnHandExit: {info.HandSide}");
        }

        public virtual void OnBimanualGrasp(ContactInfo leftInfo, ContactInfo rightInfo)
        {
            if (debugLog) Debug.Log($"[{Type}] OnBimanualGrasp — 양손 동시 쥐기!");
        }

        public virtual void OnBimanualUngrasp(HandSide releasedSide)
        {
            if (debugLog) Debug.Log($"[{Type}] OnBimanualUngrasp — {releasedSide} 해제, 나머지 손 유지");
        }

        /// <summary>
        /// 간단한 사운드 재생 헬퍼.
        /// AudioSource가 없으면 무시합니다.
        /// </summary>
        protected void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (Audio != null && clip != null)
            {
                Audio.PlayOneShot(clip, volume);
            }
        }
    }
}
