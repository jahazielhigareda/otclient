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
    private readonly List<Particle> _particles = [];
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

    public ParticleEmitter(Vector2 position, int? seed = null)
    {
        Position = position;
        _rng     = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    // ─── Update / Render ──────────────────────────────────────────────────────

    /// <summary>Advances all particles and emits new ones.</summary>
    public void Update(float deltaSeconds)
    {
        // Emit new particles
        if (IsEmitting)
        {
            _emitAccumulator += EmitRate * deltaSeconds;
            while (_emitAccumulator >= 1f)
            {
                _particles.Add(CreateParticle());
                _emitAccumulator -= 1f;
            }
        }

        // Update and prune dead particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Update(deltaSeconds);
            if (!p.IsAlive)
                _particles.RemoveAt(i);
            else
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
