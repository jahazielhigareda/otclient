using OTClient.Framework.Net;
using Xunit;

namespace OTClient.Tests.Net;

/// <summary>
/// Tests for <see cref="XteaCipher"/>.
/// Security-critical: validates round-trip correctness, confirms that
/// encryption changes the data, and checks edge-case buffer sizes.
/// Task 5.10 / task 5.12.
/// </summary>
public sealed class XteaCipherTests
{
    private static readonly uint[] ZeroKey = [0u, 0u, 0u, 0u];
    private static readonly uint[] TestKey  = [0x01234567u, 0x89ABCDEFu, 0xFEDCBA98u, 0x76543210u];

    // ─── Round-trip correctness ───────────────────────────────────────────────

    [Fact]
    public void RoundTrip_ZeroKeyZeroData_GivesOriginal()
    {
        byte[] data     = new byte[8];
        byte[] original = (byte[])data.Clone();

        XteaCipher.Encrypt(ZeroKey, data);
        XteaCipher.Decrypt(ZeroKey, data);

        Assert.Equal(original, data);
    }

    [Fact]
    public void RoundTrip_ArbitraryKeyArbitraryData_GivesOriginal()
    {
        byte[] data     = [0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE];
        byte[] original = (byte[])data.Clone();

        XteaCipher.Encrypt(TestKey, data);
        XteaCipher.Decrypt(TestKey, data);

        Assert.Equal(original, data);
    }

    [Fact]
    public void RoundTrip_MultiBlock_GivesOriginal()
    {
        byte[] data = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        byte[] orig = (byte[])data.Clone();

        XteaCipher.Encrypt(TestKey, data);
        XteaCipher.Decrypt(TestKey, data);

        Assert.Equal(orig, data);
    }

    [Fact]
    public void RoundTrip_ExtraTrailingBytes_LeftUnchanged()
    {
        // 9 bytes — the last byte is not part of any complete 8-byte block
        byte[] data = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0xFF];
        byte[] orig = (byte[])data.Clone();

        XteaCipher.Encrypt(TestKey, data);
        XteaCipher.Decrypt(TestKey, data);

        Assert.Equal(orig, data);
        // Trailing byte must be completely untouched
        Assert.Equal(0xFF, data[8]);
    }

    // ─── Encryption changes the data ─────────────────────────────────────────

    [Fact]
    public void Encrypt_ChangesData_WithNonTrivialKey()
    {
        byte[] data     = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88];
        byte[] original = (byte[])data.Clone();

        XteaCipher.Encrypt(TestKey, data);

        Assert.NotEqual(original, data);
    }

    [Fact]
    public void Decrypt_ChangesData_WithNonTrivialKey()
    {
        byte[] data     = [0xAA, 0xBB, 0xCC, 0xDD, 0x00, 0x11, 0x22, 0x33];
        byte[] original = (byte[])data.Clone();

        XteaCipher.Decrypt(TestKey, data);

        Assert.NotEqual(original, data);
    }

    // ─── Different keys → different ciphertexts ───────────────────────────────

    [Fact]
    public void Encrypt_DifferentKeys_ProduceDifferentCiphertext()
    {
        byte[] data1 = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        byte[] data2 = (byte[])data1.Clone();

        uint[] key1 = [1u, 2u, 3u, 4u];
        uint[] key2 = [9u, 8u, 7u, 6u];

        XteaCipher.Encrypt(key1, data1);
        XteaCipher.Encrypt(key2, data2);

        Assert.NotEqual(data1, data2);
    }

    // ─── Round count variants ─────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_16Rounds_GivesOriginal()
    {
        byte[] data = [0xCA, 0xFE, 0xBA, 0xBE, 0xDE, 0xAD, 0xC0, 0xDE];
        byte[] orig = (byte[])data.Clone();

        XteaCipher.Encrypt(TestKey, data, rounds: 16);
        XteaCipher.Decrypt(TestKey, data, rounds: 16);

        Assert.Equal(orig, data);
    }

    [Fact]
    public void Encrypt_32Rounds_And_16Rounds_ProduceDifferentCiphertext()
    {
        byte[] data32 = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        byte[] data16 = (byte[])data32.Clone();

        XteaCipher.Encrypt(TestKey, data32, rounds: 32);
        XteaCipher.Encrypt(TestKey, data16, rounds: 16);

        Assert.NotEqual(data32, data16);
    }

    // ─── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_EmptyBuffer_DoesNotThrow()
    {
        byte[] empty = [];
        var ex = Record.Exception(() => XteaCipher.Encrypt(TestKey, empty));
        Assert.Null(ex);
    }

    [Fact]
    public void Decrypt_EmptyBuffer_DoesNotThrow()
    {
        byte[] empty = [];
        var ex = Record.Exception(() => XteaCipher.Decrypt(TestKey, empty));
        Assert.Null(ex);
    }

    [Fact]
    public void Encrypt_KeyTooShort_Throws()
    {
        uint[] shortKey = [1u, 2u];
        byte[] data = new byte[8];
        Assert.Throws<ArgumentException>(() => XteaCipher.Encrypt((ReadOnlySpan<uint>)shortKey, data));
    }

    [Fact]
    public void Decrypt_KeyTooShort_Throws()
    {
        uint[] shortKey = [1u];
        byte[] data = new byte[8];
        Assert.Throws<ArgumentException>(() => XteaCipher.Decrypt((ReadOnlySpan<uint>)shortKey, data));
    }

    // ─── Span overload parity ─────────────────────────────────────────────────

    [Fact]
    public void ArrayAndSpanOverloads_ProduceSameResult()
    {
        byte[] d1 = [0xAB, 0xCD, 0xEF, 0x01, 0x23, 0x45, 0x67, 0x89];
        byte[] d2 = (byte[])d1.Clone();

        XteaCipher.Encrypt(TestKey, d1);                        // uint[] overload
        XteaCipher.Encrypt((ReadOnlySpan<uint>)TestKey, d2);    // Span overload

        Assert.Equal(d1, d2);
    }

    // ─── DefaultRounds constant ───────────────────────────────────────────────

    [Fact]
    public void DefaultRounds_Is32()
    {
        Assert.Equal(32, XteaCipher.DefaultRounds);
    }
}
