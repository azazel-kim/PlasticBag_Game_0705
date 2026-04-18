// PoseVisualizer.cs
// PoseSensor에서 수신한 33개 MediaPipe 랜드마크를 3D 공간에 시각화
// 에디터 테스트 및 디버깅용

using UnityEngine;
using XRExergame.Fusion;

namespace XRExergame.Sensors
{
    /// <summary>
    /// PoseSensor에서 수신한 포즈 데이터를 Scene 뷰에서 Gizmo로 시각화합니다.
    /// 에디터/런타임 양쪽에서 동작합니다.
    /// </summary>
    [RequireComponent(typeof(PoseSensor))]
    public class PoseVisualizer : MonoBehaviour
    {
        [Header("시각화 설정")]
        [SerializeField] private float _scale = 2f;           // 좌표 스케일 (MediaPipe 정규화 → 월드)
        [SerializeField] private Vector3 _offset = new Vector3(0, 2, 2); // 시각화 위치 오프셋
        [SerializeField] private float _sphereRadius = 0.02f; // 랜드마크 구 크기

        [Header("색상")]
        [SerializeField] private Color _landmarkColor = Color.green;
        [SerializeField] private Color _connectionColor = new Color(0, 0.5f, 1f, 0.8f);
        [SerializeField] private Color _lowConfidenceColor = new Color(1f, 0f, 0f, 0.3f);
        [SerializeField] private float _minVisibility = 0.5f; // 이 값 미만이면 반투명 표시

        [Header("UI")]
        [SerializeField] private bool _showStats = true;

        private PoseSensor _sensor;
        private PoseData? _currentPose;
        private float _lastConfidence;

        // MediaPipe Pose 연결선 (33 landmark pairs)
        private static readonly int[,] CONNECTIONS = {
            // 얼굴
            {0, 1}, {1, 2}, {2, 3}, {3, 7},
            {0, 4}, {4, 5}, {5, 6}, {6, 8},
            {9, 10},
            // 몸통
            {11, 12}, {11, 23}, {12, 24}, {23, 24},
            // 왼팔
            {11, 13}, {13, 15}, {15, 17}, {15, 19}, {15, 21}, {17, 19},
            // 오른팔
            {12, 14}, {14, 16}, {16, 18}, {16, 20}, {16, 22}, {18, 20},
            // 왼다리
            {23, 25}, {25, 27}, {27, 29}, {27, 31}, {29, 31},
            // 오른다리
            {24, 26}, {26, 28}, {28, 30}, {28, 32}, {30, 32},
        };

        private void Awake()
        {
            _sensor = GetComponent<PoseSensor>();
        }

        private void OnEnable()
        {
            _sensor.OnDataUpdated += OnPoseUpdated;
        }

        private void OnDisable()
        {
            _sensor.OnDataUpdated -= OnPoseUpdated;
        }

        private void OnPoseUpdated(PoseData pose)
        {
            _currentPose = pose;
            _lastConfidence = pose.Confidence;
        }

        private void OnDrawGizmos()
        {
            if (!_currentPose.HasValue) return;
            var pose = _currentPose.Value;
            if (pose.Landmarks == null) return;

            // 랜드마크 그리기
            for (int i = 0; i < PoseData.LANDMARK_COUNT; i++)
            {
                Vector3 pos = LandmarkToWorld(pose, i);
                float vis = pose.GetLandmarkVisibility(i);

                Gizmos.color = vis >= _minVisibility ? _landmarkColor : _lowConfidenceColor;
                Gizmos.DrawSphere(pos, _sphereRadius);
            }

            // 연결선 그리기
            Gizmos.color = _connectionColor;
            for (int i = 0; i < CONNECTIONS.GetLength(0); i++)
            {
                int a = CONNECTIONS[i, 0];
                int b = CONNECTIONS[i, 1];

                float visA = pose.GetLandmarkVisibility(a);
                float visB = pose.GetLandmarkVisibility(b);

                if (visA >= _minVisibility && visB >= _minVisibility)
                {
                    Gizmos.DrawLine(LandmarkToWorld(pose, a), LandmarkToWorld(pose, b));
                }
            }
        }

        /// <summary>
        /// MediaPipe 정규화 좌표(0~1)를 Unity 월드 좌표로 변환.
        /// MediaPipe: x→오른쪽, y→아래, z→카메라에서 멀어짐
        /// Unity:     x→오른쪽, y→위,   z→카메라에서 멀어짐
        /// </summary>
        private Vector3 LandmarkToWorld(PoseData pose, int index)
        {
            Vector3 mp = pose.GetLandmarkPosition(index);
            // Y축 반전 (MediaPipe y-down → Unity y-up) + 중앙 정렬
            return new Vector3(
                (mp.x - 0.5f) * _scale,
                (0.5f - mp.y) * _scale,
                mp.z * _scale
            ) + _offset;
        }

        private void OnGUI()
        {
            if (!_showStats) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 120));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"<b>PoseBridge Status</b>");
            GUILayout.Label($"수신 상태: {(_sensor.IsAvailable ? "<color=green>연결됨</color>" : "<color=red>대기 중</color>")}");
            GUILayout.Label($"FPS: {_sensor.SampleRateHz:F1} Hz");
            GUILayout.Label($"신뢰도: {_lastConfidence:F2}");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
