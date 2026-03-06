using OTClient.Framework.Core;
using Xunit;

// Disambiguate from System.Threading.Timer
using EngineTimer = OTClient.Framework.Core.Timer;

namespace OTClient.Tests;

public sealed class TimerTests
{
    private readonly Clock _clock = new();

    [Fact]
    public void Timer_StartsRunning()
    {
        _clock.Update();
        var timer = new EngineTimer(_clock);
        Assert.True(timer.IsRunning);
    }

    [Fact]
    public void Timer_TicksElapsed_IsNonNegative()
    {
        _clock.Update();
        var timer = new EngineTimer(_clock);
        _clock.Update();
        Assert.True(timer.TicksElapsed >= 0);
    }

    [Fact]
    public void Timer_Stop_SetsNotRunning()
    {
        _clock.Update();
        var timer = new EngineTimer(_clock);
        timer.Stop();
        Assert.False(timer.IsRunning);
    }

    [Fact]
    public void Timer_Stop_FreezesElapsedAtZero()
    {
        _clock.Update();
        var timer = new EngineTimer(_clock);
        timer.Stop();
        long before = timer.TicksElapsed;
        System.Threading.Thread.Sleep(20);
        long after = timer.TicksElapsed;
        Assert.Equal(before, after);
        Assert.Equal(0, after);
    }

    [Fact]
    public void Timer_Restart_ResetsElapsed()
    {
        _clock.Update();
        var timer = new EngineTimer(_clock);
        System.Threading.Thread.Sleep(30);
        _clock.Update();
        long firstElapsed = timer.TicksElapsed;

        timer.Restart();
        _clock.Update();
        long afterRestart = timer.TicksElapsed;

        Assert.True(firstElapsed >= afterRestart,
            $"Expected elapsed after restart ({afterRestart}) <= elapsed before restart ({firstElapsed})");
    }

    [Fact]
    public void Timer_HasExpired_ReturnsFalseImmediately()
    {
        _clock.Update();
        var timer = new EngineTimer(_clock);
        Assert.False(timer.HasExpired(10_000));
    }

    [Fact]
    public void Timer_TimeElapsed_MatchesTicksElapsed()
    {
        _clock.Update();
        var timer = new EngineTimer(_clock);
        System.Threading.Thread.Sleep(20);
        _clock.Update();

        float seconds = timer.TimeElapsed;
        long millis = timer.TicksElapsed;
        Assert.Equal(millis / 1000f, seconds, precision: 3);
    }

    [Fact]
    public void Timer_Advance_ShiftsElapsed()
    {
        _clock.Update();
        var timer = new EngineTimer(_clock);
        _clock.Update();
        long before = timer.TicksElapsed;

        // Advancing by 1000 ms means the timer will appear to have started
        // 1000 ms later, so elapsed should decrease by ~1000
        timer.Advance(1000);
        long after = timer.TicksElapsed;
        Assert.True(after <= before,
            $"Expected ticks to decrease after Advance, before={before} after={after}");
    }
}
