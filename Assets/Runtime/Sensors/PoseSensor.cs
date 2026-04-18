// PoseSensor.cs
// UDP로 수신된 MediaPipe 포즈 데이터를 ISensorProvider<PoseData>로 제공
// Python pose_bridge.py에서 FUSE 프로토콜로 전송한 데이터를 수신
// Samsung Galaxy XR (Android XR) — Unity 6000.1.17f1

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using XRExergame.Fusion;

namespace XRExergame.Sensors
{
    /// <summary>
    /// OBSBOT + MediaPipe에서 UDP로 전송된 포즈 데이터를 수신하는 센서.
    /// Python pose_bridge.py와 FUSE 프로토콜로 통신함.
    /// </summary>
    public class PoseSensor : MonoBehaviour, ISensorProvider<PoseData>
    {
        [Header("UDP 수신 설정")]
        [SerializeField] private int _listenPort = 9001;

        [Header("디버그")]
        [SerializeField] private bool _logReceived = false;

        // ISensorProvider 구현
        public bool IsAvailable => _isReceiving;
        public float SampleRateHz => _currentFps;
        public event Action<PoseData> OnDataUpdated;

        // 수신 상태
        private UdpClient _udpClient;
        private Thread _receiveThread;
        private volatile bool _isRunning;
        private volatile bool _isReceiving;

        // 최신 데이터 (스레드 간 공유)
        private PoseData _latestPose;
        private readonly object _lock = new object();
        private volatile bool _hasNewData;

        // FPS 측정
        private float _currentFps;
        private int _frameCount;
        private float _fpsTimer;

        // 통계
        private uint _totalReceived;
        private uint _totalErrors;

        public PoseData? GetLatestData()
        {
            if (!_isReceiving) return null;
            lock (_lock)
            {
                return _latestPose;
            }
        }

        private void OnEnable()
        {
            StartReceiver();
        }

        private void OnDisable()
        {
            StopReceiver();
        }

        private void Update()
        {
            // 수신 스레드에서 받은 데이터를 메인 스레드에서 이벤트로 발행
            if (_hasNewData)
            {
                _hasNewData = false;
                PoseData data;
                lock (_lock)
                {
                    data = _latestPose;
                }
                OnDataUpdated?.Invoke(data);

                _frameCount++;
            }

            // FPS 계산 (1초 주기)
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 1f)
            {
                _currentFps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0f;
            }
        }

        private void StartReceiver()
        {
            if (_isRunning) return;

            try
            {
                _udpClient = new UdpClient(_listenPort);
                _udpClient.Client.ReceiveTimeout = 1000; // 1초 타임아웃
                _isRunning = true;

                _receiveThread = new Thread(ReceiveLoop)
                {
                    Name = "PoseSensor_UDP",
                    IsBackground = true
                };
                _receiveThread.Start();

                Debug.Log($"[PoseSensor] UDP 수신 시작 (port {_listenPort})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PoseSensor] UDP 초기화 실패: {e.Message}");
            }
        }

        private void StopReceiver()
        {
            _isRunning = false;
            _isReceiving = false;

            _udpClient?.Close();
            _udpClient = null;

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join(2000);
            }
            _receiveThread = null;

            Debug.Log($"[PoseSensor] 종료 (수신: {_totalReceived}, 오류: {_totalErrors})");
        }

        /// <summary>
        /// 백그라운드 스레드: UDP 패킷 수신 루프
        /// </summary>
        private void ReceiveLoop()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (_isRunning)
            {
                try
                {
                    byte[] data = _udpClient.Receive(ref remoteEP);

                    // FUSE 프로토콜 검증
                    if (!UdpProtocol.IsValidPacket(data, data.Length))
                        continue;

                    // FusedDataFrame 디코딩
                    if (!UdpProtocol.DecodeFusedFrame(data, out FusedDataFrame frame))
                    {
                        _totalErrors++;
                        continue;
                    }

                    // Pose 플래그 확인
                    if (!frame.IsSensorValid(ValidSensorFlags.Pose))
                        continue;

                    // 메인 스레드로 전달
                    lock (_lock)
                    {
                        _latestPose = frame.Pose;
                    }
                    _hasNewData = true;
                    _isReceiving = true;
                    _totalReceived++;

                    if (_logReceived && _totalReceived % 30 == 0)
                    {
                        Debug.Log($"[PoseSensor] 수신 #{_totalReceived} conf={frame.Pose.Confidence:F2}");
                    }
                }
                catch (SocketException)
                {
                    // 타임아웃 또는 소켓 닫힘 — 무시하고 루프 계속
                }
                catch (ObjectDisposedException)
                {
                    // 소켓이 Dispose됨 — 루프 종료
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            StopReceiver();
        }
    }
}
