namespace OTClient.Framework.Sound;

/// <summary>
/// A named logical audio channel with an independent volume multiplier.
/// All <see cref="SoundSource"/>s assigned to this channel multiply their
/// own volume by the channel's <see cref="Volume"/>.
/// <para>
/// Maps to the channel-group concept in <c>src/framework/sound/soundmanager.*</c>.
/// Obtain an instance via <see cref="SoundManager.GetChannel"/>.
/// </para>
/// </summary>
public sealed class SoundChannel
{
    private float _volume = 1.0f;
    private readonly List<SoundSource> _sounds = [];
    private readonly object _lock = new();

    /// <param name="name">
    /// Unique name identifying this channel (e.g. <c>"effects"</c>,
    /// <c>"music"</c>, <c>"ambient"</c>).
    /// </param>
    public SoundChannel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Unique channel name (e.g. <c>"effects"</c>, <c>"music"</c>).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Per-channel volume multiplier in [0.0, 1.0].  Changing this value
    /// immediately re-applies the effective volume to every registered
    /// <see cref="SoundSource"/>.
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            SoundSource[] snapshot;
            lock (_lock) snapshot = [.. _sounds];
            foreach (var s in snapshot)
                s.ApplyEffectiveVolume();
        }
    }

    /// <summary>Stops all <see cref="SoundSource"/>s on this channel.</summary>
    public void StopAll()
    {
        SoundSource[] snapshot;
        lock (_lock) snapshot = [.. _sounds];
        foreach (var s in snapshot)
            s.Stop();
    }

    // ─── Internal registration (called by SoundSource) ───────────────────────

    internal void Add(SoundSource source)
    {
        lock (_lock)
        {
            if (!_sounds.Contains(source))
                _sounds.Add(source);
        }
    }

    internal void Remove(SoundSource source)
    {
        lock (_lock) _sounds.Remove(source);
    }
}
