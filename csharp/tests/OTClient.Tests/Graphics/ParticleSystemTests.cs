using System.Numerics;
using OTClient.Framework.Graphics;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="Particle"/>, <see cref="ParticleEmitter"/>,
/// <see cref="ParticleEffect"/>, and <see cref="ParticleManager"/>.
/// No Raylib GPU context is required — only physics and lifecycle logic is tested.
/// </summary>
public sealed class ParticleSystemTests
{
    // ─── Particle physics ─────────────────────────────────────────────────────

    [Fact]
    public void Particle_StartsAlive()
    {
        var p = new Particle { Age = 0f, Lifetime = 1f };
        Assert.True(p.IsAlive);
    }

    [Fact]
    public void Particle_IsDeadAfterLifetime()
    {
        var p = new Particle { Age = 1.5f, Lifetime = 1f };
        Assert.False(p.IsAlive);
    }

    [Fact]
    public void Particle_NormalisedAge_IsZeroWhenNew()
    {
        var p = new Particle { Age = 0f, Lifetime = 2f };
        Assert.Equal(0f, p.NormalisedAge, precision: 6);
    }

    [Fact]
    public void Particle_NormalisedAge_IsOneWhenExpired()
    {
        var p = new Particle { Age = 2f, Lifetime = 2f };
        Assert.Equal(1f, p.NormalisedAge, precision: 6);
    }

    [Fact]
    public void Particle_Update_AdvancesAge()
    {
        var p = new Particle { Age = 0f, Lifetime = 5f };
        p.Update(0.1f);
        Assert.Equal(0.1f, p.Age, precision: 6);
    }

    [Fact]
    public void Particle_Update_AppliesVelocity()
    {
        var p = new Particle
        {
            Position = Vector2.Zero,
            Velocity = new Vector2(100f, 0f),
            Lifetime = 5f,
        };
        p.Update(0.5f);
        Assert.Equal(50f, p.Position.X, precision: 4);
    }

    [Fact]
    public void Particle_Update_AppliesAcceleration()
    {
        var p = new Particle
        {
            Position     = Vector2.Zero,
            Velocity     = Vector2.Zero,
            Acceleration = new Vector2(0f, 10f), // gravity-like
            Lifetime     = 5f,
        };
        p.Update(1f);
        // After 1s: v = 10, pos.Y = 10
        Assert.True(p.Velocity.Y > 0f);
        Assert.True(p.Position.Y > 0f);
    }

    // ─── ParticleEmitter ──────────────────────────────────────────────────────

    [Fact]
    public void Emitter_StartsWithNoParticles()
    {
        var e = new ParticleEmitter(Vector2.Zero);
        Assert.Equal(0, e.ParticleCount);
    }

    [Fact]
    public void Emitter_EmitsParticles_WhenUpdated()
    {
        var e = new ParticleEmitter(Vector2.Zero, seed: 42)
        {
            EmitRate        = 100f,
            ParticleLifetime = 10f,
            IsEmitting      = true,
        };
        e.Update(0.5f);
        Assert.True(e.ParticleCount > 0);
    }

    [Fact]
    public void Emitter_StopsEmitting_WhenIsEmittingFalse()
    {
        var e = new ParticleEmitter(Vector2.Zero, seed: 42)
        {
            EmitRate = 100f,
            IsEmitting = false,
        };
        e.Update(1f);
        Assert.Equal(0, e.ParticleCount);
    }

    [Fact]
    public void Emitter_ParticlesDie_AfterLifetime()
    {
        var e = new ParticleEmitter(Vector2.Zero, seed: 42)
        {
            EmitRate         = 200f,
            ParticleLifetime = 0.01f,
        };
        e.Update(0.1f);   // emit many short-lived particles
        e.Update(1f);     // let them all die
        Assert.Equal(0, e.ParticleCount);
    }

    // ─── ParticleEffect ───────────────────────────────────────────────────────

    [Fact]
    public void Effect_Name_IsPreserved()
    {
        var effect = new ParticleEffect("fire");
        Assert.Equal("fire", effect.Name);
    }

    [Fact]
    public void Effect_AddEmitter_IncreasesEmitterCount()
    {
        var effect = new ParticleEffect("smoke");
        effect.AddEmitter(new ParticleEmitter(Vector2.Zero));
        Assert.Single(effect.Emitters);
    }

    [Fact]
    public void Effect_TotalParticleCount_SumsAcrossEmitters()
    {
        var e1 = new ParticleEmitter(Vector2.Zero, seed: 1) { EmitRate = 100f, ParticleLifetime = 5f };
        var e2 = new ParticleEmitter(Vector2.Zero, seed: 2) { EmitRate = 100f, ParticleLifetime = 5f };

        var effect = new ParticleEffect("sparks");
        effect.AddEmitter(e1);
        effect.AddEmitter(e2);
        effect.Update(0.5f);

        Assert.Equal(e1.ParticleCount + e2.ParticleCount, effect.TotalParticleCount);
    }

    // ─── ParticleManager ──────────────────────────────────────────────────────

    [Fact]
    public void Manager_EffectCount_StartsAtZero()
    {
        var manager = new ParticleManager();
        Assert.Equal(0, manager.EffectCount);
    }

    [Fact]
    public void Manager_Add_IncrementsEffectCount()
    {
        var manager = new ParticleManager();
        manager.Add(new ParticleEffect("test"));
        Assert.Equal(1, manager.EffectCount);
    }

    [Fact]
    public void Manager_Remove_DecrementsEffectCount()
    {
        var manager = new ParticleManager();
        var effect  = new ParticleEffect("test");
        manager.Add(effect);
        manager.Remove(effect);
        Assert.Equal(0, manager.EffectCount);
    }

    [Fact]
    public void Manager_Clear_RemovesAllEffects()
    {
        var manager = new ParticleManager();
        manager.Add(new ParticleEffect("a"));
        manager.Add(new ParticleEffect("b"));
        manager.Clear();
        Assert.Equal(0, manager.EffectCount);
    }

    [Fact]
    public void Manager_Update_UpdatesAllEffects()
    {
        var emitter = new ParticleEmitter(Vector2.Zero, seed: 0)
        {
            EmitRate = 100f, ParticleLifetime = 5f,
        };
        var effect  = new ParticleEffect("test");
        effect.AddEmitter(emitter);

        var manager = new ParticleManager();
        manager.Add(effect);
        manager.Update(0.5f);

        Assert.True(emitter.ParticleCount > 0);
    }
}
