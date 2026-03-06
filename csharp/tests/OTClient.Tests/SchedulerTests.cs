using OTClient.Framework.Core;
using Xunit;

namespace OTClient.Tests;

public sealed class SchedulerTests
{
    private readonly EventDispatcher _dispatcher = new();
    private readonly Clock _clock = new();
    private readonly Scheduler _scheduler;

    public SchedulerTests()
    {
        _dispatcher.Init();
        _clock.Update();
        _scheduler = new Scheduler(_dispatcher, _clock);
    }

    // ─── One-shot scheduling ──────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleEvent_FiresAfterDelay()
    {
        bool fired = false;
        _scheduler.ScheduleEvent(() => fired = true, delayMs: 50);

        // Wait long enough for the scheduler's internal timer to tick
        await Task.Delay(150);
        _clock.Update();
        _dispatcher.Poll();

        Assert.True(fired);
    }

    [Fact]
    public async Task ScheduleEvent_DoesNotFireBeforeDelay()
    {
        bool fired = false;
        _scheduler.ScheduleEvent(() => fired = true, delayMs: 500);

        await Task.Delay(30);
        _clock.Update();
        _dispatcher.Poll();

        Assert.False(fired);
    }

    // ─── Repeating / cycle events ─────────────────────────────────────────────

    [Fact]
    public async Task CycleEvent_FiresMultipleTimes()
    {
        int count = 0;
        _scheduler.CycleEvent(() => count++, intervalMs: 40);

        await Task.Delay(200);
        _clock.Update();
        _dispatcher.Poll();

        Assert.True(count >= 3, $"Expected at least 3 cycles, got {count}");
    }

    [Fact]
    public async Task CycleEvent_StopsAfterMaxCycles()
    {
        int count = 0;
        _scheduler.CycleEvent(() => count++, intervalMs: 30, maxCycles: 3);

        await Task.Delay(300);
        _clock.Update();
        _dispatcher.Poll();

        Assert.True(count <= 3, $"Expected at most 3 cycles, got {count}");
    }

    // ─── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_PreventsExecution()
    {
        bool fired = false;
        var ev = _scheduler.ScheduleEvent(() => fired = true, delayMs: 50);
        ev.Cancel();

        await Task.Delay(150);
        _clock.Update();
        _dispatcher.Poll();

        Assert.False(fired);
    }

    [Fact]
    public async Task Cancel_StopsCycleEvent()
    {
        int count = 0;
        var ev = _scheduler.CycleEvent(() => count++, intervalMs: 30);

        // Let it fire a few times
        await Task.Delay(100);
        _clock.Update();
        _dispatcher.Poll();

        // Cancel and drain any already-queued dispatcher events
        ev.Cancel();
        await Task.Delay(50);
        _clock.Update();
        _dispatcher.Poll();

        // Snapshot AFTER draining pre-cancellation events
        int snapshot = count;

        // Wait another interval; no new events should be queued
        await Task.Delay(100);
        _clock.Update();
        _dispatcher.Poll();

        // Count must not have grown after cancellation was drained
        Assert.Equal(snapshot, count);
    }

    // ─── Event metadata ───────────────────────────────────────────────────────

    [Fact]
    public void ScheduleEvent_PreservesName()
    {
        var ev = _scheduler.ScheduleEvent(() => { }, delayMs: 100, name: "test-event");
        Assert.Equal("test-event", ev.Name);
    }
}
