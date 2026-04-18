using UnityEngine;
using HandPhysics.Core;

namespace HandPhysics.Input
{
    /// <summary>
    /// 손가락 curl 거리 기반으로 grasp/pinch 정도를 0~1로 추정합니다.
    ///
    /// HPTK의 GestureDetection 전체를 가져오지 않고,
    /// Flex 제스처와 동일한 원리(proximal-to-tip 거리 정규화)를 독립 구현합니다.
    ///
    /// 사용법: OpenXRHandDataProvider와 같은 GameObject에 부착.
    /// 매 프레임 graspLerp, pinchLerp가 업데이트됩니다.
    /// </summary>
    [RequireComponent(typeof(OpenXRHandDataProvider))]
    public class SimpleGraspEstimator : MonoBehaviour
    {
        [Header("Grasp 임계값")]

        [Tooltip("손가락이 완전히 펼쳤을 때의 proximal-to-tip 거리 (m). XR Hands Tip 관절 기준.")]
        public float fingerOpenDistance = 0.09f;

        [Tooltip("손가락이 완전히 접혔을 때의 proximal-to-tip 거리 (m). XR Hands Tip 관절 기준.")]
        public float fingerClosedDistance = 0.02f;

        [Header("Pinch 임계값")]

        [Tooltip("Pinch가 완전히 열렸을 때의 thumb-index tip 거리 (m)")]
        public float pinchOpenDistance = 0.10f;

        [Tooltip("Pinch가 완전히 닫혔을 때의 thumb-index tip 거리 (m)")]
        public float pinchClosedDistance = 0.015f;

        [Header("스무딩")]

        [Tooltip("lerp 값 변화 스무딩 속도 (높을수록 빠르게 반응)")]
        public float lerpSpeed = 12f;

        // --- 출력 (외부에서 읽기 전용) ---

        /// <summary>
        /// 전체 손 쥐기 정도 (0=완전히 펼침, 1=주먹).
        /// index~pinky 4개 손가락의 curl 평균입니다.
        /// Squeeze 인터랙션에서 squeezeProgress로 사용됩니다.
        /// </summary>
        public float GraspLerp { get; private set; }

        /// <summary>
        /// 엄지-검지 핀치 정도 (0=벌림, 1=맞닿음).
        /// Grab 인터랙션의 시작 조건으로 사용할 수 있습니다.
        /// </summary>
        public float PinchLerp { get; private set; }

        /// <summary>
        /// 각 손가락별 curl 정도 (0=펼침, 1=접힘).
        /// [0]=thumb, [1]=index, [2]=middle, [3]=ring, [4]=pinky
        /// </summary>
        public float[] FingerCurls { get; private set; } = new float[5];

        /// <summary>유효한 추정값인지 (손 추적 중일 때만 true)</summary>
        public bool IsValid { get; private set; }

        // --- 내부 ---

        private OpenXRHandDataProvider _dataProvider;
        private float _targetGraspLerp;
        private float _targetPinchLerp;
        private float _calibrationLogTimer;
        private float[] _rawDistances = new float[5];

        void Awake()
        {
            _dataProvider = GetComponent<OpenXRHandDataProvider>();
        }

        void Update()
        {
            if (_dataProvider == null || !_dataProvider.IsTracked)
            {
                IsValid = false;
                return;
            }

            IsValid = true;
            float curlSum = 0f;
            int validFingers = 0;

            // 각 손가락 curl 계산 (index=1 ~ pinky=4, thumb=0은 별도)
            for (int finger = 0; finger < 5; finger++)
            {
                if (_dataProvider.TryGetFingerProximalPosition(finger, out Vector3 proximal) &&
                    _dataProvider.TryGetFingerTipPosition(finger, out Vector3 tip))
                {
                    float dist = Vector3.Distance(proximal, tip);

                    // HPTK Flex 제스처와 동일: 1 - InverseLerp(closed, open, dist)
                    float curl = 1f - Mathf.InverseLerp(fingerClosedDistance, fingerOpenDistance, dist);
                    curl = Mathf.Clamp01(curl);

                    FingerCurls[finger] = curl;
                    _rawDistances[finger] = dist;

                    // Grasp에는 thumb 제외 (index~pinky만 평균)
                    if (finger >= 1)
                    {
                        curlSum += curl;
                        validFingers++;
                    }
                }
            }

            // Grasp lerp = index~pinky 평균
            if (validFingers > 0)
            {
                _targetGraspLerp = curlSum / validFingers;
            }

            // Pinch lerp = thumb tip ↔ index tip 거리
            if (_dataProvider.TryGetFingerTipPosition(0, out Vector3 thumbTip) &&
                _dataProvider.TryGetFingerTipPosition(1, out Vector3 indexTip))
            {
                float pinchDist = Vector3.Distance(thumbTip, indexTip);
                _targetPinchLerp = 1f - Mathf.InverseLerp(pinchClosedDistance, pinchOpenDistance, pinchDist);
                _targetPinchLerp = Mathf.Clamp01(_targetPinchLerp);
            }

            // 스무딩 적용 (급격한 변화 방지)
            GraspLerp = Mathf.Lerp(GraspLerp, _targetGraspLerp, Time.deltaTime * lerpSpeed);
            PinchLerp = Mathf.Lerp(PinchLerp, _targetPinchLerp, Time.deltaTime * lerpSpeed);

            // 캘리브레이션 로그 (5초 간격, 실제 거리값 출력)
            _calibrationLogTimer += Time.deltaTime;
            if (_calibrationLogTimer > 5f)
            {
                _calibrationLogTimer = 0f;
                Debug.Log($"[GraspCalib] {_dataProvider.handSide} rawDist: " +
                    $"thumb={_rawDistances[0]:F4}, idx={_rawDistances[1]:F4}, " +
                    $"mid={_rawDistances[2]:F4}, ring={_rawDistances[3]:F4}, " +
                    $"pinky={_rawDistances[4]:F4}");
            }
        }
    }
}
