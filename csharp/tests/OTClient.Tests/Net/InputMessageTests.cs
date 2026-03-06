using OTClient.Framework.Net;
using Xunit;

namespace OTClient.Tests.Net;

/// <summary>
/// Tests for <see cref="InputMessage"/> — binary packet reader.
/// No network connection is needed; all tests operate on in-memory buffers.
/// Task 5.3 / task 5.12.
/// </summary>
public sealed class InputMessageTests
{
    // ─── Construction / state ─────────────────────────────────────────────────

    [Fact]
    public void DefaultCtor_IsEof_Immediately()
    {
        var msg = new InputMessage();
        Assert.True(msg.IsEof);
    }

    [Fact]
    public void BufferCtor_InitialisesLengthAndPosition()
    {
        var msg = new InputMessage([1, 2, 3]);
        Assert.Equal(3, msg.Length);
        Assert.Equal(0, msg.Position);
        Assert.Equal(3, msg.Remaining);
        Assert.False(msg.IsEof);
    }

    // ─── ReadU8 ───────────────────────────────────────────────────────────────

    [Fact]
    public void ReadU8_ReturnsByteAndAdvancesPosition()
    {
        var msg = new InputMessage([0x42]);
        Assert.Equal(0x42, msg.ReadU8());
        Assert.Equal(1, msg.Position);
        Assert.True(msg.IsEof);
    }

    [Fact]
    public void ReadU8_PastEnd_Throws()
    {
        var msg = new InputMessage([]);
        Assert.Throws<InvalidOperationException>(() => msg.ReadU8());
    }

    // ─── ReadU16 ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0x0000, new byte[] { 0x00, 0x00 })]
    [InlineData(0x0102, new byte[] { 0x02, 0x01 })]   // little-endian
    [InlineData(0xFFFF, new byte[] { 0xFF, 0xFF })]
    public void ReadU16_LittleEndian(ushort expected, byte[] buf)
    {
        var msg = new InputMessage(buf);
        Assert.Equal(expected, msg.ReadU16());
    }

    // ─── ReadU32 ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0x01020304u, new byte[] { 0x04, 0x03, 0x02, 0x01 })]  // LE
    [InlineData(0x00000000u, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
    [InlineData(0xFFFFFFFFu, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
    public void ReadU32_LittleEndian(uint expected, byte[] buf)
    {
        var msg = new InputMessage(buf);
        Assert.Equal(expected, msg.ReadU32());
    }

    // ─── ReadU64 ──────────────────────────────────────────────────────────────

    [Fact]
    public void ReadU64_LittleEndian()
    {
        // 0x0102030405060708 in LE
        var msg = new InputMessage([0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01]);
        Assert.Equal(0x0102030405060708UL, msg.ReadU64());
    }

    // ─── Signed integers ──────────────────────────────────────────────────────

    [Fact]
    public void ReadS8_NegativeValue()
    {
        var msg = new InputMessage([0xFF]);
        Assert.Equal(-1, msg.ReadS8());
    }

    [Fact]
    public void ReadS16_NegativeValue()
    {
        var msg = new InputMessage([0xFF, 0xFF]);
        Assert.Equal(-1, msg.ReadS16());
    }

    [Fact]
    public void ReadS32_NegativeValue()
    {
        var msg = new InputMessage([0xFF, 0xFF, 0xFF, 0xFF]);
        Assert.Equal(-1, msg.ReadS32());
    }

    [Fact]
    public void ReadS64_NegativeValue()
    {
        var msg = new InputMessage([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);
        Assert.Equal(-1L, msg.ReadS64());
    }

    // ─── ReadBool ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0x00, false)]
    [InlineData(0x01, true)]
    [InlineData(0xFF, true)]
    public void ReadBool_InterpretsByteCorrectly(byte raw, bool expected)
    {
        var msg = new InputMessage([raw]);
        Assert.Equal(expected, msg.ReadBool());
    }

    // ─── ReadString ───────────────────────────────────────────────────────────

    [Fact]
    public void ReadString_Empty()
    {
        // u16 length = 0, no following bytes
        var msg = new InputMessage([0x00, 0x00]);
        Assert.Equal(string.Empty, msg.ReadString());
    }

    [Fact]
    public void ReadString_HelloWorld()
    {
        // "Hi" = 2 bytes; prefix u16 LE = [0x02, 0x00]
        var msg = new InputMessage([0x02, 0x00, (byte)'H', (byte)'i']);
        Assert.Equal("Hi", msg.ReadString());
    }

    [Fact]
    public void ReadString_Utf8()
    {
        byte[] str = System.Text.Encoding.UTF8.GetBytes("αβ");
        var buf = new byte[2 + str.Length];
        buf[0] = (byte)str.Length;
        buf[1] = 0;
        Array.Copy(str, 0, buf, 2, str.Length);
        var msg = new InputMessage(buf);
        Assert.Equal("αβ", msg.ReadString());
    }

    // ─── ReadBytes ────────────────────────────────────────────────────────────

    [Fact]
    public void ReadBytes_ReturnsCorrectSlice()
    {
        var msg = new InputMessage([0xAA, 0xBB, 0xCC, 0xDD]);
        byte[] result = msg.ReadBytes(2);
        Assert.Equal([0xAA, 0xBB], result);
        Assert.Equal(2, msg.Position);
    }

    [Fact]
    public void ReadBytes_PastEnd_Throws()
    {
        var msg = new InputMessage([0x01]);
        Assert.Throws<InvalidOperationException>(() => msg.ReadBytes(5));
    }

    // ─── Skip ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Skip_AdvancesPosition()
    {
        var msg = new InputMessage([0x01, 0x02, 0x03, 0x04]);
        msg.Skip(2);
        Assert.Equal(2, msg.Position);
        Assert.Equal(0x03, msg.ReadU8());
    }

    [Fact]
    public void Skip_PastEnd_Throws()
    {
        var msg = new InputMessage([0x01]);
        Assert.Throws<InvalidOperationException>(() => msg.Skip(10));
    }

    // ─── Reset / SetBuffer ────────────────────────────────────────────────────

    [Fact]
    public void Reset_ResetsPosition()
    {
        var msg = new InputMessage([0xAA, 0xBB]);
        msg.ReadU8();
        msg.Reset();
        Assert.Equal(0, msg.Position);
        Assert.Equal(0xAA, msg.ReadU8());
    }

    [Fact]
    public void SetBuffer_ReplacesBufferAndResetsPosition()
    {
        var msg = new InputMessage([0x01]);
        msg.ReadU8();
        msg.SetBuffer([0x42, 0x43]);
        Assert.Equal(0, msg.Position);
        Assert.Equal(2, msg.Length);
        Assert.Equal(0x42, msg.ReadU8());
    }

    // ─── XorDecrypt ───────────────────────────────────────────────────────────

    [Fact]
    public void XorDecrypt_AppliesKeyToAllUnreadBytes()
    {
        var msg = new InputMessage([0x00, 0xFF, 0x55]);
        msg.XorDecrypt(0xFF);
        Assert.Equal(0xFF, msg.ReadU8());
        Assert.Equal(0x00, msg.ReadU8());
        Assert.Equal(0xAA, msg.ReadU8());
    }

    // ─── XteaDecrypt ─────────────────────────────────────────────────────────

    [Fact]
    public void XteaDecrypt_RoundTrip_WithOutputMessage()
    {
        // Build an OutputMessage, XTEA-encrypt it, then read it back via InputMessage
        uint[] key = [0xDEADBEEFu, 0xCAFEBABEu, 0x01234567u, 0x89ABCDEFu];
        const string payload = "Hello";

        var out_ = new OutputMessage();
        out_.WriteString(payload);

        byte[] wire    = out_.ToXteaWireFrame(key);
        // Strip the 2-byte outer length header before passing to InputMessage
        byte[] xteaBlock = wire[2..];

        var msg = new InputMessage(xteaBlock);
        msg.XteaDecrypt(key);

        // After decrypt, first two bytes are the inner size — read them
        int innerLen = msg.ReadU16();
        Assert.True(innerLen > 0);

        // The remaining data should decode to the original string
        string result = msg.ReadString();
        Assert.Equal(payload, result);
    }

    // ─── Remaining / IsEof ───────────────────────────────────────────────────

    [Fact]
    public void Remaining_DecreasesWithReads()
    {
        var msg = new InputMessage([0x01, 0x02, 0x03]);
        Assert.Equal(3, msg.Remaining);
        msg.ReadU8();
        Assert.Equal(2, msg.Remaining);
        msg.ReadU16();
        Assert.Equal(0, msg.Remaining);
        Assert.True(msg.IsEof);
    }
}
