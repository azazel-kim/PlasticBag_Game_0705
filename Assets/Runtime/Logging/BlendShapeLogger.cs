// BlendShapeLogger.cs
// BlendShapeSensor에서 발행된 블렌드셰이프 데이터를 CSV로 비동기 저장하는 로거
// Application.persistentDataPath에 저장 (Galaxy XR: /sdcard/Android/data/.../files/)
// Unity 6000.1.17f1, Android XR

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using XRExergame.Sensors;

namespace XRExergame.Logging
{
    /// <summary>
    /// BlendShapeSensor.OnDataUpdated 이벤트를 구독하여
    /// 타임스탬프 + 블렌드셰이프 가중치 + 신뢰도를 CSV 파일에 비동기 저장함.
    ///
    /// 스레드 안전성:
    ///   - 메인 스레드: OnDataReceived에서 ConcurrentQueue에 행 추가
    ///   - 백그라운드 스레드: _writerThread가 큐를 소비하여 파일에 기록
    ///   - OnDestroy에서 FlushAndClose 호출 → 큐 완전 소비 후 파일 닫기
    ///
    /// 저장 경로 예시:
    ///   /sdcard/Android/data/com.DXPLap.PlasticBagGame/files/blendshape_20260417_120000.csv
    /// </summary>
    public class BlendShapeLogger : MonoBehaviour
    {
        // ─────────────────────────────────────────
        // Inspector 설정값
        // ─────────────────────────────────────────

        [Header("로거 설정")]
        [Tooltip("활성화 시 블렌드셰이프 데이터를 CSV로 저장. 성능 측정 완료 후 비활성화 권장.")]
        [SerializeField] private bool _enableLogging = true;

        [Tooltip("CSV 파일 저장 경로. 비워두면 Application.persistentDataPath 사용.")]
        [SerializeField] private string _customSavePath = "";

        [Tooltip("로그 파일명 접두사. 실제 파일명은 '{prefix}_{yyyyMMdd_HHmmss}.csv' 형식.")]
        [SerializeField] private string _filePrefix = "blendshape";

        [Tooltip("백그라운드 쓰기 스레드의 큐 확인 주기 (밀리초).")]
        [SerializeField] private int _writerSleepMs = 16; // ~60Hz와 유사

        // ─────────────────────────────────────────
        // 컴포넌트 참조
        // ─────────────────────────────────────────

        // 데이터를 구독할 BlendShapeSensor (같은 GameObject 또는 씬에서 찾음)
        [SerializeField] private BlendShapeSensor _sensor;

        // ─────────────────────────────────────────
        // 내부 상태
        // ─────────────────────────────────────────

        // 메인스레드 → 백그라운드 스레드 간 스레드 안전 큐
        // ConcurrentQueue는 lock 없이 다중 생산자/소비자를 지원함
        private ConcurrentQueue<string> _writeQueue;

        // 백그라운드 쓰기 스레드
        private Thread _writerThread;

        // 스레드 종료 신호 (true 설정 시 스레드 루프 탈출)
        private volatile bool _stopWriter = false;

        // CSV StreamWriter (백그라운드 스레드에서만 접근)
        private StreamWriter _csvWriter;

        // 로거 초기화 성공 여부
        private bool _isInitialized = false;

        // 저장된 행 수 (통계용)
        private int _rowCount = 0;

        // 파일 전체 경로 (OnDestroy 로그 출력용)
        private string _fullFilePath = "";

        // ─────────────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────────────

        private void Start()
        {
            if (!_enableLogging)
            {
                Debug.Log("[BlendShapeLogger] 로깅 비활성화됨 (Inspector 설정).");
                return;
            }

            // BlendShapeSensor 자동 탐색 (Inspector에 할당 안 됐을 경우)
            if (_sensor == null)
                _sensor = GetComponent<BlendShapeSensor>();

            if (_sensor == null)
                _sensor = FindFirstObjectByType<BlendShapeSensor>();

            if (_sensor == null)
            {
                Debug.LogError("[BlendShapeLogger] BlendShapeSensor를 찾을 수 없음. " +
                    "같은 GameObject에 BlendShapeSensor를 추가하거나 Inspector에서 할당할 것.");
                return;
            }

            if (!InitializeCsvWriter())
                return;

            // 백그라운드 쓰기 스레드 시작
            _writeQueue = new ConcurrentQueue<string>();
            _stopWriter = false;
            _writerThread = new Thread(WriterThreadLoop)
            {
                Name = "BlendShapeLoggerWriter",
                IsBackground = true  // 앱 종료 시 자동 종료
            };
            _writerThread.Start();

            // BlendShapeSensor 이벤트 구독
            _sensor.OnDataUpdated += OnDataReceived;

            _isInitialized = true;
            Debug.Log($"[BlendShapeLogger] 초기화 완료. 저장 경로: {_fullFilePath}");
        }

        private void OnDestroy()
        {
            if (!_isInitialized)
                return;

            // 이벤트 구독 해제 (메모리 누수 방지)
            if (_sensor != null)
                _sensor.OnDataUpdated -= OnDataReceived;

            // 백그라운드 스레드에 종료 신호 전달 및 큐 완전 소비 대기
            FlushAndClose();

            Debug.Log($"[BlendShapeLogger] 로그 저장 완료. 총 {_rowCount}행. 경로: {_fullFilePath}");
        }

        // ─────────────────────────────────────────
        // 이벤트 수신 (메인 스레드)
        // ─────────────────────────────────────────

        /// <summary>
        /// BlendShapeSensor.OnDataUpdated 이벤트 핸들러.
        /// 메인 스레드에서 호출됨 — 파일 I/O 없이 큐에만 행을 추가하여 즉시 반환.
        /// </summary>
        private void OnDataReceived(BlendShapeData data)
        {
            if (!_isInitialized)
                return;

            // CSV 행 생성 (StringBuilder로 문자열 연결 최소화)
            var row = BuildCsvRow(data);

            // 큐에 추가 (lock-free, 메인스레드 블로킹 없음)
            _writeQueue.Enqueue(row);
        }

        // ─────────────────────────────────────────
        // CSV 초기화
        // ─────────────────────────────────────────

        /// <summary>
        /// CSV 파일을 생성하고 헤더를 작성함.
        /// </summary>
        /// <returns>성공 시 true, 실패 시 false</returns>
        private bool InitializeCsvWriter()
        {
            try
            {
                string dir = string.IsNullOrEmpty(_customSavePath)
                    ? Application.persistentDataPath
                    : _customSavePath;

                // 디렉토리가 없으면 생성 (Android에서 최초 실행 시 필요할 수 있음)
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string fileName = $"{_filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                _fullFilePath = Path.Combine(dir, fileName);

                // AutoFlush = false: 백그라운드 스레드에서 배치 쓰기 효율화
                _csvWriter = new StreamWriter(_fullFilePath, append: false, Encoding.UTF8)
                {
                    AutoFlush = false
                };

                // CSV 헤더 작성
                _csvWriter.WriteLine(BuildCsvHeader());
                _csvWriter.Flush();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BlendShapeLogger] CSV 파일 생성 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// CSV 헤더 문자열 생성.
        /// TimestampMs, BS_00 ~ BS_N, Conf_Lower, Conf_LeftUpper, Conf_RightUpper
        /// </summary>
        private string BuildCsvHeader()
        {
            // 실제 파라미터 개수를 XRFaceParameterIndices 열거형에서 읽음
            // (열거형 이름을 헤더에 사용하여 후처리 분석 편의성 향상)
            string[] paramNames = Enum.GetNames(typeof(Google.XR.Extensions.XRFaceParameterIndices));

            var sb = new StringBuilder("TimestampMs");
            foreach (var name in paramNames)
                sb.Append($",{name}");
            sb.Append(",Conf_Lower,Conf_LeftUpper,Conf_RightUpper");

            return sb.ToString();
        }

        // ─────────────────────────────────────────
        // CSV 행 생성
        // ─────────────────────────────────────────

        /// <summary>
        /// BlendShapeData → CSV 한 행 문자열 변환.
        /// 메인 스레드에서 호출됨.
        /// </summary>
        private string BuildCsvRow(BlendShapeData data)
        {
            var sb = new StringBuilder(data.TimestampMs.ToString());

            // 블렌드셰이프 가중치 (소수점 4자리)
            if (data.Weights != null)
            {
                for (int i = 0; i < data.Weights.Length; i++)
                    sb.Append($",{data.Weights[i]:F4}");
            }

            // 신뢰도 3개 영역 ([0]=Lower, [1]=LeftUpper, [2]=RightUpper)
            if (data.RegionConfidence != null && data.RegionConfidence.Length >= 3)
            {
                sb.Append($",{data.RegionConfidence[0]:F3}");
                sb.Append($",{data.RegionConfidence[1]:F3}");
                sb.Append($",{data.RegionConfidence[2]:F3}");
            }
            else
            {
                sb.Append(",0.000,0.000,0.000");
            }

            return sb.ToString();
        }

        // ─────────────────────────────────────────
        // 백그라운드 쓰기 스레드
        // ─────────────────────────────────────────

        /// <summary>
        /// 백그라운드 스레드 루프.
        /// ConcurrentQueue를 소비하여 CSV 파일에 기록함.
        /// _stopWriter가 true가 되고 큐가 비면 종료.
        /// </summary>
        private void WriterThreadLoop()
        {
            while (!_stopWriter || !_writeQueue.IsEmpty)
            {
                // 큐에서 최대 64행씩 배치로 처리 (I/O 호출 빈도 감소)
                int batchCount = 0;
                while (batchCount < 64 && _writeQueue.TryDequeue(out string row))
                {
                    try
                    {
                        _csvWriter.WriteLine(row);
                        _rowCount++;
                        batchCount++;
                    }
                    catch (Exception e)
                    {
                        // 파일 쓰기 실패 시 스레드 계속 유지 (한 행 실패가 전체 중단되면 안 됨)
                        Debug.LogError($"[BlendShapeLogger] 행 쓰기 실패: {e.Message}");
                    }
                }

                if (batchCount > 0)
                {
                    // 배치 완료 후 플러시 (AutoFlush=false이므로 수동 플러시)
                    try { _csvWriter.Flush(); }
                    catch (Exception e) { Debug.LogError($"[BlendShapeLogger] Flush 실패: {e.Message}"); }
                }
                else if (!_stopWriter)
                {
                    // 큐가 비어있으면 잠시 대기 (CPU 스핀 방지)
                    Thread.Sleep(_writerSleepMs);
                }
            }

            // 최종 플러시 및 파일 닫기
            try
            {
                _csvWriter?.Flush();
                _csvWriter?.Close();
                _csvWriter = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BlendShapeLogger] 파일 닫기 실패: {e.Message}");
            }
        }

        /// <summary>
        /// OnDestroy에서 호출. 백그라운드 스레드에 종료 신호 전달 후
        /// 큐가 완전히 비워질 때까지 최대 3초 대기.
        /// </summary>
        private void FlushAndClose()
        {
            _stopWriter = true;

            // 백그라운드 스레드가 완료될 때까지 최대 3초 대기
            if (_writerThread != null && _writerThread.IsAlive)
                _writerThread.Join(millisecondsTimeout: 3000);
        }
    }
}
