package com.dxplab.xrcontrol.data.linkband

import com.dxplab.xrcontrol.domain.model.AccSample
import com.dxplab.xrcontrol.domain.model.EegSample
import com.dxplab.xrcontrol.domain.model.PpgSample
import com.dxplab.xrcontrol.domain.source.ConnectionState
import com.dxplab.xrcontrol.domain.source.SensorSource
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * LinkBand2 실제 SDK 래퍼 — TODO: AAR 수령 후 구현.
 *
 * LooxidLabs SDK-Android 1.0.1 의 StateFlow 출력(EEG/PPG/ACC)을 받아 본 앱의
 * 도메인 모델([EegSample], [PpgSample], [AccSample])로 매핑합니다.
 *
 * 현재 상태:
 *   - 스텁 구현: connect/disconnect 호출 시 ERROR 상태로 전환
 *   - MockLinkBandClient를 기본으로 사용 중
 *
 * 교체 시점:
 *   1. libs.versions.toml에서 SDK 의존성 추가
 *   2. SourceModule의 @Binds로 이 클래스를 @Provides
 *   3. SDK StateFlow → Flow 변환 로직 작성
 */
class LinkBandClient : SensorSource {
    private val _connectionState = MutableStateFlow(ConnectionState.DISCONNECTED)
    override val connectionState = _connectionState.asStateFlow()

    private val _rssi = MutableStateFlow<Int?>(null)
    override val rssi = _rssi.asStateFlow()

    private val _eeg = MutableSharedFlow<EegSample>(extraBufferCapacity = 512)
    override val eegFlow = _eeg.asSharedFlow()

    private val _ppg = MutableSharedFlow<PpgSample>(extraBufferCapacity = 256)
    override val ppgFlow = _ppg.asSharedFlow()

    private val _acc = MutableSharedFlow<AccSample>(extraBufferCapacity = 128)
    override val accFlow = _acc.asSharedFlow()

    override suspend fun connect() {
        // TODO: LooxidLabs SDK 통합
        //   val scanner = LinkBandScanner(context)
        //   scanner.scan().first().connect()
        //   sdkEegFlow.collect { _eeg.emit(mapper.map(it)) }
        _connectionState.value = ConnectionState.ERROR
    }

    override suspend fun disconnect() {
        _connectionState.value = ConnectionState.DISCONNECTED
    }
}
