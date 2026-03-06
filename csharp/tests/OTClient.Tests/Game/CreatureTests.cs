using System.Numerics;
using OTClient.Framework.Game;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for <see cref="Creature"/>, <see cref="Player"/>, <see cref="LocalPlayer"/>.
/// Tasks 8.8–8.9.
/// </summary>
public sealed class CreatureTests
{
    // ─── Creature defaults ────────────────────────────────────────────────────

    [Fact]
    public void Creature_Category_IsCreature()
    {
        Assert.Equal(ThingCategory.Creature, new Creature().Category);
    }

    [Fact]
    public void Creature_IsAlive_WhenHealthGtZero()
    {
        var c = new Creature { Health = 50 };
        Assert.True(c.IsAlive);
    }

    [Fact]
    public void Creature_IsNotAlive_WhenHealthZero()
    {
        var c = new Creature { Health = 0 };
        Assert.False(c.IsAlive);
    }

    [Fact]
    public void HealthPercent_Correct()
    {
        var c = new Creature { MaxHealth = 200, Health = 100 };
        Assert.Equal(0.5f, c.HealthPercent, precision: 5);
    }

    [Fact]
    public void HealthPercent_ZeroWhenMaxIsZero()
    {
        var c = new Creature { MaxHealth = 0, Health = 0 };
        Assert.Equal(0f, c.HealthPercent);
    }

    // ─── Walk ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Walk_SetsIsWalking()
    {
        var c = new Creature { Position = new Position(5, 5, 7) };
        c.Walk(new Position(5, 6, 7), 500);
        Assert.True(c.IsWalking);
    }

    [Fact]
    public void Walk_SetsDirection()
    {
        var c = new Creature { Position = new Position(5, 5, 7) };
        c.Walk(new Position(5, 6, 7), 500);   // south
        Assert.Equal(Direction.South, c.Direction);
    }

    [Fact]
    public void Update_CompletesWalkAfterDuration()
    {
        var c = new Creature { Position = new Position(5, 5, 7) };
        c.Walk(new Position(6, 5, 7), 500);
        c.Update(600);   // more than 500 ms
        Assert.False(c.IsWalking);
        Assert.Equal(1f, c.WalkProgress);
    }

    [Fact]
    public void AddEffect_AppearsInEffects()
    {
        var c = new Creature();
        c.AddEffect(new Effect { TypeId = 10 });
        Assert.Single(c.Effects);
    }

    [Fact]
    public void PruneEffects_RemovesFinished()
    {
        var c = new Creature();
        var e = new Effect();
        e.Update(100_000);  // finish it
        c.AddEffect(e);
        c.PruneEffects();
        Assert.Empty(c.Effects);
    }

    // ─── Player ───────────────────────────────────────────────────────────────

    [Fact]
    public void Player_DefaultLevel_IsOne()
    {
        Assert.Equal(1, new Player().Level);
    }

    // ─── LocalPlayer ─────────────────────────────────────────────────────────

    [Fact]
    public void LocalPlayer_DefaultIsNotKnown()
    {
        Assert.False(new LocalPlayer().IsKnown);
    }
}
