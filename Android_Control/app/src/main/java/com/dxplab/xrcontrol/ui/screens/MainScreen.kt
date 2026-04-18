package com.dxplab.xrcontrol.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Bluetooth
import androidx.compose.material.icons.filled.Cancel
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.ShowChart
import androidx.compose.material.icons.filled.Stop
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import com.dxplab.xrcontrol.domain.source.ConnectionState
import com.dxplab.xrcontrol.service.StreamingService
import com.dxplab.xrcontrol.ui.Routes
import com.dxplab.xrcontrol.ui.viewmodel.MainViewModel

@Composable
fun MainScreen(
    nav: NavHostController,
    vm: MainViewModel = hiltViewModel(),
) {
    val state by vm.connectionState.collectAsState()
    val rssi by vm.rssi.collectAsState()
    val settings by vm.settings.collectAsState()
    val ctx = LocalContext.current
    var streaming by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("XR Control", style = MaterialTheme.typography.headlineMedium)
        Text("LinkBand2 → PC Bridge", style = MaterialTheme.typography.bodyMedium)

        // 상태 카드
        ElevatedCard(modifier = Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    StateIcon(state)
                    Spacer(Modifier.size(8.dp))
                    Text(stateLabel(state), style = MaterialTheme.typography.titleMedium)
                }
                rssi?.let { Text("RSSI: $it dBm") }
                settings?.let {
                    Text("대상 PC: ${it.pcIp}:${it.pcPort}")
                    Text("모드: ${if (it.useMock) "Mock" else "실기기"}")
                    Text("Clock offset: ${it.clockOffsetMs} ms")
                }
            }
        }

        // 제어 버튼
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Button(
                onClick = {
                    if (streaming) StreamingService.stop(ctx)
                    else StreamingService.start(ctx)
                    streaming = !streaming
                },
                modifier = Modifier.weight(1f),
            ) {
                Icon(
                    imageVector = if (streaming) Icons.Filled.Stop else Icons.Filled.PlayArrow,
                    contentDescription = null,
                )
                Spacer(Modifier.size(6.dp))
                Text(if (streaming) "중지" else "시작")
            }
        }

        // 메뉴 그리드
        Card(modifier = Modifier.fillMaxWidth()) {
            Column(Modifier.padding(8.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                MenuItem(Icons.Filled.Bluetooth, "기기 연결") { nav.navigate(Routes.CONNECTION) }
                MenuItem(Icons.Filled.ShowChart, "실시간 파형") { nav.navigate(Routes.WAVEFORM) }
                MenuItem(Icons.Filled.History, "세션 기록") { nav.navigate(Routes.SESSIONS) }
                MenuItem(Icons.Filled.Settings, "설정") { nav.navigate(Routes.SETTINGS) }
                MenuItem(Icons.Filled.Sync, "Clock Sync 보정") {
                    // TODO: ViewModel에 위임하여 ControlClient.measureOffsetMedian 실행
                }
            }
        }
    }
}

@Composable
private fun MenuItem(icon: androidx.compose.ui.graphics.vector.ImageVector, label: String, onClick: () -> Unit) {
    OutlinedButton(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Icon(icon, null)
        Spacer(Modifier.size(8.dp))
        Text(label, modifier = Modifier.fillMaxWidth())
    }
}

@Composable
private fun StateIcon(state: ConnectionState) {
    when (state) {
        ConnectionState.CONNECTED -> Icon(Icons.Filled.CheckCircle, null, tint = Color(0xFF2E7D32))
        ConnectionState.SCANNING, ConnectionState.CONNECTING, ConnectionState.RECONNECTING ->
            Icon(Icons.Filled.Favorite, null, tint = Color(0xFFFFA000))
        ConnectionState.ERROR -> Icon(Icons.Filled.Cancel, null, tint = Color(0xFFC62828))
        else -> Icon(Icons.Filled.Bluetooth, null)
    }
}

private fun stateLabel(state: ConnectionState): String = when (state) {
    ConnectionState.DISCONNECTED -> "연결 안 됨"
    ConnectionState.SCANNING -> "스캔 중…"
    ConnectionState.CONNECTING -> "연결 중…"
    ConnectionState.CONNECTED -> "연결됨"
    ConnectionState.RECONNECTING -> "재연결 중…"
    ConnectionState.ERROR -> "오류"
}
