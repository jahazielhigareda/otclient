using System.Text;

namespace OTClient.Framework.Resources;

/// <summary>
/// XOR-based asset encryption and decryption matching the scheme used by the
/// original OTClient resource manager.
/// <para>
/// The cipher applies a rotating XOR with the password bytes.
/// A 4-byte header magic <c>0xBEEFCA0E</c> is prepended to encrypted data
/// so the decryptor can verify the key is correct.
/// </para>
/// Maps to <c>src/framework/core/resourcemanager.* encrypt/decrypt</c>.
/// Task 9.7.
/// </summary>
public static class AssetCipher
{
    private const uint Magic = 0xBEEFCA0E;

    // ─── Encrypt ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts <paramref name="data"/> with the XOR rotating-key cipher.
    /// The returned bytes have a 4-byte magic header prepended.
    /// </summary>
    public static byte[] Encrypt(byte[] data, string password)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] key = Encoding.UTF8.GetBytes(password);
        var result = new byte[4 + data.Length];

        // Write magic
        WriteUInt32LE(result, 0, Magic);

        // XOR body
        for (int i = 0; i < data.Length; i++)
            result[4 + i] = (byte)(data[i] ^ key[i % key.Length]);

        return result;
    }

    // ─── Decrypt ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Decrypts data encrypted with <see cref="Encrypt"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// When the magic header does not match the expected value.
    /// </exception>
    public static byte[] Decrypt(byte[] data, string password)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(password);
        if (data.Length < 4)
            throw new InvalidDataException("Encrypted data too short to contain magic header.");

        uint magic = ReadUInt32LE(data, 0);
        if (magic != Magic)
            throw new InvalidDataException(
                $"Magic header mismatch: expected 0x{Magic:X8}, got 0x{magic:X8}. Wrong password?");

        byte[] key    = Encoding.UTF8.GetBytes(password);
        byte[] result = new byte[data.Length - 4];
        for (int i = 0; i < result.Length; i++)
            result[i] = (byte)(data[4 + i] ^ key[i % key.Length]);

        return result;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="data"/> looks like it was
    /// encrypted with <see cref="Encrypt"/> (magic header present).
    /// </summary>
    public static bool IsEncrypted(byte[] data)
        => data.Length >= 4 && ReadUInt32LE(data, 0) == Magic;

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void WriteUInt32LE(byte[] buf, int offset, uint value)
    {
        buf[offset + 0] = (byte)(value);
        buf[offset + 1] = (byte)(value >> 8);
        buf[offset + 2] = (byte)(value >> 16);
        buf[offset + 3] = (byte)(value >> 24);
    }

    private static uint ReadUInt32LE(byte[] buf, int offset)
        => (uint)(buf[offset]
                | (buf[offset + 1] << 8)
                | (buf[offset + 2] << 16)
                | (buf[offset + 3] << 24));
}
