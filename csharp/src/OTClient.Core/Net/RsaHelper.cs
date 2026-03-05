using System.Numerics;
using System.Security.Cryptography;

namespace OTClient.Framework.Net;

/// <summary>
/// RSA public-key encryption helper for the Tibia login-packet handshake.
/// Uses raw modular exponentiation (no OAEP/PKCS#1 padding — the Tibia protocol
/// inserts its own zero-byte flag and padding inside the RSA block).
/// <para>
/// Call <see cref="SetPublicKey"/> once at startup with the server's hex modulus,
/// then call <see cref="Encrypt"/> before sending the login packet.
/// </para>
/// Replaces the OpenSSL RSA calls in <c>src/framework/util/crypt.cpp</c>.
/// Task 5.11.
/// </summary>
public static class RsaHelper
{
    private static BigInteger _publicExponent = 65537;   // 0x10001
    private static BigInteger _modulus;
    private static bool _hasPublicKey;

    // ─── Key management ───────────────────────────────────────────────────────

    /// <summary>
    /// Sets the RSA public key from a big-endian hex modulus string.
    /// The public exponent defaults to 65537 (standard for Tibia servers).
    /// </summary>
    public static void SetPublicKey(string hexModulus, BigInteger? exponent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexModulus);
        // Prepend "0" so BigInteger.Parse treats it as unsigned
        _modulus = BigInteger.Parse("0" + hexModulus,
            System.Globalization.NumberStyles.HexNumber);
        if (exponent.HasValue)
            _publicExponent = exponent.Value;
        _hasPublicKey = true;
    }

    /// <summary><c>true</c> when a public key has been configured.</summary>
    public static bool HasPublicKey => _hasPublicKey;

    /// <summary>Clears the current key (useful in tests).</summary>
    public static void Reset()
    {
        _hasPublicKey = false;
        _modulus = default;
    }

    // ─── Raw RSA encryption ───────────────────────────────────────────────────

    /// <summary>
    /// Encrypts <paramref name="data"/> in-place using raw RSA (modular exponentiation).
    /// The buffer must be exactly as long as the key modulus in bytes (e.g. 128 bytes for 1024-bit).
    /// The result is written back big-endian, zero-padded to fill the buffer.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no public key is set.</exception>
    public static void Encrypt(Span<byte> data)
    {
        if (!_hasPublicKey)
            throw new InvalidOperationException("RSA public key not set. Call SetPublicKey first.");

        // Interpret data as big-endian unsigned integer
        var m = new BigInteger(data, isUnsigned: true, isBigEndian: true);

        // c = m^e mod n
        BigInteger c = BigInteger.ModPow(m, _publicExponent, _modulus);

        // Write back big-endian, zero-padded to data.Length
        byte[] result = c.ToByteArray(isUnsigned: true, isBigEndian: true);
        data.Clear();
        if (result.Length <= data.Length)
            result.CopyTo(data[(data.Length - result.Length)..]);
        else
            result[^data.Length..].CopyTo(data);
    }

    // ─── .NET RSA interop ─────────────────────────────────────────────────────

    /// <summary>
    /// Imports a PEM-encoded private or public key and returns a
    /// <see cref="RSA"/> instance for standard .NET operations (signing, OAEP, etc.).
    /// </summary>
    public static RSA ImportPem(string pem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }
}
