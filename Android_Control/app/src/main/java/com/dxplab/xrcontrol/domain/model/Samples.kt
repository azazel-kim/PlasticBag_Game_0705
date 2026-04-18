package com.dxplab.xrcontrol.domain.model

/**
 * LinkBand2 센서 샘플 — BLE/Mock 소스가 방출하는 실시간 값.
 *
 * timestampMs는 샘플 획득 시각의 UTC epoch 밀리초.
 * (ClockSync 적용 후 PC 타임으로 보정된 값 사용 권장)
 */

data class EegSample(
    val timestampMs: Long,
    /** 6채널 마이크로볼트(µV). SDK에서 2채널만 실제 값, 나머지 0.0. */
    val channelsUv: FloatArray,
    /** 전극 접촉 품질 0~255 (6채널). */
    val contactQuality: IntArray,
) {
    companion object {
        const val CHANNEL_COUNT = 6
    }

    override fun equals(other: Any?): Boolean {
        if (this === other) return true
        if (javaClass != other?.javaClass) return false
        other as EegSample
        return timestampMs == other.timestampMs &&
                channelsUv.contentEquals(other.channelsUv) &&
                contactQuality.contentEquals(other.contactQuality)
    }

    override fun hashCode(): Int {
        var result = timestampMs.hashCode()
        result = 31 * result + channelsUv.contentHashCode()
        result = 31 * result + contactQuality.contentHashCode()
        return result
    }
}

data class PpgSample(
    val timestampMs: Long,
    val ir: Float,
    val red: Float,
)

data class AccSample(
    val timestampMs: Long,
    val x: Float,
    val y: Float,
    val z: Float,
)
