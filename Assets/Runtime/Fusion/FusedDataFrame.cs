// =============================================================================
// FusedDataFrame.cs — 6종 센서 융합 데이터 프레임 (DTO)
// 네임스페이스: XRExergame.Fusion
// 역할: PC에서 30Hz로 조립된 모든 센서 데이터를 하나의 구조체로 묶어
//       UDP를 통해 Galaxy XR로 전송하기 위한 표준 데이터 형식
// =============================================================================

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace XRExergame.Fusion
{
    // =========================================================================
    // ValidSensorFlags — 비트마스크로 어떤 센서 데이터가 유효한지 표시
    // 예: BlendShape + EyeTracking만 유효 → 0b00000011 = 3
    // =========================================================================
    [Flags]
    public enum ValidSensorFlags : byte
    {
        None         = 0,
        BlendShape   = 1 << 0,  // Galaxy XR 얼굴 블렌드셰이프 (72Hz)
        EyeTracking  = 1 << 1,  // Galaxy XR 시선 추적 (72Hz)
        EEG          = 1 << 2,  // LinkBand2 뇌파 (256Hz)
        PPG          = 1 << 3,  // LinkBand2 광혈류 (25Hz)
        ACC          = 1 << 4,  // LinkBand2 가속도 (60Hz)
        Pose         = 1 << 5,  // OBSBOT MediaPipe 포즈 (30Hz)
        IMU          = 1 << 6,  // X-Sens 전신 IMU (60~120Hz)
        All          = 0x7F     // 7개 센서 전부 유효
    }

    // =========================================================================
    // 센서별 서브 구조체 — 각각 [Serializable] struct로 GC 부담 최소화
    // =========================================================================

    /// <summary>
    /// 얼굴 블렌드셰이프 데이터 (Galaxy XR 온디바이스)
    /// ARKit 호환 68개 계수 + 머리 회전(euler) + 전체 신뢰도
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BlendShapeData
    {
        // ARKit 호환 블렌드셰이프 계수 68개 (0.0~1.0)
        // 예: eyeBlinkLeft, eyeBlinkRight, jawOpen, mouthSmileLeft 등
        public const int BLEND_SHAPE_COUNT = 68;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = BLEND_SHAPE_COUNT)]
        public float[] Weights;

        // 머리 회전 (오일러 각도, degrees)
        // x=pitch(고개 끄덕), y=yaw(고개 좌우), z=roll(고개 기울임)
        public Vector3 HeadRotation;

        // 추적 신뢰도 (0.0 = 추적 실패, 1.0 = 완벽)
        public float Confidence;

        /// <summary>기본값으로 초기화된 인스턴스 생성</summary>
        public static BlendShapeData Create()
        {
            return new BlendShapeData
            {
                Weights = new float[BLEND_SHAPE_COUNT],
                HeadRotation = Vector3.zero,
                Confidence = 0f
            };
        }
    }

    /// <summary>
    /// 시선 추적 데이터 (Galaxy XR 온디바이스, OpenXR EyeGazeInteraction)
    /// 좌/우 눈 회전 쿼터니언 + 눈 깜빡임 감지 + 시선 방향 벡터
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct EyeTrackingData
    {
        public Quaternion LeftEyeRotation;   // 왼쪽 눈 회전
        public Quaternion RightEyeRotation;  // 오른쪽 눈 회전
        public Vector3 GazeDirection;        // 양안 통합 시선 방향 (월드 좌표)
        public Vector3 GazeOrigin;           // 시선 원점 (양안 중간점)
        public bool LeftBlink;               // 왼쪽 눈 깜빡임
        public bool RightBlink;              // 오른쪽 눈 깜빡임
        public float Confidence;             // 시선 추적 신뢰도 (0.0~1.0)
    }

    /// <summary>
    /// EEG 뇌파 데이터 (LinkBand2, 256Hz → 30Hz 다운샘플링)
    /// 6채널 전압(µV) + 전극 접촉 상태
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct EEGData
    {
        public const int CHANNEL_COUNT = 6;

        // 6채널 전압값 (단위: µV, 마이크로볼트)
        // 30Hz 프레임 시점에서의 최근 평균값 또는 대표값
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CHANNEL_COUNT)]
        public float[] ChannelVoltageMicroV;

        // 전극 접촉 상태 (0=미접촉, 1=불량, 2=보통, 3=양호)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CHANNEL_COUNT)]
        public byte[] ElectrodeStatus;

        // 데이터 품질 (0.0~1.0, 양호한 접촉 채널 비율)
        public float DataQuality;

        public static EEGData Create()
        {
            return new EEGData
            {
                ChannelVoltageMicroV = new float[CHANNEL_COUNT],
                ElectrodeStatus = new byte[CHANNEL_COUNT],
                DataQuality = 0f
            };
        }
    }

    /// <summary>
    /// PPG 광혈류 데이터 (LinkBand2, 25Hz)
    /// 적외선(IR) + 적색광(Red) 원시값 + 추정 심박수
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct PPGData
    {
        public float IrValue;       // 적외선 반사량 (raw ADC)
        public float RedValue;      // 적색광 반사량 (raw ADC)
        public float HeartRateBpm;  // 추정 심박수 (BPM, 유효하지 않으면 0)
        public float SpO2;          // 추정 산소포화도 (%, 유효하지 않으면 0)
        public float DataQuality;   // 센서 접촉 품질 (0.0~1.0)
    }

    /// <summary>
    /// 가속도 데이터 (LinkBand2, 60Hz)
    /// 3축 가속도 (g 단위)
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ACCData
    {
        public Vector3 Acceleration;  // 3축 가속도 (단위: g, 1g ≈ 9.81 m/s²)
        public float Magnitude;       // 가속도 크기 (√(x²+y²+z²)), 움직임 강도 판단용
    }

    /// <summary>
    /// 포즈 추정 데이터 (OBSBOT + MediaPipe, 30Hz)
    /// 33개 랜드마크 x (x, y, z, visibility) = 132 float
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct PoseData
    {
        // MediaPipe Pose 33개 랜드마크
        // 각 랜드마크: [x, y, z, visibility] (정규화 좌표 0.0~1.0)
        // 인덱스 i의 랜드마크: Landmarks[i*4+0]=x, [i*4+1]=y, [i*4+2]=z, [i*4+3]=visibility
        public const int LANDMARK_COUNT = 33;
        public const int FLOATS_PER_LANDMARK = 4; // x, y, z, visibility
        public const int TOTAL_FLOATS = LANDMARK_COUNT * FLOATS_PER_LANDMARK; // 132

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = TOTAL_FLOATS)]
        public float[] Landmarks;

        // 전체 포즈 감지 신뢰도 (0.0~1.0)
        public float Confidence;

        public static PoseData Create()
        {
            return new PoseData
            {
                Landmarks = new float[TOTAL_FLOATS],
                Confidence = 0f
            };
        }

        /// <summary>
        /// 특정 랜드마크의 3D 좌표 가져오기
        /// landmarkIndex: 0~32 (MediaPipe Pose 랜드마크 번호)
        /// </summary>
        public Vector3 GetLandmarkPosition(int landmarkIndex)
        {
            if (Landmarks == null || landmarkIndex < 0 || landmarkIndex >= LANDMARK_COUNT)
                return Vector3.zero;
            int offset = landmarkIndex * FLOATS_PER_LANDMARK;
            return new Vector3(Landmarks[offset], Landmarks[offset + 1], Landmarks[offset + 2]);
        }

        /// <summary>특정 랜드마크의 가시성(visibility) 가져오기</summary>
        public float GetLandmarkVisibility(int landmarkIndex)
        {
            if (Landmarks == null || landmarkIndex < 0 || landmarkIndex >= LANDMARK_COUNT)
                return 0f;
            return Landmarks[landmarkIndex * FLOATS_PER_LANDMARK + 3];
        }
    }

    /// <summary>
    /// IMU 관성 측정 데이터 (X-Sens, 60~120Hz)
    /// 회전(쿼터니언) + 가속도 + 자이로 + 자기장
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct IMUData
    {
        public Quaternion Rotation;      // 절대 방향 (쿼터니언)
        public Vector3 Acceleration;     // 선형 가속도 (m/s²)
        public Vector3 AngularVelocity;  // 각속도 (rad/s)
        public Vector3 MagneticField;    // 자기장 (µT, 마이크로테슬라)
        public float DataQuality;        // 센서 캘리브레이션 품질 (0.0~1.0)
    }

    /// <summary>
    /// 몰입도 설문 데이터 (IEQ: Immersive Experience Questionnaire)
    /// 4개 영역의 리커트 척도 (1~7)
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct IEQData
    {
        // 각 값은 리커트 7점 척도 (1=전혀 아님, 7=매우 그러함)
        public int CognitiveInvolvement;  // 인지적 몰입
        public int EmotionalInvolvement;  // 감정적 몰입
        public int RealWorldDissociation; // 현실 분리감
        public int Challenge;             // 도전감/난이도 적절성
        public long SurveyTimestampMs;    // 설문 응답 시각 (Unix ms)
    }

    // =========================================================================
    // FusedDataFrame — 핵심 융합 프레임 구조체
    // PC의 TimeSyncAligner가 30Hz 주기로 조립 → UDP 9001로 Galaxy XR에 전송
    // =========================================================================

    /// <summary>
    /// 30Hz 융합 데이터 프레임.
    /// 6종 센서 + 설문의 최신 데이터를 타임스탬프 기준으로 정렬한 스냅샷.
    /// struct이므로 힙 할당 없이 스택에서 처리 가능 (GC 부담 최소).
    /// 단, 배열 필드(BlendShapeData.Weights 등)는 힙에 할당됨.
    /// </summary>
    [Serializable]
    public struct FusedDataFrame
    {
        // ----- 프레임 메타정보 -----
        public long TimestampMs;         // 융합 시점의 Unix 타임스탬프 (밀리초)
        public uint FrameNumber;         // 세션 시작 후 프레임 일련번호 (0부터)
        public ValidSensorFlags ValidSensors;  // 이 프레임에서 유효한 센서 비트마스크

        // ----- 센서 데이터 -----
        public BlendShapeData BlendShape;
        public EyeTrackingData EyeTracking;
        public EEGData EEG;
        public PPGData PPG;
        public ACCData ACC;
        public PoseData Pose;
        public IMUData IMU;

        // ----- 설문 데이터 (비주기적, 마지막 응답값 유지) -----
        public IEQData IEQ;

        // =====================================================================
        // 유틸리티 메서드
        // =====================================================================

        /// <summary>특정 센서가 유효한지 확인</summary>
        public bool IsSensorValid(ValidSensorFlags sensor)
        {
            return (ValidSensors & sensor) != 0;
        }

        /// <summary>유효한 센서 개수 반환 (최대 7)</summary>
        public int ValidSensorCount
        {
            get
            {
                int count = 0;
                byte flags = (byte)ValidSensors;
                // 브라이언 커니건(Brian Kernighan)의 비트 카운팅 알고리즘
                while (flags != 0)
                {
                    flags &= (byte)(flags - 1);
                    count++;
                }
                return count;
            }
        }

        // =====================================================================
        // UDP 직렬화 — 바이트 배열 변환 (버퍼 재사용 패턴)
        // =====================================================================

        // 페이로드 크기 상수 (배열 필드의 고정 크기 기준)
        // 실제 크기는 ToBytes()에서 동적 계산
        private const int HEADER_SIZE = 8 + 4 + 1; // TimestampMs(8) + FrameNumber(4) + ValidSensors(1) = 13

        /// <summary>
        /// 이 프레임을 바이트 배열로 직렬화.
        /// UDP 전송용. 수신 측에서 FromBytes()로 복원.
        /// </summary>
        public byte[] ToBytes()
        {
            // 버퍼 크기 계산: 보수적으로 2048바이트 할당
            // 실제 운영에서는 정적 버퍼 풀 사용 권장
            byte[] buffer = new byte[2048];
            int offset = 0;

            // --- 헤더 ---
            WriteInt64(buffer, ref offset, TimestampMs);
            WriteUInt32(buffer, ref offset, FrameNumber);
            buffer[offset++] = (byte)ValidSensors;

            // --- BlendShape ---
            if (IsSensorValid(ValidSensorFlags.BlendShape))
            {
                WriteFloatArray(buffer, ref offset, BlendShape.Weights, BlendShapeData.BLEND_SHAPE_COUNT);
                WriteVector3(buffer, ref offset, BlendShape.HeadRotation);
                WriteFloat(buffer, ref offset, BlendShape.Confidence);
            }

            // --- EyeTracking ---
            if (IsSensorValid(ValidSensorFlags.EyeTracking))
            {
                WriteQuaternion(buffer, ref offset, EyeTracking.LeftEyeRotation);
                WriteQuaternion(buffer, ref offset, EyeTracking.RightEyeRotation);
                WriteVector3(buffer, ref offset, EyeTracking.GazeDirection);
                WriteVector3(buffer, ref offset, EyeTracking.GazeOrigin);
                WriteBool(buffer, ref offset, EyeTracking.LeftBlink);
                WriteBool(buffer, ref offset, EyeTracking.RightBlink);
                WriteFloat(buffer, ref offset, EyeTracking.Confidence);
            }

            // --- EEG ---
            if (IsSensorValid(ValidSensorFlags.EEG))
            {
                WriteFloatArray(buffer, ref offset, EEG.ChannelVoltageMicroV, EEGData.CHANNEL_COUNT);
                WriteByteArray(buffer, ref offset, EEG.ElectrodeStatus, EEGData.CHANNEL_COUNT);
                WriteFloat(buffer, ref offset, EEG.DataQuality);
            }

            // --- PPG ---
            if (IsSensorValid(ValidSensorFlags.PPG))
            {
                WriteFloat(buffer, ref offset, PPG.IrValue);
                WriteFloat(buffer, ref offset, PPG.RedValue);
                WriteFloat(buffer, ref offset, PPG.HeartRateBpm);
                WriteFloat(buffer, ref offset, PPG.SpO2);
                WriteFloat(buffer, ref offset, PPG.DataQuality);
            }

            // --- ACC ---
            if (IsSensorValid(ValidSensorFlags.ACC))
            {
                WriteVector3(buffer, ref offset, ACC.Acceleration);
                WriteFloat(buffer, ref offset, ACC.Magnitude);
            }

            // --- Pose ---
            if (IsSensorValid(ValidSensorFlags.Pose))
            {
                WriteFloatArray(buffer, ref offset, Pose.Landmarks, PoseData.TOTAL_FLOATS);
                WriteFloat(buffer, ref offset, Pose.Confidence);
            }

            // --- IMU ---
            if (IsSensorValid(ValidSensorFlags.IMU))
            {
                WriteQuaternion(buffer, ref offset, IMU.Rotation);
                WriteVector3(buffer, ref offset, IMU.Acceleration);
                WriteVector3(buffer, ref offset, IMU.AngularVelocity);
                WriteVector3(buffer, ref offset, IMU.MagneticField);
                WriteFloat(buffer, ref offset, IMU.DataQuality);
            }

            // 실제 사용한 크기만큼 잘라서 반환
            byte[] result = new byte[offset];
            Buffer.BlockCopy(buffer, 0, result, 0, offset);
            return result;
        }

        /// <summary>
        /// 바이트 배열에서 FusedDataFrame 복원.
        /// 수신 측(Galaxy XR)에서 UDP 패킷을 파싱할 때 사용.
        /// </summary>
        public static FusedDataFrame FromBytes(byte[] data)
        {
            var frame = new FusedDataFrame();
            int offset = 0;

            // --- 헤더 ---
            frame.TimestampMs = ReadInt64(data, ref offset);
            frame.FrameNumber = ReadUInt32(data, ref offset);
            frame.ValidSensors = (ValidSensorFlags)data[offset++];

            // --- BlendShape ---
            if (frame.IsSensorValid(ValidSensorFlags.BlendShape))
            {
                frame.BlendShape.Weights = ReadFloatArray(data, ref offset, BlendShapeData.BLEND_SHAPE_COUNT);
                frame.BlendShape.HeadRotation = ReadVector3(data, ref offset);
                frame.BlendShape.Confidence = ReadFloat(data, ref offset);
            }

            // --- EyeTracking ---
            if (frame.IsSensorValid(ValidSensorFlags.EyeTracking))
            {
                frame.EyeTracking.LeftEyeRotation = ReadQuaternion(data, ref offset);
                frame.EyeTracking.RightEyeRotation = ReadQuaternion(data, ref offset);
                frame.EyeTracking.GazeDirection = ReadVector3(data, ref offset);
                frame.EyeTracking.GazeOrigin = ReadVector3(data, ref offset);
                frame.EyeTracking.LeftBlink = ReadBool(data, ref offset);
                frame.EyeTracking.RightBlink = ReadBool(data, ref offset);
                frame.EyeTracking.Confidence = ReadFloat(data, ref offset);
            }

            // --- EEG ---
            if (frame.IsSensorValid(ValidSensorFlags.EEG))
            {
                frame.EEG.ChannelVoltageMicroV = ReadFloatArray(data, ref offset, EEGData.CHANNEL_COUNT);
                frame.EEG.ElectrodeStatus = ReadByteArray(data, ref offset, EEGData.CHANNEL_COUNT);
                frame.EEG.DataQuality = ReadFloat(data, ref offset);
            }

            // --- PPG ---
            if (frame.IsSensorValid(ValidSensorFlags.PPG))
            {
                frame.PPG.IrValue = ReadFloat(data, ref offset);
                frame.PPG.RedValue = ReadFloat(data, ref offset);
                frame.PPG.HeartRateBpm = ReadFloat(data, ref offset);
                frame.PPG.SpO2 = ReadFloat(data, ref offset);
                frame.PPG.DataQuality = ReadFloat(data, ref offset);
            }

            // --- ACC ---
            if (frame.IsSensorValid(ValidSensorFlags.ACC))
            {
                frame.ACC.Acceleration = ReadVector3(data, ref offset);
                frame.ACC.Magnitude = ReadFloat(data, ref offset);
            }

            // --- Pose ---
            if (frame.IsSensorValid(ValidSensorFlags.Pose))
            {
                frame.Pose.Landmarks = ReadFloatArray(data, ref offset, PoseData.TOTAL_FLOATS);
                frame.Pose.Confidence = ReadFloat(data, ref offset);
            }

            // --- IMU ---
            if (frame.IsSensorValid(ValidSensorFlags.IMU))
            {
                frame.IMU.Rotation = ReadQuaternion(data, ref offset);
                frame.IMU.Acceleration = ReadVector3(data, ref offset);
                frame.IMU.AngularVelocity = ReadVector3(data, ref offset);
                frame.IMU.MagneticField = ReadVector3(data, ref offset);
                frame.IMU.DataQuality = ReadFloat(data, ref offset);
            }

            return frame;
        }

        // =====================================================================
        // 바이너리 읽기/쓰기 헬퍼 — 리틀 엔디안(Little-Endian)
        // GC 할당을 피하기 위해 BitConverter 대신 직접 처리
        // =====================================================================

        private static void WriteInt64(byte[] buf, ref int offset, long value)
        {
            buf[offset++] = (byte)(value);
            buf[offset++] = (byte)(value >> 8);
            buf[offset++] = (byte)(value >> 16);
            buf[offset++] = (byte)(value >> 24);
            buf[offset++] = (byte)(value >> 32);
            buf[offset++] = (byte)(value >> 40);
            buf[offset++] = (byte)(value >> 48);
            buf[offset++] = (byte)(value >> 56);
        }

        private static long ReadInt64(byte[] buf, ref int offset)
        {
            long value = (long)buf[offset]
                       | ((long)buf[offset + 1] << 8)
                       | ((long)buf[offset + 2] << 16)
                       | ((long)buf[offset + 3] << 24)
                       | ((long)buf[offset + 4] << 32)
                       | ((long)buf[offset + 5] << 40)
                       | ((long)buf[offset + 6] << 48)
                       | ((long)buf[offset + 7] << 56);
            offset += 8;
            return value;
        }

        private static void WriteUInt32(byte[] buf, ref int offset, uint value)
        {
            buf[offset++] = (byte)(value);
            buf[offset++] = (byte)(value >> 8);
            buf[offset++] = (byte)(value >> 16);
            buf[offset++] = (byte)(value >> 24);
        }

        private static uint ReadUInt32(byte[] buf, ref int offset)
        {
            uint value = (uint)buf[offset]
                       | ((uint)buf[offset + 1] << 8)
                       | ((uint)buf[offset + 2] << 16)
                       | ((uint)buf[offset + 3] << 24);
            offset += 4;
            return value;
        }

        private static void WriteFloat(byte[] buf, ref int offset, float value)
        {
            // unsafe 없이 float→bytes 변환
            byte[] bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buf, offset, 4);
            offset += 4;
        }

        private static float ReadFloat(byte[] buf, ref int offset)
        {
            float value = BitConverter.ToSingle(buf, offset);
            offset += 4;
            return value;
        }

        private static void WriteBool(byte[] buf, ref int offset, bool value)
        {
            buf[offset++] = value ? (byte)1 : (byte)0;
        }

        private static bool ReadBool(byte[] buf, ref int offset)
        {
            return buf[offset++] != 0;
        }

        private static void WriteVector3(byte[] buf, ref int offset, Vector3 v)
        {
            WriteFloat(buf, ref offset, v.x);
            WriteFloat(buf, ref offset, v.y);
            WriteFloat(buf, ref offset, v.z);
        }

        private static Vector3 ReadVector3(byte[] buf, ref int offset)
        {
            float x = ReadFloat(buf, ref offset);
            float y = ReadFloat(buf, ref offset);
            float z = ReadFloat(buf, ref offset);
            return new Vector3(x, y, z);
        }

        private static void WriteQuaternion(byte[] buf, ref int offset, Quaternion q)
        {
            WriteFloat(buf, ref offset, q.x);
            WriteFloat(buf, ref offset, q.y);
            WriteFloat(buf, ref offset, q.z);
            WriteFloat(buf, ref offset, q.w);
        }

        private static Quaternion ReadQuaternion(byte[] buf, ref int offset)
        {
            float x = ReadFloat(buf, ref offset);
            float y = ReadFloat(buf, ref offset);
            float z = ReadFloat(buf, ref offset);
            float w = ReadFloat(buf, ref offset);
            return new Quaternion(x, y, z, w);
        }

        private static void WriteFloatArray(byte[] buf, ref int offset, float[] arr, int count)
        {
            if (arr == null || arr.Length < count)
            {
                // null이면 0으로 채움
                for (int i = 0; i < count; i++)
                    WriteFloat(buf, ref offset, 0f);
                return;
            }
            for (int i = 0; i < count; i++)
                WriteFloat(buf, ref offset, arr[i]);
        }

        private static float[] ReadFloatArray(byte[] buf, ref int offset, int count)
        {
            float[] arr = new float[count];
            for (int i = 0; i < count; i++)
                arr[i] = ReadFloat(buf, ref offset);
            return arr;
        }

        private static void WriteByteArray(byte[] buf, ref int offset, byte[] arr, int count)
        {
            if (arr == null || arr.Length < count)
            {
                for (int i = 0; i < count; i++)
                    buf[offset++] = 0;
                return;
            }
            Buffer.BlockCopy(arr, 0, buf, offset, count);
            offset += count;
        }

        private static byte[] ReadByteArray(byte[] buf, ref int offset, int count)
        {
            byte[] arr = new byte[count];
            Buffer.BlockCopy(buf, offset, arr, 0, count);
            offset += count;
            return arr;
        }

        // =====================================================================
        // ToString — 디버그용 요약 문자열
        // =====================================================================

        public override string ToString()
        {
            return $"[FusedFrame #{FrameNumber} @{TimestampMs}ms | " +
                   $"Valid: {ValidSensors} ({ValidSensorCount}/7)]";
        }
    }
}
