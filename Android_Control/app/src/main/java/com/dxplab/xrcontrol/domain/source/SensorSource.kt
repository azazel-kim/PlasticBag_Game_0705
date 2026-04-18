package com.dxplab.xrcontrol.domain.source

import com.dxplab.xrcontrol.domain.model.AccSample
import com.dxplab.xrcontrol.domain.model.EegSample
import com.dxplab.xrcontrol.domain.model.PpgSample
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.StateFlow

/**
 * LinkBand2 데이터 소스 추상화 — 실SDK와 Mock을 런타임 교체 가능하도록 분리.
 *
 * 구현체:
 *   - [MockLinkBandClient] : 사인파·화이트노이즈 생성 (LinkBand2 없이 개발)
 *   - [LinkBandClient]     : LooxidLabs SDK-Android 래퍼 (TODO, AAR 수령 후)
 *
 * 라이프사이클:
 *   connect() → streams 방출 시작 → disconnect()
 *
 * 스레딩:
 *   모든 Flow는 IO 디스패처에서 emit되며, UI는 collect 시 main으로 switch 권장.
 */
interface SensorSource {
    /** 현재 연결 상태 — UI가 관찰. */
    val connectionState: StateFlow<ConnectionState>

    /** BLE 신호 강도 (RSSI, dBm). 미사용 시 null. */
    val rssi: StateFlow<Int?>

    /** 2~6채널 EEG 샘플 스트림. 실SDK는 ~250Hz, Mock은 250Hz. */
    val eegFlow: Flow<EegSample>

    /** PPG 샘플 스트림. 실SDK 50Hz, Mock 50Hz. */
    val ppgFlow: Flow<PpgSample>

    /** 3축 가속도 샘플 스트림. 실SDK 25Hz, Mock 25Hz. */
    val accFlow: Flow<AccSample>

    /** 스캔 후 첫 기기에 자동 연결. 이미 연결된 경우 재연결하지 않음. */
    suspend fun connect()

    /** 연결 해제 및 내부 스트림 중단. */
    suspend fun disconnect()
}

enum class ConnectionState {
    DISCONNECTED,
    SCANNING,
    CONNECTING,
    CONNECTED,
    RECONNECTING,
    ERROR,
}
