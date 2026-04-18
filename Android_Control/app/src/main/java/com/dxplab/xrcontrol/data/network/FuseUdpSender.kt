package com.dxplab.xrcontrol.data.network

import com.dxplab.xrcontrol.domain.model.AccSample
import com.dxplab.xrcontrol.domain.model.EegSample
import com.dxplab.xrcontrol.domain.model.PpgSample
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.atomic.AtomicInteger

/**
 * FUSE v1.0 프로토콜 파셜 인코더 — EEG/PPG/ACC만 채워서 PC Bridge로 송신.
 *
 * 구조:
 *   - 956 byte 고정 payload에 각 센서 슬롯 순서대로 기록
 *   - 제공되지 않은 센서 영역은 0 패딩
 *   - ValidFlags는 제공된 센서 비트만 set
 *   - PC 측 [udp_protocol.py].FusedDataFrame.from_bytes가 비트 체크 후 읽음
 *
 * 스레드 안전:
 *   - send(...)은 스레드 안전 (synchronized + 내부 seq atomic)
 *   - close()는 한 번만 호출
 */
class FuseUdpSender(
    private val targetHost: String,
    private val targetPort: Int = FuseProtocol.PORT_LINKBAND_INGEST,
) : AutoCloseable {

    private val socket = DatagramSocket()
    private val address: InetAddress = InetAddress.getByName(targetHost)
    private val fusedSeq = AtomicInteger(0)
    private val controlSeq = AtomicInteger(0)

    /**
     * EEG/PPG/ACC 중 제공된 것만 포함하는 파셜 FusedFrame 송신.
     *
     * @param timestampMs 송신 시각 (clock offset 적용 후 권장).
     *                    null이면 System.currentTimeMillis() 사용.
     * @return 송신된 바이트 수 (헤더 20 + payload 956 = 976)
     */
    fun sendPartialFrame(
        eeg: EegSample?,
        ppg: PpgSample?,
        acc: AccSample?,
        timestampMs: Long? = null,
    ): Int {
        val ts = timestampMs ?: System.currentTimeMillis()
        val payload = encodePayload(eeg, ppg, acc, ts)
        val packet = wrapHeader(
            packetType = FuseProtocol.PACKET_FUSED_FRAME,
            seq = fusedSeq.getAndIncrement(),
            timestampMs = ts,
            payload = payload,
        )
        return sendRaw(packet)
    }

    fun sendHandshake(timestampMs: Long? = null): Int {
        val ts = timestampMs ?: System.currentTimeMillis()
        val packet = wrapHeader(
            packetType = FuseProtocol.PACKET_HANDSHAKE,
            seq = controlSeq.getAndIncrement(),
            timestampMs = ts,
            payload = ByteArray(0),
        )
        return sendRaw(packet)
    }

    fun sendHeartbeat(timestampMs: Long? = null): Int {
        val ts = timestampMs ?: System.currentTimeMillis()
        val packet = wrapHeader(
            packetType = FuseProtocol.PACKET_HEARTBEAT,
            seq = controlSeq.getAndIncrement(),
            timestampMs = ts,
            payload = ByteArray(0),
        )
        return sendRaw(packet)
    }

    /**
     * ClockSync 요청 패킷 송신. 응답은 [com.dxplab.xrcontrol.data.network.ControlClient]에서 수신.
     * @return 송신 시점 t1 (ms)
     */
    fun sendClockSyncRequest(): Long {
        val t1 = System.currentTimeMillis()
        val payload = ByteBuffer.allocate(16)
            .order(ByteOrder.LITTLE_ENDIAN)
            .putLong(t1)
            .putLong(0L)
            .array()
        val packet = wrapHeader(
            packetType = FuseProtocol.PACKET_CLOCK_SYNC,
            seq = controlSeq.getAndIncrement(),
            timestampMs = t1,
            payload = payload,
        )
        sendRaw(packet)
        return t1
    }

    override fun close() {
        try { socket.close() } catch (_: Throwable) {}
    }

    // ── 내부 ──

    /** 956 byte payload 인코딩 (제공되지 않은 센서 0 패딩). */
    private fun encodePayload(
        eeg: EegSample?,
        ppg: PpgSample?,
        acc: AccSample?,
        timestampMs: Long,
    ): ByteArray {
        val buf = ByteBuffer.allocate(FuseProtocol.FUSED_PAYLOAD_SIZE)
            .order(ByteOrder.LITTLE_ENDIAN)

        // BlendShape (284)
        buf.position(buf.position() + FuseProtocol.SIZE_BLENDSHAPE)
        // Eye (33)
        buf.position(buf.position() + FuseProtocol.SIZE_EYE)

        // EEG (30): float32[6] + uint8[6]
        var flags = 0
        if (eeg != null) {
            flags = flags or FuseProtocol.FLAG_EEG
            require(eeg.channelsUv.size >= EegSample.CHANNEL_COUNT) {
                "EEG channels must have at least ${EegSample.CHANNEL_COUNT} floats"
            }
            for (i in 0 until EegSample.CHANNEL_COUNT) buf.putFloat(eeg.channelsUv[i])
            for (i in 0 until EegSample.CHANNEL_COUNT)
                buf.put((eeg.contactQuality.getOrElse(i) { 0 }.coerceIn(0, 255)).toByte())
        } else {
            buf.position(buf.position() + FuseProtocol.SIZE_EEG)
        }

        // PPG (8): ir + red
        if (ppg != null) {
            flags = flags or FuseProtocol.FLAG_PPG
            buf.putFloat(ppg.ir)
            buf.putFloat(ppg.red)
        } else {
            buf.position(buf.position() + FuseProtocol.SIZE_PPG)
        }

        // ACC (12): x, y, z
        if (acc != null) {
            flags = flags or FuseProtocol.FLAG_ACC
            buf.putFloat(acc.x)
            buf.putFloat(acc.y)
            buf.putFloat(acc.z)
        } else {
            buf.position(buf.position() + FuseProtocol.SIZE_ACC)
        }

        // Pose (528) + IMU (52)
        buf.position(buf.position() + FuseProtocol.SIZE_POSE)
        buf.position(buf.position() + FuseProtocol.SIZE_IMU)

        // ValidFlags (1) + TimestampMs (8)
        buf.put(flags.toByte())
        buf.putLong(timestampMs)

        check(buf.position() == FuseProtocol.FUSED_PAYLOAD_SIZE) {
            "Payload size mismatch: ${buf.position()} (expected ${FuseProtocol.FUSED_PAYLOAD_SIZE})"
        }
        return buf.array()
    }

    /** 20 byte 헤더를 만들고 payload와 concat. */
    private fun wrapHeader(
        packetType: Byte,
        seq: Int,
        timestampMs: Long,
        payload: ByteArray,
    ): ByteArray {
        val header = ByteBuffer.allocate(FuseProtocol.HEADER_SIZE)
            .order(ByteOrder.LITTLE_ENDIAN)
        header.put(FuseProtocol.MAGIC)
        header.put(FuseProtocol.VERSION)
        header.put(packetType)
        header.putInt(seq)
        header.putLong(timestampMs)
        header.putShort(payload.size.toShort())
        val bytes = ByteArray(FuseProtocol.HEADER_SIZE + payload.size)
        System.arraycopy(header.array(), 0, bytes, 0, FuseProtocol.HEADER_SIZE)
        System.arraycopy(payload, 0, bytes, FuseProtocol.HEADER_SIZE, payload.size)
        return bytes
    }

    @Synchronized
    private fun sendRaw(bytes: ByteArray): Int {
        val packet = DatagramPacket(bytes, bytes.size, address, targetPort)
        socket.send(packet)
        return bytes.size
    }
}
