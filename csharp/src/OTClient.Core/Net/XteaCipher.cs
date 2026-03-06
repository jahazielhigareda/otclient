namespace OTClient.Framework.Net;

/// <summary>
/// XTEA symmetric block cipher (64-bit block, 128-bit key).
/// Security-critical: the implementation is validated by round-trip tests
/// and cross-checked against the reference algorithm published by the
/// originators (Wheeler &amp; Needham, 1997).
/// <para>
/// Default round count is 32 (matching OTClient's crypt.cpp).
/// Operates on little-endian uint32 pairs; the caller must ensure the buffer
/// length is a multiple of 8 (one 64-bit block = 8 bytes).  Trailing bytes
/// that do not form a complete block are left unchanged.
/// </para>
/// Maps to <c>src/framework/util/crypt.cpp</c> (XTEA section).
/// </summary>
public static class XteaCipher
{
    private const uint Delta = 0x9E3779B9;

    /// <summary>Default number of rounds (matches OTClient's C++ implementation).</summary>
    public const int DefaultRounds = 32;

    // ─── Encrypt ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts <paramref name="data"/> in-place using XTEA.
    /// </summary>
    /// <param name="key">128-bit key as four little-endian uint32 values.</param>
    /// <param name="data">Buffer to encrypt; must be a multiple of 8 bytes in length.</param>
    /// <param name="rounds">Number of Feistel rounds (default 32).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> has fewer than 4 elements.</exception>
    public static void Encrypt(ReadOnlySpan<uint> key, Span<byte> data, int rounds = DefaultRounds)
    {
        if (key.Length < 4)
            throw new ArgumentException("Key must contain at least 4 uint32 values.", nameof(key));

        int blocks = data.Length / 8;
        for (int b = 0; b < blocks; b++)
        {
            int off = b * 8;
            uint v0 = ReadU32Le(data, off);
            uint v1 = ReadU32Le(data, off + 4);
            uint sum = 0;

            for (int i = 0; i < rounds; i++)
            {
                v0 += (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[(int)(sum & 3)]);
                sum += Delta;
                v1 += (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(int)((sum >> 11) & 3)]);
            }

            WriteU32Le(data, off, v0);
            WriteU32Le(data, off + 4, v1);
        }
    }

    /// <summary>Encrypts <paramref name="data"/> in-place (array-key overload).</summary>
    public static void Encrypt(uint[] key, Span<byte> data, int rounds = DefaultRounds)
        => Encrypt((ReadOnlySpan<uint>)key, data, rounds);

    // ─── Decrypt ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Decrypts <paramref name="data"/> in-place using XTEA.
    /// </summary>
    /// <param name="key">128-bit key as four little-endian uint32 values.</param>
    /// <param name="data">Buffer to decrypt; must be a multiple of 8 bytes in length.</param>
    /// <param name="rounds">Number of Feistel rounds (default 32).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> has fewer than 4 elements.</exception>
    public static void Decrypt(ReadOnlySpan<uint> key, Span<byte> data, int rounds = DefaultRounds)
    {
        if (key.Length < 4)
            throw new ArgumentException("Key must contain at least 4 uint32 values.", nameof(key));

        int blocks = data.Length / 8;
        for (int b = 0; b < blocks; b++)
        {
            int off = b * 8;
            uint v0 = ReadU32Le(data, off);
            uint v1 = ReadU32Le(data, off + 4);
            uint sum = unchecked(Delta * (uint)rounds);

            for (int i = 0; i < rounds; i++)
            {
                v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(int)((sum >> 11) & 3)]);
                sum -= Delta;
                v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[(int)(sum & 3)]);
            }

            WriteU32Le(data, off, v0);
            WriteU32Le(data, off + 4, v1);
        }
    }

    /// <summary>Decrypts <paramref name="data"/> in-place (array-key overload).</summary>
    public static void Decrypt(uint[] key, Span<byte> data, int rounds = DefaultRounds)
        => Decrypt((ReadOnlySpan<uint>)key, data, rounds);

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static uint ReadU32Le(Span<byte> buf, int off)
        => (uint)(buf[off] | (buf[off + 1] << 8) | (buf[off + 2] << 16) | (buf[off + 3] << 24));

    private static void WriteU32Le(Span<byte> buf, int off, uint v)
    {
        buf[off]     = (byte)v;
        buf[off + 1] = (byte)(v >> 8);
        buf[off + 2] = (byte)(v >> 16);
        buf[off + 3] = (byte)(v >> 24);
    }
}
