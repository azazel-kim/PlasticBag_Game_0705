package com.dxplab.xrcontrol.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.navigation.NavHostController

@Composable
fun SessionLogScreen(nav: NavHostController) {
    // 세션 목록은 SessionRecorder 구현 후 확장 (현재 스텁)
    Column(
        Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("세션 기록", style = MaterialTheme.typography.headlineSmall)
        Text(
            "(TODO) 로컬 CSV 세션 리스트가 표시됩니다. 현재 Bridge 기능만 활성.",
            style = MaterialTheme.typography.bodyMedium,
        )
        Button(onClick = { nav.popBackStack() }) { Text("메인으로") }
    }
}
