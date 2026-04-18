using UnityEngine;
using HandPhysics.Core;
using HandPhysics.Input;

namespace HandPhysics.Debugging
{
    /// <summary>
    /// OpenXRHandDataProvider의 19-bone 데이터를 시각적으로 검증하는 디버그 도구.
    ///
    /// 기능:
    /// 1. 각 bone 위치에 작은 구체를 표시 (VR 내에서 직접 확인)
    /// 2. Grasp/Pinch lerp 값을 월드 텍스트로 표시
    /// 3. ADB logcat용 주기적 로그 출력
    ///
    /// 사용법:
    /// - 빈 GameObject에 이 스크립트 부착
    /// - handProvider에 OpenXRHandDataProvider 할당
    /// - 빌드 후 헤드셋에서 손을 움직이면 구체가 따라다님
    ///
    /// 테스트 완료 후 이 스크립트는 비활성화하거나 삭제해도 됩니다.
    /// </summary>
    public class HandDataDebugVisualizer : MonoBehaviour
    {
        [Header("대상")]
        [Tooltip("시각화할 손 데이터 제공자")]
        public OpenXRHandDataProvider handProvider;

        [Tooltip("Grasp 추정기 (있으면 lerp 값도 표시)")]
        public SimpleGraspEstimator graspEstimator;

        [Header("시각화 설정")]
        [Tooltip("bone 구체 크기 (m)")]
        public float sphereScale = 0.015f;

        [Tooltip("bone 구체 색상")]
        public Color boneColor = Color.cyan;

        [Tooltip("특수 bone(thumb tip, index tip) 색상")]
        public Color specialBoneColor = Color.yellow;

        [Header("로그 설정")]
        [Tooltip("ADB logcat용 로그 출력 간격 (초)")]
        public float logInterval = 3f;

        // bone 이름 (디버그 표시용)
        private static readonly string[] BoneNames = new string[]
        {
            "Wrist", "Forearm",
            "Thumb0", "Thumb1", "Thumb2", "ThumbTip",
            "Index1", "Index2", "IndexTip",
            "Mid1", "Mid2", "MidTip",
            "Ring1", "Ring2", "RingTip",
            "Pinky0", "Pinky1", "Pinky2", "PinkyTip"
        };

        // 특수 bone index (시각적으로 강조)
        private static readonly int[] SpecialBones = { 5, 8, 11, 14, 18 }; // 각 손가락 끝

        private GameObject[] _spheres;
        private GameObject[] _tipSpheres; // 실제 XR Hands Tip 관절 (5개)
        private Material _boneMat;
        private Material _specialMat;
        private float _lastLogTime;
        private static readonly string[] TipNames = { "ThumbTip", "IndexTip", "MiddleTip", "RingTip", "PinkyTip" };

        void Start()
        {
            if (handProvider == null)
            {
                UnityEngine.Debug.LogError("[HandDebug] handProvider가 할당되지 않았습니다!");
                enabled = false;
                return;
            }

            // URP Unlit 머티리얼 생성 (색상이 보이게)
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            _boneMat = new Material(shader);
            _boneMat.color = boneColor;

            _specialMat = new Material(shader);
            _specialMat.color = specialBoneColor;

            // 19개 bone에 대한 구체 생성
            _spheres = new GameObject[OpenXRHandDataProvider.BoneCount];

            for (int i = 0; i < OpenXRHandDataProvider.BoneCount; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"DebugBone_{handProvider.handSide}_{BoneNames[i]}";
                sphere.transform.localScale = Vector3.one * sphereScale;
                sphere.transform.SetParent(transform);

                // Collider 제거 (시각화 전용)
                var col = sphere.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // 특수 bone은 다른 색상
                bool isSpecial = System.Array.IndexOf(SpecialBones, i) >= 0;
                var renderer = sphere.GetComponent<Renderer>();
                renderer.material = isSpecial ? _specialMat : _boneMat;

                // 특수 bone은 약간 더 크게
                if (isSpecial)
                    sphere.transform.localScale = Vector3.one * sphereScale * 1.5f;

                sphere.SetActive(false);
                _spheres[i] = sphere;
            }

            // 실제 XR Hands Tip 관절 구체 5개 (노란색, 더 크게)
            _tipSpheres = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.name = $"DebugTip_{handProvider.handSide}_{TipNames[i]}";
                tip.transform.localScale = Vector3.one * sphereScale * 2f;
                tip.transform.SetParent(transform);

                var tipCol = tip.GetComponent<Collider>();
                if (tipCol != null) Destroy(tipCol);

                tip.GetComponent<Renderer>().material = _specialMat;
                tip.SetActive(false);
                _tipSpheres[i] = tip;
            }

            // 19-bone 배열의 기존 special bone은 일반 색상으로 변경 (혼동 방지)
            foreach (int idx in SpecialBones)
            {
                if (_spheres[idx] != null)
                    _spheres[idx].GetComponent<Renderer>().material = _boneMat;
            }

            UnityEngine.Debug.Log($"[HandDebug] {handProvider.handSide} 시각화 준비 완료. {_spheres.Length}개 bone + 5개 tip 구체 생성.");
        }

        void Update()
        {
            if (handProvider == null) return;

            // 각 bone 위치에 구체 이동
            for (int i = 0; i < OpenXRHandDataProvider.BoneCount; i++)
            {
                if (_spheres[i] == null) continue;

                if (handProvider.BoneValid[i])
                {
                    _spheres[i].SetActive(true);
                    _spheres[i].transform.position = handProvider.BonePositions[i];
                    _spheres[i].transform.rotation = handProvider.BoneRotations[i];
                }
                else
                {
                    _spheres[i].SetActive(false);
                }
            }

            // 실제 Tip 관절 구체 업데이트 (노란색)
            for (int i = 0; i < 5; i++)
            {
                if (_tipSpheres[i] == null) continue;

                if (handProvider.FingerTipValid[i])
                {
                    _tipSpheres[i].SetActive(true);
                    _tipSpheres[i].transform.position = handProvider.FingerTipPositions[i];
                }
                else
                {
                    _tipSpheres[i].SetActive(false);
                }
            }

            // 주기적 로그 출력 (ADB logcat으로 확인)
            if (Time.time - _lastLogTime > logInterval)
            {
                _lastLogTime = Time.time;
                LogStatus();
            }
        }

        private void LogStatus()
        {
            if (!handProvider.IsTracked)
            {
                UnityEngine.Debug.Log($"[HandDebug] {handProvider.handSide}: NOT TRACKED");
                return;
            }

            // 유효 bone 수 세기
            int validCount = 0;
            for (int i = 0; i < OpenXRHandDataProvider.BoneCount; i++)
            {
                if (handProvider.BoneValid[i]) validCount++;
            }

            string msg = $"[HandDebug] {handProvider.handSide}: " +
                $"tracked, conf={handProvider.Confidence:F2}, " +
                $"valid={validCount}/{OpenXRHandDataProvider.BoneCount}";

            // Wrist 위치
            if (handProvider.BoneValid[0])
                msg += $", wrist={handProvider.BonePositions[0]:F3}";

            // Grasp/Pinch lerp
            if (graspEstimator != null && graspEstimator.IsValid)
                msg += $", grasp={graspEstimator.GraspLerp:F2}, pinch={graspEstimator.PinchLerp:F2}";

            UnityEngine.Debug.Log(msg);
        }

        void OnDestroy()
        {
            // 정리
            if (_spheres != null)
            {
                foreach (var s in _spheres)
                {
                    if (s != null) Destroy(s);
                }
            }
            if (_tipSpheres != null)
            {
                foreach (var s in _tipSpheres)
                {
                    if (s != null) Destroy(s);
                }
            }
            if (_boneMat != null) Destroy(_boneMat);
            if (_specialMat != null) Destroy(_specialMat);
        }
    }
}
