package com.dxplab.xrcontrol.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import com.dxplab.xrcontrol.ui.viewmodel.MainViewModel

@Composable
fun ConnectionScreen(
    nav: NavHostController,
    vm: MainViewModel = hiltViewModel(),
) {
    val state by vm.connectionState.collectAsState()
    val rssi by vm.rssi.collectAsState()
    val scope = rememberCoroutineScope()

    Column(
        Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("LinkBand2 연결", style = MaterialTheme.typography.headlineSmall)
        Card {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text("상태: $state")
                rssi?.let { Text("RSSI: $it dBm") } ?: Text("RSSI: -")
            }
        }
        Text(
            "Mock 모드에서는 스캔/페어링이 즉시 완료됩니다. 실기기 연동 시 BLE 권한을 먼저 허용해야 합니다.",
            style = MaterialTheme.typography.bodySmall,
        )
        Button(onClick = { nav.popBackStack() }) { Text("메인으로") }
    }
}
