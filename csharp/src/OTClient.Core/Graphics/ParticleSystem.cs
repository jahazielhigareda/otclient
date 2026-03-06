using System.Numerics;
using Raylib_cs;

namespace OTClient.Framework.Graphics;

// ─── Particle ─────────────────────────────────────────────────────────────────

/// <summary>
/// A single live particle instance.  Updated by <see cref="ParticleEmitter"/>
/// each frame and rendered as a coloured circle or textured quad.
/// Maps to <c>src/framework/graphics/particle.{h,cpp}</c>.
/// </summary>
public sealed class Particle
{
    /// <summary>Current position in world/screen coordinates.</summary>
    public Vector2 Position;

    /// <summary>Velocity in pixels per second.</summary>
    public Vector2 Velocity;

    /// <summary>Acceleration applied every second (e.g. gravity).</summary>
    public Vector2 Acceleration;

    /// <summary>Current radius in pixels.</summary>
    public float Radius;

    /// <summary>Rate of change of <see cref="Radius"/> per second.</summary>
    public float RadiusDelta;

    /// <summary>Current tint (including alpha for fade).</summary>
    public Color Color;

    /// <summary>Total lifetime in seconds.</summary>
    public float Lifetime;

    /// <summary>Seconds elapsed since this particle was emitted.</summary>
    public float Age;

    /// <summary><c>true</c> while <see cref="Age"/> &lt; <see cref="Lifetime"/>.</summary>
    public bool IsAlive => Age < Lifetime;

    /// <summary>Normalised age: 0 = just emitted, 1 = expired.</summary>
    public float NormalisedAge => Lifetime > 0 ? Math.Clamp(Age / Lifetime, 0f, 1f) : 1f;

    /// <summary>Advances physics and age by <paramref name="delta"/> seconds.</summary>
    internal void Update(float delta)
    {
        Velocity  += Acceleration * delta;
        Position  += Velocity * delta;
        Radius    += RadiusDelta * delta;
        Age       += delta;
    }
}

// ─── ParticleEmitter ──────────────────────────────────────────────────────────

/// <summary>
/// Emits and manages the lifetime of a group of <see cref="Particle"/> objects.
/// Maps to <c>src/framework/graphics/particleemitter.{h,cpp}</c>.
/// </summary>
public sealed class ParticleEmitter
{
    private readonly List<Particle>          _particles  = [];
    private readonly List<ParticleAffector>  _affectors  = [];
    private float _emitAccumulator;
    private readonly Random _rng;

    // ─── Emitter configuration ────────────────────────────────────────────────

    /// <summary>World position at which particles are emitted.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Particles per second.</summary>
    public float EmitRate { get; set; } = 20f;

    /// <summary>Initial speed range in pixels per second.</summary>
    public float SpeedMin { get; set; } = 30f;

    /// <summary>Initial speed max in pixels per second.</summary>
    public float SpeedMax { get; set; } = 80f;

    /// <summary>Emission angle range in degrees (0 = rightward).</summary>
    public float AngleMin { get; set; } = 0f;

    /// <summary>Emission angle range in degrees.</summary>
    public float AngleMax { get; set; } = 360f;

    /// <summary>Starting radius of each particle.</summary>
    public float ParticleRadius { get; set; } = 3f;

    /// <summary>Lifetime in seconds for each particle.</summary>
    public float ParticleLifetime { get; set; } = 1.5f;

    /// <summary>Start colour.</summary>
    public Color ColorStart { get; set; } = Color.Yellow;

    /// <summary>End colour (linearly interpolated over the particle's lifetime).</summary>
    public Color ColorEnd { get; set; } = new Color(255, 200, 0, 0); // fade out

    /// <summary>Gravity acceleration in pixels per second².</summary>
    public Vector2 Gravity { get; set; } = new(0f, 60f);

    /// <summary>When <c>false</c> the emitter stops emitting new particles.</summary>
    public bool IsEmitting { get; set; } = true;

    /// <summary>Live particle count.</summary>
    public int ParticleCount => _particles.Count;

    /// <summary>
    /// Optional particle type template.  When set, new particles are spawned
    /// using <see cref="ParticleType"/> instead of the inline emitter fields.
    /// </summary>
    public ParticleType? ParticleType { get; set; }

    /// <summary>Read-only view of registered affectors.</summary>
    public IReadOnlyList<ParticleAffector> Affectors => _affectors;

    public ParticleEmitter(Vector2 position, int? seed = null)
    {
        Position = position;
        _rng     = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    // ─── Affector management ──────────────────────────────────────────────────

    /// <summary>Registers an affector that will be applied to every live particle each frame.</summary>
    public void AddAffector(ParticleAffector affector)
    {
        ArgumentNullException.ThrowIfNull(affector);
        _affectors.Add(affector);
    }

    /// <summary>Removes a previously registered affector.</summary>
    public void RemoveAffector(ParticleAffector affector) => _affectors.Remove(affector);

    // ─── Update / Render ──────────────────────────────────────────────────────

    /// <summary>Advances all particles and emits new ones.</summary>
    public void Update(float deltaSeconds)
    {
        // Advance affectors
        foreach (var a in _affectors) a.Update(deltaSeconds);

        // Emit new particles
        if (IsEmitting)
        {
            _emitAccumulator += EmitRate * deltaSeconds;
            while (_emitAccumulator >= 1f)
            {
                _particles.Add(ParticleType is not null
                    ? ParticleType.CreateParticle(Position, _rng)
                    : CreateParticle());
                _emitAccumulator -= 1f;
            }
        }

        // Update particles, apply affectors, prune dead ones
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Update(deltaSeconds);
            if (!p.IsAlive)
            {
                _particles.RemoveAt(i);
                continue;
            }
            // Apply active affectors
            foreach (var a in _affectors)
                if (a.IsActive) a.UpdateParticle(p, deltaSeconds);
            if (ParticleType is null)
                LerpColor(p);
        }
    }

    /// <summary>Draws all live particles using Raylib circle calls.</summary>
    public void Draw()
    {
        foreach (var p in _particles)
        {
            if (p.Radius > 0)
                Raylib.DrawCircleV(p.Position, p.Radius, p.Color);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private Particle CreateParticle()
    {
        float angle  = (_rng.NextSingle() * (AngleMax - AngleMin) + AngleMin) * MathF.PI / 180f;
        float speed  = _rng.NextSingle() * (SpeedMax - SpeedMin) + SpeedMin;
        return new Particle
        {
            Position     = Position,
            Velocity     = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
            Acceleration = Gravity,
            Radius       = ParticleRadius,
            Color        = ColorStart,
            Lifetime     = ParticleLifetime,
            Age          = 0f,
        };
    }

    private void LerpColor(Particle p)
    {
        float t = p.NormalisedAge;
        p.Color = new Color(
            (byte)(ColorStart.R + (ColorEnd.R - ColorStart.R) * t),
            (byte)(ColorStart.G + (ColorEnd.G - ColorStart.G) * t),
            (byte)(ColorStart.B + (ColorEnd.B - ColorStart.B) * t),
            (byte)(ColorStart.A + (ColorEnd.A - ColorStart.A) * t));
    }
}

// ─── ParticleEffect ───────────────────────────────────────────────────────────

/// <summary>
/// A named collection of <see cref="ParticleEmitter"/> objects that together
/// produce a compound visual effect (e.g. fire = flame + smoke + sparks).
/// Maps to <c>src/framework/graphics/particleeffect.{h,cpp}</c>.
/// </summary>
public sealed class ParticleEffect
{
    private readonly List<ParticleEmitter> _emitters = [];

    public string Name { get; }

    public ParticleEffect(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void AddEmitter(ParticleEmitter emitter)
    {
        ArgumentNullException.ThrowIfNull(emitter);
        _emitters.Add(emitter);
    }

    public IReadOnlyList<ParticleEmitter> Emitters => _emitters;

    /// <summary>Updates all emitters.</summary>
    public void Update(float deltaSeconds)
    {
        foreach (var e in _emitters) e.Update(deltaSeconds);
    }

    /// <summary>Draws all emitters.</summary>
    public void Draw()
    {
        foreach (var e in _emitters) e.Draw();
    }

    /// <summary>Total live particles across all emitters.</summary>
    public int TotalParticleCount => _emitters.Sum(e => e.ParticleCount);
}

// ─── ParticleManager ─────────────────────────────────────────────────────────

/// <summary>
/// Global registry and update driver for all active <see cref="ParticleEffect"/>
/// instances in a scene.
/// Maps to <c>src/framework/graphics/particlemanager.{h,cpp}</c>.
/// </summary>
public sealed class ParticleManager
{
    private readonly List<ParticleEffect> _effects = [];

    public void Add(ParticleEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _effects.Add(effect);
    }

    public void Remove(ParticleEffect effect) => _effects.Remove(effect);

    public void Clear() => _effects.Clear();

    /// <summary>Updates all registered effects.</summary>
    public void Update(float deltaSeconds)
    {
        foreach (var e in _effects) e.Update(deltaSeconds);
    }

    /// <summary>Draws all registered effects.</summary>
    public void Draw()
    {
        foreach (var e in _effects) e.Draw();
    }

    /// <summary>Number of registered effects.</summary>
    public int EffectCount => _effects.Count;
}

// ─── ParticleAffector ─────────────────────────────────────────────────────────

/// <summary>
/// Base class for all particle affectors — objects that modify live particles
/// every frame after they are emitted.  Affectors can be delayed and have an
/// optional finite duration.
/// Maps to <c>src/framework/graphics/particleaffector.{h,cpp}</c>.
/// Task T30.
/// </summary>
public abstract class ParticleAffector
{
    private float _elapsed;

    /// <summary>Seconds before the affector becomes active.</summary>
    public float Delay    { get; set; } = 0f;

    /// <summary>
    /// Active duration in seconds. Negative means unlimited.
    /// </summary>
    public float Duration { get; set; } = -1f;

    /// <summary><c>true</c> once the affector is past its <see cref="Delay"/>.</summary>
    public bool IsActive    { get; private set; }

    /// <summary><c>true</c> when the affector has completed its <see cref="Duration"/>.</summary>
    public bool HasFinished { get; private set; }

    /// <summary>
    /// Advances the affector's internal clock.
    /// Call this once per frame before applying the affector to particles.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        if (HasFinished) return;

        _elapsed += deltaSeconds;

        if (!IsActive && _elapsed > Delay)
            IsActive = true;

        if (Duration >= 0 && _elapsed >= Delay + Duration)
        {
            HasFinished = true;
            IsActive    = false;
        }
    }

    /// <summary>
    /// Applies this affector's effect to a single live <paramref name="particle"/>.
    /// Only called when <see cref="IsActive"/> is <c>true</c>.
    /// </summary>
    public abstract void UpdateParticle(Particle particle, float deltaSeconds);
}

// ─── GravityAffector ─────────────────────────────────────────────────────────

/// <summary>
/// Adds a directional gravitational acceleration to each particle's velocity.
/// Maps to <c>GravityAffector</c> in <c>particleaffector.{h,cpp}</c>.
/// Task T30.
/// </summary>
public sealed class GravityAffector : ParticleAffector
{
    /// <summary>
    /// Direction of gravity in standard mathematical degrees (0° = right/east,
    /// 90° = down in screen coordinates, 270° = up in screen coordinates).
    /// Default 270° mirrors the C++ default which points upward in screen space
    /// (sin(270°) = -1 → negative Y).
    /// </summary>
    public float AngleDegrees { get; set; } = 270f;

    /// <summary>Gravitational acceleration in pixels per second².</summary>
    public float GravityStrength { get; set; } = 9.8f;

    /// <inheritdoc/>
    public override void UpdateParticle(Particle particle, float deltaSeconds)
    {
        float rad = AngleDegrees * MathF.PI / 180f;
        particle.Velocity += new Vector2(
            GravityStrength * deltaSeconds * MathF.Cos(rad),
            GravityStrength * deltaSeconds * MathF.Sin(rad));
    }
}

// ─── AttractionAffector ───────────────────────────────────────────────────────

/// <summary>
/// Accelerates particles toward (or away from) a fixed world position,
/// with optional velocity-reduction damping.
/// Maps to <c>AttractionAffector</c> in <c>particleaffector.{h,cpp}</c>.
/// Task T30.
/// </summary>
public sealed class AttractionAffector : ParticleAffector
{
    /// <summary>Attraction / repulsion centre in world coordinates.</summary>
    public Vector2 AttractPosition { get; set; }

    /// <summary>Acceleration magnitude in pixels per second².</summary>
    public float Acceleration { get; set; } = 32f;

    /// <summary>
    /// Velocity reduction percentage per second (0 = no damping).
    /// Mirrors <c>velocity-reduction-percent</c>.
    /// </summary>
    public float VelocityReductionPercent { get; set; } = 0f;

    /// <summary>When <c>true</c>, particles are pushed away instead of pulled in.</summary>
    public bool Repelish { get; set; } = false;

    /// <inheritdoc/>
    public override void UpdateParticle(Particle particle, float deltaSeconds)
    {
        var delta = AttractPosition - particle.Position;
        float len = delta.Length();
        if (len == 0f) return;

        var direction = Repelish ? -(delta / len) : (delta / len);
        particle.Velocity += direction * Acceleration * deltaSeconds;

        if (VelocityReductionPercent > 0f)
            particle.Velocity -= particle.Velocity * (VelocityReductionPercent / 100f) * deltaSeconds;
    }
}

// ─── ParticleType ─────────────────────────────────────────────────────────────

/// <summary>
/// Named template that fully describes how to spawn and visually represent a
/// class of particles.  <see cref="ParticleEmitter"/> uses an optional
/// <see cref="ParticleType"/> to create particles instead of its own inline
/// scalar properties.
/// Maps to <c>src/framework/graphics/particletype.{h,cpp}</c>.
/// Task T30.
/// </summary>
public sealed class ParticleType
{
    // ─── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Unique name used by the manager/OTML.</summary>
    public string Name { get; set; } = string.Empty;

    // ─── Visual: multi-stop color gradient ────────────────────────────────────

    /// <summary>Color values for the gradient (one entry per stop in <see cref="ColorStops"/>).</summary>
    public List<Color> Colors { get; } = [Color.White];

    /// <summary>
    /// Normalised time values [0,1] for each color in <see cref="Colors"/>.
    /// Must have the same count as <see cref="Colors"/>.
    /// </summary>
    public List<float> ColorStops { get; } = [0f];

    // ─── Visual: composition ──────────────────────────────────────────────────

    /// <summary>How the particle is composited onto the framebuffer.</summary>
    public ParticleCompositionMode CompositionMode { get; set; } = ParticleCompositionMode.Normal;

    // ─── Size ─────────────────────────────────────────────────────────────────

    /// <summary>Radius of a freshly emitted particle (pixels).</summary>
    public float StartRadius { get; set; } = 16f;

    /// <summary>Radius at end of life (particles shrink/grow to this).</summary>
    public float FinalRadius { get; set; } = 16f;

    // ─── Position scatter (relative to emitter) ───────────────────────────────

    public float MinPositionRadius { get; set; } = 0f;
    public float MaxPositionRadius { get; set; } = 3f;
    public float MinPositionAngle  { get; set; } = 0f;    // degrees
    public float MaxPositionAngle  { get; set; } = 360f;  // degrees

    // ─── Initial velocity ─────────────────────────────────────────────────────

    public float MinVelocity      { get; set; } = 32f;
    public float MaxVelocity      { get; set; } = 64f;
    public float MinVelocityAngle { get; set; } = 0f;    // degrees
    public float MaxVelocityAngle { get; set; } = 360f;  // degrees

    // ─── Initial acceleration ─────────────────────────────────────────────────

    public float MinAcceleration      { get; set; } = 0f;
    public float MaxAcceleration      { get; set; } = 0f;
    public float MinAccelerationAngle { get; set; } = 0f;    // degrees
    public float MaxAccelerationAngle { get; set; } = 360f;  // degrees

    // ─── Lifetime ─────────────────────────────────────────────────────────────

    public float MinDuration { get; set; } = 0f;
    public float MaxDuration { get; set; } = 10f;

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a new <see cref="Particle"/> using this type's configuration
    /// and the supplied emitter position and RNG.
    /// </summary>
    public Particle CreateParticle(Vector2 emitterPos, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        // Scatter position inside an annular sector
        float posAngle  = Rand(rng, MinPositionAngle, MaxPositionAngle) * MathF.PI / 180f;
        float posRadius = Rand(rng, MinPositionRadius, MaxPositionRadius);
        var   position  = emitterPos + new Vector2(MathF.Cos(posAngle) * posRadius,
                                                   MathF.Sin(posAngle) * posRadius);

        // Velocity
        float velAngle = Rand(rng, MinVelocityAngle, MaxVelocityAngle) * MathF.PI / 180f;
        float velMag   = Rand(rng, MinVelocity, MaxVelocity);
        var   velocity = new Vector2(MathF.Cos(velAngle) * velMag, MathF.Sin(velAngle) * velMag);

        // Acceleration
        float accAngle = Rand(rng, MinAccelerationAngle, MaxAccelerationAngle) * MathF.PI / 180f;
        float accMag   = Rand(rng, MinAcceleration, MaxAcceleration);
        var   accel    = new Vector2(MathF.Cos(accAngle) * accMag, MathF.Sin(accAngle) * accMag);

        float lifetime  = Rand(rng, MinDuration, MaxDuration);
        float radiusDelta = lifetime > 0 ? (FinalRadius - StartRadius) / lifetime : 0f;

        return new Particle
        {
            Position     = position,
            Velocity     = velocity,
            Acceleration = accel,
            Radius       = StartRadius,
            RadiusDelta  = radiusDelta,
            Color        = SampleColor(0f),
            Lifetime     = lifetime,
            Age          = 0f,
        };
    }

    /// <summary>
    /// Returns the interpolated color at normalised age <paramref name="t"/> (0–1).
    /// Uses the multi-stop gradient defined by <see cref="Colors"/> and <see cref="ColorStops"/>.
    /// </summary>
    public Color SampleColor(float t)
    {
        if (Colors.Count == 0) return Color.White;
        if (Colors.Count == 1) return Colors[0];

        // Find the two surrounding stops
        for (int i = 0; i < ColorStops.Count - 1; i++)
        {
            float s0 = ColorStops[i];
            float s1 = ColorStops[i + 1];
            if (t <= s1)
            {
                float f  = s1 > s0 ? (t - s0) / (s1 - s0) : 0f;
                var   c0 = Colors[i];
                var   c1 = Colors[i + 1];
                return new Color(
                    (byte)(c0.R + (c1.R - c0.R) * f),
                    (byte)(c0.G + (c1.G - c0.G) * f),
                    (byte)(c0.B + (c1.B - c0.B) * f),
                    (byte)(c0.A + (c1.A - c0.A) * f));
            }
        }

        return Colors[^1];
    }

    private static float Rand(Random rng, float min, float max)
        => min >= max ? min : rng.NextSingle() * (max - min) + min;
}

// ─── ParticleCompositionMode ──────────────────────────────────────────────────

/// <summary>
/// How a particle type's pixels are blended onto the framebuffer.
/// Maps to <c>CompositionMode</c> in <c>particletype.h</c>.
/// Task T30.
/// </summary>
public enum ParticleCompositionMode
{
    Normal,
    Multiply,
    Addition,
}
