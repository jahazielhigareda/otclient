using System.Diagnostics;

namespace OTClient.Framework.Core;

/// <summary>
/// Monotonic clock updated once per frame (or on demand via <see cref="Update"/>).
/// All values are taken from a single <see cref="Stopwatch"/> snapshot so that
/// code running within the same frame sees a consistent time.
/// Maps to <c>src/framework/core/clock.{h,cpp}</c>.
/// </summary>
public sealed class Clock
{
    private static readonly Stopwatch _sw = Stopwatch.StartNew();

    private long _currentMicros;
    private long _currentMillis;
    private double _currentSeconds;

    /// <summary>
    /// Captures the current wall-clock instant into the cached properties.
    /// Call once at the top of each frame from the main thread.
    /// </summary>
    public void Update()
    {
        long elapsed = _sw.ElapsedTicks;
        _currentMicros = elapsed * 1_000_000L / Stopwatch.Frequency;
        _currentMillis = elapsed * 1_000L   / Stopwatch.Frequency;
        _currentSeconds = (double)elapsed   / Stopwatch.Frequency;
    }

    // ─── Cached (frame-stable) accessors ─────────────────────────────────────

    /// <summary>Cached microseconds since the process started.</summary>
    public long Micros => Interlocked.Read(ref _currentMicros);

    /// <summary>Cached milliseconds since the process started.</summary>
    public long Millis => Interlocked.Read(ref _currentMillis);

    /// <summary>Cached seconds since the process started.</summary>
    public double Seconds => _currentSeconds;

    // ─── Real-time (non-cached) accessors ────────────────────────────────────

    /// <summary>Live microseconds, bypassing the cached update.</summary>
    public static long RealMicros =>
        _sw.ElapsedTicks * 1_000_000L / Stopwatch.Frequency;

    /// <summary>Live milliseconds, bypassing the cached update.</summary>
    public static long RealMillis =>
        _sw.ElapsedTicks * 1_000L / Stopwatch.Frequency;
}
