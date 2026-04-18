package com.dxplab.xrcontrol.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import com.dxplab.xrcontrol.ui.viewmodel.MainViewModel

@Composable
fun SettingsScreen(
    nav: NavHostController,
    vm: MainViewModel = hiltViewModel(),
) {
    val settings by vm.settings.collectAsState()

    var ip by remember { mutableStateOf("") }
    var port by remember { mutableStateOf("") }
    var useMock by remember { mutableStateOf(true) }
    var autoReconnect by remember { mutableStateOf(true) }
    var clockOffset by remember { mutableStateOf("0") }

    LaunchedEffect(settings) {
        settings?.let {
            ip = it.pcIp
            port = it.pcPort.toString()
            useMock = it.useMock
            autoReconnect = it.autoReconnect
            clockOffset = it.clockOffsetMs.toString()
        }
    }

    Column(
        Modifier.fillMaxSize()
            .padding(16.dp)
            .verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("설정", style = MaterialTheme.typography.headlineSmall)

        OutlinedTextField(
            value = ip, onValueChange = { ip = it },
            label = { Text("PC Bridge IP") },
            modifier = Modifier.fillMaxWidth(1f),
        )
        OutlinedTextField(
            value = port, onValueChange = { port = it.filter { c -> c.isDigit() } },
            label = { Text("PC Bridge 포트 (기본 9010)") },
            modifier = Modifier.fillMaxWidth(1f),
        )

        Row(useMock, "Mock 모드 사용") { on -> useMock = on; vm.updateUseMock(on) }
        Row(autoReconnect, "자동 재연결") { on -> autoReconnect = on; vm.updateAutoReconnect(on) }

        OutlinedTextField(
            value = clockOffset,
            onValueChange = { clockOffset = it.filter { c -> c.isDigit() || c == '-' } },
            label = { Text("Clock offset (ms)") },
            modifier = Modifier.fillMaxWidth(1f),
        )

        Button(onClick = {
            vm.updatePcIp(ip.trim())
            port.toIntOrNull()?.let { vm.updatePcPort(it) }
            clockOffset.toLongOrNull()?.let { vm.updateClockOffset(it) }
            nav.popBackStack()
        }) { Text("저장하고 돌아가기") }
    }
}

@Composable
private fun Row(checked: Boolean, label: String, onChange: (Boolean) -> Unit) {
    androidx.compose.foundation.layout.Row(
        Modifier.fillMaxWidth(1f),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(label)
        Switch(checked = checked, onCheckedChange = onChange)
    }
}
