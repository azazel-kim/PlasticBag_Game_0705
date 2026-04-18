using UnityEngine;
using HandPhysics.Config;
using HandPhysics.Input;

namespace HandPhysics.Core
{
    /// <summary>
    /// 물리 기반 손 시스템의 오케스트레이터.
    ///
    /// 역할:
    /// 1. 양손 OpenXRHandDataProvider + SimpleGraspEstimator 관리
    /// 2. 레거시 시스템(HandColliderSetup)과의 안전한 공존/전환
    /// 3. 디버그 정보 출력
    ///
    /// Phase 0에서는 데이터 제공자만 관리합니다.
    /// Phase 1에서 PhysicsHandBuilder를 여기에 연결합니다.
    ///
    /// 사용법: XR Origin 또는 별도 Manager 오브젝트에 부착.
    /// </summary>
    public class PhysicsHandManager : MonoBehaviour
    {
        [Header("=== 물리 손 설정 ===")]

        [Tooltip("물리 손 시스템 활성화 여부. false면 레거시 시스템만 사용.")]
        public bool usePhysicsHands = true;

        [Tooltip("레거시 HandColliderSetup도 동시에 사용할지. Phase 0~1에서는 true 권장.")]
        public bool keepLegacyColliders = true;

        [Tooltip("물리 손 설정 ScriptableObject")]
        public PhysicsHandConfig config;

        [Header("=== 데이터 제공자 (자동 감지 또는 수동 할당) ===")]

        [Tooltip("왼손 데이터 제공자. 비어있으면 자식에서 자동 검색.")]
        public OpenXRHandDataProvider leftHandProvider;

        [Tooltip("오른손 데이터 제공자. 비어있으면 자식에서 자동 검색.")]
        public OpenXRHandDataProvider rightHandProvider;

        [Header("=== Grasp 추정기 ===")]

        public SimpleGraspEstimator leftGraspEstimator;
        public SimpleGraspEstimator rightGraspEstimator;

        [Header("=== 디버그 ===")]

        [Tooltip("콘솔에 추적 상태 로그 출력 (성능 영향 있음)")]
        public bool debugLog = false;

        [Tooltip("디버그 로그 출력 간격 (초)")]
        public float debugLogInterval = 2f;

        // --- 싱글턴 (선택적) ---
        // 씬에 하나만 존재해야 하지만, 엄격한 싱글턴 패턴은 사용하지 않습니다.
        // 다른 스크립트에서 FindFirstObjectByType<PhysicsHandManager>()로 접근하세요.

        /// <summary>왼손 추적 중인지</summary>
        public bool IsLeftHandTracked => leftHandProvider != null && leftHandProvider.IsTracked;

        /// <summary>오른손 추적 중인지</summary>
        public bool IsRightHandTracked => rightHandProvider != null && rightHandProvider.IsTracked;

        /// <summary>왼손 grasp lerp (0~1)</summary>
        public float LeftGraspLerp => leftGraspEstimator != null ? leftGraspEstimator.GraspLerp : 0f;

        /// <summary>오른손 grasp lerp (0~1)</summary>
        public float RightGraspLerp => rightGraspEstimator != null ? rightGraspEstimator.GraspLerp : 0f;

        /// <summary>왼손 pinch lerp (0~1)</summary>
        public float LeftPinchLerp => leftGraspEstimator != null ? leftGraspEstimator.PinchLerp : 0f;

        /// <summary>오른손 pinch lerp (0~1)</summary>
        public float RightPinchLerp => rightGraspEstimator != null ? rightGraspEstimator.PinchLerp : 0f;

        private float _lastDebugTime;

        void Awake()
        {
            // 자동 검색: 할당되지 않은 제공자를 자식에서 찾기
            if (leftHandProvider == null || rightHandProvider == null)
            {
                var providers = GetComponentsInChildren<OpenXRHandDataProvider>(true);
                foreach (var p in providers)
                {
                    if (p.handSide == HandSide.Left && leftHandProvider == null)
                        leftHandProvider = p;
                    else if (p.handSide == HandSide.Right && rightHandProvider == null)
                        rightHandProvider = p;
                }
            }

            // Grasp 추정기 자동 검색
            if (leftGraspEstimator == null && leftHandProvider != null)
                leftGraspEstimator = leftHandProvider.GetComponent<SimpleGraspEstimator>();
            if (rightGraspEstimator == null && rightHandProvider != null)
                rightGraspEstimator = rightHandProvider.GetComponent<SimpleGraspEstimator>();
        }

        void Start()
        {
            // config가 없으면 기본값 생성 (런타임에서만)
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PhysicsHandConfig>();
                Debug.LogWarning("[PhysicsHandManager] PhysicsHandConfig 미할당. 런타임 기본값 사용 중.");
            }

            // 데이터 제공자에 스무딩 값 동기화
            if (leftHandProvider != null)
                leftHandProvider.smoothing = config.trackingSmoothing;
            if (rightHandProvider != null)
                rightHandProvider.smoothing = config.trackingSmoothing;

            Debug.Log($"[PhysicsHandManager] 초기화 완료. " +
                $"usePhysicsHands={usePhysicsHands}, keepLegacy={keepLegacyColliders}, " +
                $"leftProvider={leftHandProvider != null}, rightProvider={rightHandProvider != null}");
        }

        void Update()
        {
            if (!usePhysicsHands) return;

            // 디버그 로그
            if (debugLog && Time.time - _lastDebugTime > debugLogInterval)
            {
                _lastDebugTime = Time.time;
                LogDebugStatus();
            }
        }

        /// <summary>
        /// 레거시 HandColliderSetup 시스템을 활성화/비활성화합니다.
        /// Phase 1 완료 후 비활성화할 때 사용합니다.
        /// 기존 HandColliderSetup을 직접 수정하지 않고, 활성 상태만 제어합니다.
        /// </summary>
        public void SetLegacyCollidersActive(bool active)
        {
            keepLegacyColliders = active;

            // 씬에서 HandColliderSetup 찾아서 활성/비활성
            var legacySetups = FindObjectsByType<HandColliderSetup>(FindObjectsSortMode.None);
            foreach (var setup in legacySetups)
            {
                setup.enabled = active;
                if (debugLog)
                    Debug.Log($"[PhysicsHandManager] Legacy HandColliderSetup ({setup.handedness}) → {(active ? "활성" : "비활성")}");
            }
        }

        /// <summary>
        /// 특정 손의 데이터 제공자를 반환합니다.
        /// </summary>
        public OpenXRHandDataProvider GetProvider(HandSide side)
        {
            return side == HandSide.Left ? leftHandProvider : rightHandProvider;
        }

        /// <summary>
        /// 특정 손의 grasp 추정기를 반환합니다.
        /// </summary>
        public SimpleGraspEstimator GetGraspEstimator(HandSide side)
        {
            return side == HandSide.Left ? leftGraspEstimator : rightGraspEstimator;
        }

        private void LogDebugStatus()
        {
            string left = leftHandProvider != null && leftHandProvider.IsTracked
                ? $"L: tracked, conf={leftHandProvider.Confidence:F2}, grasp={LeftGraspLerp:F2}, pinch={LeftPinchLerp:F2}"
                : "L: not tracked";

            string right = rightHandProvider != null && rightHandProvider.IsTracked
                ? $"R: tracked, conf={rightHandProvider.Confidence:F2}, grasp={RightGraspLerp:F2}, pinch={RightPinchLerp:F2}"
                : "R: not tracked";

            Debug.Log($"[PhysicsHandManager] {left} | {right}");
        }
    }
}
