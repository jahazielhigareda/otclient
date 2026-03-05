using OTClient.Framework.Core;
using Xunit;

namespace OTClient.Tests;

/// <summary>
/// Tests for <see cref="Clock"/> — the frame-stable monotonic time source.
/// No Raylib window or GPU context is required.
/// </summary>
public sealed class ClockTests
{
    // ─── Cached accessors ─────────────────────────────────────────────────────

    [Fact]
    public void Update_PopulatesMillis()
    {
        var clock = new Clock();
        clock.Update();
        Assert.True(clock.Millis >= 0);
    }

    [Fact]
    public void Update_PopulatesMicros()
    {
        var clock = new Clock();
        clock.Update();
        Assert.True(clock.Micros >= 0);
    }

    [Fact]
    public void Update_PopulatesSeconds()
    {
        var clock = new Clock();
        clock.Update();
        Assert.True(clock.Seconds >= 0.0);
    }

    [Fact]
    public void Millis_IsConsistentWithinSameUpdate()
    {
        // Read Millis twice after a single Update — must return the same cached value.
        var clock = new Clock();
        clock.Update();
        long first  = clock.Millis;
        long second = clock.Millis;
        Assert.Equal(first, second);
    }

    [Fact]
    public void Micros_IsConsistentWithinSameUpdate()
    {
        var clock = new Clock();
        clock.Update();
        long first  = clock.Micros;
        long second = clock.Micros;
        Assert.Equal(first, second);
    }

    [Fact]
    public void Seconds_IsConsistentWithinSameUpdate()
    {
        var clock = new Clock();
        clock.Update();
        double first  = clock.Seconds;
        double second = clock.Seconds;
        Assert.Equal(first, second);
    }

    // ─── Time progression ─────────────────────────────────────────────────────

    [Fact]
    public void Millis_AdvancesAfterSleep()
    {
        var clock = new Clock();
        clock.Update();
        long before = clock.Millis;

        Thread.Sleep(30);
        clock.Update();
        long after = clock.Millis;

        Assert.True(after >= before, $"Expected time to advance: before={before} after={after}");
    }

    [Fact]
    public void Micros_AdvancesAfterSleep()
    {
        var clock = new Clock();
        clock.Update();
        long before = clock.Micros;

        Thread.Sleep(30);
        clock.Update();
        long after = clock.Micros;

        Assert.True(after >= before, $"Expected micros to advance: before={before} after={after}");
    }

    [Fact]
    public void Seconds_AdvancesAfterSleep()
    {
        var clock = new Clock();
        clock.Update();
        double before = clock.Seconds;

        Thread.Sleep(30);
        clock.Update();
        double after = clock.Seconds;

        Assert.True(after >= before, $"Expected seconds to advance: before={before} after={after}");
    }

    // ─── Units consistency ────────────────────────────────────────────────────

    [Fact]
    public void Millis_And_Micros_AreConsistent()
    {
        var clock = new Clock();
        clock.Update();
        long millis = clock.Millis;
        long micros = clock.Micros;

        // micros should be approximately millis * 1000 (within 10 ms of slack)
        Assert.True(Math.Abs(micros - millis * 1000L) < 10_000L,
            $"Inconsistent units: millis={millis} micros={micros}");
    }

    [Fact]
    public void Seconds_And_Millis_AreConsistent()
    {
        var clock = new Clock();
        clock.Update();
        long millis = clock.Millis;
        double seconds = clock.Seconds;

        // seconds should be approximately millis / 1000
        Assert.True(Math.Abs(seconds - millis / 1000.0) < 0.01,
            $"Inconsistent units: millis={millis} seconds={seconds}");
    }

    // ─── Static real-time accessors ───────────────────────────────────────────

    [Fact]
    public void RealMillis_IsNonNegative()
    {
        Assert.True(Clock.RealMillis >= 0);
    }

    [Fact]
    public void RealMicros_IsNonNegative()
    {
        Assert.True(Clock.RealMicros >= 0);
    }

    [Fact]
    public void RealMillis_AdvancesOverTime()
    {
        long before = Clock.RealMillis;
        Thread.Sleep(20);
        long after = Clock.RealMillis;
        Assert.True(after >= before, $"Expected RealMillis to advance: before={before} after={after}");
    }

    [Fact]
    public void RealMicros_AdvancesOverTime()
    {
        long before = Clock.RealMicros;
        Thread.Sleep(20);
        long after = Clock.RealMicros;
        Assert.True(after >= before, $"Expected RealMicros to advance: before={before} after={after}");
    }

    [Fact]
    public void RealMillis_IsIndependentOfUpdate()
    {
        // RealMillis should advance even if Update() is never called
        long before = Clock.RealMillis;
        Thread.Sleep(20);
        long after = Clock.RealMillis;
        Assert.True(after > before, "RealMillis must not be frozen by lack of Update()");
    }

    // ─── Cached vs real-time ──────────────────────────────────────────────────

    [Fact]
    public void CachedMillis_DoesNotAdvanceWithoutUpdate()
    {
        var clock = new Clock();
        clock.Update();
        long snapshot = clock.Millis;

        Thread.Sleep(40);
        // No Update() call — cached value must not change
        Assert.Equal(snapshot, clock.Millis);
    }
}
