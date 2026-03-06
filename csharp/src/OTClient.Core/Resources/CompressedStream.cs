using System.IO;
using System.IO.Compression;

namespace OTClient.Framework.Resources;

// ─── CompressionType ──────────────────────────────────────────────────────────

/// <summary>Supported decompression algorithms.</summary>
public enum CompressionType
{
    Deflate,
    GZip,
    ZLib,
    /// <summary>LZMA — requires an external shim (see <see cref="LzmaStream"/>).</summary>
    Lzma,
}

// ─── CompressedStream ─────────────────────────────────────────────────────────

/// <summary>
/// Decompresses an inner stream using one of the supported algorithms.
/// Uses <see cref="DeflateStream"/>, <see cref="GZipStream"/> or
/// <see cref="ZLibStream"/> from <c>System.IO.Compression</c>.
/// Task 9.3.
/// </summary>
public sealed class CompressedStream : Stream
{
    private readonly Stream _inner;

    public CompressedStream(Stream compressedData, CompressionType type, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(compressedData);
        _inner = type switch
        {
            CompressionType.Deflate => new DeflateStream(compressedData, CompressionMode.Decompress, leaveOpen),
            CompressionType.GZip    => new GZipStream(compressedData, CompressionMode.Decompress, leaveOpen),
            CompressionType.ZLib    => new ZLibStream(compressedData, CompressionMode.Decompress, leaveOpen),
            CompressionType.Lzma    => throw new NotSupportedException(
                "LZMA decompression requires SharpCompress or a .NET 10 LZMA shim. " +
                "Reference SharpCompress ≥ 0.37 and wrap with LzmaStream."),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    // ─── Stream delegation ────────────────────────────────────────────────────

    public override bool CanRead  => _inner.CanRead;
    public override bool CanSeek  => false;
    public override bool CanWrite => false;
    public override long Length   => throw new NotSupportedException();
    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
        => _inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer)
        => _inner.Read(buffer);

    public override void Flush()   => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value)                => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    // ─── Convenience helpers ──────────────────────────────────────────────────

    /// <summary>Decompresses all bytes from <paramref name="src"/> using the given algorithm.</summary>
    public static byte[] DecompressAll(Stream src, CompressionType type)
    {
        ArgumentNullException.ThrowIfNull(src);
        using var cs  = new CompressedStream(src, type, leaveOpen: false);
        using var ms  = new MemoryStream();
        cs.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Decompresses a byte array using the given algorithm.</summary>
    public static byte[] DecompressAll(byte[] data, CompressionType type)
    {
        using var src = new MemoryStream(data, writable: false);
        return DecompressAll(src, type);
    }

    /// <summary>
    /// Compresses a byte array using Deflate (raw) and returns the compressed bytes.
    /// </summary>
    public static byte[] CompressDeflate(byte[] data)
    {
        using var ms  = new MemoryStream();
        using var def = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true);
        def.Write(data);
        def.Flush();
        return ms.ToArray();
    }
}
