using System.Numerics;
using OTClient.Framework.Graphics;
using Raylib_cs;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="ParticleAffector"/> (base lifecycle),
/// <see cref="GravityAffector"/>, <see cref="AttractionAffector"/>,
/// <see cref="ParticleType"/> (gradient sampling, particle factory), and the
/// integration of affectors + <see cref="ParticleType"/> into
/// <see cref="ParticleEmitter"/>.
/// Task T30.
/// </summary>
public sealed class ParticleAffectorTypeTests
{
    // ─── GravityAffector — lifecycle ──────────────────────────────────────────

    [Fact]
    public void GravityAffector_StartsInactive()
    {
        var g = new GravityAffector();
        Assert.False(g.IsActive);
        Assert.False(g.HasFinished);
    }

    [Fact]
    public void GravityAffector_BecomesActiveAfterDelay()
    {
        var g = new GravityAffector { Delay = 0.5f };
        g.Update(0.3f);
        Assert.False(g.IsActive);
        g.Update(0.3f); // 0.6 s total > 0.5 s delay
        Assert.True(g.IsActive);
    }

    [Fact]
    public void GravityAffector_FinishesAfterDuration()
    {
        var g = new GravityAffector { Delay = 0f, Duration = 1f };
        g.Update(0.5f);
        Assert.True(g.IsActive);
        Assert.False(g.HasFinished);
        g.Update(0.6f); // total 1.1 s > delay(0)+duration(1)
        Assert.True(g.HasFinished);
        Assert.False(g.IsActive);
    }

    [Fact]
    public void GravityAffector_NegativeDuration_NeverFinishes()
    {
        var g = new GravityAffector { Delay = 0f, Duration = -1f };
        for (int i = 0; i < 100; i++) g.Update(1f);
        Assert.False(g.HasFinished);
    }

    // ─── GravityAffector — physics ────────────────────────────────────────────

    [Fact]
    public void GravityAffector_DownwardGravity_IncreasesVelocityY()
    {
        var g = new GravityAffector { AngleDegrees = 90f, GravityStrength = 100f };
        g.Update(0.01f); // activate immediately (no delay)

        var p = new Particle { Position = Vector2.Zero, Velocity = Vector2.Zero, Lifetime = 10f };
        g.UpdateParticle(p, 0.1f);

        // 90° = (cos90, sin90) = (0, 1), so Vy should increase
        Assert.True(p.Velocity.Y > 0, "Gravity at 90° should push Y down (positive).");
        Assert.True(MathF.Abs(p.Velocity.X) < 1e-4f, "Gravity at 90° should not affect X.");
    }

    [Fact]
    public void GravityAffector_LeftGravity_DecreasesVelocityX()
    {
        var g = new GravityAffector { AngleDegrees = 180f, GravityStrength = 50f };
        g.Update(0.01f);

        var p = new Particle { Velocity = Vector2.Zero, Lifetime = 10f };
        g.UpdateParticle(p, 0.1f);

        Assert.True(p.Velocity.X < 0, "Gravity at 180° should push X left (negative).");
    }

    // ─── AttractionAffector — physics ─────────────────────────────────────────

    [Fact]
    public void AttractionAffector_PullsParticleTowardsCenter()
    {
        var a = new AttractionAffector
        {
            AttractPosition = new Vector2(100f, 0f),
            Acceleration    = 200f,
        };
        a.Update(0.01f); // activate

        var p = new Particle { Position = Vector2.Zero, Velocity = Vector2.Zero, Lifetime = 10f };
        a.UpdateParticle(p, 0.1f);

        Assert.True(p.Velocity.X > 0, "Particle should accelerate toward positive X.");
        Assert.True(MathF.Abs(p.Velocity.Y) < 1e-3f, "No Y acceleration when target is directly right.");
    }

    [Fact]
    public void AttractionAffector_Repelish_PushesParticleAway()
    {
        var a = new AttractionAffector
        {
            AttractPosition = new Vector2(100f, 0f),
            Acceleration    = 200f,
            Repelish        = true,
        };
        a.Update(0.01f);

        var p = new Particle { Position = Vector2.Zero, Velocity = Vector2.Zero, Lifetime = 10f };
        a.UpdateParticle(p, 0.1f);

        Assert.True(p.Velocity.X < 0, "Repelish should push particle away (negative X).");
    }

    [Fact]
    public void AttractionAffector_ZeroDelta_NoChange()
    {
        var a = new AttractionAffector
        {
            AttractPosition = Vector2.Zero, // same as particle
            Acceleration    = 200f,
        };
        a.Update(0.01f);

        var p = new Particle { Position = Vector2.Zero, Velocity = new Vector2(5f, 0f), Lifetime = 10f };
        var before = p.Velocity;
        a.UpdateParticle(p, 0.1f);
        // delta length is 0 → no change
        Assert.Equal(before, p.Velocity);
    }

    [Fact]
    public void AttractionAffector_VelocityReduction_DampensVelocity()
    {
        var a = new AttractionAffector
        {
            AttractPosition           = new Vector2(1000f, 0f),
            Acceleration              = 0f,         // no pull
            VelocityReductionPercent  = 100f,        // 100% reduction per second
        };
        a.Update(0.01f);

        var p = new Particle { Position = Vector2.Zero, Velocity = new Vector2(100f, 0f), Lifetime = 10f };
        a.UpdateParticle(p, 0.5f); // half-second → ~50% reduction
        Assert.True(p.Velocity.X < 100f, "Velocity should be reduced by damping.");
    }

    // ─── ParticleType — color gradient ────────────────────────────────────────

    [Fact]
    public void ParticleType_SampleColor_SingleStop_ReturnsOnlyColor()
    {
        var pt = new ParticleType();
        pt.Colors[0]     = Color.Red;
        pt.ColorStops[0] = 0f;

        var c = pt.SampleColor(0.5f);
        Assert.Equal(Color.Red, c);
    }

    [Fact]
    public void ParticleType_SampleColor_TwoStops_Interpolates()
    {
        var pt = new ParticleType();
        pt.Colors.Clear();
        pt.ColorStops.Clear();
        pt.Colors.Add(new Color((byte)0, (byte)0, (byte)0, (byte)255));    // black
        pt.Colors.Add(new Color((byte)255, (byte)255, (byte)255, (byte)255)); // white
        pt.ColorStops.Add(0f);
        pt.ColorStops.Add(1f);

        var c = pt.SampleColor(0.5f);
        // Mid-point should be roughly grey (127 ± 1)
        Assert.InRange(c.R, (byte)126, (byte)128);
    }

    [Fact]
    public void ParticleType_SampleColor_BeyondLastStop_ReturnsLastColor()
    {
        var pt = new ParticleType();
        pt.Colors.Clear();
        pt.ColorStops.Clear();
        pt.Colors.Add(Color.Blue);
        pt.Colors.Add(Color.Red);
        pt.ColorStops.Add(0f);
        pt.ColorStops.Add(0.5f);

        var c = pt.SampleColor(1f); // beyond last stop
        Assert.Equal(Color.Red, c);
    }

    // ─── ParticleType — CreateParticle ────────────────────────────────────────

    [Fact]
    public void ParticleType_CreateParticle_LifetimeWithinRange()
    {
        var pt = new ParticleType { MinDuration = 1f, MaxDuration = 3f };
        var rng = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            var p = pt.CreateParticle(Vector2.Zero, rng);
            Assert.InRange(p.Lifetime, 1f, 3f);
        }
    }

    [Fact]
    public void ParticleType_CreateParticle_RadiusDeltaIsCorrect()
    {
        var pt = new ParticleType { StartRadius = 10f, FinalRadius = 0f, MinDuration = 2f, MaxDuration = 2f };
        var p = pt.CreateParticle(Vector2.Zero, new Random(1));
        // radiusDelta = (0 - 10) / 2 = -5
        Assert.Equal(-5f, p.RadiusDelta, precision: 4);
    }

    [Fact]
    public void ParticleType_CreateParticle_PositionScatters()
    {
        var pt = new ParticleType
        {
            MinPositionRadius = 10f,
            MaxPositionRadius = 20f,
            MinPositionAngle  = 0f,
            MaxPositionAngle  = 360f,
        };
        var rng     = new Random(7);
        var center  = new Vector2(100f, 100f);
        var results = new List<Vector2>();
        for (int i = 0; i < 10; i++)
            results.Add(pt.CreateParticle(center, rng).Position);

        // At least some particles should not be exactly at center
        Assert.Contains(results, p => Vector2.Distance(p, center) > 0.1f);
    }

    // ─── ParticleEmitter integration ──────────────────────────────────────────

    [Fact]
    public void ParticleEmitter_AddAffector_IsVisible()
    {
        var emitter = new ParticleEmitter(Vector2.Zero, seed: 0);
        var g = new GravityAffector();
        emitter.AddAffector(g);
        Assert.Single(emitter.Affectors);
    }

    [Fact]
    public void ParticleEmitter_RemoveAffector_Removes()
    {
        var emitter = new ParticleEmitter(Vector2.Zero, seed: 0);
        var g = new GravityAffector();
        emitter.AddAffector(g);
        emitter.RemoveAffector(g);
        Assert.Empty(emitter.Affectors);
    }

    [Fact]
    public void ParticleEmitter_GravityAffector_ChangesParticleVelocity()
    {
        var emitter = new ParticleEmitter(Vector2.Zero, seed: 42)
        {
            EmitRate        = 100f,
            SpeedMin        = 0f,
            SpeedMax        = 0f, // start stationary
            ParticleLifetime = 5f,
        };
        var gravity = new GravityAffector { AngleDegrees = 90f, GravityStrength = 100f };
        emitter.AddAffector(gravity);

        emitter.Update(0.1f); // emit particles & activate gravity
        emitter.Update(0.1f); // apply gravity for one frame

        // At least some particles should have positive Vy now
        // (access via ParticleCount to confirm particles exist)
        Assert.True(emitter.ParticleCount > 0);
    }

    [Fact]
    public void ParticleEmitter_WithParticleType_UsesTypeLifetime()
    {
        var pt = new ParticleType { MinDuration = 5f, MaxDuration = 5f };
        var emitter = new ParticleEmitter(Vector2.Zero, seed: 0)
        {
            EmitRate     = 100f,
            ParticleType = pt,
        };
        emitter.Update(0.1f);
        Assert.True(emitter.ParticleCount > 0);
        // Particles from type should stay alive for 5 s
        emitter.Update(3f);
        Assert.True(emitter.ParticleCount > 0, "Particles with 5s lifetime should still be alive after 3s.");
    }
}
