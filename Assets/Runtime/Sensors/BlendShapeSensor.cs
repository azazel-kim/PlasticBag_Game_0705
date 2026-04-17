// BlendShapeSensor.cs
// Samsung Galaxy XR IR 페이스 트래킹 카메라로부터 블렌드셰이프 데이터 수집
// XRFaceTrackingManager (Google XR Extensions)를 통해 블렌드셰이프 계수를 수집
// Unity 6000.1.17f1, Android XR, OpenXR + Google XR Extensions v1.2.0

using System;
using UnityEngine;
using Google.XR.Extensions;

namespace XRExergame.Sensors
{
    /// <summary>
    /// 한 프레임에 수집된 블렌드셰이프 데이터를 담는 구조체.
    /// 값 타입(struct)이므로 GC 부하 없이 이벤트로 전달 가능.
    /// </summary>
    public struct BlendShapeData
    {
        /// <summary>
        /// 블렌드셰이프 가중치 배열.
        /// 각 값 범위: 0.0(근육 이완) ~ 1.0(최대 수축).
        /// 인덱스 매핑은 XRFaceParameterIndices 열거형 참조.
        /// </summary>
        public float[] Weights;

        /// <summary>
        /// 3개 얼굴 영역별 신뢰도 (0.0 ~ 1.0).
        /// [0] = Lower (하안면: 입, 턱)
        /// [1] = LeftUpper (좌측 상안면: 왼쪽 눈, 이마)
        /// [2] = RightUpper (우측 상안면: 오른쪽 눈, 이마)
        /// XRFaceConfidenceRegion 열거형과 동일 순서.
        /// </summary>
        public float[] RegionConfidence;

        /// <summary>
        /// 데이터 수집 시각 (Unix 에포크 기준 밀리초).
        /// FusedDataFrame에서 다른 센서와 시간 동기화에 사용.
        /// </summary>
        public long TimestampMs;
    }

    /// <summary>
    /// Samsung Galaxy XR 온디바이스 IR 카메라로 블렌드셰이프를 수집하는 센서 래퍼.
    /// XRFaceTrackingManager (Google XR Extensions) API에 연동함.
    /// ISensorProvider 인터페이스를 구현하여 FusedDataFrame에 데이터를 공급함.
    ///
    /// 사용 방법:
    ///   이 컴포넌트와 함께 XRFaceTrackingManager를 같은 GameObject에 추가해야 함.
    ///   (RequireComponent 어트리뷰트로 자동 강제됨)
    /// </summary>
    [RequireComponent(typeof(XRFaceTrackingManager))]
    public class BlendShapeSensor : MonoBehaviour, ISensorProvider<BlendShapeData>
    {
        // ─────────────────────────────────────────
        // Inspector 설정값
        // ─────────────────────────────────────────

        [Header("센서 설정")]
        [Tooltip("블렌드셰이프 신뢰도가 이 값 미만이면 해당 영역 데이터를 무시함")]
        [SerializeField] private float _confidenceThreshold = 0.5f;

        [Tooltip("센서 폴링 주파수 (Hz). 하드웨어 최대치는 72Hz")]
        [SerializeField] private float _targetSampleRateHz = 72f;

        // ─────────────────────────────────────────
        // 내부 컴포넌트 참조 (캐싱)
        // ─────────────────────────────────────────

        // XRFaceTrackingManager: Google XR Extensions가 제공하는 페이스 트래킹 관리자
        // Awake에서 GetComponent로 캐싱하여 반복 호출 비용 제거
        private XRFaceTrackingManager _faceManager;

        // ─────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────

        // 가장 최근 수집된 데이터
        private BlendShapeData? _latestData = null;

        // 센서 준비 완료 여부 (트래킹 상태가 Tracking이면 true)
        private bool _isAvailable = false;

        // 폴링 간격 추적 (샘플레이트 제어용)
        private float _nextSampleTime = 0f;

        // ─────────────────────────────────────────
        // ISensorProvider 구현
        // ─────────────────────────────────────────

        /// <inheritdoc/>
        public bool IsAvailable => _isAvailable;

        /// <inheritdoc/>
        public float SampleRateHz => _targetSampleRateHz;

        /// <inheritdoc/>
        public BlendShapeData? GetLatestData() => _latestData;

        /// <inheritdoc/>
        public event Action<BlendShapeData> OnDataUpdated;

        // ─────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────

        private void Awake()
        {
            // XRFaceTrackingManager 컴포넌트 캐싱 (RequireComponent로 반드시 존재함)
            _faceManager = GetComponent<XRFaceTrackingManager>();
        }

        private void Update()
        {
            // 샘플레이트 제한: _targetSampleRateHz를 초과하지 않게 폴링
            if (Time.time < _nextSampleTime)
                return;

            _nextSampleTime = Time.time + (1f / _targetSampleRateHz);

            PollFaceTrackingData();
        }

        // ─────────────────────────────────────────
        // 데이터 폴링
        // ─────────────────────────────────────────

        /// <summary>
        /// 매 샘플 주기마다 호출. XRFaceTrackingManager.Face에서 블렌드셰이프 가중치를 읽어옴.
        /// 에디터에서는 더미 사인파 데이터를 생성하여 파이프라인 테스트 가능.
        /// </summary>
        private void PollFaceTrackingData()
        {
#if UNITY_EDITOR
            // 에디터 더미 데이터: 사인파로 블렌드셰이프 시뮬레이션
            PollEditorDummyData();
#else
            // 디바이스 실제 데이터: XRFaceTrackingManager에서 읽기
            PollDeviceData();
#endif
        }

        /// <summary>
        /// 에디터 전용 더미 데이터 생성.
        /// 파이프라인(FusedDataFrame, UDP, CSV)을 디바이스 없이 테스트할 때 사용.
        /// </summary>
        private void PollEditorDummyData()
        {
            // 파라미터 개수를 런타임에 열거형에서 읽어 향후 API 변경에 대응
            int paramCount = Enum.GetNames(typeof(XRFaceParameterIndices)).Length;
            float[] weights = new float[paramCount];
            float t = Time.time;
            for (int i = 0; i < paramCount; i++)
                weights[i] = Mathf.Abs(Mathf.Sin(t * (1f + i * 0.05f))) * 0.3f;

            // 신뢰도: [0]=Lower, [1]=LeftUpper, [2]=RightUpper
            float[] confidence = new float[3] { 0.85f, 0.9f, 0.9f };

            _isAvailable = true;
            PublishData(weights, confidence);
        }

        /// <summary>
        /// 디바이스 전용 실제 데이터 폴링.
        /// XRFaceTrackingManager가 매 Update마다 갱신한 XRFaceState를 읽어옴.
        /// </summary>
        private void PollDeviceData()
        {
            // XRFaceTrackingFeature 익스텐션이 활성화됐는지 확인
            // null이면 아직 XrInstance 초기화 전 → 대기
            if (!XRFaceTrackingFeature.IsFaceTrackingExtensionEnabled.HasValue)
            {
                if (_isAvailable)
                {
                    _isAvailable = false;
                    Debug.Log("[BlendShapeSensor] XrInstance 초기화 대기 중...");
                }
                return;
            }

            if (!XRFaceTrackingFeature.IsFaceTrackingExtensionEnabled.Value)
            {
                if (_isAvailable)
                {
                    _isAvailable = false;
                    Debug.LogWarning("[BlendShapeSensor] XR_ANDROID_face_tracking 익스텐션 비활성화. " +
                        "OpenXR Project Settings > Features > 'Android XR: Face Tracking' 활성화 필요.");
                }
                return;
            }

            // 트래킹 상태 확인: Tracking 상태일 때만 데이터 수집
            XRFaceState face = _faceManager.Face;
            bool isTracking = face.TrackingState == XRFaceTrackingStates.Tracking;

            if (!isTracking)
            {
                // 처음 비가용 전환 시 한 번만 로그
                if (_isAvailable)
                {
                    _isAvailable = false;
                    Debug.LogWarning($"[BlendShapeSensor] 트래킹 중단: {face.TrackingState}. " +
                        "HMD를 착용하고 페이스 트래킹 캘리브레이션 필요.");
                }
                return;
            }

            // IsValid: 이번 프레임에 유효한 데이터가 없어도 이전 데이터를 사용할 수 있음
            // 단, 처음 Tracking 진입 후 IsValid가 false이면 아직 데이터 미수신
            if (!face.IsValid)
            {
                if (!_isAvailable)
                    Debug.Log("[BlendShapeSensor] Tracking 상태 진입. 유효 데이터 대기 중...");
                return;
            }

            // 정상 트래킹 중
            if (!_isAvailable)
            {
                _isAvailable = true;
                Debug.Log("[BlendShapeSensor] 페이스 트래킹 활성화. 데이터 수집 시작.");
            }

            // Parameters가 null이거나 비어있으면 초기화 미완료
            if (face.Parameters == null || face.Parameters.Length == 0)
                return;

            // ConfidenceRegions가 null이거나 3개 미만이면 신뢰도 전부 1.0으로 폴백
            float[] confidence;
            if (face.ConfidenceRegions != null && face.ConfidenceRegions.Length >= 3)
            {
                confidence = face.ConfidenceRegions;
            }
            else
            {
                // 신뢰도 데이터 없음 → 폴백: 모든 영역 신뢰도 1.0
                confidence = new float[3] { 1f, 1f, 1f };
            }

            PublishData(face.Parameters, confidence);
        }

        /// <summary>
        /// 신뢰도 검사 후 BlendShapeData를 생성하여 이벤트로 발행.
        /// </summary>
        /// <param name="weights">블렌드셰이프 가중치 배열</param>
        /// <param name="confidence">3개 영역 신뢰도 배열 ([0]=Lower, [1]=LeftUpper, [2]=RightUpper)</param>
        private void PublishData(float[] weights, float[] confidence)
        {
            // 신뢰도 검사: 모든 영역이 임계값 미만이면 드롭
            bool anyRegionValid = confidence[0] >= _confidenceThreshold
                               || confidence[1] >= _confidenceThreshold
                               || confidence[2] >= _confidenceThreshold;

            if (!anyRegionValid)
            {
                Debug.LogWarning("[BlendShapeSensor] 전 영역 신뢰도 임계값 미달, 프레임 드롭 " +
                    $"(Lower={confidence[0]:F2}, LeftUpper={confidence[1]:F2}, RightUpper={confidence[2]:F2})");
                return;
            }

            // BlendShapeData 구조체 생성 (Clone으로 원본 배열 변형 방지)
            var data = new BlendShapeData
            {
                Weights          = (float[])weights.Clone(),
                RegionConfidence = (float[])confidence.Clone(),
                TimestampMs      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _latestData = data;

            // 이벤트 발행: FusedDataFrame, BlendShapeLogger 등 구독자에게 알림
            OnDataUpdated?.Invoke(data);
        }
    }
}
