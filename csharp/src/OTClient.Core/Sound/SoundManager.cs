using Raylib_cs;

namespace OTClient.Framework.Sound;

/// <summary>
/// Central audio manager: initialises the Raylib audio device, owns all
/// <see cref="SoundChannel"/>s, and drives <see cref="MusicSource"/> stream
/// updates each frame.
/// <para>
/// Maps to <c>SoundManager</c> in <c>src/framework/sound/soundmanager.*</c>.
/// Call <see cref="Init"/> once (from <see cref="OTClient.Framework.Core.Application.Init"/>),
/// then <see cref="Update"/> every frame (from the main loop), then
/// <see cref="Terminate"/> on shutdown.
/// </para>
/// Lua surface: <c>g_sounds</c>
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly Dictionary<string, SoundChannel> _channels = [];
    private readonly List<MusicSource> _streams = [];
    private readonly object _lock = new();
    private float _masterVolume = 1.0f;
    private bool _initialised;
    private bool _disposed;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the Raylib audio device and applies the current
    /// <see cref="MasterVolume"/>.  Safe to call from
    /// <see cref="OTClient.Framework.Core.Application.Init"/>.
    /// </summary>
    public void Init()
    {
        if (_initialised) return;
        Raylib.InitAudioDevice();
        if (Raylib.IsAudioDeviceReady())
            Raylib.SetMasterVolume(_masterVolume);
        _initialised = true;
    }

    /// <summary>
    /// Stops all sounds, clears channels, and closes the Raylib audio device.
    /// Called automatically by <see cref="OTClient.Framework.Core.Application.Terminate"/>.
    /// </summary>
    public void Terminate()
    {
        lock (_lock)
        {
            foreach (var ch in _channels.Values)
                ch.StopAll();
            _channels.Clear();
            _streams.Clear();
        }
        if (_initialised)
        {
            if (Raylib.IsAudioDeviceReady())
                Raylib.CloseAudioDevice();
            _initialised = false;
        }
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary><c>true</c> when the audio device is open and ready.</summary>
    public bool IsReady => Raylib.IsAudioDeviceReady();

    /// <summary>
    /// Master volume in [0.0, 1.0].  Changing this value calls
    /// <c>Raylib.SetMasterVolume</c> immediately when the device is ready.
    /// <para>Lua: <c>g_sounds:setMasterVolume(vol)</c></para>
    /// </summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            if (Raylib.IsAudioDeviceReady())
                Raylib.SetMasterVolume(_masterVolume);
        }
    }

    // ─── Channel management ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the named channel, creating it if it does not yet exist.
    /// <para>Lua: <c>g_sounds:getChannel(name)</c></para>
    /// </summary>
    public SoundChannel GetChannel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_lock)
        {
            if (!_channels.TryGetValue(name, out var ch))
            {
                ch = new SoundChannel(name);
                _channels[name] = ch;
            }
            return ch;
        }
    }

    /// <summary>
    /// Sets the volume of the named channel (creates it if needed).
    /// <para>Lua: <c>g_sounds:setVolume(channelName, vol)</c></para>
    /// </summary>
    public void SetChannelVolume(string name, float volume)
        => GetChannel(name).Volume = volume;

    /// <summary>Snapshot of all registered channels.</summary>
    public IReadOnlyList<SoundChannel> Channels
    {
        get { lock (_lock) return [.. _channels.Values]; }
    }

    // ─── Playback helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="SoundSource"/>, loads the audio file, optionally
    /// assigns it to the named channel, starts playback, and returns it.
    /// OGG Vorbis, WAV, and MP3 are all supported natively by Raylib.
    /// <para>Lua: <c>g_sounds:play(file, channel)</c></para>
    /// </summary>
    public SoundSource Play(string filePath, string? channelName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var src = new SoundSource(channelName is not null ? GetChannel(channelName) : null);
        src.Load(filePath);
        src.Play();
        return src;
    }

    /// <summary>
    /// Creates a <see cref="MusicSource"/>, loads the stream, optionally assigns
    /// it to the named channel, and registers it for automatic
    /// <see cref="MusicSource.Update"/> calls each frame.
    /// <para>Lua: <c>g_sounds:createMusic(file, channel)</c></para>
    /// </summary>
    public MusicSource CreateMusic(string filePath, string? channelName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var src = new MusicSource(channelName is not null ? GetChannel(channelName) : null, this);
        src.Load(filePath);
        return src;
    }

    // ─── Per-frame update ─────────────────────────────────────────────────────

    /// <summary>
    /// Feeds the decode buffer for all active streaming <see cref="MusicSource"/>s.
    /// Must be called once per frame from the main loop.
    /// </summary>
    public void Update()
    {
        MusicSource[] snapshot;
        lock (_lock) snapshot = [.. _streams];
        foreach (var s in snapshot)
            s.Update();
    }

    // ─── Internal stream registration (called by MusicSource) ─────────────────

    internal void RegisterStream(MusicSource source)
    {
        lock (_lock)
        {
            if (!_streams.Contains(source))
                _streams.Add(source);
        }
    }

    internal void UnregisterStream(MusicSource source)
    {
        lock (_lock) _streams.Remove(source);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Terminate();
    }
}
