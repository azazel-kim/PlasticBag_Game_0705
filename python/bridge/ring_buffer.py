"""
Thread-safe 순환 버퍼 (Ring Buffer)

각 센서별 독립 인스턴스로 운영하며, 512 슬롯 기본.
타임스탬프 기반 최근접 검색(get_nearest)을 지원하여
TimeSyncAligner에서 융합 시점의 데이터를 정확히 추출할 수 있음.
"""

from __future__ import annotations

import threading
from dataclasses import dataclass
from typing import Generic, Optional, TypeVar

T = TypeVar("T")

# ─────────────────────────────────────────────
# 버퍼 엔트리: 데이터 + 타임스탬프
# ─────────────────────────────────────────────

@dataclass(slots=True)
class _Entry(Generic[T]):
    """버퍼 내부 슬롯 — 데이터와 수신 타임스탬프를 함께 저장."""
    data: T
    timestamp_ms: int


# ─────────────────────────────────────────────
# RingBuffer
# ─────────────────────────────────────────────

class RingBuffer(Generic[T]):
    """Thread-safe 순환 버퍼.

    매개변수:
        capacity: 슬롯 수 (기본 512)

    사용 예시::

        buf: RingBuffer[EEGData] = RingBuffer(capacity=512)
        buf.push(eeg_sample, timestamp_ms=1713300000123)
        nearest = buf.get_nearest(target_ms=1713300000100, max_drift_ms=100)
    """

    def __init__(self, capacity: int = 512) -> None:
        if capacity <= 0:
            raise ValueError(f"capacity는 양수여야 함: {capacity}")
        self._capacity = capacity
        # 내부 배열을 None으로 초기화 — 아직 데이터 없음을 표시
        self._buffer: list[Optional[_Entry[T]]] = [None] * capacity
        self._head: int = 0          # 다음 쓰기 위치
        self._count: int = 0         # 현재 저장된 데이터 수
        self._lock = threading.Lock()

    # ── 속성 ──

    @property
    def capacity(self) -> int:
        """버퍼 최대 슬롯 수."""
        return self._capacity

    @property
    def count(self) -> int:
        """현재 저장된 데이터 수."""
        with self._lock:
            return self._count

    @property
    def is_empty(self) -> bool:
        """버퍼가 비어 있는지 여부."""
        with self._lock:
            return self._count == 0

    # ── 쓰기 ──

    def push(self, data: T, timestamp_ms: int) -> None:
        """새 데이터를 버퍼에 추가.

        버퍼가 가득 차면 가장 오래된 데이터를 덮어씀.

        매개변수:
            data: 센서 데이터 객체
            timestamp_ms: 데이터 수신 시각 (UTC epoch ms)
        """
        with self._lock:
            self._buffer[self._head] = _Entry(data=data, timestamp_ms=timestamp_ms)
            self._head = (self._head + 1) % self._capacity
            if self._count < self._capacity:
                self._count += 1

    # ── 읽기 ──

    def get_latest(self) -> Optional[tuple[T, int]]:
        """가장 최근에 추가된 데이터를 반환.

        반환값:
            (data, timestamp_ms) 튜플 또는 비어있으면 None
        """
        with self._lock:
            if self._count == 0:
                return None
            # head는 "다음 쓰기 위치"이므로 head-1이 마지막 기록
            idx = (self._head - 1) % self._capacity
            entry = self._buffer[idx]
            if entry is None:
                return None
            return (entry.data, entry.timestamp_ms)

    def get_nearest(
        self,
        target_timestamp_ms: int,
        max_drift_ms: int = 100,
    ) -> Optional[tuple[T, int]]:
        """목표 타임스탬프에 가장 가까운 데이터를 검색.

        max_drift_ms 이내에 데이터가 없으면 None 반환 (stale data 방지).

        매개변수:
            target_timestamp_ms: 찾고자 하는 시점 (UTC epoch ms)
            max_drift_ms: 허용 최대 시간 차이 (ms)

        반환값:
            (data, timestamp_ms) 튜플 또는 범위 밖이면 None
        """
        with self._lock:
            if self._count == 0:
                return None

            best_entry: Optional[_Entry[T]] = None
            best_diff: int = max_drift_ms + 1  # 초기값: 허용 범위 밖

            # 저장된 데이터만큼 순회
            for i in range(self._count):
                # 가장 오래된 데이터부터 순서대로 접근
                idx = (self._head - self._count + i) % self._capacity
                entry = self._buffer[idx]
                if entry is None:
                    continue

                diff = abs(entry.timestamp_ms - target_timestamp_ms)
                if diff < best_diff:
                    best_diff = diff
                    best_entry = entry

            # max_drift_ms 초과 시 None
            if best_entry is None or best_diff > max_drift_ms:
                return None

            return (best_entry.data, best_entry.timestamp_ms)

    def get_range(
        self,
        start_ms: int,
        end_ms: int,
    ) -> list[tuple[T, int]]:
        """지정 시간 범위 내의 모든 데이터를 시간순으로 반환.

        매개변수:
            start_ms: 시작 시각 (포함, UTC epoch ms)
            end_ms: 종료 시각 (포함, UTC epoch ms)

        반환값:
            [(data, timestamp_ms), ...] 시간순 정렬 리스트
        """
        with self._lock:
            results: list[tuple[T, int]] = []
            for i in range(self._count):
                idx = (self._head - self._count + i) % self._capacity
                entry = self._buffer[idx]
                if entry is None:
                    continue
                if start_ms <= entry.timestamp_ms <= end_ms:
                    results.append((entry.data, entry.timestamp_ms))

            # 시간순 정렬 (센서 데이터가 항상 순서대로 오지 않을 수 있음)
            results.sort(key=lambda x: x[1])
            return results

    def clear(self) -> None:
        """버퍼 초기화 — 모든 데이터 삭제."""
        with self._lock:
            self._buffer = [None] * self._capacity
            self._head = 0
            self._count = 0

    # ── 디버그 ──

    def __repr__(self) -> str:
        return f"RingBuffer(capacity={self._capacity}, count={self._count})"
