using OTClient.Framework.Sound;
using Xunit;

namespace OTClient.Tests.Sound;

/// <summary>
/// Unit tests for <see cref="SoundManager"/> — no audio device is opened;
/// only CPU-side state and channel management are exercised.
/// </summary>
public sealed class SoundManagerTests
{
    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void IsReady_ReturnsFalse_WhenNotInitialised()
    {
        // Raylib.IsAudioDeviceReady() returns false without InitAudioDevice.
        var mgr = new SoundManager();
        Assert.False(mgr.IsReady);
    }

    [Fact]
    public void MasterVolume_DefaultsToOne()
    {
        var mgr = new SoundManager();
        Assert.Equal(1.0f, mgr.MasterVolume);
    }

    [Fact]
    public void Channels_EmptyOnCreation()
    {
        var mgr = new SoundManager();
        Assert.Empty(mgr.Channels);
    }

    // ─── MasterVolume clamping ────────────────────────────────────────────────

    [Theory]
    [InlineData(0.5f,  0.5f)]
    [InlineData(0.0f,  0.0f)]
    [InlineData(1.0f,  1.0f)]
    [InlineData(-0.5f, 0.0f)]
    [InlineData(1.5f,  1.0f)]
    public void MasterVolume_SetterClampsToRange(float input, float expected)
    {
        var mgr = new SoundManager();
        mgr.MasterVolume = input;
        Assert.Equal(expected, mgr.MasterVolume);
    }

    // ─── Channel management ───────────────────────────────────────────────────

    [Fact]
    public void GetChannel_ReturnsNewChannelWithCorrectName()
    {
        var mgr = new SoundManager();
        var ch = mgr.GetChannel("effects");
        Assert.Equal("effects", ch.Name);
    }

    [Fact]
    public void GetChannel_SameName_ReturnsSameInstance()
    {
        var mgr = new SoundManager();
        var ch1 = mgr.GetChannel("music");
        var ch2 = mgr.GetChannel("music");
        Assert.Same(ch1, ch2);
    }

    [Fact]
    public void GetChannel_DifferentNames_ReturnDifferentInstances()
    {
        var mgr = new SoundManager();
        var ch1 = mgr.GetChannel("effects");
        var ch2 = mgr.GetChannel("music");
        Assert.NotSame(ch1, ch2);
    }

    [Fact]
    public void GetChannel_AppearsInChannelsList()
    {
        var mgr = new SoundManager();
        mgr.GetChannel("ambient");
        Assert.Single(mgr.Channels);
        Assert.Equal("ambient", mgr.Channels[0].Name);
    }

    [Fact]
    public void GetChannel_MultipleChannels_AllListedInChannels()
    {
        var mgr = new SoundManager();
        mgr.GetChannel("effects");
        mgr.GetChannel("music");
        mgr.GetChannel("ambient");
        Assert.Equal(3, mgr.Channels.Count);
    }

    [Fact]
    public void SetChannelVolume_SetsVolumeOnChannel()
    {
        var mgr = new SoundManager();
        mgr.SetChannelVolume("effects", 0.4f);
        Assert.Equal(0.4f, mgr.GetChannel("effects").Volume);
    }

    [Fact]
    public void SetChannelVolume_CreatesChannelIfNotExists()
    {
        var mgr = new SoundManager();
        mgr.SetChannelVolume("newchannel", 0.8f);
        Assert.NotNull(mgr.GetChannel("newchannel"));
        Assert.Equal(0.8f, mgr.GetChannel("newchannel").Volume);
    }

    // ─── Update with no streams ───────────────────────────────────────────────

    [Fact]
    public void Update_DoesNotThrow_WhenNoStreamsRegistered()
    {
        var mgr = new SoundManager();
        var ex = Record.Exception(() => mgr.Update());
        Assert.Null(ex);
    }

    // ─── Terminate ────────────────────────────────────────────────────────────

    [Fact]
    public void Terminate_DoesNotThrow_WhenNotInitialised()
    {
        var mgr = new SoundManager();
        var ex = Record.Exception(() => mgr.Terminate());
        Assert.Null(ex);
    }

    [Fact]
    public void Terminate_ClearsChannels()
    {
        var mgr = new SoundManager();
        mgr.GetChannel("effects");
        mgr.Terminate();
        Assert.Empty(mgr.Channels);
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var mgr = new SoundManager();
        var ex = Record.Exception(() => mgr.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var mgr = new SoundManager();
        mgr.Dispose();
        var ex = Record.Exception(() => mgr.Dispose());
        Assert.Null(ex);
    }
}
