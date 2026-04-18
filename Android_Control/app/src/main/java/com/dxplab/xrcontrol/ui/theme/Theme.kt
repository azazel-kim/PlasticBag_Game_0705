package com.dxplab.xrcontrol.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val DarkColors = darkColorScheme(
    primary = Color(0xFF8DD3FF),
    onPrimary = Color(0xFF003547),
    secondary = Color(0xFFB5CAD7),
    tertiary = Color(0xFFCBC4FF),
    background = Color(0xFF111217),
    surface = Color(0xFF1A1C22),
)

private val LightColors = lightColorScheme(
    primary = Color(0xFF006A87),
    onPrimary = Color.White,
    secondary = Color(0xFF4D6170),
    tertiary = Color(0xFF5F5AA2),
    background = Color(0xFFF8FAFD),
    surface = Color.White,
)

@Composable
fun XRControlTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit,
) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        content = content,
    )
}
