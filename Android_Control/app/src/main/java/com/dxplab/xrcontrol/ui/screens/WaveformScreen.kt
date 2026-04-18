package com.dxplab.xrcontrol.ui.screens

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import com.dxplab.xrcontrol.ui.viewmodel.WaveformViewModel
import kotlin.math.abs

@Composable
fun WaveformScreen(
    nav: NavHostController,
    vm: WaveformViewModel = hiltViewModel(),
) {
    val ch0 by vm.ch0State.collectAsState()
    val ch1 by vm.ch1State.collectAsState()

    Column(
        Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("EEG 실시간 파형 (2채널)", style = MaterialTheme.typography.headlineSmall)
        Text("2초 윈도우, 250Hz 샘플", style = MaterialTheme.typography.bodySmall)

        WaveformCanvas(values = ch0, color = Color(0xFF4FC3F7),
            modifier = Modifier.fillMaxWidth().height(180.dp))
        Text("Ch 0", style = MaterialTheme.typography.labelMedium)

        WaveformCanvas(values = ch1, color = Color(0xFFF06292),
            modifier = Modifier.fillMaxWidth().height(180.dp))
        Text("Ch 1", style = MaterialTheme.typography.labelMedium)

        Spacer(Modifier.height(8.dp))
        Button(onClick = { nav.popBackStack() }) { Text("메인으로") }
    }
}

@Composable
private fun WaveformCanvas(values: FloatArray, color: Color, modifier: Modifier) {
    Canvas(modifier = modifier) {
        if (values.isEmpty()) return@Canvas
        val w = size.width
        val h = size.height
        val midY = h / 2f
        // 자동 스케일링 — |max|
        var max = 0f
        for (v in values) if (abs(v) > max) max = abs(v)
        if (max < 1f) max = 1f
        val amp = (h / 2f) * 0.9f / max
        val dx = if (values.size > 1) w / (values.size - 1) else w
        val path = Path()
        path.moveTo(0f, midY - values[0] * amp)
        for (i in 1 until values.size) {
            path.lineTo(i * dx, midY - values[i] * amp)
        }
        drawPath(path, color = color, style = Stroke(width = 2f))
        drawLine(
            color = Color.Gray.copy(alpha = 0.3f),
            start = Offset(0f, midY),
            end = Offset(w, midY),
            strokeWidth = 1f,
        )
    }
}
