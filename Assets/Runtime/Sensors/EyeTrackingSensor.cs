// EyeTrackingSensor.cs
// OpenXR EyeGazeInteraction을 통해 좌/우 눈 회전 및 깜박임을 수집하는 센서 래퍼
// 기존 EyeGazeRaycaster.cs의 InputAction 패턴을 참고하되,
// 레이캐스트 없이 순수 데이터 수집에 집중하는 새 파일로 작성함
// Samsung Galaxy XR (Android XR) — Unity 6000.1.17f1

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace XRExergame.Sensors
{
    /// <summary>
    /// 한 프레임에 수집된 눈 추적 데이터를 담는 구조체.
    /// 값 타입(struct)이므로 GC 부하 없이 이벤트로 전달 가능.
    /// </summary>
    public struct EyeTrackingData
    {
        /// <summary>
        /// 왼쪽 눈의 회전값 (OpenXR 좌표계 기준).
        /// 값이 Quaternion.identity이면 유효하지 않은 데이터.
        /// </summary>
        public Quaternion LeftRotation;

        /// <summary>
        /// 오른쪽 눈의 회전값 (OpenXR 좌표계 기준).
        /// Samsung Galaxy XR는 합산 시선 1개를 제공하므로 좌/우가 동일할 수 있음.
        /// </summary>
        public Quaternion RightRotation;

        /// <summary>
        /// 눈 깜박임 여부.
        /// true = 현재 눈 감은 상태 (블렌드셰이프 EyeBlink 또는 trackingState 소실로 판단).
        /// </summary>
        public bool IsBlinking;

        /// <summary>
        /// 데이터 수집 시각 (Unix 에포크 기준 밀리초).
        /// FusedDataFrame에서 BlendShapeData와 시간 동기화에 사용.
        /// </summary>
        public long TimestampMs;
    }

    /// <summary>
    /// OpenXR EyeGazeInteraction으로부터 눈 추적 데이터를 수집하는 센서 래퍼.
    /// EyeGazeRaycaster.cs의 InputAction 연결 패턴을 재사용하되,
    /// 시선 레이캐스트 로직은 포함하지 않음 (관심사 분리).
    /// </summary>
    public class EyeTrackingSensor : MonoBehaviour, ISensorProvider<EyeTrackingData>
    {
        // ─────────────────────────────────────────
        // Inspector 설정값
        // ─────────────────────────────────────────

        [Header("센서 설정")]
        [Tooltip("눈 추적 샘플링 주파수 (Hz). 하드웨어 최대치는 72Hz")]
        [SerializeField] private float _targetSampleRateHz = 72f;

        [Tooltip("디바이스 감지 타임아웃 (초). 초과 시 비가용 상태로 전환")]
        [SerializeField] private float _deviceDetectionTimeoutSec = 15f;

        [Header("깜박임 감지 설정")]
        [Tooltip("trackingState가 연속으로 이 프레임 수 이상 0이면 깜박임으로 간주")]
        [SerializeField] private int _blinkFrameThreshold = 3;

        // ─────────────────────────────────────────
        // InputAction (OpenXR EyeGazeInteraction)
        // ─────────────────────────────────────────

        // 합산 시선(combined gaze)의 위치, 회전, 트래킹 상태 액션
        // EyeGazeRaycaster.cs와 동일한 바인딩 경로 사용
        private InputAction _gazePositionAction;
        private InputAction _gazeRotationAction;
        private InputAction _gazeTrackingStateAction;

        // ─────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────

        // 가장 최근 수집된 데이터
        private EyeTrackingData? _latestData = null;

        // 센서 준비 완료 여부
        private bool _isAvailable = false;

        // trackingState가 0인 연속 프레임 수 (깜박임 감지용)
        private int _noTrackingFrameCount = 0;

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
        public EyeTrackingData? GetLatestData() => _latestData;

        /// <inheritdoc/>
        public event Action<EyeTrackingData> OnDataUpdated;

        // ─────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────

        private void Awake()
        {
            // InputAction 생성 (EyeGazeRaycaster.cs와 동일한 바인딩 경로)
            // "<EyeGaze>/pose/..." 경로는 OpenXR EyeGazeInteraction 표준 경로
            _gazePositionAction     = new InputAction("EyeGazePosition",     binding: "<EyeGaze>/pose/position");
            _gazeRotationAction     = new InputAction("EyeGazeRotation",     binding: "<EyeGaze>/pose/rotation");
            _gazeTrackingStateAction = new InputAction("EyeGazeTrackingState", binding: "<EyeGaze>/pose/trackingState");
        }

        private void Start()
        {
            RequestEyeTrackingPermission();

            // InputAction 활성화
            _gazePositionAction.Enable();
            _gazeRotationAction.Enable();
            _gazeTrackingStateAction.Enable();

            Debug.Log("[EyeTrackingSensor] InputActions 활성화 완료. 디바이스 감지 대기 중...");

            // 디바이스 감지 코루틴 시작 (EyeGazeRaycaster.cs의 WaitForEyeGazeDevice 패턴 재사용)
            StartCoroutine(WaitForEyeGazeDevice());
        }

        private void Update()
        {
            // 센서가 준비되지 않았으면 폴링 스킵
            if (!_isAvailable)
                return;

            // 샘플레이트 제한
            if (Time.time < _nextSampleTime)
                return;

            _nextSampleTime = Time.time + (1f / _targetSampleRateHz);

            PollEyeTrackingData();
        }

        private void OnDestroy()
        {
            // InputAction 반드시 해제 (메모리 누수 방지)
            _gazePositionAction?.Disable();
            _gazeRotationAction?.Disable();
            _gazeTrackingStateAction?.Disable();
        }

        // ─────────────────────────────────────────
        // 퍼미션 요청
        // ─────────────────────────────────────────

        /// <summary>
        /// Android XR에서 눈 추적 런타임 퍼미션 요청.
        /// EyeGazeRaycaster.cs의 RequestEyeTrackingPermission 패턴 동일.
        /// </summary>
        private void RequestEyeTrackingPermission()
        {
#if !UNITY_EDITOR
            const string eyeTrackingPermission = "android.permission.EYE_TRACKING";

            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(eyeTrackingPermission))
            {
                Debug.Log("[EyeTrackingSensor] Eye Tracking 퍼미션 요청 중...");
                UnityEngine.Android.Permission.RequestUserPermission(eyeTrackingPermission);
            }
            else
            {
                Debug.Log("[EyeTrackingSensor] Eye Tracking 퍼미션 이미 허용됨");
            }
#else
            Debug.Log("[EyeTrackingSensor] 에디터 모드: 퍼미션 요청 스킵");
#endif
        }

        // ─────────────────────────────────────────
        // 디바이스 감지 코루틴
        // ─────────────────────────────────────────

        /// <summary>
        /// OpenXR Eye Gaze 디바이스가 등록될 때까지 최대 _deviceDetectionTimeoutSec 동안 폴링.
        /// EyeGazeRaycaster.cs의 WaitForEyeGazeDevice() 패턴을 참고하여 4가지 방법으로 감지.
        /// </summary>
        private IEnumerator WaitForEyeGazeDevice()
        {
            // 퍼미션 승인 대기
            yield return new WaitForSeconds(1.0f);

            float elapsed = 0f;
            const float pollInterval = 0.5f;

            while (elapsed < _deviceDetectionTimeoutSec)
            {
                yield return new WaitForSeconds(pollInterval);
                elapsed += pollInterval;

                // 감지 방법 1: InputSystem에 EyeGaze 디바이스가 등록됐는지 확인
                var eyeDevice = InputSystem.GetDevice("EyeGaze");
                if (eyeDevice != null)
                {
                    Debug.Log($"[EyeTrackingSensor] InputSystem 디바이스 감지: {eyeDevice.displayName} ({elapsed:F1}s)");
                    _isAvailable = true;
                    yield break;
                }

                // 감지 방법 2: XR InputDevice API에서 EyeTracking 특성 디바이스 확인
                var xrDevices = new List<UnityEngine.XR.InputDevice>();
                UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                    UnityEngine.XR.InputDeviceCharacteristics.EyeTracking, xrDevices);

                if (xrDevices.Count > 0)
                {
                    Debug.Log($"[EyeTrackingSensor] XR EyeTracking 디바이스 감지: {xrDevices[0].name} ({elapsed:F1}s)");
                    _isAvailable = true;
                    yield break;
                }

                // 감지 방법 3: trackingState 값이 0보다 크면 활성 상태
                int trackingState = _gazeTrackingStateAction.ReadValue<int>();
                if (trackingState > 0)
                {
                    Debug.Log($"[EyeTrackingSensor] trackingState={trackingState} 감지 ({elapsed:F1}s)");
                    _isAvailable = true;
                    yield break;
                }

                // 감지 방법 4: 실제 눈 회전 데이터가 들어오는지 확인
                Quaternion rot = _gazeRotationAction.ReadValue<Quaternion>();
                Vector3 pos    = _gazePositionAction.ReadValue<Vector3>();
                if (IsValidGazeData(pos, rot))
                {
                    Debug.Log($"[EyeTrackingSensor] 유효 EyeGaze 데이터 수신 ({elapsed:F1}s)");
                    _isAvailable = true;
                    yield break;
                }

                Debug.Log($"[EyeTrackingSensor] 디바이스 감지 대기 중... ({elapsed:F1}s / {_deviceDetectionTimeoutSec}s)");
            }

            // 타임아웃: 센서를 비가용 상태로 유지
            Debug.LogWarning($"[EyeTrackingSensor] {_deviceDetectionTimeoutSec}s 타임아웃. " +
                             "Eye Tracking 비가용 상태. HMD를 착용하고 눈 트래킹 캘리브레이션 필요.");
            _isAvailable = false;
        }

        // ─────────────────────────────────────────
        // 데이터 폴링
        // ─────────────────────────────────────────

        /// <summary>
        /// 매 샘플 주기마다 호출. InputAction에서 눈 회전과 트래킹 상태를 읽어옴.
        /// Samsung Galaxy XR는 합산 시선 1개를 제공하므로 좌/우 회전에 동일 값을 설정함.
        /// </summary>
        private void PollEyeTrackingData()
        {
            // InputAction에서 현재 값 읽기
            Quaternion gazeRot   = _gazeRotationAction.ReadValue<Quaternion>();
            Vector3    gazePos   = _gazePositionAction.ReadValue<Vector3>();
            int        trackingState = _gazeTrackingStateAction.ReadValue<int>();

            bool hasValidData = IsValidGazeData(gazePos, gazeRot) && trackingState > 0;

            // 깜박임 감지: trackingState가 _blinkFrameThreshold 프레임 연속으로 소실되면 블링크
            if (trackingState == 0)
                _noTrackingFrameCount++;
            else
                _noTrackingFrameCount = 0;

            bool isBlinking = _noTrackingFrameCount >= _blinkFrameThreshold;

            // trackingState가 없어도 기존 유효 데이터가 있으면 사용 (EyeGazeRaycaster 패턴 동일)
            if (!hasValidData && IsValidGazeData(gazePos, gazeRot))
                hasValidData = true;

            if (!hasValidData)
            {
                // 유효 데이터 없음: 이전 데이터 유지 (이벤트 미발행)
                return;
            }

            // Samsung Galaxy XR는 단일 합산 시선 제공 → 좌/우 모두 동일 회전값 사용
            // TODO: 디바이스가 좌/우 개별 눈 회전을 제공하면 아래 별도 바인딩 추가
            // 별도 바인딩 경로 예시: "<EyeGaze>/leftEye/rotation", "<EyeGaze>/rightEye/rotation"
            var data = new EyeTrackingData
            {
                LeftRotation  = gazeRot,
                RightRotation = gazeRot,
                IsBlinking    = isBlinking,
                TimestampMs   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _latestData = data;

            // 이벤트 발행: FusedDataFrame 등 구독자에게 알림
            OnDataUpdated?.Invoke(data);
        }

        // ─────────────────────────────────────────
        // 유틸리티
        // ─────────────────────────────────────────

        /// <summary>
        /// Eye Gaze 데이터가 유효한지 검사.
        /// Quaternion(0,0,0,0)이거나 identity + 위치 0 조합이면 무효 데이터.
        /// EyeGazeRaycaster.cs의 IsValidGazeData() 동일 로직.
        /// </summary>
        private bool IsValidGazeData(Vector3 pos, Quaternion rot)
        {
            // 완전히 0인 쿼터니언은 초기화되지 않은 값
            if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f)
                return false;

            // identity 회전 + 위치 0 → 아직 데이터 없음
            bool isIdentity = Mathf.Approximately(rot.x, 0f)
                           && Mathf.Approximately(rot.y, 0f)
                           && Mathf.Approximately(rot.z, 0f)
                           && Mathf.Approximately(rot.w, 1f);
            bool isZeroPos  = pos.sqrMagnitude < 0.0001f;

            if (isIdentity && isZeroPos)
                return false;

            return true;
        }
    }
}
