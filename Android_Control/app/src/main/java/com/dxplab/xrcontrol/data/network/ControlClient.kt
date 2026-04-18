package com.dxplab.xrcontrol.data.network

import android.util.Log
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.nio.ByteBuffer
import java.nio.ByteOrder
import kotlin.concurrent.thread

/**
 * PC Bridge의 ControlServer(9003)와 통신하여 ClockSync offset을 측정합니다.
 *
 * 사용 흐름:
 *   1. [requestClockSync]를 여러 번(3~5) 호출하여 왕복 시간 측정
 *   2. 각 응답의 (t1, t2) + 수신 시각 t3으로 offset 평균 산출
 *   3. 결과는 [SettingsRepository]에 저장되어 [FuseUdpSender]가 timestamp 보정
 *
 * NTP 스타일 offset:
 *   offset = ((t2 - t1) + (t3 - t4)) / 2
 *   (여기서 t3는 PC가 응답을 보낸 시각, t4는 Android가 응답을 받은 시각)
 *
 * 현재 단순화: t2와 t4만 사용하는 근사 — offset ≈ t2 - ((t1 + t4) / 2)
 */
class ControlClient(
    private val targetHost: String,
    private val targetPort: Int = FuseProtocol.PORT_CONTROL,
    private val timeoutMs: Int = 500,
) {
    companion object {
        private const val TAG = "ControlClient"
    }

    private val address: InetAddress = InetAddress.getByName(targetHost)

    /**
     * 단일 ClockSync 왕복 → offset(ms) 또는 null(타임아웃).
     * offset이 양수면 PC가 Android보다 빠른 시계를 가짐.
     */
    fun requestClockSync(): Long? {
        DatagramSocket().use { sock ->
            sock.soTimeout = timeoutMs

            val t1 = System.currentTimeMillis()
            val reqPayload = ByteBuffer.allocate(16)
                .order(ByteOrder.LITTLE_ENDIAN)
                .putLong(t1).putLong(0L).array()
            val header = ByteBuffer.allocate(FuseProtocol.HEADER_SIZE)
                .order(ByteOrder.LITTLE_ENDIAN)
            header.put(FuseProtocol.MAGIC)
            header.put(FuseProtocol.VERSION)
            header.put(FuseProtocol.PACKET_CLOCK_SYNC)
            header.putInt(0) // seq
            header.putLong(t1)
            header.putShort(reqPayload.size.toShort())
            val packet = ByteArray(FuseProtocol.HEADER_SIZE + reqPayload.size)
            System.arraycopy(header.array(), 0, packet, 0, FuseProtocol.HEADER_SIZE)
            System.arraycopy(reqPayload, 0, packet, FuseProtocol.HEADER_SIZE, reqPayload.size)

            sock.send(DatagramPacket(packet, packet.size, address, targetPort))

            // 응답 수신
            val buf = ByteArray(2048)
            val dp = DatagramPacket(buf, buf.size)
            return try {
                sock.receive(dp)
                val t4 = System.currentTimeMillis()
                // 헤더 20 byte 스킵 후 payload(16 byte: t1, t2) 파싱
                if (dp.length < FuseProtocol.HEADER_SIZE + 16) return null
                val payload = ByteBuffer.wrap(buf, FuseProtocol.HEADER_SIZE, 16)
                    .order(ByteOrder.LITTLE_ENDIAN)
                val respT1 = payload.long
                val t2 = payload.long
                if (respT1 != t1) {
                    Log.w(TAG, "ClockSync t1 mismatch: sent=$t1 recv=$respT1")
                    return null
                }
                // offset 근사: 서버 시계 - 클라이언트 중간시각
                val midpoint = (t1 + t4) / 2
                t2 - midpoint
            } catch (e: java.net.SocketTimeoutException) {
                Log.w(TAG, "ClockSync timeout")
                null
            }
        }
    }

    /**
     * 여러 번 반복 후 median 반환. 실패한 요청은 스킵.
     */
    fun measureOffsetMedian(samples: Int = 5): Long? {
        val offsets = mutableListOf<Long>()
        repeat(samples) {
            requestClockSync()?.let { offsets.add(it) }
            Thread.sleep(50)
        }
        if (offsets.isEmpty()) return null
        offsets.sort()
        return offsets[offsets.size / 2]
    }
}
