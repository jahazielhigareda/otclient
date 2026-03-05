using OTClient.Framework.Net;
using Xunit;

namespace OTClient.Tests.Net;

/// <summary>
/// Tests for <see cref="OutputMessage"/> — binary packet writer.
/// No network connection is needed; all tests inspect the raw byte output.
/// Task 5.4 / task 5.12.
/// </summary>
public sealed class OutputMessageTests
{
    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void NewMessage_HasZeroLength()
    {
        var msg = new OutputMessage();
        Assert.Equal(0, msg.Length);
    }

    [Fact]
    public void ToArray_EmptyMessage_ReturnsEmptyArray()
    {
        var msg = new OutputMessage();
        Assert.Empty(msg.ToArray());
    }

    // ─── WriteU8 ──────────────────────────────────────────────────────────────

    [Fact]
    public void WriteU8_SingleByte_VisibleInToArray()
    {
        var msg = new OutputMessage();
        msg.WriteU8(0x42);
        Assert.Equal([0x42], msg.ToArray());
    }

    [Fact]
    public void WriteU8_MultipleBytes_InOrder()
    {
        var msg = new OutputMessage();
        msg.WriteU8(0xAA);
        msg.WriteU8(0xBB);
        msg.WriteU8(0xCC);
        Assert.Equal([0xAA, 0xBB, 0xCC], msg.ToArray());
    }

    // ─── WriteU16 ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0x0000, new byte[] { 0x00, 0x00 })]
    [InlineData(0x0102, new byte[] { 0x02, 0x01 })]   // little-endian
    [InlineData(0xFFFF, new byte[] { 0xFF, 0xFF })]
    public void WriteU16_LittleEndian(ushort value, byte[] expected)
    {
        var msg = new OutputMessage();
        msg.WriteU16(value);
        Assert.Equal(expected, msg.ToArray());
    }

    // ─── WriteU32 ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0x01020304u, new byte[] { 0x04, 0x03, 0x02, 0x01 })]
    [InlineData(0x00000000u, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
    [InlineData(0xFFFFFFFFu, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
    public void WriteU32_LittleEndian(uint value, byte[] expected)
    {
        var msg = new OutputMessage();
        msg.WriteU32(value);
        Assert.Equal(expected, msg.ToArray());
    }

    // ─── WriteU64 ─────────────────────────────────────────────────────────────

    [Fact]
    public void WriteU64_LittleEndian()
    {
        var msg = new OutputMessage();
        msg.WriteU64(0x0102030405060708UL);
        // LE: least significant byte first
        Assert.Equal([0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01], msg.ToArray());
    }

    // ─── Signed integers ──────────────────────────────────────────────────────

    [Fact]
    public void WriteS8_NegativeOne_ProducesFF()
    {
        var msg = new OutputMessage();
        msg.WriteS8(-1);
        Assert.Equal([0xFF], msg.ToArray());
    }

    [Fact]
    public void WriteS32_NegativeOne_ProducesAllFF()
    {
        var msg = new OutputMessage();
        msg.WriteS32(-1);
        Assert.Equal([0xFF, 0xFF, 0xFF, 0xFF], msg.ToArray());
    }

    // ─── WriteBool ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteBool_True_ProducesOne()
    {
        var msg = new OutputMessage();
        msg.WriteBool(true);
        Assert.Equal([0x01], msg.ToArray());
    }

    [Fact]
    public void WriteBool_False_ProducesZero()
    {
        var msg = new OutputMessage();
        msg.WriteBool(false);
        Assert.Equal([0x00], msg.ToArray());
    }

    // ─── WriteString ─────────────────────────────────────────────────────────

    [Fact]
    public void WriteString_Empty_WritesTwoZeroBytes()
    {
        var msg = new OutputMessage();
        msg.WriteString(string.Empty);
        Assert.Equal([0x00, 0x00], msg.ToArray());
    }

    [Fact]
    public void WriteString_AsciiContent_WritesLengthThenBytes()
    {
        var msg = new OutputMessage();
        msg.WriteString("Hi");
        // [0x02, 0x00] (u16 LE length) + [0x48, 0x69] ('H', 'i')
        Assert.Equal([0x02, 0x00, (byte)'H', (byte)'i'], msg.ToArray());
    }

    // ─── WriteBytes ───────────────────────────────────────────────────────────

    [Fact]
    public void WriteBytes_Array_AppendsBytes()
    {
        var msg = new OutputMessage();
        msg.WriteBytes([0xDE, 0xAD, 0xBE, 0xEF]);
        Assert.Equal([0xDE, 0xAD, 0xBE, 0xEF], msg.ToArray());
    }

    [Fact]
    public void WriteBytes_Span_AppendsBytes()
    {
        var msg = new OutputMessage();
        ReadOnlySpan<byte> span = [0x11, 0x22];
        msg.WriteBytes(span);
        Assert.Equal([0x11, 0x22], msg.ToArray());
    }

    // ─── Reset ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsPayload()
    {
        var msg = new OutputMessage();
        msg.WriteU32(0xDEADBEEF);
        msg.Reset();
        Assert.Equal(0, msg.Length);
        Assert.Empty(msg.ToArray());
    }

    // ─── ToWireFrame ─────────────────────────────────────────────────────────

    [Fact]
    public void ToWireFrame_PrependsTwoByteLength()
    {
        var msg = new OutputMessage();
        msg.WriteU8(0xAA);
        msg.WriteU8(0xBB);
        byte[] frame = msg.ToWireFrame();
        // [0x02, 0x00] (LE u16 = 2) + [0xAA, 0xBB]
        Assert.Equal([0x02, 0x00, 0xAA, 0xBB], frame);
    }

    [Fact]
    public void ToWireFrame_EmptyPayload_WritesTwoZeroBytes()
    {
        var msg = new OutputMessage();
        byte[] frame = msg.ToWireFrame();
        Assert.Equal([0x00, 0x00], frame);
    }

    // ─── ToXteaWireFrame ─────────────────────────────────────────────────────

    [Fact]
    public void ToXteaWireFrame_FrameSizeIsMultipleOfEightPlusTwo()
    {
        var msg   = new OutputMessage();
        uint[] key = [1u, 2u, 3u, 4u];
        msg.WriteU8(0xFF); // 1-byte payload
        byte[] wire = msg.ToXteaWireFrame(key);

        // Outer 2 bytes + XTEA block (padded to multiple of 8)
        Assert.Equal(2, wire.Length % 8); // remainder = 2 (outer header)
    }

    [Fact]
    public void ToXteaWireFrame_ThenInputMessageDecrypt_RoundTrip()
    {
        uint[] key = [0xAABBCCDDu, 0x11223344u, 0x55667788u, 0x99AABBCCu];
        var msg = new OutputMessage();
        msg.WriteU32(0xDEADBEEF);

        byte[] wire      = msg.ToXteaWireFrame(key);
        byte[] xteaBlock = wire[2..]; // strip outer 2-byte size

        var input = new InputMessage(xteaBlock);
        input.XteaDecrypt(key);

        int innerLen = input.ReadU16();
        Assert.Equal(4, innerLen); // we wrote a u32

        uint value = input.ReadU32();
        Assert.Equal(0xDEADBEEFu, value);
    }

    // ─── Length tracking ─────────────────────────────────────────────────────

    [Fact]
    public void Length_ReflectsWrittenBytes()
    {
        var msg = new OutputMessage();
        Assert.Equal(0, msg.Length);
        msg.WriteU8(1);
        Assert.Equal(1, msg.Length);
        msg.WriteU16(0);
        Assert.Equal(3, msg.Length);
        msg.WriteU32(0);
        Assert.Equal(7, msg.Length);
    }
}
