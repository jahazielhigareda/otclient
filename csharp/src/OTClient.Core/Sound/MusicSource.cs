using Raylib_cs;

namespace OTClient.Framework.Sound;

/// <summary>
/// A streaming music source backed by <c>Raylib.Music</c>.
/// Call <see cref="Load"/> to open the stream, <see cref="Play"/> to start
/// playback, and ensure <see cref="Update"/> is called every frame (done
/// automatically when created via <see cref="SoundManager.CreateMusic"/>).
/// <para>
/// Supports OGG Vorbis, WAV, MP3 and other formats natively via Raylib —
/// no extra library is required (task 4.6).
/// </para>
/// Maps to <c>StreamSoundSource</c> in
/// <c>src/framework/sound/streamsoundsource.*</c>.
/// </summary>
public sealed class MusicSource : IDisposable
{
    private Music _music;
    private float _volume = 1.0f;
    private bool _loaded;
    private bool _disposed;
    private readonly SoundManager? _manager;

    /// <param name="channel">
    /// Optional channel this source belongs to.  The effective playback
    /// volume = <see cref="Volume"/> × <see cref="SoundChannel.Volume"/>.
    /// </param>
    /// <param name="manager">
    /// The owning <see cref="SoundManager"/> that will call <see cref="Update"/>
    /// each frame.  Pass <c>null</c> when creating sources in unit tests.
    /// </param>
    public MusicSource(SoundChannel? channel = null, SoundManager? manager = null)
    {
        Channel = channel;
        _manager = manager;
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>The file path last passed to <see cref="Load"/>.</summary>
    public string FilePath { get; private set; } = string.Empty;

    /// <summary>The channel this source belongs to, or <c>null</c>.</summary>
    public SoundChannel? Channel { get; }

    /// <summary><c>true</c> after a successful <see cref="Load"/> call.</summary>
    public bool IsLoaded => _loaded;

    /// <summary>
    /// Source volume in [0.0, 1.0], independent of the channel multiplier.
    /// Changing this immediately calls <c>Raylib.SetMusicVolume</c> when loaded.
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            ApplyEffectiveVolume();
        }
    }

    /// <summary>Effective volume = <see cref="Volume"/> × channel volume.</summary>
    public float EffectiveVolume => _volume * (Channel?.Volume ?? 1f);

    /// <summary>
    /// Whether the stream loops when it reaches the end.
    /// Maps to <c>Music.Looping</c> in the native Raylib struct.
    /// </summary>
    public bool Loop
    {
        get => _music.Looping;
        set => _music.Looping = value;
    }

    /// <summary><c>true</c> while the music stream is playing.</summary>
    public bool IsPlaying => _loaded && Raylib.IsMusicStreamPlaying(_music);

    /// <summary><c>true</c> when the underlying Raylib Music handle is valid.</summary>
    public bool IsValid => _loaded && Raylib.IsMusicValid(_music);

    /// <summary>Total length of the track in seconds, or 0 when not loaded.</summary>
    public float TimeLength => _loaded ? Raylib.GetMusicTimeLength(_music) : 0f;

    /// <summary>Playback position in seconds, or 0 when not loaded.</summary>
    public float TimePlayed => _loaded ? Raylib.GetMusicTimePlayed(_music) : 0f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a music stream from <paramref name="filePath"/>.
    /// Unloads any previously loaded stream first.
    /// Supports OGG Vorbis, WAV, MP3 and other formats natively via Raylib.
    /// </summary>
    public void Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (_loaded) Unload();
        _music = Raylib.LoadMusicStream(filePath);
        _loaded = true;
        FilePath = filePath;
        ApplyEffectiveVolume();
        _manager?.RegisterStream(this);
    }

    /// <summary>Stops playback and releases the native stream buffer.</summary>
    public void Unload()
    {
        if (!_loaded) return;
        _manager?.UnregisterStream(this);
        Raylib.StopMusicStream(_music);
        Raylib.UnloadMusicStream(_music);
        _music = default;
        _loaded = false;
        FilePath = string.Empty;
    }

    // ─── Playback ─────────────────────────────────────────────────────────────

    /// <summary>Starts streaming the music from the beginning.</summary>
    public void Play()
    {
        if (!_loaded) return;
        ApplyEffectiveVolume();
        Raylib.PlayMusicStream(_music);
    }

    /// <summary>Stops playback and resets the stream position.</summary>
    public void Stop()
    {
        if (!_loaded) return;
        Raylib.StopMusicStream(_music);
    }

    /// <summary>Pauses playback at the current stream position.</summary>
    public void Pause()
    {
        if (!_loaded) return;
        Raylib.PauseMusicStream(_music);
    }

    /// <summary>Resumes a paused stream.</summary>
    public void Resume()
    {
        if (!_loaded) return;
        Raylib.ResumeMusicStream(_music);
    }

    /// <summary>Seeks to <paramref name="positionSeconds"/> in the stream.</summary>
    public void Seek(float positionSeconds)
    {
        if (!_loaded) return;
        Raylib.SeekMusicStream(_music, positionSeconds);
    }

    /// <summary>
    /// Feeds the audio decode buffer.  Must be called once per frame while the
    /// stream is playing.  Called automatically by <see cref="SoundManager.Update"/>.
    /// </summary>
    public void Update()
    {
        if (_loaded)
            Raylib.UpdateMusicStream(_music);
    }

    // ─── Volume helpers ───────────────────────────────────────────────────────

    private void ApplyEffectiveVolume()
    {
        if (_loaded)
            Raylib.SetMusicVolume(_music, EffectiveVolume);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unload();
    }
}
