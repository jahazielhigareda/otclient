using Raylib_cs;

namespace OTClient.Framework.Sound;

/// <summary>
/// A fire-and-forget sound source backed by <c>Raylib.Sound</c>.
/// Load a file with <see cref="Load"/>, then call <see cref="Play"/> as many
/// times as needed.  Dispose when done to release the native audio buffer.
/// <para>
/// Supports OGG Vorbis, WAV, MP3, and other formats natively via Raylib —
/// no extra library is required for OGG (task 4.6).
/// </para>
/// Maps to <c>CombinedSoundSource</c> in
/// <c>src/framework/sound/combinedsoundsource.*</c>.
/// </summary>
public sealed class SoundSource : IDisposable
{
    private Raylib_cs.Sound _sound;
    private float _volume = 1.0f;
    private bool _loaded;
    private bool _disposed;

    /// <param name="channel">
    /// Optional channel this source belongs to.  The effective playback
    /// volume = <see cref="Volume"/> × <see cref="SoundChannel.Volume"/>.
    /// </param>
    public SoundSource(SoundChannel? channel = null)
    {
        Channel = channel;
        channel?.Add(this);
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>The file path last passed to <see cref="Load"/>.</summary>
    public string FilePath { get; private set; } = string.Empty;

    /// <summary>
    /// The channel this source belongs to, or <c>null</c> if unassigned.
    /// </summary>
    public SoundChannel? Channel { get; }

    /// <summary><c>true</c> after a successful <see cref="Load"/> call.</summary>
    public bool IsLoaded => _loaded;

    /// <summary>
    /// Source volume in [0.0, 1.0], independent of the channel multiplier.
    /// Changing this immediately calls <c>Raylib.SetSoundVolume</c> when loaded.
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

    /// <summary><c>true</c> while the sound is playing.</summary>
    public bool IsPlaying => _loaded && Raylib.IsSoundPlaying(_sound);

    /// <summary><c>true</c> when the underlying Raylib Sound handle is valid.</summary>
    public bool IsValid => _loaded && Raylib.IsSoundValid(_sound);

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a sound file into memory.  Unloads any previously loaded sound
    /// first.  Supports OGG Vorbis, WAV, MP3 and other formats natively.
    /// </summary>
    public void Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (_loaded) Unload();
        _sound = Raylib.LoadSound(filePath);
        _loaded = true;
        FilePath = filePath;
        ApplyEffectiveVolume();
    }

    /// <summary>Unloads the native audio buffer and resets state.</summary>
    public void Unload()
    {
        if (!_loaded) return;
        Raylib.UnloadSound(_sound);
        _sound = default;
        _loaded = false;
        FilePath = string.Empty;
    }

    // ─── Playback ─────────────────────────────────────────────────────────────

    /// <summary>Plays the sound from the beginning at the current effective volume.</summary>
    public void Play()
    {
        if (!_loaded) return;
        ApplyEffectiveVolume();
        Raylib.PlaySound(_sound);
    }

    /// <summary>Stops the sound immediately.</summary>
    public void Stop()
    {
        if (!_loaded) return;
        Raylib.StopSound(_sound);
    }

    /// <summary>Pauses the sound mid-play.</summary>
    public void Pause()
    {
        if (!_loaded) return;
        Raylib.PauseSound(_sound);
    }

    /// <summary>Resumes a paused sound.</summary>
    public void Resume()
    {
        if (!_loaded) return;
        Raylib.ResumeSound(_sound);
    }

    // ─── Volume helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Pushes the current effective volume to Raylib.
    /// Called automatically when <see cref="Volume"/> or the channel volume changes.
    /// </summary>
    internal void ApplyEffectiveVolume()
    {
        if (_loaded)
            Raylib.SetSoundVolume(_sound, EffectiveVolume);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Channel?.Remove(this);
        Unload();
    }
}
