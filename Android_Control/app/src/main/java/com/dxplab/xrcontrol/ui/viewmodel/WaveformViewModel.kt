package com.dxplab.xrcontrol.ui.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.dxplab.xrcontrol.domain.model.EegSample
import com.dxplab.xrcontrol.domain.source.SensorSource
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * 2초 윈도우의 EEG 2채널 샘플을 버퍼링하여 Compose Canvas에 공급.
 *
 * 샘플 레이트가 250Hz이므로 2초 = 500 샘플. 다운샘플링 없이 그대로 유지.
 */
@HiltViewModel
class WaveformViewModel @Inject constructor(
    sensorSource: SensorSource,
) : ViewModel() {

    private val bufferSize = 500
    private val ch0 = ArrayDeque<Float>(bufferSize)
    private val ch1 = ArrayDeque<Float>(bufferSize)

    private val _ch0State = MutableStateFlow<FloatArray>(FloatArray(0))
    val ch0State: StateFlow<FloatArray> = _ch0State.asStateFlow()

    private val _ch1State = MutableStateFlow<FloatArray>(FloatArray(0))
    val ch1State: StateFlow<FloatArray> = _ch1State.asStateFlow()

    val latestSample: StateFlow<EegSample?> = sensorSource.eegFlow
        .onEach { sample ->
            pushAndPublish(sample)
        }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), null)

    private fun pushAndPublish(sample: EegSample) {
        append(ch0, sample.channelsUv.getOrElse(0) { 0f })
        append(ch1, sample.channelsUv.getOrElse(1) { 0f })
        _ch0State.value = ch0.toFloatArray()
        _ch1State.value = ch1.toFloatArray()
    }

    private fun append(deque: ArrayDeque<Float>, v: Float) {
        deque.addLast(v)
        while (deque.size > bufferSize) deque.removeFirst()
    }

    private fun ArrayDeque<Float>.toFloatArray(): FloatArray {
        val arr = FloatArray(size)
        var i = 0
        for (v in this) { arr[i] = v; i++ }
        return arr
    }

    fun start() = viewModelScope.launch { /* flow 수집은 stateIn에서 자동 */ }
}
