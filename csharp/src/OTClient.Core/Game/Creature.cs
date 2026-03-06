using Raylib_cs;

namespace OTClient.Framework.Game;

// ─── Creature ─────────────────────────────────────────────────────────────────

/// <summary>
/// A living entity in the world: has an outfit, walks between tiles,
/// shows a health bar, and is visible on the minimap.
/// Maps to <c>src/client/creature.h</c>.
/// Task 8.8.
/// </summary>
public class Creature : Thing
{
    public override ThingCategory Category => ThingCategory.Creature;

    // ─── Identity ─────────────────────────────────────────────────────────────

    public uint   Id   { get; set; }
    public string Name { get; set; } = string.Empty;

    // ─── Outfit ───────────────────────────────────────────────────────────────

    public Outfit Outfit { get; set; } = Outfit.Default;

    // ─── Walking ──────────────────────────────────────────────────────────────

    public Direction    Direction      { get; set; } = Direction.South;
    public bool         IsWalking      { get; private set; }
    public float        WalkProgress   { get; private set; }   // 0→1

    private Position    _walkFrom;
    private float       _walkDurationMs;
    private float       _walkElapsedMs;

    /// <summary>
    /// Starts a walk animation from <see cref="Thing.Position"/> towards
    /// <paramref name="destination"/>.
    /// </summary>
    public virtual void Walk(Position destination, int durationMs = 500)
    {
        Direction    = Position.DirectionTo(destination);
        _walkFrom    = Position;
        _walkDurationMs  = Math.Max(1, durationMs);
        _walkElapsedMs   = 0f;
        IsWalking    = true;
        WalkProgress = 0f;
    }

    public virtual void Update(float deltaMs)
    {
        if (!IsWalking) return;
        _walkElapsedMs += deltaMs;
        WalkProgress = Math.Clamp(_walkElapsedMs / _walkDurationMs, 0f, 1f);
        if (_walkElapsedMs >= _walkDurationMs)
        {
            IsWalking = false;
            WalkProgress = 1f;
        }
    }

    // ─── Health ───────────────────────────────────────────────────────────────

    public int  MaxHealth   { get; set; } = 100;
    public int  Health      { get; set; } = 100;
    public bool IsAlive     => Health > 0;
    public float HealthPercent => MaxHealth > 0 ? (float)Health / MaxHealth : 0f;

    // ─── Skull / party ────────────────────────────────────────────────────────

    public byte Skull  { get; set; }
    public byte Shield { get; set; }
    public byte Emblem { get; set; }

    // ─── Speed ────────────────────────────────────────────────────────────────

    public int Speed { get; set; } = 200;

    // ─── Light emission ───────────────────────────────────────────────────────

    public int LightLevel  { get; set; }
    public int LightRadius { get; set; }

    // ─── Effects ──────────────────────────────────────────────────────────────

    private readonly List<Effect> _effects = [];
    public  IReadOnlyList<Effect> Effects  => _effects;

    public void AddEffect(Effect e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _effects.Add(e);
    }

    /// <summary>Removes finished effects.</summary>
    public void PruneEffects()
        => _effects.RemoveAll(e => e.IsFinished);
}

// ─── Player ───────────────────────────────────────────────────────────────────

/// <summary>
/// A player-controlled creature.  Carries stat & skill info.
/// Maps to <c>src/client/player.h</c>.
/// Task 8.9.
/// </summary>
public class Player : Creature
{
    // ─── Level & experience ───────────────────────────────────────────────────

    public int   Level  { get; set; } = 1;
    public ulong Exp    { get; set; }

    // ─── Stats ────────────────────────────────────────────────────────────────

    public int MaxMana   { get; set; } = 100;
    public int Mana      { get; set; } = 100;

    public int MaxCapacity   { get; set; } = 400;
    public int UsedCapacity  { get; set; }

    // ─── Vocations ────────────────────────────────────────────────────────────

    public int Vocation { get; set; }

    // ─── Gold ─────────────────────────────────────────────────────────────────

    public long Gold { get; set; }
}

// ─── LocalPlayer ──────────────────────────────────────────────────────────────

/// <summary>
/// The character controlled by the logged-in client — extends
/// <see cref="Player"/> with own-client specific properties.
/// Maps to <c>src/client/localplayer.h</c>.
/// Task 8.9.
/// </summary>
public sealed class LocalPlayer : Player
{
    // ─── Stamina ──────────────────────────────────────────────────────────────

    public int    Stamina    { get; set; } = 2520;   // minutes
    public int    Soul       { get; set; } = 100;

    // ─── Condition flags ──────────────────────────────────────────────────────

    public uint Conditions   { get; set; }

    // ─── Premium ──────────────────────────────────────────────────────────────

    public bool IsPremium { get; set; }

    // ─── Known / visible ──────────────────────────────────────────────────────

    /// <summary>
    /// Set to <c>true</c> once the server has confirmed login and the first
    /// map data arrived.
    /// </summary>
    public bool IsKnown { get; set; }
}
