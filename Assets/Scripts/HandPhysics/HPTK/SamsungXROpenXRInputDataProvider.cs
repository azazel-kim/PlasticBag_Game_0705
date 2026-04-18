using UnityEngine;
using HandPhysicsToolkit.Input;
using HandPhysicsToolkit.Helpers;
// 'HPTK' 이름이 우리 namespace(HandPhysics.HPTK.Input)와 충돌하지 않도록 alias로 참조
using HPTKCore = HandPhysicsToolkit.HPTK;

namespace HandPhysics.HPTKBridge
{
    /// <summary>
    /// Samsung Galaxy XR(OpenXR + XR Hands)의 손 추적 데이터를 HPTK-Plus의
    /// InputDataProvider 포맷으로 전달하는 어댑터.
    ///
    /// 우리 프로젝트에 이미 존재하는 <see cref="global::HandPhysics.Input.OpenXRHandDataProvider"/>가
    /// XR Hands 26관절 → HPTK 호환 19-bone 배열 매핑을 완료한 상태이므로,
    /// 이 어댑터는 단순히 그 결과를 HPTK의 AbstractTsf[] bones에 복사합니다.
    ///
    /// 사용법:
    ///  - XR Origin 하위에 GameObject 생성
    ///  - OpenXRHandDataProvider + 이 스크립트를 같이 부착
    ///  - 씬에 HPTK singleton(HPTK.prefab)이 배치돼 있어야 trackingSpace 변환 가능
    /// </summary>
    [RequireComponent(typeof(global::HandPhysics.Input.OpenXRHandDataProvider))]
    public class SamsungXROpenXRInputDataProvider : InputDataProvider
    {
        [Header("데이터 소스")]
        [Tooltip("XR Hands → 19-bone 매핑을 수행하는 기존 제공자. 같은 GameObject에서 자동 검색.")]
        public global::HandPhysics.Input.OpenXRHandDataProvider source;

        [Header("스모크 테스트 디버그")]
        [Tooltip("매 프레임 bone 개수/신뢰도를 로그에 남길지")]
        public bool verboseLog = false;

        void Start()
        {
            // HPTK의 bones 배열(AbstractTsf[19])을 초기화
            InitData();

            // 소스 자동 할당
            if (source == null)
                source = GetComponent<global::HandPhysics.Input.OpenXRHandDataProvider>();

            // Side enum 동기화 — HPTK의 Side와 우리의 HandSide 매핑
            if (source != null)
            {
                side = source.handSide == global::HandPhysics.Core.HandSide.Left
                    ? Side.Left
                    : Side.Right;
            }
        }

        void Update()
        {
            // HPTK 매니저가 직접 UpdateData를 호출하는 경우도 있지만,
            // 스모크 테스트 단계에서는 자체 Update 루프로 호출.
            UpdateData();
        }

        public override void UpdateData()
        {
            base.UpdateData();

            if (source == null || !source.IsTracked)
            {
                confidence = 0f;
                log = source == null ? "[HPTK-Bridge] source=NULL" : "[HPTK-Bridge] not tracked";
                return;
            }

            // HPTK.core.trackingSpace가 있으면 world 좌표를 트래킹 스페이스로 보정
            // (UnityXRControllerTracker와 동일한 패턴)
            Transform trackingSpace = HPTKCore.core != null ? HPTKCore.core.trackingSpace : null;

            int copied = 0;
            for (int i = 0; i < 19; i++)
            {
                if (!source.BoneValid[i]) continue;

                Vector3 pos = source.BonePositions[i];
                Quaternion rot = source.BoneRotations[i];

                if (trackingSpace != null)
                {
                    pos = trackingSpace.TransformPoint(pos);
                    rot = trackingSpace.rotation * rot;
                }

                bones[i].space = Space.World;
                bones[i].position = pos;
                bones[i].rotation = rot;
                copied++;
            }

            // HPTK가 FingerPose(thumb/index/middle/ring/pinky)로도 접근하므로 동기화
            UpdateFingerPosesFromBones();

            confidence = source.Confidence;

            if (verboseLog)
                log = $"[HPTK-Bridge] {side} copied={copied}/19 conf={confidence:F2}";
        }
    }
}
