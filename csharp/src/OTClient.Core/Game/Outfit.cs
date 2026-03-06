namespace OTClient.Framework.Game;

// ─── Outfit ───────────────────────────────────────────────────────────────────

/// <summary>
/// Describes the visual appearance of a creature — outfit type, colour layers,
/// addon flags, and optional mount.
/// Maps to <c>src/client/outfit.h</c>.
/// Task 8.7.
/// </summary>
public sealed record Outfit
{
    /// <summary>Outfit type ID (0 = hidden).</summary>
    public int Id { get; init; }

    /// <summary>Mount creature ID (0 = not mounted).</summary>
    public int MountId { get; init; }

    // ─── Colour layers ────────────────────────────────────────────────────────

    /// <summary>Head colour index.</summary>
    public byte Head  { get; init; }
    /// <summary>Body colour index.</summary>
    public byte Body  { get; init; }
    /// <summary>Legs colour index.</summary>
    public byte Legs  { get; init; }
    /// <summary>Feet colour index.</summary>
    public byte Feet  { get; init; }

    // ─── Addons ───────────────────────────────────────────────────────────────

    /// <summary>Addon bitmask: bit 0 = addon 1, bit 1 = addon 2.</summary>
    public byte Addons { get; init; }

    public bool HasAddon1 => (Addons & 1) != 0;
    public bool HasAddon2 => (Addons & 2) != 0;
    public bool IsMounted => MountId > 0;

    // ─── Preset ───────────────────────────────────────────────────────────────

    /// <summary>Default "naked" human outfit.</summary>
    public static readonly Outfit Default = new() { Id = 128 };
}

// ─── Animator ─────────────────────────────────────────────────────────────────

/// <summary>
/// Manages sprite-frame sequencing for animated things.
/// Supports looping, ping-pong, and one-shot animation phases.
/// Maps to <c>src/client/animator.h</c>.
/// Task 8.19.
/// </summary>
public sealed class Animator
{
    private readonly int _frameCount;
    private readonly int _minDuration;   // ms per frame (minimum)
    private readonly int _maxDuration;   // ms per frame (maximum)
    private readonly bool _loop;

    private int  _currentFrame;
    private int  _elapsedMs;
    private bool _finished;
    private int  _direction = 1;   // +1 = forward, -1 = backward (ping-pong)

    public int   CurrentFrame => _currentFrame;
    public bool  IsFinished   => _finished;

    public Animator(int frameCount, int minDurationMs = 300, int maxDurationMs = 300, bool loop = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frameCount, 1);
        _frameCount  = frameCount;
        _minDuration = Math.Max(1, minDurationMs);
        _maxDuration = Math.Max(_minDuration, maxDurationMs);
        _loop        = loop;
    }

    /// <summary>Advances the animation by <paramref name="deltaMs"/> milliseconds.</summary>
    public void Update(int deltaMs)
    {
        if (_finished) return;
        // Single-frame animations only finish when not looping (after first update)
        if (_frameCount <= 1)
        {
            if (!_loop) _finished = true;
            return;
        }

        _elapsedMs += deltaMs;
        int duration = (_minDuration + _maxDuration) / 2;

        while (_elapsedMs >= duration)
        {
            _elapsedMs -= duration;
            _currentFrame += _direction;

            if (_currentFrame >= _frameCount)
            {
                if (_loop)
                    _currentFrame = 0;
                else
                {
                    _currentFrame = _frameCount - 1;
                    _finished     = true;
                    return;
                }
            }
            else if (_currentFrame < 0)
            {
                if (_loop)
                    _currentFrame = _frameCount - 1;
                else
                {
                    _currentFrame = 0;
                    _finished     = true;
                    return;
                }
            }
        }
    }

    public void Reset()
    {
        _currentFrame = 0;
        _elapsedMs    = 0;
        _finished     = false;
        _direction    = 1;
    }
}
