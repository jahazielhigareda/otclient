using OTClient.Framework.Sound;
using Xunit;

namespace OTClient.Tests.Sound;

/// <summary>
/// Unit tests for <see cref="SoundSource"/> — no audio device is required.
/// Only CPU-side state (volume, IsLoaded, EffectiveVolume) is exercised.
/// </summary>
public sealed class SoundSourceTests
{
    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void IsLoaded_IsFalse_BeforeLoad()
    {
        var src = new SoundSource();
        Assert.False(src.IsLoaded);
    }

    [Fact]
    public void FilePath_IsEmpty_BeforeLoad()
    {
        var src = new SoundSource();
        Assert.Equal(string.Empty, src.FilePath);
    }

    [Fact]
    public void Volume_DefaultsToOne()
    {
        var src = new SoundSource();
        Assert.Equal(1.0f, src.Volume);
    }

    [Fact]
    public void Channel_IsNull_WhenCreatedWithoutChannel()
    {
        var src = new SoundSource();
        Assert.Null(src.Channel);
    }

    [Fact]
    public void Channel_IsSet_WhenCreatedWithChannel()
    {
        var ch = new SoundChannel("effects");
        var src = new SoundSource(ch);
        Assert.Same(ch, src.Channel);
    }

    // ─── Volume clamping ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.5f,  0.5f)]
    [InlineData(0.0f,  0.0f)]
    [InlineData(1.0f,  1.0f)]
    [InlineData(-0.1f, 0.0f)]
    [InlineData(2.0f,  1.0f)]
    public void Volume_SetterClampsToRange(float input, float expected)
    {
        var src = new SoundSource();
        src.Volume = input;
        Assert.Equal(expected, src.Volume);
    }

    // ─── EffectiveVolume ──────────────────────────────────────────────────────

    [Fact]
    public void EffectiveVolume_EqualsVolume_WhenNoChannel()
    {
        var src = new SoundSource();
        src.Volume = 0.7f;
        Assert.Equal(0.7f, src.EffectiveVolume, precision: 5);
    }

    [Fact]
    public void EffectiveVolume_IsProductOfSourceAndChannelVolume()
    {
        var ch = new SoundChannel("effects");
        ch.Volume = 0.5f;
        var src = new SoundSource(ch);
        src.Volume = 0.8f;
        // 0.8 × 0.5 = 0.4
        Assert.Equal(0.4f, src.EffectiveVolume, precision: 5);
    }

    [Fact]
    public void EffectiveVolume_IsZero_WhenChannelVolumeIsZero()
    {
        var ch = new SoundChannel("effects");
        ch.Volume = 0f;
        var src = new SoundSource(ch);
        src.Volume = 1.0f;
        Assert.Equal(0f, src.EffectiveVolume);
    }

    [Fact]
    public void EffectiveVolume_IsZero_WhenSourceVolumeIsZero()
    {
        var ch = new SoundChannel("effects");
        ch.Volume = 1.0f;
        var src = new SoundSource(ch);
        src.Volume = 0f;
        Assert.Equal(0f, src.EffectiveVolume);
    }

    // ─── Playback guards (no audio device) ───────────────────────────────────

    [Fact]
    public void Play_DoesNotThrow_WhenNotLoaded()
    {
        var src = new SoundSource();
        var ex = Record.Exception(() => src.Play());
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_DoesNotThrow_WhenNotLoaded()
    {
        var src = new SoundSource();
        var ex = Record.Exception(() => src.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Pause_DoesNotThrow_WhenNotLoaded()
    {
        var src = new SoundSource();
        var ex = Record.Exception(() => src.Pause());
        Assert.Null(ex);
    }

    [Fact]
    public void Resume_DoesNotThrow_WhenNotLoaded()
    {
        var src = new SoundSource();
        var ex = Record.Exception(() => src.Resume());
        Assert.Null(ex);
    }

    [Fact]
    public void IsPlaying_IsFalse_WhenNotLoaded()
    {
        var src = new SoundSource();
        Assert.False(src.IsPlaying);
    }

    [Fact]
    public void IsValid_IsFalse_WhenNotLoaded()
    {
        var src = new SoundSource();
        Assert.False(src.IsValid);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow_WhenNotLoaded()
    {
        var src = new SoundSource();
        var ex = Record.Exception(() => src.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var src = new SoundSource();
        src.Dispose();
        var ex = Record.Exception(() => src.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_RemovesSourceFromChannel()
    {
        // After disposal the source should not be in the channel's list,
        // verified by StopAll not attempting to call into a disposed source.
        var ch = new SoundChannel("effects");
        var src = new SoundSource(ch);
        src.Dispose();

        // StopAll must not throw after the source was disposed and removed.
        var ex = Record.Exception(() => ch.StopAll());
        Assert.Null(ex);
    }
}
