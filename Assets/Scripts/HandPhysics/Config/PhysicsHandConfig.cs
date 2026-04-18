using UnityEngine;

namespace HandPhysics.Config
{
    /// <summary>
    /// ArticulationBody 기반 물리 손의 튜닝 파라미터.
    /// Inspector에서 실시간 조정 가능하며, 프리셋으로 저장/교체할 수 있습니다.
    ///
    /// 사용법: Assets > Create > HandPhysics > Physics Hand Config
    /// </summary>
    [CreateAssetMenu(fileName = "PhysicsHandConfig", menuName = "HandPhysics/Physics Hand Config")]
    public class PhysicsHandConfig : ScriptableObject
    {
        [Header("=== 질량 설정 ===")]

        [Tooltip("손바닥 ArticulationBody 질량 (kg)")]
        public float palmMass = 0.5f;

        [Tooltip("손가락 각 관절 ArticulationBody 질량 (kg)")]
        public float fingerBoneMass = 0.01f;

        [Header("=== 위치 Drive (손바닥 추종) ===")]

        [Tooltip("위치 Drive Stiffness — 높을수록 빠르게 추종하지만 진동 위험")]
        public float positionDriveStiffness = 50000f;

        [Tooltip("위치 Drive Damping — 높을수록 부드럽지만 느림")]
        public float positionDriveDamping = 100f;

        [Header("=== 회전 Drive (손가락/손목 추종) ===")]

        [Tooltip("회전 Drive Stiffness")]
        public float rotationDriveStiffness = 500f;

        [Tooltip("회전 Drive Damping")]
        public float rotationDriveDamping = 5f;

        [Header("=== 속도 제한 (물리 폭발 방지) ===")]

        [Tooltip("최대 선형 속도 (m/s) — HPTK Pheasy 참조")]
        public float maxLinearVelocity = 3.0f;

        [Tooltip("최대 각속도 (rad/s)")]
        public float maxAngularVelocity = 20.0f;

        [Tooltip("최대 관통 복구 속도 (m/s) — 낮을수록 안정적")]
        public float maxDepenetrationVelocity = 2.0f;

        [Header("=== 충돌 설정 ===")]

        [Tooltip("충돌 감지 모드 — ContinuousSpeculative이 모바일에서 가장 경량")]
        public CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousSpeculative;

        [Tooltip("손에 중력 적용 여부 — 일반적으로 false")]
        public bool useGravity = false;

        [Header("=== Collider 크기 ===")]

        [Tooltip("손바닥 SphereCollider 반지름 (m)")]
        public float palmColliderRadius = 0.04f;

        [Tooltip("손가락 끝 SphereCollider 반지름 (m)")]
        public float fingerTipColliderRadius = 0.015f;

        [Header("=== 노이즈 필터 ===")]

        [Tooltip("XR Hands 추적 노이즈 필터 강도 (0=필터 없음, 1=최대 스무딩)")]
        [Range(0f, 1f)]
        public float trackingSmoothing = 0.3f;

        [Tooltip("추적 신뢰도가 이 값 미만이면 물리 손 비활성화")]
        [Range(0f, 1f)]
        public float minTrackingConfidence = 0.5f;
    }
}
