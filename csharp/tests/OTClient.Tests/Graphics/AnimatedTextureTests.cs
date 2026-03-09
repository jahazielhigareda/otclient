using OTClient.Framework.Graphics;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="AnimatedTexture"/> (frame-timing logic) and
/// <see cref="ApngReader"/> (APNG chunk parser).
/// Task T33.
/// </summary>
public sealed class AnimatedTextureTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a minimal RGBA frame buffer (all black) for the given dimensions.</summary>
    private static byte[] MakeFrame(int w, int h) => new byte[w * h * 4];

    /// <summary>Creates an <see cref="AnimatedTexture"/> with N identical 10 ms frames.</summary>
    private static AnimatedTexture MakeAnim(int frames, int delayMs = 10, int numPlays = 0)
    {
        var f = Enumerable.Range(0, frames).Select(_ => MakeFrame(2, 2)).ToList<byte[]>();
        var d = Enumerable.Repeat(delayMs, frames).ToList();
        return new AnimatedTexture(2, 2, f, d, numPlays);
    }

    // ─── AnimatedTexture constructor validation ───────────────────────────────

    [Fact]
    public void Constructor_ZeroWidth_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new AnimatedTexture(0, 2, [MakeFrame(0, 2)], [10]));

    [Fact]
    public void Constructor_ZeroHeight_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new AnimatedTexture(2, 0, [MakeFrame(2, 0)], [10]));

    [Fact]
    public void Constructor_NullFrames_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new AnimatedTexture(2, 2, null!, [10]));

    [Fact]
    public void Constructor_NullDelays_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new AnimatedTexture(2, 2, [MakeFrame(2, 2)], null!));

    [Fact]
    public void Constructor_EmptyFrames_Throws()
        => Assert.Throws<ArgumentException>(
            () => new AnimatedTexture(2, 2, [], []));

    [Fact]
    public void Constructor_MismatchedDelayCounts_Throws()
        => Assert.Throws<ArgumentException>(
            () => new AnimatedTexture(2, 2, [MakeFrame(2, 2)], [10, 20]));

    [Fact]
    public void Constructor_NegativeNumPlays_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new AnimatedTexture(2, 2, [MakeFrame(2, 2)], [10], -1));

    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_CurrentFrameIsZero()
        => Assert.Equal(0, MakeAnim(3).CurrentFrameIndex);

    [Fact]
    public void InitialState_IsRunningTrue_ForInfinite()
        => Assert.True(MakeAnim(3, numPlays: 0).IsRunning);

    [Fact]
    public void InitialState_IsRunningTrue_ForFinite()
        => Assert.True(MakeAnim(3, numPlays: 2).IsRunning);

    [Fact]
    public void InitialState_CurrentPlayCountIsZero()
        => Assert.Equal(0, MakeAnim(3).CurrentPlayCount);

    [Fact]
    public void Properties_Dimensions_Correct()
    {
        var anim = new AnimatedTexture(8, 16, [MakeFrame(8, 16)], [10]);
        Assert.Equal(8,  anim.Width);
        Assert.Equal(16, anim.Height);
    }

    [Fact]
    public void Properties_FrameCount_Correct()
        => Assert.Equal(3, MakeAnim(3).FrameCount);

    // ─── Update: frame advancement ────────────────────────────────────────────

    [Fact]
    public void Update_LessThanDelay_StaysOnFrame0()
    {
        var anim = MakeAnim(3, delayMs: 100);
        anim.Update(50);
        Assert.Equal(0, anim.CurrentFrameIndex);
    }

    [Fact]
    public void Update_ExactlyDelay_AdvancesToFrame1()
    {
        var anim = MakeAnim(3, delayMs: 100);
        anim.Update(100);
        Assert.Equal(1, anim.CurrentFrameIndex);
    }

    [Fact]
    public void Update_TwoDelays_AdvancesToFrame2()
    {
        var anim = MakeAnim(3, delayMs: 50);
        anim.Update(100);
        Assert.Equal(2, anim.CurrentFrameIndex);
    }

    [Fact]
    public void Update_LastFrame_WrapsToFrame0()
    {
        var anim = MakeAnim(2, delayMs: 10);
        anim.Update(10); // → frame 1
        anim.Update(10); // → frame 0 (wrap)
        Assert.Equal(0, anim.CurrentFrameIndex);
    }

    [Fact]
    public void Update_NegativeDelta_NoChange()
    {
        var anim = MakeAnim(3, delayMs: 10);
        anim.Update(-5);
        Assert.Equal(0, anim.CurrentFrameIndex);
    }

    [Fact]
    public void Update_ZeroDelta_NoChange()
    {
        var anim = MakeAnim(3, delayMs: 10);
        anim.Update(0);
        Assert.Equal(0, anim.CurrentFrameIndex);
    }

    // ─── Update: play counts & IsRunning ─────────────────────────────────────

    [Fact]
    public void Update_InfiniteLoop_NeverStops()
    {
        var anim = MakeAnim(2, delayMs: 10, numPlays: 0);
        for (int i = 0; i < 100; i++)
            anim.Update(10);
        Assert.True(anim.IsRunning);
    }

    [Fact]
    public void Update_OnePlay_StopsAfterAllFrames()
    {
        // 2 frames × 10 ms = 20 ms for one full play
        var anim = MakeAnim(2, delayMs: 10, numPlays: 1);
        anim.Update(20); // completes play 1 — wraps past last frame
        Assert.False(anim.IsRunning);
    }

    [Fact]
    public void Update_TwoPlays_StopsAfterTwoCycles()
    {
        var anim = MakeAnim(2, delayMs: 10, numPlays: 2);
        anim.Update(40); // 2 plays × 2 frames × 10 ms
        Assert.False(anim.IsRunning);
        Assert.Equal(2, anim.CurrentPlayCount);
    }

    [Fact]
    public void Update_FinishedAnim_DoesNotAdvanceFrame()
    {
        var anim = MakeAnim(2, delayMs: 10, numPlays: 1);
        anim.Update(20); // finish
        int frameAfterFinish = anim.CurrentFrameIndex;
        anim.Update(100); // should be a no-op
        Assert.Equal(frameAfterFinish, anim.CurrentFrameIndex);
    }

    // ─── Restart ──────────────────────────────────────────────────────────────

    [Fact]
    public void Restart_ResetsFrameAndPlayCount()
    {
        var anim = MakeAnim(3, delayMs: 10, numPlays: 1);
        anim.Update(30); // finish
        Assert.False(anim.IsRunning);

        anim.Restart();
        Assert.Equal(0, anim.CurrentFrameIndex);
        Assert.Equal(0, anim.CurrentPlayCount);
        Assert.True(anim.IsRunning);
    }

    [Fact]
    public void Restart_AfterPartialPlay_ResetsToFrame0()
    {
        var anim = MakeAnim(3, delayMs: 10);
        anim.Update(15); // advance partway through frame 1
        anim.Restart();
        Assert.Equal(0, anim.CurrentFrameIndex);
    }

    // ─── GetFramePixels / GetFrameDelayMs ─────────────────────────────────────

    [Fact]
    public void GetFramePixels_ReturnsCorrectBuffer()
    {
        byte[] frame0 = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        byte[] frame1 = [17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        var anim = new AnimatedTexture(2, 2, [frame0, frame1], [10, 20]);
        Assert.True(anim.GetFramePixels(0).Span.SequenceEqual(frame0));
        Assert.True(anim.GetFramePixels(1).Span.SequenceEqual(frame1));
    }

    [Fact]
    public void GetFrameDelayMs_ReturnsCorrectValues()
    {
        var anim = new AnimatedTexture(2, 2,
            [MakeFrame(2, 2), MakeFrame(2, 2)],
            [33, 66]);
        Assert.Equal(33, anim.GetFrameDelayMs(0));
        Assert.Equal(66, anim.GetFrameDelayMs(1));
    }

    [Fact]
    public void GetFramePixels_OutOfRange_Throws()
    {
        var anim = MakeAnim(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => anim.GetFramePixels(2));
    }

    [Fact]
    public void GetFrameDelayMs_NegativeIndex_Throws()
    {
        var anim = MakeAnim(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => anim.GetFrameDelayMs(-1));
    }

    // ─── Single-frame edge case ───────────────────────────────────────────────

    [Fact]
    public void SingleFrame_InfiniteLoop_AlwaysFrame0()
    {
        var anim = MakeAnim(1, delayMs: 10);
        anim.Update(1000);
        Assert.Equal(0, anim.CurrentFrameIndex);
        Assert.True(anim.IsRunning);
    }

    // ─── CurrentFramePixels convenience property ──────────────────────────────

    [Fact]
    public void CurrentFramePixels_InitiallyReturnsFrame0()
    {
        byte[] f0 = [7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22];
        var anim = new AnimatedTexture(2, 2, [f0, MakeFrame(2, 2)], [10, 10]);
        Assert.True(anim.CurrentFramePixels.Span.SequenceEqual(f0));
    }

    [Fact]
    public void CurrentFramePixels_AfterAdvance_ReturnsFrame1()
    {
        byte[] f0 = MakeFrame(2, 2);
        byte[] f1 = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var anim = new AnimatedTexture(2, 2, [f0, f1], [10, 10]);
        anim.Update(10);
        Assert.True(anim.CurrentFramePixels.Span.SequenceEqual(f1));
    }
}

// ─── ApngReader tests ─────────────────────────────────────────────────────────

public sealed class ApngReaderTests
{
    // ─── Minimal APNG chunk builder ───────────────────────────────────────────

    /// <summary>Builds a raw byte sequence from PNG chunks without zlib image data.</summary>
    private static byte[] BuildApng(int width, int height,
        (int delayNum, int delayDen)[]? fcTls = null,
        int numPlays = 0,
        bool includeActl = true)
    {
        var buf = new List<byte>();

        // PNG signature
        buf.AddRange([137, 80, 78, 71, 13, 10, 26, 10]);

        // IHDR (13 bytes payload)
        AppendChunk(buf, "IHDR", data =>
        {
            WriteI32(data, width);
            WriteI32(data, height);
            data.Add(8); // bit depth
            data.Add(2); // color type = RGB
            data.Add(0); data.Add(0); data.Add(0); // compression / filter / interlace
        });

        if (includeActl && fcTls is not null)
        {
            // acTL (8 bytes payload)
            AppendChunk(buf, "acTL", data =>
            {
                WriteI32(data, fcTls.Length); // numFrames
                WriteI32(data, numPlays);
            });

            // fcTL per frame (26 bytes payload each)
            for (int i = 0; i < fcTls.Length; i++)
            {
                int seqNum = i;
                var (dnum, dden) = fcTls[i];
                AppendChunk(buf, "fcTL", data =>
                {
                    WriteI32(data, seqNum);    // sequence number
                    WriteI32(data, width);     // frame width
                    WriteI32(data, height);    // frame height
                    WriteI32(data, 0);         // x offset
                    WriteI32(data, 0);         // y offset
                    WriteI16(data, dnum);      // delay numerator
                    WriteI16(data, dden);      // delay denominator
                    data.Add(0);               // dispose op
                    data.Add(0);               // blend op
                });
            }
        }

        // IEND (0 bytes payload)
        AppendChunk(buf, "IEND", _ => { });

        return [.. buf];
    }

    private static void AppendChunk(List<byte> buf, string type, Action<List<byte>> writeData)
    {
        var data = new List<byte>();
        writeData(data);

        WriteI32(buf, data.Count);
        buf.AddRange(System.Text.Encoding.ASCII.GetBytes(type));
        buf.AddRange(data);
        buf.AddRange([0, 0, 0, 0]); // CRC placeholder (parser skips CRC validation)
    }

    private static void WriteI32(List<byte> buf, int v)
    {
        buf.Add((byte)(v >> 24));
        buf.Add((byte)(v >> 16));
        buf.Add((byte)(v >> 8));
        buf.Add((byte)v);
    }

    private static void WriteI16(List<byte> buf, int v)
    {
        buf.Add((byte)(v >> 8));
        buf.Add((byte)v);
    }

    // ─── Null / empty input ───────────────────────────────────────────────────

    [Fact]
    public void TryParseMetadata_Null_ReturnsNull()
        => Assert.Null(ApngReader.TryParseMetadata(null));

    [Fact]
    public void TryParseMetadata_Empty_ReturnsNull()
        => Assert.Null(ApngReader.TryParseMetadata([]));

    [Fact]
    public void TryParseMetadata_TooShort_ReturnsNull()
        => Assert.Null(ApngReader.TryParseMetadata([1, 2, 3]));

    // ─── Invalid signature ────────────────────────────────────────────────────

    [Fact]
    public void TryParseMetadata_InvalidSignature_ReturnsNull()
    {
        byte[] bad = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        Assert.Null(ApngReader.TryParseMetadata(bad));
    }

    // ─── Static PNG (no acTL) ─────────────────────────────────────────────────

    [Fact]
    public void TryParseMetadata_StaticPng_ReturnsSingleSyntheticFrame()
    {
        byte[] png = BuildApng(32, 16, fcTls: null, includeActl: false);
        var result = ApngReader.TryParseMetadata(png);

        Assert.NotNull(result);
        Assert.Equal(32, result!.Width);
        Assert.Equal(16, result.Height);
        Assert.Single(result.Frames);
        Assert.Equal(1, result.NumPlays);
    }

    [Fact]
    public void TryParseMetadata_StaticPng_SyntheticFrame_Has100msDelay()
    {
        byte[] png = BuildApng(8, 8, fcTls: null, includeActl: false);
        var result = ApngReader.TryParseMetadata(png)!;
        Assert.Equal(100, result.Frames[0].DelayMs);
    }

    // ─── Valid APNG ───────────────────────────────────────────────────────────

    [Fact]
    public void TryParseMetadata_ValidApng_ReturnsDimensions()
    {
        byte[] apng = BuildApng(64, 48, [(1, 10), (1, 10)]);
        var result = ApngReader.TryParseMetadata(apng);
        Assert.NotNull(result);
        Assert.Equal(64, result!.Width);
        Assert.Equal(48, result.Height);
    }

    [Fact]
    public void TryParseMetadata_ValidApng_ReturnsFrameCount()
    {
        byte[] apng = BuildApng(4, 4, [(1, 10), (1, 10), (1, 10)]);
        var result = ApngReader.TryParseMetadata(apng)!;
        Assert.Equal(3, result.Frames.Count);
    }

    [Fact]
    public void TryParseMetadata_ValidApng_ReturnsNumPlays()
    {
        byte[] apng = BuildApng(4, 4, [(1, 10)], numPlays: 5);
        var result = ApngReader.TryParseMetadata(apng)!;
        Assert.Equal(5, result.NumPlays);
    }

    [Fact]
    public void TryParseMetadata_InfiniteLoopApng_NumPlaysIsZero()
    {
        byte[] apng = BuildApng(4, 4, [(1, 10)], numPlays: 0);
        var result = ApngReader.TryParseMetadata(apng)!;
        Assert.Equal(0, result.NumPlays);
    }

    [Fact]
    public void TryParseMetadata_FrameDelays_CalculatedCorrectly()
    {
        // 1/10 s = 100 ms,  1/20 s = 50 ms
        byte[] apng = BuildApng(4, 4, [(1, 10), (1, 20)]);
        var result = ApngReader.TryParseMetadata(apng)!;
        Assert.Equal(100, result.Frames[0].DelayMs);
        Assert.Equal(50,  result.Frames[1].DelayMs);
    }

    [Fact]
    public void TryParseMetadata_ZeroDelayDenominator_DefaultsTo100()
    {
        // delay_den = 0 should be treated as 100 per APNG spec
        byte[] apng = BuildApng(4, 4, [(1, 0)]);
        var result = ApngReader.TryParseMetadata(apng)!;
        Assert.Equal(10, result.Frames[0].DelayMs); // 1/100 s = 10 ms
    }

    [Fact]
    public void TryParseMetadata_FrameOffsets_CorrectForFirstFrame()
    {
        byte[] apng = BuildApng(4, 4, [(1, 10)]);
        var result = ApngReader.TryParseMetadata(apng)!;
        Assert.Equal(0, result.Frames[0].XOffset);
        Assert.Equal(0, result.Frames[0].YOffset);
    }
}
