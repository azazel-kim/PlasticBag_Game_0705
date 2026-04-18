package com.dxplab.xrcontrol.ui.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.dxplab.xrcontrol.data.prefs.Settings
import com.dxplab.xrcontrol.data.prefs.SettingsRepository
import com.dxplab.xrcontrol.domain.source.ConnectionState
import com.dxplab.xrcontrol.domain.source.SensorSource
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Main 화면 상태 모음 — 연결 상태, RSSI, 현재 설정.
 */
@HiltViewModel
class MainViewModel @Inject constructor(
    sensorSource: SensorSource,
    private val settingsRepository: SettingsRepository,
) : ViewModel() {

    val connectionState: StateFlow<ConnectionState> = sensorSource.connectionState
    val rssi: StateFlow<Int?> = sensorSource.rssi

    val settings: StateFlow<Settings?> = settingsRepository.settings
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), null)

    fun updateUseMock(on: Boolean) = viewModelScope.launch {
        settingsRepository.setUseMock(on)
    }

    fun updatePcIp(ip: String) = viewModelScope.launch {
        settingsRepository.setPcIp(ip)
    }

    fun updatePcPort(port: Int) = viewModelScope.launch {
        settingsRepository.setPcPort(port)
    }

    fun updateClockOffset(offsetMs: Long) = viewModelScope.launch {
        settingsRepository.setClockOffsetMs(offsetMs)
    }

    fun updateAutoReconnect(on: Boolean) = viewModelScope.launch {
        settingsRepository.setAutoReconnect(on)
    }
}
