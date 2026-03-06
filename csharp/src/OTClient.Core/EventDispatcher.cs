using System.Collections.Concurrent;

namespace OTClient.Framework.Core;

/// <summary>
/// Priority level for events submitted to the <see cref="EventDispatcher"/>.
/// Lower numeric values are processed first within each <see cref="Poll"/> call.
/// </summary>
public enum EventPriority
{
    High   = 0,
    Normal = 1,
    Low    = 2,
}

/// <summary>
/// Represents a single dispatchable event, carrying a callback and a priority.
/// </summary>
public sealed class DispatcherEvent
{
    public DispatcherEvent(Action callback, EventPriority priority = EventPriority.Normal, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        Callback = callback;
        Priority = priority;
        Name = name;
        IsCancelled = false;
    }

    public Action Callback { get; }
    public EventPriority Priority { get; }
    public string? Name { get; }
    public bool IsCancelled { get; private set; }

    /// <summary>Prevents the callback from executing during the next <see cref="EventDispatcher.Poll"/>.</summary>
    public void Cancel() => IsCancelled = true;
}

/// <summary>
/// Thread-safe event dispatcher that queues callbacks and executes them on the
/// calling thread during <see cref="Poll"/>.  Events are drained in priority order
/// (High → Normal → Low) and, within the same priority, in submission order (FIFO).
/// <para>
/// Maps to <c>src/framework/core/eventdispatcher.{h,cpp}</c>.
/// </para>
/// </summary>
public sealed class EventDispatcher
{
    // Each priority band gets its own lock-free queue so that producers can
    // enqueue without contention and Poll can drain them in the right order.
    private readonly ConcurrentQueue<DispatcherEvent>[] _queues;
    private readonly List<DispatcherEvent> _deferred = [];
    private bool _shutdown;

    public EventDispatcher()
    {
        int bands = Enum.GetValues<EventPriority>().Length;
        _queues = new ConcurrentQueue<DispatcherEvent>[bands];
        for (int i = 0; i < bands; i++)
            _queues[i] = new ConcurrentQueue<DispatcherEvent>();
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>Initialises the dispatcher (idempotent).</summary>
    public void Init() => _shutdown = false;

    /// <summary>Marks the dispatcher as shut down; further events are silently dropped.</summary>
    public void Shutdown() => _shutdown = true;

    // ─── Producers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Schedules <paramref name="callback"/> to run during the next <see cref="Poll"/>.
    /// Thread-safe; may be called from any thread.
    /// </summary>
    public DispatcherEvent AddEvent(Action callback,
                                    EventPriority priority = EventPriority.Normal,
                                    string? name = null)
    {
        var ev = new DispatcherEvent(callback, priority, name);
        if (!_shutdown)
            _queues[(int)priority].Enqueue(ev);
        return ev;
    }

    /// <summary>
    /// Defers <paramref name="callback"/> until the <em>end</em> of the current
    /// <see cref="Poll"/> invocation (runs after all regular events in this cycle).
    /// Must be called from the poll thread.
    /// </summary>
    public void DeferEvent(Action callback, string? name = null)
    {
        if (!_shutdown)
            _deferred.Add(new DispatcherEvent(callback, EventPriority.Normal, name));
    }

    // ─── Consumer ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Drains and executes all currently queued events in priority order.
    /// Must be called from a single thread (the main/game thread).
    /// </summary>
    public void Poll()
    {
        if (_shutdown) return;

        // Drain each priority band in order
        foreach (var queue in _queues)
        {
            // Snapshot the current depth so that events added by callbacks
            // during this Poll are processed in the *next* cycle.
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                if (!queue.TryDequeue(out var ev)) break;
                if (!ev.IsCancelled)
                    ev.Callback();
            }
        }

        // Execute deferred events collected during this Poll
        if (_deferred.Count > 0)
        {
            var batch = _deferred.ToArray();
            _deferred.Clear();
            foreach (var ev in batch)
            {
                if (!ev.IsCancelled)
                    ev.Callback();
            }
        }
    }

    /// <summary>Number of pending events across all priority queues.</summary>
    public int PendingCount
    {
        get
        {
            int total = 0;
            foreach (var q in _queues)
                total += q.Count;
            return total;
        }
    }
}
