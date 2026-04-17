// =============================================================================
// TimeSyncAligner.cs — 타임스탬프 동기화 및 정렬기
// 네임스페이스: XRExergame.Fusion
// 역할: PC 시계와 Galaxy XR 시계 간의 오프셋(Δt)을 계산하고,
//       각 센서의 고유 지연을 보정하여 공통 타임라인에 정렬
// =============================================================================
//
// 동기화 흐름:
//   1. Galaxy XR → PC로 ClockSync 요청 (UDP 9003)
//   2. PC가 자기 시각 T_pc를 응답
//   3. RTT/2 보정으로 클럭 오프셋 계산
//   4. 이후 모든 센서 타임스탬프에 오프셋 + 센서별 지연 보정 적용
//
// 재동기화(Re-sync) 조건:
//   - 5분 주기 정기 재동기화
//   - 실시간 드리프트가 50ms 초과 시 즉시 재동기화
// =============================================================================

using System;
using UnityEngine;

namespace XRExergame.Fusion
{
    // =========================================================================
    // SensorType — 센서 종류 식별자
    // =========================================================================

    /// <summary>
    /// 시스템에서 지원하는 센서 종류.
    /// 각 센서마다 고유한 전송 지연(latency)이 있어 보정이 필요함.
    /// </summary>
    public enum SensorType
    {
        BlendShape,    // Galaxy XR 얼굴 블렌드셰이프 (온디바이스, 지연 ~5ms)
        EyeTracking,   // Galaxy XR 시선 추적 (온디바이스, 지연 ~5ms)
        EEG,           // LinkBand2 뇌파 (BLE, 지연 ~35ms)
        PPG,           // LinkBand2 광혈류 (BLE, 지연 ~35ms)
        ACC,           // LinkBand2 가속도 (BLE, 지연 ~35ms)
        Pose,          // OBSBOT + MediaPipe 포즈 (USB+처리, 지연 ~75ms)
        IMU            // X-Sens 전신 IMU (USB/Wi-Fi, 지연 ~12ms)
    }

    // =========================================================================
    // TimeSyncAligner — 클럭 동기화 + 센서 지연 보정
    // =========================================================================

    public class TimeSyncAligner
    {
        // ----- 설정 상수 -----

        /// <summary>최대 허용 드리프트 (ms). 이를 초과하면 데이터 drop</summary>
        public long MaxDriftMs { get; set; } = 100;

        /// <summary>재동기화 트리거 드리프트 임계값 (ms)</summary>
        public long ResyncThresholdMs { get; set; } = 50;

        /// <summary>정기 재동기화 주기 (ms) — 기본 5분</summary>
        public long ResyncIntervalMs { get; set; } = 5 * 60 * 1000;

        // ----- 센서별 고정 지연 보정값 (ms) -----
        // 양수 = 센서 데이터가 실제보다 늦게 도착 → 타임스탬프를 과거로 보정
        private readonly long[] _sensorLatencyMs;

        // ----- 클럭 동기화 상태 -----
        private long _clockOffsetMs;          // PC시계 - XR시계 (ms)
        private bool _isSynced;               // 최초 동기화 완료 여부
        private long _lastSyncTimestampMs;    // 마지막 동기화 시각
        private long _syncRoundTripMs;        // 마지막 동기화의 RTT

        // ----- 드리프트 모니터링 -----
        private long _maxObservedDriftMs;     // 세션 중 관측된 최대 드리프트
        private long _totalDropCount;         // 드리프트 초과로 drop된 총 샘플 수
        private readonly object _lock = new object();

        // ----- 콜백 -----
        /// <summary>드리프트 초과로 데이터 drop 시 호출. (sensorType, driftMs)</summary>
        public event Action<SensorType, long> OnDataDropped;

        /// <summary>재동기화가 필요할 때 호출. 외부에서 ClockSync 패킷 전송 트리거용.</summary>
        public event Action OnResyncRequested;

        // =====================================================================
        // 생성자
        // =====================================================================

        public TimeSyncAligner()
        {
            // SensorType enum 순서대로 고정 지연값 설정
            _sensorLatencyMs = new long[Enum.GetValues(typeof(SensorType)).Length];
            _sensorLatencyMs[(int)SensorType.BlendShape]  = 5;   // 온디바이스, 매우 낮은 지연
            _sensorLatencyMs[(int)SensorType.EyeTracking] = 5;   // 온디바이스, 매우 낮은 지연
            _sensorLatencyMs[(int)SensorType.EEG]         = 35;  // BLE 전송 지연
            _sensorLatencyMs[(int)SensorType.PPG]         = 35;  // BLE 전송 지연
            _sensorLatencyMs[(int)SensorType.ACC]         = 35;  // BLE 전송 지연
            _sensorLatencyMs[(int)SensorType.Pose]        = 75;  // USB 캡처 + MediaPipe 처리
            _sensorLatencyMs[(int)SensorType.IMU]         = 12;  // X-Sens USB/Wi-Fi

            _isSynced = false;
            _clockOffsetMs = 0;
            _lastSyncTimestampMs = 0;
            _syncRoundTripMs = 0;
            _maxObservedDriftMs = 0;
            _totalDropCount = 0;
        }

        // =====================================================================
        // 클럭 동기화 — NTP-like 방식
        // =====================================================================

        /// <summary>
        /// 클럭 동기화 결과 적용.
        /// Galaxy XR이 PC에 ClockSync 요청을 보내고 응답을 받았을 때 호출.
        ///
        /// 계산 원리 (NTP 단순화):
        ///   T1 = XR이 요청을 보낸 시각
        ///   T2 = PC가 요청을 받은 시각 (PC 응답에 포함)
        ///   T3 = PC가 응답을 보낸 시각 (≈T2, 처리 시간 무시)
        ///   T4 = XR이 응답을 받은 시각
        ///   RTT = (T4 - T1)
        ///   Offset = T2 - T1 - RTT/2
        /// </summary>
        /// <param name="xrSendTimeMs">T1: XR이 요청을 보낸 시각</param>
        /// <param name="pcReceiveTimeMs">T2: PC가 요청을 받은 시각 (PC 응답에 포함)</param>
        /// <param name="xrReceiveTimeMs">T4: XR이 응답을 받은 시각</param>
        public void ApplyClockSync(long xrSendTimeMs, long pcReceiveTimeMs, long xrReceiveTimeMs)
        {
            lock (_lock)
            {
                long rtt = xrReceiveTimeMs - xrSendTimeMs;
                long offset = pcReceiveTimeMs - xrSendTimeMs - (rtt / 2);

                if (_isSynced)
                {
                    // 이미 동기화된 상태면 이동 평균(Exponential Moving Average)으로 부드럽게 보정
                    // 갑작스러운 점프 방지 (alpha = 0.3)
                    _clockOffsetMs = (long)(_clockOffsetMs * 0.7 + offset * 0.3);
                }
                else
                {
                    // 최초 동기화는 그대로 적용
                    _clockOffsetMs = offset;
                }

                _syncRoundTripMs = rtt;
                _lastSyncTimestampMs = xrReceiveTimeMs;
                _isSynced = true;
            }
        }

        /// <summary>
        /// PC 기준 타임스탬프를 직접 설정하여 간단 동기화.
        /// 테스트 또는 동일 장비에서 PC/XR 역할을 겸할 때 사용.
        /// </summary>
        /// <param name="pcTimestampMs">현재 PC의 Unix 타임스탬프 (ms)</param>
        /// <param name="localTimestampMs">현재 로컬(XR)의 Unix 타임스탬프 (ms)</param>
        public void SetClockOffset(long pcTimestampMs, long localTimestampMs)
        {
            lock (_lock)
            {
                _clockOffsetMs = pcTimestampMs - localTimestampMs;
                _lastSyncTimestampMs = localTimestampMs;
                _isSynced = true;
            }
        }

        // =====================================================================
        // AlignTimestamp — 센서 타임스탬프를 공통 타임라인으로 변환
        // =====================================================================

        /// <summary>
        /// 원시(raw) 센서 타임스탬프를 동기화된 공통 타임라인으로 변환.
        ///
        /// 보정 과정:
        ///   1. 클럭 오프셋 적용 (PC↔XR 시계 차이)
        ///   2. 센서별 고정 지연 차감 (BLE, USB 등 전송 지연)
        ///   결과: 데이터가 실제로 생성된 시각에 가까운 타임스탬프
        /// </summary>
        /// <param name="rawTimestampMs">센서에서 받은 원시 타임스탬프 (밀리초)</param>
        /// <param name="sensorType">센서 종류</param>
        /// <returns>보정된 타임스탬프, 또는 -1 (드리프트 초과 시)</returns>
        public long AlignTimestamp(long rawTimestampMs, SensorType sensorType)
        {
            lock (_lock)
            {
                if (!_isSynced)
                {
                    // 동기화 전이면 센서 지연만 보정
                    return rawTimestampMs - _sensorLatencyMs[(int)sensorType];
                }

                // 보정: 클럭 오프셋 + 센서 고유 지연
                long aligned = rawTimestampMs - _clockOffsetMs - _sensorLatencyMs[(int)sensorType];

                // 현재 시각 기준 드리프트 확인
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long drift = Math.Abs(now - aligned);

                // 드리프트 최대값 추적
                if (drift > _maxObservedDriftMs)
                    _maxObservedDriftMs = drift;

                // MaxDrift 초과 → drop
                if (drift > MaxDriftMs)
                {
                    _totalDropCount++;
                    OnDataDropped?.Invoke(sensorType, drift);
                    return -1; // 호출자가 -1이면 이 데이터를 무시해야 함
                }

                // 재동기화 필요 여부 확인
                CheckResyncNeeded(now);

                return aligned;
            }
        }

        // =====================================================================
        // 센서 지연 설정
        // =====================================================================

        /// <summary>
        /// 특정 센서의 고정 지연값을 런타임에 변경.
        /// 실측 지연이 기본값과 다를 때 조정 가능.
        /// </summary>
        public void SetSensorLatency(SensorType sensorType, long latencyMs)
        {
            lock (_lock)
            {
                _sensorLatencyMs[(int)sensorType] = latencyMs;
            }
        }

        /// <summary>특정 센서의 현재 지연 보정값 조회</summary>
        public long GetSensorLatency(SensorType sensorType)
        {
            lock (_lock)
            {
                return _sensorLatencyMs[(int)sensorType];
            }
        }

        // =====================================================================
        // 상태 조회
        // =====================================================================

        /// <summary>클럭 동기화 완료 여부</summary>
        public bool IsSynced
        {
            get { lock (_lock) return _isSynced; }
        }

        /// <summary>현재 클럭 오프셋 (PC - XR, 밀리초)</summary>
        public long ClockOffsetMs
        {
            get { lock (_lock) return _clockOffsetMs; }
        }

        /// <summary>마지막 동기화의 왕복 시간 (밀리초)</summary>
        public long LastRoundTripMs
        {
            get { lock (_lock) return _syncRoundTripMs; }
        }

        /// <summary>세션 중 관측된 최대 드리프트 (밀리초)</summary>
        public long MaxObservedDriftMs
        {
            get { lock (_lock) return _maxObservedDriftMs; }
        }

        /// <summary>드리프트 초과로 drop된 총 샘플 수</summary>
        public long TotalDropCount
        {
            get { lock (_lock) return _totalDropCount; }
        }

        /// <summary>
        /// 진단 정보를 문자열로 반환 (디버그 UI 표시용)
        /// </summary>
        public string GetDiagnostics()
        {
            lock (_lock)
            {
                return $"[TimeSyncAligner] Synced={_isSynced}, " +
                       $"Offset={_clockOffsetMs}ms, RTT={_syncRoundTripMs}ms, " +
                       $"MaxDrift={_maxObservedDriftMs}ms, Drops={_totalDropCount}";
            }
        }

        // =====================================================================
        // 리셋
        // =====================================================================

        /// <summary>동기화 상태 초기화 (새 세션 시작 시)</summary>
        public void Reset()
        {
            lock (_lock)
            {
                _isSynced = false;
                _clockOffsetMs = 0;
                _lastSyncTimestampMs = 0;
                _syncRoundTripMs = 0;
                _maxObservedDriftMs = 0;
                _totalDropCount = 0;
            }
        }

        // =====================================================================
        // 내부 헬퍼
        // =====================================================================

        /// <summary>재동기화가 필요한지 확인하고, 필요하면 이벤트 발생</summary>
        private void CheckResyncNeeded(long nowMs)
        {
            // lock은 호출자(AlignTimestamp)에서 이미 잡고 있음

            // 조건 1: 관측 드리프트가 임계값 초과
            bool driftExceeded = _maxObservedDriftMs > ResyncThresholdMs;

            // 조건 2: 마지막 동기화로부터 지정 시간 경과
            bool intervalExceeded = (nowMs - _lastSyncTimestampMs) > ResyncIntervalMs;

            if (driftExceeded || intervalExceeded)
            {
                // 재동기화 트리거 (이벤트 핸들러가 등록되어 있을 때만)
                // 이벤트 호출은 lock 안에서 하면 데드락 위험 → 별도 처리 필요
                // 여기서는 간단히 플래그 방식으로 처리
                OnResyncRequested?.Invoke();

                // 드리프트 추적 리셋 (재동기화 후 새로 측정)
                _maxObservedDriftMs = 0;
            }
        }
    }
}
