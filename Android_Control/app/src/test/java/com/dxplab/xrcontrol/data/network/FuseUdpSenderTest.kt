package com.dxplab.xrcontrol.data.network

import com.dxplab.xrcontrol.domain.model.AccSample
import com.dxplab.xrcontrol.domain.model.EegSample
import com.dxplab.xrcontrol.domain.model.PpgSample
import org.junit.Test
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import java.lang.reflect.Method
import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * FuseUdpSender의 encodePayload가 FUSE v1.0 스펙(956 byte 고정)을 정확히 준수하는지
 * 그리고 Python udp_protocol.py/fused_data_frame.py와 바이트 호환되는지 검증.
 *
 * 네트워크 송신 없이 encodePayload만 reflection으로 호출.
 */
class FuseUdpSenderTest {

    private fun invokeEncode(
        sender: FuseUdpSender,
        eeg: EegSample?,
        ppg: PpgSample?,
        acc: AccSample?,
        timestampMs: Long,
    ): ByteArray {
        val m: Method = sender.javaClass.getDeclaredMethod(
            "encodePayload",
            EegSample::class.java,
            PpgSample::class.java,
            AccSample::class.java,
            java.lang.Long.TYPE,
        )
        m.isAccessible = true
        return m.invoke(sender, eeg, ppg, acc, timestampMs) as ByteArray
    }

    @Test
    fun payload_is_956_bytes_fixed() {
        val sender = FuseUdpSender(targetHost = "127.0.0.1", targetPort = 65000)
        sender.use {
            val bytes = invokeEncode(it, null, null, null, 0L)
            assertEquals(FuseProtocol.FUSED_PAYLOAD_SIZE, bytes.size)
        }
    }

    @Test
    fun partial_eeg_ppg_acc_flags_and_values() {
        val sender = FuseUdpSender(targetHost = "127.0.0.1", targetPort = 65000)
        sender.use {
            val eeg = EegSample(
                timestampMs = 1_700_000_000_000L,
                channelsUv = floatArrayOf(1.0f, 2.0f, 0f, 0f, 0f, 0f),
                contactQuality = intArrayOf(255, 255, 0, 0, 0, 0),
            )
            val ppg = PpgSample(timestampMs = 0, ir = 123f, red = 456f)
            val acc = AccSample(timestampMs = 0, x = 0.1f, y = 0.2f, z = 0.3f)
            val ts = 1_700_000_000_123L

            val bytes = invokeEncode(it, eeg, ppg, acc, ts)
            assertEquals(FuseProtocol.FUSED_PAYLOAD_SIZE, bytes.size)

            val buf = ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN)

            // EEG 영역 시작 = 284(BlendShape) + 33(Eye) = 317
            val eegOffset = FuseProtocol.SIZE_BLENDSHAPE + FuseProtocol.SIZE_EYE
            buf.position(eegOffset)
            assertEquals(1.0f, buf.float, 1e-6f)
            assertEquals(2.0f, buf.float, 1e-6f)
            // 나머지 채널은 0
            for (i in 0 until 4) assertEquals(0f, buf.float, 1e-6f)
            // 접촉 품질
            assertEquals(255.toByte(), buf.get())
            assertEquals(255.toByte(), buf.get())

            // PPG 영역 시작 = 317 + 30 = 347
            val ppgOffset = eegOffset + FuseProtocol.SIZE_EEG
            buf.position(ppgOffset)
            assertEquals(123f, buf.float, 1e-6f)
            assertEquals(456f, buf.float, 1e-6f)

            // ACC 영역
            val accOffset = ppgOffset + FuseProtocol.SIZE_PPG
            buf.position(accOffset)
            assertEquals(0.1f, buf.float, 1e-6f)
            assertEquals(0.2f, buf.float, 1e-6f)
            assertEquals(0.3f, buf.float, 1e-6f)

            // ValidFlags + Timestamp at offset 956 - 9 = 947
            val tailOffset = FuseProtocol.FUSED_PAYLOAD_SIZE - 9
            buf.position(tailOffset)
            val flags = buf.get().toInt() and 0xff
            val expectedFlags = FuseProtocol.FLAG_EEG or FuseProtocol.FLAG_PPG or FuseProtocol.FLAG_ACC
            assertEquals(expectedFlags, flags)
            val tsRead = buf.long
            assertEquals(ts, tsRead)
        }
    }

    @Test
    fun zero_padding_for_absent_sensors() {
        val sender = FuseUdpSender(targetHost = "127.0.0.1", targetPort = 65000)
        sender.use {
            val bytes = invokeEncode(it, null, null, null, 42L)
            // BlendShape 전체 0
            for (i in 0 until FuseProtocol.SIZE_BLENDSHAPE) assertEquals(0.toByte(), bytes[i])
            // Eye 전체 0
            for (i in 0 until FuseProtocol.SIZE_EYE)
                assertEquals(0.toByte(), bytes[FuseProtocol.SIZE_BLENDSHAPE + i])
            // Flags = 0
            assertEquals(0.toByte(), bytes[FuseProtocol.FUSED_PAYLOAD_SIZE - 9])
            // Timestamp = 42
            val buf = ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN)
            buf.position(FuseProtocol.FUSED_PAYLOAD_SIZE - 8)
            assertEquals(42L, buf.long)
        }
    }
}
