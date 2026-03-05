using OTClient.Framework.Sound;
using Xunit;

namespace OTClient.Tests.Sound;

/// <summary>
/// Unit tests for <see cref="SoundChannel"/> — no audio device is required.
/// </summary>
public sealed class SoundChannelTests
{
    // ─── Name ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Name_ReturnsConstructorValue()
    {
        var ch = new SoundChannel("music");
        Assert.Equal("music", ch.Name);
    }

    [Theory]
    [InlineData("effects")]
    [InlineData("ambient")]
    [InlineData("ui")]
    public void Name_MatchesGivenString(string name)
    {
        var ch = new SoundChannel(name);
        Assert.Equal(name, ch.Name);
    }

    // ─── Volume ───────────────────────────────────────────────────────────────

    [Fact]
    public void Volume_DefaultsToOne()
    {
        var ch = new SoundChannel("effects");
        Assert.Equal(1.0f, ch.Volume);
    }

    [Theory]
    [InlineData(0.5f,  0.5f)]
    [InlineData(0.0f,  0.0f)]
    [InlineData(1.0f,  1.0f)]
    [InlineData(-0.5f, 0.0f)]
    [InlineData(1.5f,  1.0f)]
    public void Volume_SetterClampsToRange(float input, float expected)
    {
        var ch = new SoundChannel("effects");
        ch.Volume = input;
        Assert.Equal(expected, ch.Volume);
    }

    // ─── StopAll ──────────────────────────────────────────────────────────────

    [Fact]
    public void StopAll_DoesNotThrow_WhenNoSoundsRegistered()
    {
        var ch = new SoundChannel("effects");
        var ex = Record.Exception(() => ch.StopAll());
        Assert.Null(ex);
    }

    // ─── Volume propagation to SoundSource ───────────────────────────────────

    [Fact]
    public void Volume_Change_UpdatesEffectiveVolumeOnRegisteredSources()
    {
        var ch = new SoundChannel("effects");
        ch.Volume = 1.0f;

        // SoundSource without loading — EffectiveVolume is computed from volumes only
        var src = new SoundSource(ch);
        src.Volume = 0.8f;                // own volume
        ch.Volume = 0.5f;                 // channel volume → effective = 0.8 × 0.5 = 0.4

        Assert.Equal(0.4f, src.EffectiveVolume, precision: 5);
    }
}
