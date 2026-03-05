namespace OTClient.Framework.Core;

/// <summary>
/// A lightweight elapsed-time helper built on top of <see cref="Clock"/>.
/// Mirrors the C++ <c>Timer</c> class in <c>src/framework/core/timer.{h,cpp}</c>.
/// </summary>
public sealed class Timer
{
    private long _startMillis;
    private bool _stopped;

    /// <param name="clock">The shared <see cref="Clock"/> instance to read time from.</param>
    public Timer(Clock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Clock = clock;
        Restart();
    }

    internal Clock Clock { get; }

    // ─── Control ──────────────────────────────────────────────────────────────

    /// <summary>Resets the elapsed time counter.  Optionally shifts the start by <paramref name="shiftMs"/> ms.</summary>
    public void Restart(int shiftMs = 0)
    {
        _startMillis = Clock.Millis + shiftMs;
        _stopped = false;
    }

    /// <summary>Freezes the timer at its current elapsed value.</summary>
    public void Stop() => _stopped = true;

    /// <summary>Advances the start time forward by <paramref name="tickMs"/> ms (delays the next expiry).</summary>
    public void Advance(long tickMs) => _startMillis += tickMs;

    // ─── Query ────────────────────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> if the timer has not been stopped.</summary>
    public bool IsRunning => !_stopped;

    /// <summary>The absolute millisecond value at which the timer was last started.</summary>
    public long StartMillis => _startMillis;

    /// <summary>Milliseconds elapsed since the last <see cref="Restart"/>.</summary>
    public long TicksElapsed => _stopped ? 0 : Clock.Millis - _startMillis;

    /// <summary>Seconds elapsed since the last <see cref="Restart"/>.</summary>
    public float TimeElapsed => TicksElapsed / 1000f;

    /// <summary>Returns <c>true</c> if at least <paramref name="ms"/> milliseconds have elapsed.</summary>
    public bool HasExpired(long ms) => TicksElapsed >= ms;
}
