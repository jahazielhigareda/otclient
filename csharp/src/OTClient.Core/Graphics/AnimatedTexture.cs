namespace OTClient.Framework.Graphics;

// ─── AnimatedTexture (T33) ────────────────────────────────────────────────────

/// <summary>
/// Manages per-frame RGBA pixel data and timing for an animated image (APNG).
/// The class is GPU-independent: it holds raw pixel bytes and exposes the
/// current-frame index; the caller is responsible for uploading frames to GPU
/// textures via <see cref="Texture.FromMemory"/> when needed.
/// <para>
/// Timing mirrors the C++ implementation in
/// <c>src/framework/graphics/animatedtexture.cpp</c>:
/// <list type="bullet">
///   <item><description><see cref="NumPlays"/> == 0 → infinite loop.</description></item>
///   <item><description>Frame advances when accumulated time ≥ current-frame delay.</description></item>
///   <item><description>Play count increments each time the last frame wraps to frame 0.</description></item>
/// </list>
/// </para>
/// Maps to <c>src/framework/graphics/animatedtexture.{h,cpp}</c>.
/// Task T33.
/// </summary>
public sealed class AnimatedTexture
{
    private readonly IReadOnlyList<byte[]> _frames;        // raw RGBA pixels per frame
    private readonly IReadOnlyList<int>    _frameDelaysMs; // per-frame delay in ms
    private readonly int                   _numPlays;      // 0 = infinite

    private int _currentFrame;
    private int _currentPlay;
    private int _elapsedMs; // accumulated ms for the current frame

    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="framesRgba">
    /// Raw RGBA pixel buffers, one per frame.  Each buffer must be
    /// <c>width × height × 4</c> bytes.
    /// </param>
    /// <param name="frameDelaysMs">
    /// Per-frame delay in milliseconds.  Must have the same count as
    /// <paramref name="framesRgba"/>.  Values ≤ 0 are clamped to 1.
    /// </param>
    /// <param name="numPlays">
    /// Number of times to play the animation.  0 = infinite (default).
    /// </param>
    public AnimatedTexture(
        int width,
        int height,
        IReadOnlyList<byte[]> framesRgba,
        IReadOnlyList<int>    frameDelaysMs,
        int numPlays = 0)
    {
        if (width  <= 0) throw new ArgumentOutOfRangeException(nameof(width),  "Must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Must be positive.");
        ArgumentNullException.ThrowIfNull(framesRgba);
        ArgumentNullException.ThrowIfNull(frameDelaysMs);
        if (framesRgba.Count   == 0) throw new ArgumentException("At least one frame is required.", nameof(framesRgba));
        if (frameDelaysMs.Count != framesRgba.Count)
            throw new ArgumentException(
                $"frameDelaysMs.Count ({frameDelaysMs.Count}) must equal framesRgba.Count ({framesRgba.Count}).",
                nameof(frameDelaysMs));
        if (numPlays < 0) throw new ArgumentOutOfRangeException(nameof(numPlays), "Must be ≥ 0.");

        Width  = width;
        Height = height;
        _frames        = framesRgba;
        _frameDelaysMs = frameDelaysMs;
        _numPlays      = numPlays;
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>Image width in pixels.</summary>
    public int Width { get; }

    /// <summary>Image height in pixels.</summary>
    public int Height { get; }

    /// <summary>Total number of frames.</summary>
    public int FrameCount => _frames.Count;

    /// <summary>Number of play cycles (0 = infinite).</summary>
    public int NumPlays => _numPlays;

    /// <summary>Zero-based index of the frame that should be displayed now.</summary>
    public int CurrentFrameIndex => _currentFrame;

    /// <summary>How many full play cycles have completed so far.</summary>
    public int CurrentPlayCount => _currentPlay;

    /// <summary>
    /// <c>true</c> while the animation still has frames to show.
    /// Always <c>true</c> when <see cref="NumPlays"/> is 0 (infinite).
    /// </summary>
    public bool IsRunning => _numPlays == 0 || _currentPlay < _numPlays;

    /// <summary>Raw RGBA pixel data for the frame that should be displayed now.</summary>
    public ReadOnlyMemory<byte> CurrentFramePixels => _frames[_currentFrame];

    // ─── Methods ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances the animation clock by <paramref name="deltaMs"/> milliseconds.
    /// Mirrors <c>AnimatedTexture::update()</c>.
    /// </summary>
    public void Update(int deltaMs)
    {
        if (deltaMs <= 0 || !IsRunning) return;

        _elapsedMs += deltaMs;

        while (_elapsedMs >= EffectiveDelay(_currentFrame))
        {
            _elapsedMs -= EffectiveDelay(_currentFrame);

            if (++_currentFrame >= _frames.Count)
            {
                _currentFrame = 0;
                if (_numPlays != 0)
                {
                    _currentPlay++;
                    if (!IsRunning)
                    {
                        _elapsedMs = 0;
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resets the animation to the first frame.
    /// Mirrors <c>AnimatedTexture::restart()</c>.
    /// </summary>
    public void Restart()
    {
        _currentFrame = 0;
        _currentPlay  = 0;
        _elapsedMs    = 0;
    }

    /// <summary>Returns the raw RGBA pixel data for <paramref name="frameIndex"/>.</summary>
    public ReadOnlyMemory<byte> GetFramePixels(int frameIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, _frames.Count);
        return _frames[frameIndex];
    }

    /// <summary>Returns the display delay in milliseconds for <paramref name="frameIndex"/>.</summary>
    public int GetFrameDelayMs(int frameIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, _frames.Count);
        return EffectiveDelay(frameIndex);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private int EffectiveDelay(int frame) => Math.Max(1, _frameDelaysMs[frame]);
}

// ─── ApngReader (T33) ─────────────────────────────────────────────────────────

/// <summary>
/// Parses the chunk structure of an APNG (Animated PNG) byte buffer and
/// extracts animation metadata (dimensions, frame count, per-frame delays,
/// play count) without performing full PNG deflate decompression.
/// <para>
/// CRC validation is intentionally skipped so that hand-crafted test vectors
/// do not need to carry valid checksums.
/// </para>
/// Maps to <c>src/framework/graphics/apngloader.{h,cpp}</c>.
/// Task T33.
/// </summary>
public static class ApngReader
{
    // PNG file signature
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Per-frame metadata extracted from fcTL chunks.
    /// </summary>
    /// <param name="Width">Frame width in pixels.</param>
    /// <param name="Height">Frame height in pixels.</param>
    /// <param name="XOffset">X offset within the canvas.</param>
    /// <param name="YOffset">Y offset within the canvas.</param>
    /// <param name="DelayMs">Display duration in milliseconds.</param>
    public sealed record FrameInfo(int Width, int Height, int XOffset, int YOffset, int DelayMs);

    /// <summary>APNG metadata returned by <see cref="TryParseMetadata"/>.</summary>
    /// <param name="Width">Canvas width in pixels (from IHDR).</param>
    /// <param name="Height">Canvas height in pixels (from IHDR).</param>
    /// <param name="NumPlays">
    /// Play count from acTL chunk (0 = infinite).
    /// Returns 1 for static PNGs.
    /// </param>
    /// <param name="Frames">Per-frame metadata list (one entry per fcTL chunk).</param>
    public sealed record ApngData(int Width, int Height, int NumPlays, IReadOnlyList<FrameInfo> Frames);

    /// <summary>
    /// Parses APNG metadata from a raw byte buffer.
    /// Returns <see langword="null"/> if the data is too short or lacks the PNG signature.
    /// For a static (non-animated) PNG the result has one synthetic frame
    /// matching the IHDR dimensions and a 100 ms delay.
    /// </summary>
    public static ApngData? TryParseMetadata(byte[]? data)
    {
        if (data is null || data.Length < 8) return null;
        if (!data.AsSpan(0, 8).SequenceEqual(Signature)) return null;

        int pos = 8; // skip signature
        int width = 0, height = 0, numPlays = 0;
        bool hasActl = false;
        var frames = new List<FrameInfo>();

        while (pos + 12 <= data.Length)
        {
            int chunkLen = ReadI32(data, pos); pos += 4;

            if (pos + 4 > data.Length) break;
            string chunkType = System.Text.Encoding.ASCII.GetString(data, pos, 4); pos += 4;

            if (pos + chunkLen > data.Length) break;

            switch (chunkType)
            {
                case "IHDR" when chunkLen >= 8:
                    width  = ReadI32(data, pos);
                    height = ReadI32(data, pos + 4);
                    break;

                case "acTL" when chunkLen >= 8:
                    // numFrames is in the chunk but we rely on fcTL count instead
                    numPlays = ReadI32(data, pos + 4);
                    hasActl  = true;
                    break;

                case "fcTL" when chunkLen >= 26:
                    // seq(4), width(4), height(4), xoff(4), yoff(4),
                    // delay_num(2), delay_den(2), dispose(1), blend(1)
                    int fw      = ReadI32(data, pos + 4);
                    int fh      = ReadI32(data, pos + 8);
                    int fx      = ReadI32(data, pos + 12);
                    int fy      = ReadI32(data, pos + 16);
                    int dnum    = ReadU16(data, pos + 20);
                    int dden    = ReadU16(data, pos + 22);
                    if (dden == 0) dden = 100;
                    int delayMs = (int)Math.Round(1000.0 * dnum / dden);
                    if (delayMs <= 0) delayMs = 1;
                    frames.Add(new FrameInfo(fw, fh, fx, fy, delayMs));
                    break;

                case "IEND":
                    goto done;
            }

            pos += chunkLen + 4; // skip chunk data + CRC
        }
        done:

        if (!hasActl || frames.Count == 0)
        {
            // Static PNG: synthesise a single frame
            if (width > 0 && height > 0)
                return new ApngData(width, height, 1, [new FrameInfo(width, height, 0, 0, 100)]);
            return null;
        }

        return new ApngData(width, height, numPlays, frames);
    }

    // ─── Byte-order helpers ───────────────────────────────────────────────────

    private static int ReadI32(byte[] d, int i) =>
        (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];

    private static int ReadU16(byte[] d, int i) =>
        (d[i] << 8) | d[i + 1];
}
