package com.dxplab.xrcontrol.data.mock

import com.dxplab.xrcontrol.domain.model.AccSample
import com.dxplab.xrcontrol.domain.model.EegSample
import com.dxplab.xrcontrol.domain.model.PpgSample
import com.dxplab.xrcontrol.domain.source.ConnectionState
import com.dxplab.xrcontrol.domain.source.SensorSource
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlin.math.PI
import kotlin.math.sin
import kotlin.random.Random

/**
 * Mock LinkBand2 — 실기기 없이 E2E 검증/개발을 위해 사인파·화이트노이즈 생성.
 *
 * 주파수 스펙 (실SDK와 동일):
 *   - EEG 250Hz / 2채널 (α 10Hz + θ 6Hz + Gaussian noise)
 *   - PPG 50Hz / IR+Red (심박 72bpm 시뮬레이션)
 *   - ACC 25Hz / 1Hz 저주파 진동
 */
class MockLinkBandClient : SensorSource {

    private val _connectionState = MutableStateFlow(ConnectionState.DISCONNECTED)
    override val connectionState = _connectionState.asStateFlow()

    private val _rssi = MutableStateFlow<Int?>(null)
    override val rssi = _rssi.asStateFlow()

    // SharedFlow로 각 샘플 1:N 브로드캐스트 (서비스 + ViewModel 동시 구독)
    private val _eeg = MutableSharedFlow<EegSample>(extraBufferCapacity = 512)
    override val eegFlow = _eeg.asSharedFlow()

    private val _ppg = MutableSharedFlow<PpgSample>(extraBufferCapacity = 256)
    override val ppgFlow = _ppg.asSharedFlow()

    private val _acc = MutableSharedFlow<AccSample>(extraBufferCapacity = 128)
    override val accFlow = _acc.asSharedFlow()

    private var scope: CoroutineScope? = null
    private var jobs: List<Job> = emptyList()

    override suspend fun connect() {
        if (_connectionState.value == ConnectionState.CONNECTED) return
        _connectionState.value = ConnectionState.SCANNING
        delay(300) // "스캔" 연출
        _connectionState.value = ConnectionState.CONNECTING
        delay(200)
        _rssi.value = -45
        _connectionState.value = ConnectionState.CONNECTED

        val cs = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        scope = cs
        val start = System.currentTimeMillis()
        jobs = listOf(
            cs.launch { eegLoop(start) },
            cs.launch { ppgLoop(start) },
            cs.launch { accLoop(start) },
        )
    }

    override suspend fun disconnect() {
        jobs.forEach { it.cancel() }
        jobs = emptyList()
        scope?.cancel()
        scope = null
        _rssi.value = null
        _connectionState.value = ConnectionState.DISCONNECTED
    }

    private suspend fun eegLoop(startMs: Long) {
        val periodNs = 1_000_000_000L / 250L
        var next = System.nanoTime()
        while (scope?.isActive == true) {
            val t = (System.currentTimeMillis() - startMs) / 1000.0
            val ch = FloatArray(EegSample.CHANNEL_COUNT)
            val q = IntArray(EegSample.CHANNEL_COUNT)
            for (i in 0 until 2) {
                val alpha = sin(2 * PI * 10.0 * t + 0.5 * i).toFloat()
                val theta = (0.6 * sin(2 * PI * 6.0 * t)).toFloat()
                val noise = Random.nextGaussian(0.0, 5.0).toFloat()
                ch[i] = 30f * (alpha + theta) + noise
                q[i] = 255
            }
            _eeg.tryEmit(EegSample(System.currentTimeMillis(), ch, q))
            next += periodNs
            val sleepNs = next - System.nanoTime()
            if (sleepNs > 0) delay(sleepNs / 1_000_000L)
            else next = System.nanoTime()
        }
    }

    private suspend fun ppgLoop(startMs: Long) {
        val periodNs = 1_000_000_000L / 50L
        var next = System.nanoTime()
        while (scope?.isActive == true) {
            val t = (System.currentTimeMillis() - startMs) / 1000.0
            val hz = 72.0 / 60.0
            val pulse = sin(2 * PI * hz * t).toFloat()
            _ppg.tryEmit(
                PpgSample(
                    timestampMs = System.currentTimeMillis(),
                    ir = 30000f + 800f * pulse + Random.nextGaussian(0.0, 20.0).toFloat(),
                    red = 28000f + 650f * pulse + Random.nextGaussian(0.0, 20.0).toFloat(),
                )
            )
            next += periodNs
            val sleepNs = next - System.nanoTime()
            if (sleepNs > 0) delay(sleepNs / 1_000_000L)
            else next = System.nanoTime()
        }
    }

    private suspend fun accLoop(startMs: Long) {
        val periodNs = 1_000_000_000L / 25L
        var next = System.nanoTime()
        while (scope?.isActive == true) {
            val t = (System.currentTimeMillis() - startMs) / 1000.0
            val sway = (0.05 * sin(2 * PI * 1.0 * t)).toFloat()
            _acc.tryEmit(
                AccSample(
                    timestampMs = System.currentTimeMillis(),
                    x = sway + Random.nextGaussian(0.0, 0.01).toFloat(),
                    y = -1.0f + Random.nextGaussian(0.0, 0.01).toFloat(),
                    z = sway + Random.nextGaussian(0.0, 0.01).toFloat(),
                )
            )
            next += periodNs
            val sleepNs = next - System.nanoTime()
            if (sleepNs > 0) delay(sleepNs / 1_000_000L)
            else next = System.nanoTime()
        }
    }
}

// Random.nextGaussian 유틸 — kotlin.random.Random에는 없음
private fun Random.nextGaussian(mean: Double, stdDev: Double): Double {
    // Box-Muller
    val u1 = nextDouble()
    val u2 = nextDouble()
    val z0 = kotlin.math.sqrt(-2.0 * kotlin.math.ln(u1)) * kotlin.math.cos(2 * PI * u2)
    return mean + stdDev * z0
}
