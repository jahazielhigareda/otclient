using System.Collections.Concurrent;

namespace OTClient.Framework.Core;

/// <summary>
/// Represents a single scheduled task returned by the <see cref="Scheduler"/>.
/// </summary>
public sealed class ScheduledEvent
{
    private int _cancelled;
    private int _cyclesExecuted;

    internal ScheduledEvent(Action callback, long fireAtMillis, int intervalMs, int maxCycles, string? name)
    {
        Callback = callback;
        FireAtMillis = fireAtMillis;
        IntervalMs = intervalMs;
        MaxCycles = maxCycles;
        Name = name;
    }

    internal Action Callback { get; }
    internal long FireAtMillis { get; set; }
    internal int IntervalMs { get; }
    internal int MaxCycles { get; }
    public string? Name { get; }
    public bool IsCancelled => _cancelled != 0;
    public int CyclesExecuted => _cyclesExecuted;

    public void Cancel() => Interlocked.Exchange(ref _cancelled, 1);

    internal bool TryAdvanceCycle()
    {
        Interlocked.Increment(ref _cyclesExecuted);
        if (MaxCycles > 0 && _cyclesExecuted >= MaxCycles)
        {
            Cancel();
            return false;
        }
        return true;
    }
}

/// <summary>
/// Schedules callbacks to fire after a delay or on a repeating interval.
/// All callbacks are dispatched through the supplied <see cref="EventDispatcher"/>
/// so they always execute on the game/main thread.
/// Maps to <c>src/framework/core/scheduler.*</c> and the scheduler portion of
/// <c>EventDispatcher</c> in the C++ source.
/// </summary>
public sealed class Scheduler : IDisposable
{
    private readonly EventDispatcher _dispatcher;
    private readonly Clock _clock;

    // Min-heap ordered by fire time
    private readonly ConcurrentBag<ScheduledEvent> _pending = [];
    private readonly System.Threading.Timer _timer;
    private bool _disposed;

    private const int TickIntervalMs = 10; // resolution of the scheduler tick

    public Scheduler(EventDispatcher dispatcher, Clock clock)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(clock);
        _dispatcher = dispatcher;
        _clock = clock;
        _timer = new System.Threading.Timer(OnTick, null, TickIntervalMs, TickIntervalMs);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Schedules <paramref name="callback"/> to run once after <paramref name="delayMs"/> ms.</summary>
    public ScheduledEvent ScheduleEvent(Action callback, int delayMs, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var ev = new ScheduledEvent(callback, Clock.RealMillis + delayMs, 0, 1, name);
        _pending.Add(ev);
        return ev;
    }

    /// <summary>
    /// Schedules <paramref name="callback"/> to run repeatedly every <paramref name="intervalMs"/> ms.
    /// Pass <paramref name="maxCycles"/> &gt; 0 to limit the number of executions.
    /// </summary>
    public ScheduledEvent CycleEvent(Action callback, int intervalMs, int maxCycles = 0, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs), "Interval must be positive.");
        var ev = new ScheduledEvent(callback, Clock.RealMillis + intervalMs, intervalMs, maxCycles, name);
        _pending.Add(ev);
        return ev;
    }

    // ─── Internal tick ────────────────────────────────────────────────────────

    private void OnTick(object? _)
    {
        long now = Clock.RealMillis;
        // Snapshot the bag into a local list so we can re-add non-fired events
        var snapshot = _pending.ToArray();

        // Clear the bag – ConcurrentBag has no "clear" so drain it
        while (_pending.TryTake(out ScheduledEvent? _)) { }

        foreach (var ev in snapshot)
        {
            if (ev.IsCancelled) continue;

            if (now >= ev.FireAtMillis)
            {
                // Dispatch to game thread via the EventDispatcher
                var capture = ev;
                _dispatcher.AddEvent(() => capture.Callback(), EventPriority.Normal, capture.Name);

                // Re-schedule if it is a cycle event
                if (ev.IntervalMs > 0 && ev.TryAdvanceCycle())
                {
                    ev.FireAtMillis = now + ev.IntervalMs;
                    _pending.Add(ev);
                }
            }
            else
            {
                _pending.Add(ev);
            }
        }
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
