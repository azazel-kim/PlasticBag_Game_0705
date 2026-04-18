using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;
using HandPhysics.Core;

namespace HandPhysics.Input
{
    /// <summary>
    /// XR Hands API (26 관절) → HPTK 호환 19-bone 배열로 매핑하는 입력 브릿지.
    ///
    /// HPTK의 InputDataProvider를 직접 상속하지 않고 독립적으로 구현하여,
    /// HPTK 패키지 의존 없이 동일한 데이터 구조를 제공합니다.
    /// 추후 Phase 1에서 PhysicsHandBuilder가 이 데이터를 소비합니다.
    ///
    /// 19-bone 배열 구조:
    ///  [0] wrist, [1] forearm(합성),
    ///  [2-5] thumb0~3, [6-8] index1~3, [9-11] middle1~3,
    ///  [12-14] ring1~3, [15-18] pinky0~3
    /// </summary>
    public class OpenXRHandDataProvider : MonoBehaviour
    {
        /// <summary>총 bone 수 (HPTK 호환)</summary>
        public const int BoneCount = 19;

        /// <summary>forearm 합성 시 wrist 뒤쪽 오프셋 거리 (m)</summary>
        private const float ForearmOffset = 0.15f;

        [Header("설정")]
        [Tooltip("왼손/오른손")]
        public HandSide handSide = HandSide.Left;

        [Tooltip("노이즈 필터 강도 (0=없음, 1=최대 스무딩)")]
        [Range(0f, 1f)]
        public float smoothing = 0.3f;

        // --- 출력 데이터 (외부에서 읽기 전용) ---

        /// <summary>19개 bone의 월드 좌표 위치</summary>
        public Vector3[] BonePositions { get; private set; } = new Vector3[BoneCount];

        /// <summary>19개 bone의 월드 좌표 회전</summary>
        public Quaternion[] BoneRotations { get; private set; } = new Quaternion[BoneCount];

        /// <summary>각 bone이 유효한 추적 데이터를 가지고 있는지</summary>
        public bool[] BoneValid { get; private set; } = new bool[BoneCount];

        /// <summary>손 추적 중인지 (전체)</summary>
        public bool IsTracked { get; private set; }

        /// <summary>추적 신뢰도 (0~1)</summary>
        public float Confidence { get; private set; }

        /// <summary>
        /// 각 손가락 끝(Tip) 위치 — XR Hands의 실제 Tip 관절 사용.
        /// [0]=ThumbTip, [1]=IndexTip, [2]=MiddleTip, [3]=RingTip, [4]=LittleTip
        /// Grasp 추정에서 proximal-to-tip 거리 계산에 사용됩니다.
        /// </summary>
        public Vector3[] FingerTipPositions { get; private set; } = new Vector3[5];

        /// <summary>각 손가락 Tip 위치가 유효한지</summary>
        public bool[] FingerTipValid { get; private set; } = new bool[5];

        /// <summary>XR Hands Tip 관절 ID (손가락 순서)</summary>
        private static readonly XRHandJointID[] TipJointIds = new[]
        {
            XRHandJointID.ThumbTip,
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingTip,
            XRHandJointID.LittleTip
        };

        // --- 내부 상태 ---

        private XRHandSubsystem _handSubsystem;
        private bool _subsystemReady;

        // 이전 프레임 값 (스무딩용)
        private Vector3[] _prevPositions = new Vector3[BoneCount];
        private Quaternion[] _prevRotations = new Quaternion[BoneCount];
        private bool _hasPreviousFrame;

        /// <summary>
        /// XR Hands JointID → HPTK bone index 매핑 테이블.
        /// forearm(index 1)은 wrist에서 합성하므로 제외.
        /// </summary>
        private static readonly (int boneIndex, XRHandJointID jointId)[] JointMapping = new[]
        {
            (0,  XRHandJointID.Wrist),

            // Thumb: 4 bones (index 2-5)
            (2,  XRHandJointID.ThumbMetacarpal),
            (3,  XRHandJointID.ThumbProximal),
            (4,  XRHandJointID.ThumbDistal),
            (5,  XRHandJointID.ThumbTip),

            // Index: 3 bones (index 6-8) — metacarpal 생략, proximal부터
            (6,  XRHandJointID.IndexProximal),
            (7,  XRHandJointID.IndexIntermediate),
            (8,  XRHandJointID.IndexDistal),

            // Middle: 3 bones (index 9-11)
            (9,  XRHandJointID.MiddleProximal),
            (10, XRHandJointID.MiddleIntermediate),
            (11, XRHandJointID.MiddleDistal),

            // Ring: 3 bones (index 12-14)
            (12, XRHandJointID.RingProximal),
            (13, XRHandJointID.RingIntermediate),
            (14, XRHandJointID.RingDistal),

            // Pinky: 4 bones (index 15-18) — metacarpal 포함
            (15, XRHandJointID.LittleMetacarpal),
            (16, XRHandJointID.LittleProximal),
            (17, XRHandJointID.LittleIntermediate),
            (18, XRHandJointID.LittleDistal),
        };

        void Start()
        {
            // 회전 배열 초기화 (identity)
            for (int i = 0; i < BoneCount; i++)
            {
                BoneRotations[i] = Quaternion.identity;
                _prevRotations[i] = Quaternion.identity;
            }

            // XRHandSubsystem을 HandColliderSetup.cs와 동일한 패턴으로 획득
            Invoke(nameof(InitializeSubsystem), 1.0f);
        }

        private void InitializeSubsystem()
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);

            if (subsystems.Count == 0)
            {
                Debug.LogWarning($"[HandDataProvider] XRHandSubsystem 없음. 1초 후 재시도. ({handSide})");
                Invoke(nameof(InitializeSubsystem), 1.0f);
                return;
            }

            _handSubsystem = subsystems[0];
            _subsystemReady = true;
            Debug.Log($"[HandDataProvider] {handSide} 초기화 완료. XRHandSubsystem 연결됨.");
        }

        void Update()
        {
            if (!_subsystemReady || _handSubsystem == null || !_handSubsystem.running)
            {
                IsTracked = false;
                Confidence = 0f;
                return;
            }

            // 손 데이터 가져오기
            XRHand hand = (handSide == HandSide.Left)
                ? _handSubsystem.leftHand
                : _handSubsystem.rightHand;

            if (!hand.isTracked)
            {
                IsTracked = false;
                Confidence = 0f;
                return;
            }

            IsTracked = true;

            // 매핑 테이블 순회하여 bone 데이터 업데이트
            int validCount = 0;

            foreach (var (boneIndex, jointId) in JointMapping)
            {
                var joint = hand.GetJoint(jointId);
                if (joint.TryGetPose(out Pose pose))
                {
                    Vector3 pos = pose.position;
                    Quaternion rot = pose.rotation;

                    // 노이즈 필터 (exponential moving average)
                    if (_hasPreviousFrame && smoothing > 0f)
                    {
                        pos = Vector3.Lerp(pos, _prevPositions[boneIndex], smoothing);
                        rot = Quaternion.Slerp(rot, _prevRotations[boneIndex], smoothing);
                    }

                    BonePositions[boneIndex] = pos;
                    BoneRotations[boneIndex] = rot;
                    BoneValid[boneIndex] = true;

                    _prevPositions[boneIndex] = pos;
                    _prevRotations[boneIndex] = rot;

                    validCount++;
                }
                else
                {
                    BoneValid[boneIndex] = false;
                }
            }

            // 각 손가락 Tip 위치 업데이트 (XR Hands의 실제 Tip 관절)
            for (int i = 0; i < 5; i++)
            {
                var tipJoint = hand.GetJoint(TipJointIds[i]);
                if (tipJoint.TryGetPose(out Pose tipPose))
                {
                    FingerTipPositions[i] = tipPose.position;
                    FingerTipValid[i] = true;
                }
                else
                {
                    FingerTipValid[i] = false;
                }
            }

            // Forearm (index 1): wrist에서 뒤쪽으로 합성
            if (BoneValid[0]) // wrist가 유효하면
            {
                Vector3 wristPos = BonePositions[0];
                Quaternion wristRot = BoneRotations[0];

                // wrist의 뒤쪽 방향(-forward)으로 ForearmOffset만큼 이동
                BonePositions[1] = wristPos - (wristRot * Vector3.forward) * ForearmOffset;
                BoneRotations[1] = wristRot;
                BoneValid[1] = true;
                validCount++;
            }
            else
            {
                BoneValid[1] = false;
            }

            // 신뢰도 = 유효 bone 비율
            Confidence = (float)validCount / BoneCount;
            _hasPreviousFrame = true;
        }

        /// <summary>
        /// 특정 bone의 월드 Pose를 반환합니다.
        /// </summary>
        public bool TryGetBonePose(int boneIndex, out Pose pose)
        {
            if (boneIndex >= 0 && boneIndex < BoneCount && BoneValid[boneIndex])
            {
                pose = new Pose(BonePositions[boneIndex], BoneRotations[boneIndex]);
                return true;
            }
            pose = Pose.identity;
            return false;
        }

        /// <summary>
        /// 특정 손가락 끝(Tip)의 위치를 반환합니다.
        /// XR Hands의 실제 Tip 관절(ThumbTip, IndexTip 등)을 사용합니다.
        /// SimpleGraspEstimator에서 grasp/pinch 계산에 사용합니다.
        /// </summary>
        public bool TryGetFingerTipPosition(int fingerIndex, out Vector3 position)
        {
            // fingerIndex: 0=thumb, 1=index, 2=middle, 3=ring, 4=pinky
            if (fingerIndex >= 0 && fingerIndex < 5 && FingerTipValid[fingerIndex])
            {
                position = FingerTipPositions[fingerIndex];
                return true;
            }
            position = Vector3.zero;
            return false;
        }

        /// <summary>
        /// 특정 손가락 proximal(첫째 마디)의 위치를 반환합니다.
        /// Grasp 추정에서 손가락 curl 거리 계산에 사용합니다.
        /// </summary>
        public bool TryGetFingerProximalPosition(int fingerIndex, out Vector3 position)
        {
            // proximal bone index: thumb=2, index=6, middle=9, ring=12, pinky=16
            int boneIndex = fingerIndex switch
            {
                0 => 2,  // ThumbMetacarpal
                1 => 6,  // IndexProximal
                2 => 9,  // MiddleProximal
                3 => 12, // RingProximal
                4 => 16, // PinkyProximal (LittleProximal)
                _ => -1
            };

            if (boneIndex >= 0 && BoneValid[boneIndex])
            {
                position = BonePositions[boneIndex];
                return true;
            }
            position = Vector3.zero;
            return false;
        }
    }
}
