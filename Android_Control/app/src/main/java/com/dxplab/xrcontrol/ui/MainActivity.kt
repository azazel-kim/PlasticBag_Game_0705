package com.dxplab.xrcontrol.ui

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.core.content.ContextCompat
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.dxplab.xrcontrol.ui.screens.ConnectionScreen
import com.dxplab.xrcontrol.ui.screens.MainScreen
import com.dxplab.xrcontrol.ui.screens.SessionLogScreen
import com.dxplab.xrcontrol.ui.screens.SettingsScreen
import com.dxplab.xrcontrol.ui.screens.WaveformScreen
import com.dxplab.xrcontrol.ui.theme.XRControlTheme
import dagger.hilt.android.AndroidEntryPoint

object Routes {
    const val MAIN = "main"
    const val CONNECTION = "connection"
    const val WAVEFORM = "waveform"
    const val SESSIONS = "sessions"
    const val SETTINGS = "settings"
}

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    private val requiredPermissions: Array<String> =
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            arrayOf(
                Manifest.permission.BLUETOOTH_SCAN,
                Manifest.permission.BLUETOOTH_CONNECT,
                Manifest.permission.POST_NOTIFICATIONS,
            )
        } else {
            arrayOf(Manifest.permission.BLUETOOTH_SCAN, Manifest.permission.BLUETOOTH_CONNECT)
        }

    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { /* 결과는 UI가 ViewModel을 통해 재조회 */ }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        requestPermissionsIfNeeded()
        setContent {
            XRControlTheme {
                AppNav()
            }
        }
    }

    private fun requestPermissionsIfNeeded() {
        val missing = requiredPermissions.filter {
            ContextCompat.checkSelfPermission(this, it) != PackageManager.PERMISSION_GRANTED
        }
        if (missing.isNotEmpty()) permissionLauncher.launch(missing.toTypedArray())
    }
}

@Composable
fun AppNav() {
    val nav: NavHostController = rememberNavController()
    Scaffold { padding ->
        NavHost(
            navController = nav,
            startDestination = Routes.MAIN,
            modifier = Modifier.padding(padding),
        ) {
            composable(Routes.MAIN) { MainScreen(nav) }
            composable(Routes.CONNECTION) { ConnectionScreen(nav) }
            composable(Routes.WAVEFORM) { WaveformScreen(nav) }
            composable(Routes.SESSIONS) { SessionLogScreen(nav) }
            composable(Routes.SETTINGS) { SettingsScreen(nav) }
        }
    }
}
