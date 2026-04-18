package com.dxplab.xrcontrol.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.lifecycle.LifecycleService
import androidx.lifecycle.lifecycleScope
import com.dxplab.xrcontrol.R
import com.dxplab.xrcontrol.data.network.FuseUdpSender
import com.dxplab.xrcontrol.data.prefs.SettingsRepository
import com.dxplab.xrcontrol.domain.model.AccSample
import com.dxplab.xrcontrol.domain.model.EegSample
import com.dxplab.xrcontrol.domain.model.PpgSample
import com.dxplab.xrcontrol.domain.source.SensorSource
import com.dxplab.xrcontrol.ui.MainActivity
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.launchIn
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * BLE 연결·데이터 스트리밍·UDP 송신을 담당하는 Foreground Service.
 *
 * 동작:
 *   1. 시작 시: 설정 로드 → SensorSource.connect() → FuseUdpSender 생성
 *   2. eeg/ppg/acc Flow를 collect하여 최신값을 캐싱
 *   3. 30Hz 타이머로 파셜 FusedFrame 송신 (Python orchestrator와 주기 일치)
 *   4. 1초마다 Heartbeat 송신
 */
@AndroidEntryPoint
class StreamingService : LifecycleService() {

    companion object {
        private const val TAG = "StreamingService"
        private const val CHANNEL_ID = "xrcontrol_streaming"
        private const val NOTIFICATION_ID = 101

        const val ACTION_START = "com.dxplab.xrcontrol.action.START"
        const val ACTION_STOP = "com.dxplab.xrcontrol.action.STOP"

        fun start(context: Context) {
            val intent = Intent(context, StreamingService::class.java).apply { action = ACTION_START }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }

        fun stop(context: Context) {
            val intent = Intent(context, StreamingService::class.java).apply { action = ACTION_STOP }
            context.startService(intent)
        }
    }

    @Inject lateinit var sensorSource: SensorSource
    @Inject lateinit var settings: SettingsRepository

    private var sender: FuseUdpSender? = null
    private var latestEeg: EegSample? = null
    private var latestPpg: PpgSample? = null
    private var latestAcc: AccSample? = null
    private var clockOffset: Long = 0L

    override fun onBind(intent: Intent): IBinder? {
        super.onBind(intent)
        return null
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        super.onStartCommand(intent, flags, startId)
        when (intent?.action) {
            ACTION_START -> startStreaming()
            ACTION_STOP -> { stopStreaming(); stopSelf() }
        }
        return START_STICKY
    }

    private fun startStreaming() {
        createChannel()
        val notif = buildNotification("스트리밍 시작 중…")
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            startForeground(NOTIFICATION_ID, notif, ServiceInfo.FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE)
        } else {
            startForeground(NOTIFICATION_ID, notif)
        }

        lifecycleScope.launch(Dispatchers.IO) {
            val cfg = settings.settings.first()
            clockOffset = cfg.clockOffsetMs
            sender = FuseUdpSender(targetHost = cfg.pcIp, targetPort = cfg.pcPort)
            try { sender?.sendHandshake() } catch (t: Throwable) { Log.w(TAG, "handshake 실패", t) }
            sensorSource.connect()

            sensorSource.eegFlow.onEach { latestEeg = it }.launchIn(this)
            sensorSource.ppgFlow.onEach { latestPpg = it }.launchIn(this)
            sensorSource.accFlow.onEach { latestAcc = it }.launchIn(this)

            // 30Hz 송신 루프
            val periodMs = 1000L / 30L
            var next = System.currentTimeMillis()
            var heartbeatTick = next
            while (true) {
                try {
                    val ts = System.currentTimeMillis() + clockOffset
                    sender?.sendPartialFrame(latestEeg, latestPpg, latestAcc, timestampMs = ts)
                    if (System.currentTimeMillis() - heartbeatTick >= 1000L) {
                        sender?.sendHeartbeat(ts)
                        heartbeatTick = System.currentTimeMillis()
                    }
                } catch (t: Throwable) {
                    Log.w(TAG, "전송 실패", t)
                }
                next += periodMs
                val sleep = next - System.currentTimeMillis()
                if (sleep > 0) {
                    try { kotlinx.coroutines.delay(sleep) } catch (_: Throwable) { break }
                } else {
                    next = System.currentTimeMillis()
                }
            }
        }
    }

    private fun stopStreaming() {
        lifecycleScope.launch(Dispatchers.IO) {
            try { sensorSource.disconnect() } catch (_: Throwable) {}
            try { sender?.close() } catch (_: Throwable) {}
            sender = null
        }
    }

    override fun onDestroy() {
        stopStreaming()
        super.onDestroy()
    }

    // ── 알림 ──

    private fun createChannel() {
        val nm = getSystemService(NotificationManager::class.java)
        if (nm.getNotificationChannel(CHANNEL_ID) == null) {
            val channel = NotificationChannel(
                CHANNEL_ID, "XR Control 스트리밍",
                NotificationManager.IMPORTANCE_LOW,
            )
            channel.description = "LinkBand2 센서를 PC로 실시간 전송 중"
            nm.createNotificationChannel(channel)
        }
    }

    private fun buildNotification(text: String): Notification {
        val pending = PendingIntent.getActivity(
            this, 0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle(getString(R.string.app_name))
            .setContentText(text)
            .setSmallIcon(android.R.drawable.stat_sys_data_bluetooth)
            .setContentIntent(pending)
            .setOngoing(true)
            .build()
    }
}
