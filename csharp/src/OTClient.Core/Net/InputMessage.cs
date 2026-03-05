using System.Text;

namespace OTClient.Framework.Net;

/// <summary>
/// Binary packet reader with little-endian integer decoding and Pascal-style
/// string support.  Supports in-place XOR scrambling and XTEA decryption of
/// the packet payload, as used by the Tibia game protocol.
/// <para>
/// Maps to <c>src/framework/net/inputmessage.{h,cpp}</c>.
/// Task 5.3.
/// </para>
/// </summary>
public sealed class InputMessage
{
    private byte[] _buffer;
    private int _pos;
    private int _length;

    // ─── Construction ─────────────────────────────────────────────────────────

    /// <summary>Creates an empty <see cref="InputMessage"/>.</summary>
    public InputMessage() : this([]) { }

    /// <summary>Creates an <see cref="InputMessage"/> pre-loaded with <paramref name="data"/>.</summary>
    public InputMessage(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _buffer = data;
        _length = data.Length;
        _pos    = 0;
    }

    // ─── State ────────────────────────────────────────────────────────────────

    /// <summary>Current read cursor position.</summary>
    public int Position  => _pos;

    /// <summary>Total number of bytes in the buffer (may be reduced by <see cref="XteaDecrypt"/>).</summary>
    public int Length    => _length;

    /// <summary>Bytes that can still be read.</summary>
    public int Remaining => _length - _pos;

    /// <summary><c>true</c> when the cursor is at or past the end of the message.</summary>
    public bool IsEof    => _pos >= _length;

    // ─── Buffer control ───────────────────────────────────────────────────────

    /// <summary>Resets the cursor to the beginning without clearing the data.</summary>
    public void Reset() => _pos = 0;

    /// <summary>Replaces the internal buffer and resets the cursor.</summary>
    public void SetBuffer(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _buffer = data;
        _length = data.Length;
        _pos    = 0;
    }

    // ─── Unsigned integers ────────────────────────────────────────────────────

    /// <summary>Reads one unsigned byte.</summary>
    public byte ReadU8()
    {
        Require(1);
        return _buffer[_pos++];
    }

    /// <summary>Reads a little-endian uint16.</summary>
    public ushort ReadU16()
    {
        Require(2);
        ushort v = (ushort)(_buffer[_pos] | (_buffer[_pos + 1] << 8));
        _pos += 2;
        return v;
    }

    /// <summary>Reads a little-endian uint32.</summary>
    public uint ReadU32()
    {
        Require(4);
        uint v = (uint)(_buffer[_pos]
                      | (_buffer[_pos + 1] << 8)
                      | (_buffer[_pos + 2] << 16)
                      | (_buffer[_pos + 3] << 24));
        _pos += 4;
        return v;
    }

    /// <summary>Reads a little-endian uint64.</summary>
    public ulong ReadU64()
    {
        ulong lo = ReadU32();
        ulong hi = ReadU32();
        return lo | (hi << 32);
    }

    // ─── Signed integers ──────────────────────────────────────────────────────

    /// <summary>Reads one signed byte.</summary>
    public sbyte ReadS8()  => (sbyte)ReadU8();

    /// <summary>Reads a little-endian int16.</summary>
    public short  ReadS16() => (short)ReadU16();

    /// <summary>Reads a little-endian int32.</summary>
    public int    ReadS32() => (int)ReadU32();

    /// <summary>Reads a little-endian int64.</summary>
    public long   ReadS64() => (long)ReadU64();

    // ─── Other primitives ─────────────────────────────────────────────────────

    /// <summary>Reads a single byte and interprets it as a boolean (0 = false).</summary>
    public bool ReadBool() => ReadU8() != 0;

    /// <summary>
    /// Reads a Tibia-encoded double: 1-byte precision then int32 value.
    /// Actual value = (int32 − <see cref="int.MinValue"/>) / 10^precision.
    /// </summary>
    public double ReadDouble()
    {
        byte precision = ReadU8();
        int  raw       = ReadS32();
        return (raw - (double)int.MinValue) / Math.Pow(10, precision);
    }

    // ─── String ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a Pascal-style string: uint16 byte-length followed by UTF-8 bytes.
    /// </summary>
    public string ReadString()
    {
        int len = ReadU16();
        if (len == 0) return string.Empty;
        Require(len);
        string s = Encoding.UTF8.GetString(_buffer, _pos, len);
        _pos += len;
        return s;
    }

    // ─── Raw bytes ────────────────────────────────────────────────────────────

    /// <summary>Reads exactly <paramref name="count"/> bytes and returns them as a new array.</summary>
    public byte[] ReadBytes(int count)
    {
        Require(count);
        var result = new byte[count];
        Array.Copy(_buffer, _pos, result, 0, count);
        _pos += count;
        return result;
    }

    /// <summary>Advances the cursor by <paramref name="count"/> bytes without reading.</summary>
    public void Skip(int count)
    {
        Require(count);
        _pos += count;
    }

    // ─── Decryption ───────────────────────────────────────────────────────────

    /// <summary>
    /// Applies XOR scrambling to the unread portion of the buffer.
    /// Used by older Tibia versions.
    /// </summary>
    public void XorDecrypt(byte key)
    {
        for (int i = _pos; i < _length; i++)
            _buffer[i] ^= key;
    }

    /// <summary>
    /// Decrypts the unread portion of the buffer in-place using XTEA, then
    /// trims <see cref="Length"/> to the inner message size encoded in the first
    /// two decrypted bytes.
    /// <para>
    /// After this call, <see cref="Position"/> is still at the start of the
    /// decrypted block; the caller reads the 2-byte inner size with
    /// <see cref="ReadU16"/> and then parses opcodes normally.
    /// </para>
    /// </summary>
    public void XteaDecrypt(uint[] key)
    {
        int remaining = _length - _pos;
        if (remaining <= 0) return;

        XteaCipher.Decrypt(key, new Span<byte>(_buffer, _pos, remaining));

        // Trim logical length to the inner message size in the first two
        // decrypted bytes so reads beyond the real content are rejected.
        if (remaining >= 2)
        {
            int innerLen = _buffer[_pos] | (_buffer[_pos + 1] << 8);
            int newLen   = _pos + 2 + innerLen;
            if (newLen <= _buffer.Length)
                _length = newLen;
        }
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private void Require(int count)
    {
        if (_pos + count > _length)
            throw new InvalidOperationException(
                $"InputMessage: attempted to read {count} byte(s) at position {_pos} " +
                $"but only {_length - _pos} byte(s) remain.");
    }
}
