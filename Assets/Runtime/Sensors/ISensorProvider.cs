// ISensorProvider.cs
// 모든 온디바이스 센서의 공통 인터페이스 정의
// FusedDataFrame에 데이터를 공급하는 센서들이 이 인터페이스를 구현함
// Samsung Galaxy XR (Android XR) — Unity 6000.1.17f1

namespace XRExergame.Sensors
{
    /// <summary>
    /// 제네릭 센서 제공자 인터페이스.
    /// T는 반드시 struct여야 함 (BlendShapeData, EyeTrackingData 등).
    /// </summary>
    /// <typeparam name="T">센서가 생산하는 데이터 구조체 타입</typeparam>
    public interface ISensorProvider<T> where T : struct
    {
        /// <summary>
        /// 센서가 현재 사용 가능한 상태인지 여부.
        /// 퍼미션 미승인, 하드웨어 미지원, 초기화 실패 시 false 반환.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 센서의 샘플링 주파수 (Hz).
        /// BlendShape/EyeTracking 모두 72Hz 목표.
        /// </summary>
        float SampleRateHz { get; }

        /// <summary>
        /// 가장 최근에 수집된 센서 데이터를 반환.
        /// 데이터가 없으면 null 반환 (nullable struct).
        /// </summary>
        T? GetLatestData();

        /// <summary>
        /// 새로운 센서 데이터가 수집될 때마다 발행되는 이벤트.
        /// Update() 또는 FixedUpdate()에서 발행됨.
        /// </summary>
        event System.Action<T> OnDataUpdated;
    }
}
