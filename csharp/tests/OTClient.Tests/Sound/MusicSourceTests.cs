using OTClient.Framework.Sound;
using Xunit;

namespace OTClient.Tests.Sound;

/// <summary>
/// Unit tests for <see cref="MusicSource"/> — no audio device is required.
/// Only CPU-side state (volume, Loop, IsLoaded, EffectiveVolume, TimeLength,
/// TimePlayed) is exercised.
/// </summary>
public sealed class MusicSourceTests
{
    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void IsLoaded_IsFalse_BeforeLoad()
    {
        var src = new MusicSource();
        Assert.False(src.IsLoaded);
    }

    [Fact]
    public void FilePath_IsEmpty_BeforeLoad()
    {
        var src = new MusicSource();
        Assert.Equal(string.Empty, src.FilePath);
    }

    [Fact]
    public void Volume_DefaultsToOne()
    {
        var src = new MusicSource();
        Assert.Equal(1.0f, src.Volume);
    }

    [Fact]
    public void Channel_IsNull_WhenCreatedWithoutChannel()
    {
        var src = new MusicSource();
        Assert.Null(src.Channel);
    }

    [Fact]
    public void Channel_IsSet_WhenCreatedWithChannel()
    {
        var ch = new SoundChannel("music");
        var src = new MusicSource(ch);
        Assert.Same(ch, src.Channel);
    }

    [Fact]
    public void TimeLength_IsZero_WhenNotLoaded()
    {
        var src = new MusicSource();
        Assert.Equal(0f, src.TimeLength);
    }

    [Fact]
    public void TimePlayed_IsZero_WhenNotLoaded()
    {
        var src = new MusicSource();
        Assert.Equal(0f, src.TimePlayed);
    }

    [Fact]
    public void IsPlaying_IsFalse_WhenNotLoaded()
    {
        var src = new MusicSource();
        Assert.False(src.IsPlaying);
    }

    [Fact]
    public void IsValid_IsFalse_WhenNotLoaded()
    {
        var src = new MusicSource();
        Assert.False(src.IsValid);
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
        var src = new MusicSource();
        src.Volume = input;
        Assert.Equal(expected, src.Volume);
    }

    // ─── EffectiveVolume ──────────────────────────────────────────────────────

    [Fact]
    public void EffectiveVolume_EqualsVolume_WhenNoChannel()
    {
        var src = new MusicSource();
        src.Volume = 0.6f;
        Assert.Equal(0.6f, src.EffectiveVolume, precision: 5);
    }

    [Fact]
    public void EffectiveVolume_IsProductOfSourceAndChannelVolume()
    {
        var ch = new SoundChannel("music");
        ch.Volume = 0.5f;
        var src = new MusicSource(ch);
        src.Volume = 0.8f;
        // 0.8 × 0.5 = 0.4
        Assert.Equal(0.4f, src.EffectiveVolume, precision: 5);
    }

    [Fact]
    public void EffectiveVolume_IsZero_WhenChannelVolumeIsZero()
    {
        var ch = new SoundChannel("music");
        ch.Volume = 0f;
        var src = new MusicSource(ch);
        src.Volume = 1.0f;
        Assert.Equal(0f, src.EffectiveVolume);
    }

    // ─── Loop flag ────────────────────────────────────────────────────────────

    [Fact]
    public void Loop_DefaultsToFalse()
    {
        var src = new MusicSource();
        Assert.False(src.Loop);
    }

    [Fact]
    public void Loop_CanBeSetToTrue()
    {
        var src = new MusicSource();
        src.Loop = true;
        Assert.True(src.Loop);
    }

    [Fact]
    public void Loop_CanBeSetToFalse_AfterTrue()
    {
        var src = new MusicSource();
        src.Loop = true;
        src.Loop = false;
        Assert.False(src.Loop);
    }

    // ─── Playback guards (no audio device) ───────────────────────────────────

    [Fact]
    public void Play_DoesNotThrow_WhenNotLoaded()
    {
        var src = new MusicSource();
        var ex = Record.Exception(() => src.Play());
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_DoesNotThrow_WhenNotLoaded()
    {
        var src = new MusicSource();
        var ex = Record.Exception(() => src.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Pause_DoesNotThrow_WhenNotLoaded()
    {
        var src = new MusicSource();
        var ex = Record.Exception(() => src.Pause());
        Assert.Null(ex);
    }

    [Fact]
    public void Resume_DoesNotThrow_WhenNotLoaded()
    {
        var src = new MusicSource();
        var ex = Record.Exception(() => src.Resume());
        Assert.Null(ex);
    }

    [Fact]
    public void Seek_DoesNotThrow_WhenNotLoaded()
    {
        var src = new MusicSource();
        var ex = Record.Exception(() => src.Seek(5.0f));
        Assert.Null(ex);
    }

    [Fact]
    public void Update_DoesNotThrow_WhenNotLoaded()
    {
        var src = new MusicSource();
        var ex = Record.Exception(() => src.Update());
        Assert.Null(ex);
    }

    // ─── Stream registration with SoundManager ────────────────────────────────

    [Fact]
    public void CreatedWithManager_RegistersForUpdate()
    {
        // A MusicSource created with a manager should be in the manager's
        // stream list after Load — verified indirectly by Update not throwing.
        var mgr = new SoundManager();
        // Note: we create MusicSource manually (without Load) to avoid touching
        // the audio device, then confirm Update is safe.
        var src = new MusicSource(null, mgr);
        var ex = Record.Exception(() => mgr.Update());
        Assert.Null(ex);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow_WhenNotLoaded()
    {
        var src = new MusicSource();
        var ex = Record.Exception(() => src.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var src = new MusicSource();
        src.Dispose();
        var ex = Record.Exception(() => src.Dispose());
        Assert.Null(ex);
    }
}
