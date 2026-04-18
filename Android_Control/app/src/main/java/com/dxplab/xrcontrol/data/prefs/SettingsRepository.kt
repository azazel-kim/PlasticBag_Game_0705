package com.dxplab.xrcontrol.data.prefs

import android.content.Context
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

private val Context.dataStore by preferencesDataStore(name = "xr_control_settings")

/**
 * 사용자 설정 저장소 — DataStore 기반.
 *
 * 관리 항목:
 *   - PC Bridge IP/Port
 *   - Mock 모드 토글
 *   - ClockSync offset (ms, 부호 있음)
 *   - 자동 재연결
 */
@Singleton
class SettingsRepository @Inject constructor(
    @ApplicationContext private val context: Context,
) {
    companion object {
        val KEY_PC_IP = stringPreferencesKey("pc_ip")
        val KEY_PC_PORT = intPreferencesKey("pc_port")
        val KEY_USE_MOCK = booleanPreferencesKey("use_mock")
        val KEY_CLOCK_OFFSET_MS = longPreferencesKey("clock_offset_ms")
        val KEY_AUTO_RECONNECT = booleanPreferencesKey("auto_reconnect")

        const val DEFAULT_PC_IP = "192.168.0.10"
        const val DEFAULT_PC_PORT = 9010
        const val DEFAULT_USE_MOCK = true
        const val DEFAULT_AUTO_RECONNECT = true
    }

    val settings: Flow<Settings> = context.dataStore.data.map { prefs ->
        Settings(
            pcIp = prefs[KEY_PC_IP] ?: DEFAULT_PC_IP,
            pcPort = prefs[KEY_PC_PORT] ?: DEFAULT_PC_PORT,
            useMock = prefs[KEY_USE_MOCK] ?: DEFAULT_USE_MOCK,
            clockOffsetMs = prefs[KEY_CLOCK_OFFSET_MS] ?: 0L,
            autoReconnect = prefs[KEY_AUTO_RECONNECT] ?: DEFAULT_AUTO_RECONNECT,
        )
    }

    suspend fun setPcIp(ip: String) = edit { it[KEY_PC_IP] = ip }
    suspend fun setPcPort(port: Int) = edit { it[KEY_PC_PORT] = port }
    suspend fun setUseMock(on: Boolean) = edit { it[KEY_USE_MOCK] = on }
    suspend fun setClockOffsetMs(offset: Long) = edit { it[KEY_CLOCK_OFFSET_MS] = offset }
    suspend fun setAutoReconnect(on: Boolean) = edit { it[KEY_AUTO_RECONNECT] = on }

    private suspend fun edit(block: (androidx.datastore.preferences.core.MutablePreferences) -> Unit) {
        context.dataStore.edit(block)
    }
}

data class Settings(
    val pcIp: String,
    val pcPort: Int,
    val useMock: Boolean,
    val clockOffsetMs: Long,
    val autoReconnect: Boolean,
)
