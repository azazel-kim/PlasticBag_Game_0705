// BlendShapeSensor.cs
// Samsung Galaxy XR IR 페이스 트래킹 카메라로부터 블렌드셰이프 데이터 수집
// XRFaceTrackingFeature (Google XR Extensions)를 통해 68 FACS 계수를 @72Hz 수집
// Unity 6000.1.17f1, Android XR, OpenXR + Google XR Extensions

using System;
using System.IO;
using UnityEngine;

// TODO: Google XR Extensions 패키지가 프로젝트에 추가된 후 아래 using 주석 해제
// using Google.XR.Extensions;
// using UnityEngine.XR.OpenXR.Features;

namespace XRExergame.Sensors
{
    /// <summary>
    /// 한 프레임에 수집된 블렌드셰이프 데이터를 담는 구조체.
    /// 값 타입(struct)이므로 GC 부하 없이 이벤트로 전달 가능.
    /// </summary>
    public struct BlendShapeData
    {
        /// <summary>
        /// 68개 FACS(얼굴 움직임 코딩 시스템) 블렌드셰이프 가중치.
        /// 각 값 범위: 0.0(근육 이완) ~ 1.0(최대 수축).
        /// 인덱스 매핑은 XRFaceTrackingFeature.BlendShapeIndex 열거형 참조.
        /// </summary>
        public float[] Weights;

        /// <summary>
        /// 3개 얼굴 영역별 신뢰도 (0.0 ~ 1.0).
        /// [0] = 상안면 (이마, 눈썹)
        /// [1] = 중안면 (눈, 코)
        /// [2] = 하안면 (입, 턱)
        /// 신뢰도가 낮으면 해당 영역 가중치 사용 금지.
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
    /// XRFaceTrackingFeature (Google XR Extensions) API에 연동함.
    /// ISensorProvider 인터페이스를 구현하여 FusedDataFrame에 데이터를 공급함.
    /// </summary>
    public class BlendShapeSensor : MonoBehaviour, ISensorProvider<BlendShapeData>
    {
        // ─────────────────────────────────────────
        // Inspector 설정값
        // ─────────────────────────────────────────

        [Header("센서 설정")]
        [Tooltip("블렌드셰이프 신뢰도가 이 값 미만이면 해당 영역 데이터를 무시함")]
        [SerializeField] private float _confidenceThreshold = 0.5f;

        [Tooltip("센서 폴링 주파수 (Hz). 하드웨어 최대치는 72Hz)")]
        [SerializeField] private float _targetSampleRateHz = 72f;

        [Header("CSV 로깅 (디버그용)")]
        [Tooltip("활성화하면 블렌드셰이프 데이터를 CSV로 저장함")]
        [SerializeField] private bool _enableCsvLogging = false;

        [Tooltip("CSV 파일 저장 경로. 비워두면 Application.persistentDataPath 사용")]
        [SerializeField] private string _csvLogPath = "";

        // ─────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────

        // 블렌드셰이프 가중치 배열 (68개). 매 프레임 재사용하여 GC 방지
        private float[] _weightBuffer = new float[68];

        // 3개 영역 신뢰도 배열
        private float[] _confidenceBuffer = new float[3];

        // 가장 최근 수집된 데이터
        private BlendShapeData? _latestData = null;

        // 센서 준비 완료 여부
        private bool _isAvailable = false;

        // 퍼미션 요청 완료 여부
        private bool _permissionRequested = false;

        // CSV 로깅용 StreamWriter
        private StreamWriter _csvWriter = null;

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
            // 버퍼 초기화 (GC 방지를 위해 Awake에서 한 번만 할당)
            _weightBuffer = new float[68];
            _confidenceBuffer = new float[3];
        }

        private void Start()
        {
            RequestFaceTrackingPermission();
            InitializeFaceTracking();

            if (_enableCsvLogging)
                InitializeCsvLogger();
        }

        private void Update()
        {
            // 샘플레이트 제한: _targetSampleRateHz를 초과하지 않게 폴링
            if (Time.time < _nextSampleTime)
                return;

            _nextSampleTime = Time.time + (1f / _targetSampleRateHz);

            PollFaceTrackingData();
        }

        private void OnDestroy()
        {
            _csvWriter?.Close();
            _csvWriter = null;
        }

        // ─────────────────────────────────────────
        // 퍼미션 요청
        // ─────────────────────────────────────────

        /// <summary>
        /// Android XR에서 페이스 트래킹 런타임 퍼미션을 요청함.
        /// </summary>
        private void RequestFaceTrackingPermission()
        {
#if !UNITY_EDITOR
            // Android 런타임 퍼미션: 페이스 트래킹은 별도 퍼미션 필요
            const string faceTrackingPermission = "android.permission.FACE_TRACKING";

            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(faceTrackingPermission))
            {
                Debug.Log("[BlendShapeSensor] Face Tracking 퍼미션 요청 중...");
                UnityEngine.Android.Permission.RequestUserPermission(faceTrackingPermission);
            }
            else
            {
                Debug.Log("[BlendShapeSensor] Face Tracking 퍼미션 이미 허용됨");
            }
#else
            Debug.Log("[BlendShapeSensor] 에디터 모드: 퍼미션 요청 스킵");
#endif
            _permissionRequested = true;
        }

        // ─────────────────────────────────────────
        // Face Tracking 초기화
        // ─────────────────────────────────────────

        /// <summary>
        /// XRFaceTrackingFeature를 OpenXR 세션에서 가져와서 활성화 확인.
        /// </summary>
        private void InitializeFaceTracking()
        {
            // TODO: Google XR Extensions 패키지 추가 후 아래 코드 활성화
            // ─── 실제 API 연동 지점 (BEGIN) ───────────────────────────────────────
            //
            // var faceTrackingFeature = OpenXRSettings.Instance
            //     .GetFeature<XRFaceTrackingFeature>();
            //
            // if (faceTrackingFeature == null)
            // {
            //     Debug.LogWarning("[BlendShapeSensor] XRFaceTrackingFeature를 찾을 수 없음. " +
            //         "OpenXR Project Settings > Features에서 'Face Tracking' 활성화 필요.");
            //     _isAvailable = false;
            //     return;
            // }
            //
            // if (!faceTrackingFeature.enabled)
            // {
            //     Debug.LogWarning("[BlendShapeSensor] XRFaceTrackingFeature가 비활성화됨.");
            //     _isAvailable = false;
            //     return;
            // }
            //
            // _isAvailable = true;
            // Debug.Log("[BlendShapeSensor] XRFaceTrackingFeature 초기화 완료.");
            //
            // ─── 실제 API 연동 지점 (END) ─────────────────────────────────────────

            // 임시: 에디터에서는 항상 가용 상태로 처리
#if UNITY_EDITOR
            _isAvailable = true;
            Debug.Log("[BlendShapeSensor] 에디터 모드: 더미 데이터 모드로 초기화됨");
#else
            // 디바이스에서는 퍼미션 확인 후 가용 여부 결정
            // TODO: 실제 XRFaceTrackingFeature 연동 후 이 부분 교체
            _isAvailable = _permissionRequested;
            Debug.Log($"[BlendShapeSensor] 디바이스 초기화 (임시): isAvailable={_isAvailable}");
#endif
        }

        // ─────────────────────────────────────────
        // 데이터 폴링
        // ─────────────────────────────────────────

        /// <summary>
        /// 매 샘플 주기마다 호출. XRFaceTrackingFeature에서 블렌드셰이프 가중치를 읽어옴.
        /// </summary>
        private void PollFaceTrackingData()
        {
            if (!_isAvailable)
                return;

            // TODO: Google XR Extensions 패키지 추가 후 아래 코드 활성화
            // ─── 실제 API 연동 지점 (BEGIN) ───────────────────────────────────────
            //
            // XRFaceTrackingFeature 사용 예시:
            //
            // var faceTrackingFeature = OpenXRSettings.Instance
            //     .GetFeature<XRFaceTrackingFeature>();
            //
            // // 블렌드셰이프 가중치 읽기 (NativeArray<float> 반환)
            // bool success = faceTrackingFeature.TryGetFaceExpressionWeights(
            //     out NativeArray<float> weights,
            //     out XRFaceTrackingFeature.RegionConfidence confidence);
            //
            // if (!success || !weights.IsCreated)
            //     return;
            //
            // // NativeArray → managed float[] 복사 (GC 최소화)
            // weights.CopyTo(_weightBuffer);
            //
            // // 3개 영역 신뢰도 추출
            // _confidenceBuffer[0] = confidence.Upper; // 상안면
            // _confidenceBuffer[1] = confidence.Mid;   // 중안면
            // _confidenceBuffer[2] = confidence.Lower; // 하안면
            //
            // ─── 실제 API 연동 지점 (END) ─────────────────────────────────────────

#if UNITY_EDITOR
            // 에디터 더미 데이터: 사인파로 블렌드셰이프 시뮬레이션
            float t = Time.time;
            for (int i = 0; i < 68; i++)
                _weightBuffer[i] = Mathf.Abs(Mathf.Sin(t * (1f + i * 0.05f))) * 0.3f;
            _confidenceBuffer[0] = 0.9f;
            _confidenceBuffer[1] = 0.9f;
            _confidenceBuffer[2] = 0.85f;
#endif

            // 신뢰도 검사: 모든 영역이 임계값 미만이면 드롭
            bool anyRegionValid = _confidenceBuffer[0] >= _confidenceThreshold
                               || _confidenceBuffer[1] >= _confidenceThreshold
                               || _confidenceBuffer[2] >= _confidenceThreshold;

            if (!anyRegionValid)
            {
                Debug.LogWarning("[BlendShapeSensor] 전 영역 신뢰도 임계값 미달, 프레임 드롭");
                return;
            }

            // BlendShapeData 구조체 생성 (new float[] 대신 기존 버퍼 복사로 GC 방지)
            var data = new BlendShapeData
            {
                Weights          = (float[])_weightBuffer.Clone(),
                RegionConfidence = (float[])_confidenceBuffer.Clone(),
                TimestampMs      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _latestData = data;

            // 이벤트 발행: FusedDataFrame 등 구독자에게 알림
            OnDataUpdated?.Invoke(data);

            // CSV 로깅 (디버그용)
            if (_enableCsvLogging && _csvWriter != null)
                WriteCsvRow(data);
        }

        // ─────────────────────────────────────────
        // CSV 로깅 (디버그용)
        // ─────────────────────────────────────────

        /// <summary>
        /// CSV 로거 초기화. persistentDataPath 또는 지정 경로에 파일 생성.
        /// </summary>
        private void InitializeCsvLogger()
        {
            try
            {
                string dir = string.IsNullOrEmpty(_csvLogPath)
                    ? Application.persistentDataPath
                    : _csvLogPath;

                string fileName = $"blendshape_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullPath = Path.Combine(dir, fileName);

                _csvWriter = new StreamWriter(fullPath, append: false);

                // 헤더 작성: 타임스탬프 + 68개 가중치 + 3개 신뢰도
                var header = new System.Text.StringBuilder("TimestampMs");
                for (int i = 0; i < 68; i++)
                    header.Append($",BS_{i:D2}");
                header.Append(",Conf_Upper,Conf_Mid,Conf_Lower");

                _csvWriter.WriteLine(header.ToString());
                Debug.Log($"[BlendShapeSensor] CSV 로깅 시작: {fullPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BlendShapeSensor] CSV 초기화 실패: {e.Message}");
                _enableCsvLogging = false;
            }
        }

        /// <summary>
        /// BlendShapeData 한 행을 CSV에 기록.
        /// </summary>
        private void WriteCsvRow(BlendShapeData data)
        {
            try
            {
                var row = new System.Text.StringBuilder(data.TimestampMs.ToString());
                for (int i = 0; i < data.Weights.Length; i++)
                    row.Append($",{data.Weights[i]:F4}");
                row.Append($",{data.RegionConfidence[0]:F3}");
                row.Append($",{data.RegionConfidence[1]:F3}");
                row.Append($",{data.RegionConfidence[2]:F3}");

                _csvWriter.WriteLine(row.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[BlendShapeSensor] CSV 쓰기 실패: {e.Message}");
            }
        }
    }
}
