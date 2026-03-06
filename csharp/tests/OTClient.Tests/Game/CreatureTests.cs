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

    // ─── AnimPhase ────────────────────────────────────────────────────────────

    [Fact]
    public void AnimPhase_Default_IsZero()
    {
        var c = new Creature();
        Assert.Equal(0, c.AnimPhase);
    }

    [Fact]
    public void Walk_AdvancesAnimPhaseToOne()
    {
        var c = new Creature { Position = new Position(5, 5, 7) };
        c.Walk(new Position(5, 6, 7), 500);
        Assert.Equal(1, c.AnimPhase);
    }

    [Fact]
    public void Walk_SecondContinuousStep_AdvancesAnimPhaseToTwo()
    {
        // In continuous walking the new Walk() is called before Update() finishes
        // the previous step, so AnimPhase accumulates without a reset.
        var c = new Creature { Position = new Position(5, 5, 7) };
        c.Walk(new Position(5, 6, 7), 500);   // AnimPhase: 0→1
        // Don't call Update() to completion — simulate continuous walking
        c.Walk(new Position(5, 7, 7), 500);   // AnimPhase: 1→2
        Assert.Equal(2, c.AnimPhase);
    }

    [Fact]
    public void Walk_CyclesAnimPhaseAfterMax()
    {
        // In continuous walking (no Update-to-completion in between), phase cycles.
        var c = new Creature { Position = new Position(5, 5, 7), AnimPhaseCount = 3 };
        // Phase: 0→1→2→3→1 (wraps back to 1 after reaching AnimPhaseCount)
        c.Walk(new Position(5, 6, 7), 500);   // AnimPhase: 0→1
        c.Walk(new Position(5, 7, 7), 500);   // AnimPhase: 1→2
        c.Walk(new Position(5, 8, 7), 500);   // AnimPhase: 2→3
        c.Walk(new Position(5, 9, 7), 500);   // AnimPhase: 3→1 (wraps)
        Assert.Equal(1, c.AnimPhase);
    }

    [Fact]
    public void Update_ResetsAnimPhaseToZeroOnWalkCompletion()
    {
        var c = new Creature { Position = new Position(5, 5, 7) };
        c.Walk(new Position(5, 6, 7), 500);
        c.Update(600);
        Assert.Equal(0, c.AnimPhase);
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

    // ─── Player skill fields (T05) ────────────────────────────────────────────

    [Fact]
    public void Player_FreeCapacity_DefaultZero()
    {
        Assert.Equal(0, new Player().FreeCapacity);
    }

    [Fact]
    public void Player_SetSkill_GetSkill_RoundTrips()
    {
        var p = new Player();
        p.SetSkill(SkillType.Sword, 42, 75);
        Assert.Equal(42, p.GetSkillLevel(SkillType.Sword));
        Assert.Equal(75, p.GetSkillPercent(SkillType.Sword));
    }

    [Fact]
    public void Player_DefaultSkill_IsZero()
    {
        var p = new Player();
        Assert.Equal(0, p.GetSkillLevel(SkillType.Fist));
        Assert.Equal(0, p.GetSkillPercent(SkillType.Fist));
    }

    [Fact]
    public void LocalPlayer_DefaultFightMode_IsBalanced()
    {
        Assert.Equal(FightMode.Balanced, new LocalPlayer().FightMode);
    }

    [Fact]
    public void LocalPlayer_DefaultChaseMode_IsDontChase()
    {
        Assert.Equal(ChaseMode.DontChase, new LocalPlayer().ChaseMode);
    }

    [Fact]
    public void LocalPlayer_DefaultSafeMode_IsTrue()
    {
        Assert.True(new LocalPlayer().SafeMode);
    }
}
