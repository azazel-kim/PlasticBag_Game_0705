// =============================================================================
// RingBuffer.cs — Thread-safe 제네릭 순환 버퍼
// 네임스페이스: XRExergame.Fusion
// 역할: 각 센서의 데이터를 고정 크기 순환 버퍼에 저장하고,
//       특정 타임스탬프에 가장 가까운 데이터를 O(log n)으로 검색
// =============================================================================
//
// 사용 예시:
//   var eegBuffer = new RingBuffer<EEGData>(512);
//   eegBuffer.Push(eegSample, currentTimestampMs);
//   EEGData? nearest = eegBuffer.GetNearest(targetTimestamp, maxDriftMs: 100);
//
// 동작 원리:
//   - 고정 크기 배열을 순환하면서 덮어쓰기 (가장 오래된 데이터부터 제거)
//   - 타임스탬프는 별도 배열에 병렬 저장
//   - GetNearest()는 이진 탐색으로 가장 가까운 샘플을 찾음
//   - lock 기반으로 멀티스레드 안전 보장 (BLE 수신 스레드 ↔ 융합 스레드)
// =============================================================================

using System;

namespace XRExergame.Fusion
{
    /// <summary>
    /// Thread-safe 순환 버퍼. 센서 데이터를 타임스탬프와 함께 저장하고,
    /// 특정 시각에 가장 가까운 데이터를 빠르게 검색할 수 있음.
    /// </summary>
    /// <typeparam name="T">저장할 데이터 타입 (struct 권장, GC 방지)</typeparam>
    public class RingBuffer<T> where T : struct
    {
        // ----- 내부 저장소 -----
        private readonly T[] _data;            // 센서 데이터 배열
        private readonly long[] _timestamps;   // 대응하는 타임스탬프 배열 (Unix ms)
        private readonly object _lock = new object(); // 동기화 객체

        private int _head;    // 다음에 쓸 위치 (가장 최근 데이터의 다음)
        private int _count;   // 현재 저장된 항목 수

        // ----- 공개 프로퍼티 -----

        /// <summary>버퍼에 저장된 항목 수</summary>
        public int Count
        {
            get { lock (_lock) return _count; }
        }

        /// <summary>버퍼의 최대 용량 (슬롯 수)</summary>
        public int Capacity => _data.Length;

        // =====================================================================
        // 생성자
        // =====================================================================

        /// <summary>
        /// 순환 버퍼 생성
        /// </summary>
        /// <param name="capacity">최대 슬롯 수 (기본 512). 2의 거듭제곱 권장 (캐시 효율)</param>
        public RingBuffer(int capacity = 512)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "버퍼 용량은 1 이상이어야 함");

            _data = new T[capacity];
            _timestamps = new long[capacity];
            _head = 0;
            _count = 0;
        }

        // =====================================================================
        // Push — 데이터 삽입
        // =====================================================================

        /// <summary>
        /// 새 데이터를 버퍼에 추가.
        /// 버퍼가 가득 차면 가장 오래된 데이터를 덮어씀.
        /// </summary>
        /// <param name="item">센서 데이터</param>
        /// <param name="timestampMs">데이터 취득 시각 (Unix 밀리초)</param>
        public void Push(T item, long timestampMs)
        {
            lock (_lock)
            {
                _data[_head] = item;
                _timestamps[_head] = timestampMs;
                _head = (_head + 1) % _data.Length;

                if (_count < _data.Length)
                    _count++;
            }
        }

        // =====================================================================
        // GetLatest — 가장 최신 데이터 조회
        // =====================================================================

        /// <summary>
        /// 가장 최근에 Push된 데이터를 반환.
        /// 버퍼가 비어있으면 null.
        /// </summary>
        public T? GetLatest()
        {
            lock (_lock)
            {
                if (_count == 0)
                    return null;

                // _head는 "다음에 쓸 위치"이므로, 가장 최근 데이터는 _head - 1
                int latestIndex = (_head - 1 + _data.Length) % _data.Length;
                return _data[latestIndex];
            }
        }

        /// <summary>
        /// 가장 최근 데이터의 타임스탬프를 반환.
        /// 버퍼가 비어있으면 -1.
        /// </summary>
        public long GetLatestTimestamp()
        {
            lock (_lock)
            {
                if (_count == 0)
                    return -1;

                int latestIndex = (_head - 1 + _data.Length) % _data.Length;
                return _timestamps[latestIndex];
            }
        }

        // =====================================================================
        // GetNearest — 특정 타임스탬프에 가장 가까운 데이터 검색
        // =====================================================================

        /// <summary>
        /// 목표 타임스탬프에 가장 가까운 데이터를 반환.
        /// maxDriftMs 이내의 데이터가 없으면 null (stale data 방지).
        /// </summary>
        /// <param name="targetTimestampMs">목표 시각 (Unix 밀리초)</param>
        /// <param name="maxDriftMs">최대 허용 시간 차이 (밀리초, 기본 100ms)</param>
        /// <returns>가장 가까운 데이터, 또는 null</returns>
        public T? GetNearest(long targetTimestampMs, long maxDriftMs = 100)
        {
            lock (_lock)
            {
                if (_count == 0)
                    return null;

                // 선형 배열로 정렬된 뷰 구성 (논리적 인덱스 0 = 가장 오래된 데이터)
                // 이진 탐색으로 가장 가까운 타임스탬프 찾기
                int bestIndex = -1;
                long bestDiff = long.MaxValue;

                // 순환 버퍼의 시작 인덱스 계산
                int start = (_count < _data.Length)
                    ? 0
                    : _head; // 꽉 찼으면 _head가 가장 오래된 위치

                // 이진 탐색: 정렬된 타임스탬프 배열에서 targetTimestampMs에 가장 가까운 값
                int lo = 0;
                int hi = _count - 1;

                while (lo <= hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    int actualIndex = (start + mid) % _data.Length;
                    long ts = _timestamps[actualIndex];

                    long diff = Math.Abs(ts - targetTimestampMs);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestIndex = actualIndex;
                    }

                    if (ts < targetTimestampMs)
                        lo = mid + 1;
                    else if (ts > targetTimestampMs)
                        hi = mid - 1;
                    else
                        break; // 정확히 일치
                }

                // maxDrift 초과 확인
                if (bestIndex < 0 || bestDiff > maxDriftMs)
                    return null;

                return _data[bestIndex];
            }
        }

        /// <summary>
        /// GetNearest와 동일하지만 타임스탬프도 함께 반환.
        /// out 파라미터로 실제 데이터의 타임스탬프를 받을 수 있음.
        /// </summary>
        public T? GetNearest(long targetTimestampMs, out long actualTimestampMs, long maxDriftMs = 100)
        {
            actualTimestampMs = 0;

            lock (_lock)
            {
                if (_count == 0)
                    return null;

                int bestIndex = -1;
                long bestDiff = long.MaxValue;

                int start = (_count < _data.Length) ? 0 : _head;
                int lo = 0;
                int hi = _count - 1;

                while (lo <= hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    int actualIndex = (start + mid) % _data.Length;
                    long ts = _timestamps[actualIndex];

                    long diff = Math.Abs(ts - targetTimestampMs);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestIndex = actualIndex;
                    }

                    if (ts < targetTimestampMs)
                        lo = mid + 1;
                    else if (ts > targetTimestampMs)
                        hi = mid - 1;
                    else
                        break;
                }

                if (bestIndex < 0 || bestDiff > maxDriftMs)
                    return null;

                actualTimestampMs = _timestamps[bestIndex];
                return _data[bestIndex];
            }
        }

        // =====================================================================
        // Clear — 버퍼 초기화
        // =====================================================================

        /// <summary>버퍼 내용 전체 삭제 (세션 리셋 시 사용)</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _head = 0;
                _count = 0;
                // 배열 자체는 해제하지 않음 (재사용)
            }
        }

        // =====================================================================
        // GetRange — 시간 범위 내 데이터 일괄 조회
        // =====================================================================

        /// <summary>
        /// 지정한 시간 범위 내의 모든 데이터를 시간순으로 반환.
        /// 분석 윈도우(예: 최근 30초) 내 데이터 추출에 사용.
        /// </summary>
        /// <param name="fromMs">시작 시각 (포함)</param>
        /// <param name="toMs">종료 시각 (포함)</param>
        /// <returns>시간순 정렬된 (타임스탬프, 데이터) 배열</returns>
        public (long timestampMs, T data)[] GetRange(long fromMs, long toMs)
        {
            lock (_lock)
            {
                if (_count == 0)
                    return Array.Empty<(long, T)>();

                // 1차: 범위 내 항목 개수 세기
                int matchCount = 0;
                int start = (_count < _data.Length) ? 0 : _head;

                for (int i = 0; i < _count; i++)
                {
                    int idx = (start + i) % _data.Length;
                    long ts = _timestamps[idx];
                    if (ts >= fromMs && ts <= toMs)
                        matchCount++;
                }

                if (matchCount == 0)
                    return Array.Empty<(long, T)>();

                // 2차: 결과 배열 채우기
                var result = new (long timestampMs, T data)[matchCount];
                int writePos = 0;

                for (int i = 0; i < _count; i++)
                {
                    int idx = (start + i) % _data.Length;
                    long ts = _timestamps[idx];
                    if (ts >= fromMs && ts <= toMs)
                    {
                        result[writePos++] = (ts, _data[idx]);
                    }
                }

                return result;
            }
        }
    }
}
