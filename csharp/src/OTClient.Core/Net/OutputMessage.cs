using System.Text;

namespace OTClient.Framework.Net;

/// <summary>
/// Binary packet writer with little-endian integer encoding and Pascal-style
/// string support.  Supports XTEA encryption as used by the Tibia game protocol.
/// <para>
/// Buffer layout when built:
/// <list type="table">
///   <item><term>bytes 0–5</term><description>Header reserve (outer size, inner size)</description></item>
///   <item><term>bytes 6–N</term><description>Payload (opcode + data)</description></item>
/// </list>
/// Use <see cref="ToWireFrame"/> for unencrypted packets or
/// <see cref="ToXteaWireFrame"/> for XTEA-encrypted ones.
/// </para>
/// Maps to <c>src/framework/net/outputmessage.{h,cpp}</c>.
/// Task 5.4.
/// </summary>
public sealed class OutputMessage
{
    // Reserve 6 bytes before the payload:
    //   [0..1] spare  [2..3] outer size  [4..5] inner size (XTEA)
    private const int HeaderReserve = 6;
    private const int DefaultCapacity = 512;
    private const int MaxPacketSize = 65535;

    private byte[] _buf;
    private int    _len;   // bytes written into the payload region

    // ─── Construction ─────────────────────────────────────────────────────────

    public OutputMessage(int capacity = DefaultCapacity)
    {
        _buf = new byte[HeaderReserve + capacity];
        _len = 0;
    }

    // ─── State ────────────────────────────────────────────────────────────────

    /// <summary>Number of payload bytes written so far.</summary>
    public int Length => _len;

    /// <summary>Resets the write cursor so the buffer can be reused.</summary>
    public void Reset() => _len = 0;

    // ─── Unsigned integers ────────────────────────────────────────────────────

    public void WriteU8(byte v)
    {
        Grow(1);
        _buf[HeaderReserve + _len] = v;
        _len++;
    }

    public void WriteU16(ushort v)
    {
        Grow(2);
        int p = HeaderReserve + _len;
        _buf[p]     = (byte)v;
        _buf[p + 1] = (byte)(v >> 8);
        _len += 2;
    }

    public void WriteU32(uint v)
    {
        Grow(4);
        int p = HeaderReserve + _len;
        _buf[p]     = (byte)v;
        _buf[p + 1] = (byte)(v >> 8);
        _buf[p + 2] = (byte)(v >> 16);
        _buf[p + 3] = (byte)(v >> 24);
        _len += 4;
    }

    public void WriteU64(ulong v)
    {
        WriteU32((uint)v);
        WriteU32((uint)(v >> 32));
    }

    // ─── Signed integers ──────────────────────────────────────────────────────

    public void WriteS8(sbyte v)  => WriteU8((byte)v);
    public void WriteS16(short v) => WriteU16((ushort)v);
    public void WriteS32(int v)   => WriteU32((uint)v);
    public void WriteS64(long v)  => WriteU64((ulong)v);

    // ─── Other primitives ─────────────────────────────────────────────────────

    public void WriteBool(bool v) => WriteU8(v ? (byte)1 : (byte)0);

    /// <summary>
    /// Writes a Tibia-encoded double: 1-byte precision then int32.
    /// </summary>
    public void WriteDouble(double v, byte precision = 2)
    {
        WriteU8(precision);
        WriteS32((int)(v * Math.Pow(10, precision)) + int.MinValue);
    }

    // ─── String ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a Pascal-style string: uint16 byte-length followed by UTF-8 bytes.
    /// </summary>
    public void WriteString(string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
        if (bytes.Length > ushort.MaxValue)
            throw new ArgumentException("String too long for a single protocol packet.");
        WriteU16((ushort)bytes.Length);
        WriteBytes(bytes);
    }

    // ─── Raw bytes ────────────────────────────────────────────────────────────

    public void WriteBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Grow(data.Length);
        Array.Copy(data, 0, _buf, HeaderReserve + _len, data.Length);
        _len += data.Length;
    }

    public void WriteBytes(ReadOnlySpan<byte> data)
    {
        Grow(data.Length);
        data.CopyTo(new Span<byte>(_buf, HeaderReserve + _len, data.Length));
        _len += data.Length;
    }

    // ─── Wire frame building ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the payload bytes only (without any length header).
    /// </summary>
    public byte[] ToArray()
    {
        var result = new byte[_len];
        Array.Copy(_buf, HeaderReserve, result, 0, _len);
        return result;
    }

    /// <summary>
    /// Returns an unencrypted wire frame: <c>[uint16 payload-length][payload]</c>.
    /// </summary>
    public byte[] ToWireFrame()
    {
        // Write outer size 2 bytes before payload (at _buf[4..5])
        int sizeOff = HeaderReserve - 2;
        _buf[sizeOff]     = (byte)_len;
        _buf[sizeOff + 1] = (byte)(_len >> 8);

        var frame = new byte[2 + _len];
        Array.Copy(_buf, sizeOff, frame, 0, 2 + _len);
        return frame;
    }

    /// <summary>
    /// Returns an XTEA-encrypted wire frame:
    /// <c>[uint16 outer-size][XTEA([uint16 inner-size][payload] + padding)]</c>.
    /// Pads the XTEA block to a multiple of 8 bytes.
    /// </summary>
    public byte[] ToXteaWireFrame(uint[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        // Build the XTEA input block: [2-byte inner size][payload][padding]
        int innerBlockLen = 2 + _len;
        int paddedLen     = (innerBlockLen + 7) & ~7;

        byte[] xteaBlock = new byte[paddedLen];
        xteaBlock[0] = (byte)_len;
        xteaBlock[1] = (byte)(_len >> 8);
        Array.Copy(_buf, HeaderReserve, xteaBlock, 2, _len);
        // remaining bytes are already zero (padding)

        XteaCipher.Encrypt(key, xteaBlock);

        // Wire frame = [2-byte outer size][XTEA block]
        var frame = new byte[2 + paddedLen];
        frame[0] = (byte)paddedLen;
        frame[1] = (byte)(paddedLen >> 8);
        Array.Copy(xteaBlock, 0, frame, 2, paddedLen);
        return frame;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void Grow(int needed)
    {
        if (HeaderReserve + _len + needed <= _buf.Length) return;
        int newSize = Math.Max(_buf.Length * 2, HeaderReserve + _len + needed);
        if (newSize - HeaderReserve > MaxPacketSize)
            throw new InvalidOperationException(
                $"OutputMessage exceeds maximum packet size ({MaxPacketSize} bytes).");
        Array.Resize(ref _buf, newSize);
    }
}
