using OTClient.Framework.Core;
using Xunit;

namespace OTClient.Tests;

public sealed class EventDispatcherTests
{
    private readonly EventDispatcher _dispatcher = new();

    public EventDispatcherTests() => _dispatcher.Init();

    // ─── Basic enqueue / poll ─────────────────────────────────────────────────

    [Fact]
    public void AddEvent_ExecutesOnPoll()
    {
        bool called = false;
        _dispatcher.AddEvent(() => called = true);
        _dispatcher.Poll();
        Assert.True(called);
    }

    [Fact]
    public void AddEvent_NotExecutedBeforePoll()
    {
        bool called = false;
        _dispatcher.AddEvent(() => called = true);
        Assert.False(called);
    }

    [Fact]
    public void MultipleEvents_AllExecutedInPoll()
    {
        int count = 0;
        for (int i = 0; i < 5; i++)
            _dispatcher.AddEvent(() => count++);
        _dispatcher.Poll();
        Assert.Equal(5, count);
    }

    // ─── Priority ordering ────────────────────────────────────────────────────

    [Fact]
    public void HighPriority_ExecutedBeforeLow()
    {
        var order = new List<string>();
        _dispatcher.AddEvent(() => order.Add("low"),  EventPriority.Low);
        _dispatcher.AddEvent(() => order.Add("high"), EventPriority.High);
        _dispatcher.AddEvent(() => order.Add("norm"), EventPriority.Normal);

        _dispatcher.Poll();

        Assert.Equal(["high", "norm", "low"], order);
    }

    // ─── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public void CancelledEvent_IsNotExecuted()
    {
        bool called = false;
        var ev = _dispatcher.AddEvent(() => called = true);
        ev.Cancel();
        _dispatcher.Poll();
        Assert.False(called);
    }

    // ─── Deferred events ──────────────────────────────────────────────────────

    [Fact]
    public void DeferEvent_RunsAfterRegularEvents()
    {
        var order = new List<string>();
        _dispatcher.AddEvent(() => order.Add("regular"));
        _dispatcher.DeferEvent(() => order.Add("deferred"));

        _dispatcher.Poll();

        Assert.Equal(["regular", "deferred"], order);
    }

    [Fact]
    public void DeferredEventFromInsidePoll_RunsAtEndOfSamePoll()
    {
        var order = new List<string>();
        _dispatcher.AddEvent(() =>
        {
            order.Add("first");
            _dispatcher.DeferEvent(() => order.Add("deferred-from-callback"));
        });

        _dispatcher.Poll();

        Assert.Contains("deferred-from-callback", order);
        Assert.True(order.IndexOf("first") < order.IndexOf("deferred-from-callback"));
    }

    // ─── Shutdown ─────────────────────────────────────────────────────────────

    [Fact]
    public void AfterShutdown_NewEventsAreDropped()
    {
        _dispatcher.Shutdown();
        bool called = false;
        _dispatcher.AddEvent(() => called = true);
        _dispatcher.Poll();
        Assert.False(called);
    }

    // ─── PendingCount ─────────────────────────────────────────────────────────

    [Fact]
    public void PendingCount_ReflectsQueuedEvents()
    {
        _dispatcher.AddEvent(() => { });
        _dispatcher.AddEvent(() => { });
        Assert.Equal(2, _dispatcher.PendingCount);
        _dispatcher.Poll();
        Assert.Equal(0, _dispatcher.PendingCount);
    }

    // ─── Event name ───────────────────────────────────────────────────────────

    [Fact]
    public void EventName_IsPreserved()
    {
        var ev = _dispatcher.AddEvent(() => { }, name: "my-event");
        Assert.Equal("my-event", ev.Name);
    }
}
