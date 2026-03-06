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

    public int Speed     { get; set; } = 200;
    public int BaseSpeed { get; set; } = 200;

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
/// Combat skill identifiers matching the Tibia protocol skill ordering.
/// </summary>
public enum SkillType : int
{
    Fist      = 0,
    Club      = 1,
    Sword     = 2,
    Axe       = 3,
    Distance  = 4,
    Shielding = 5,
    Fishing   = 6,
}

/// <summary>
/// A player-controlled creature.  Carries stat & skill info.
/// Maps to <c>src/client/player.h</c>.
/// Task 8.9.
/// </summary>
public class Player : Creature
{
    // ─── Level & experience ───────────────────────────────────────────────────

    public int   Level        { get; set; } = 1;
    public int   LevelPercent { get; set; }
    public ulong Exp          { get; set; }

    // ─── Stats ────────────────────────────────────────────────────────────────

    public int MaxMana   { get; set; } = 100;
    public int Mana      { get; set; } = 100;

    public int MaxCapacity   { get; set; } = 400;
    public int UsedCapacity  { get; set; }
    public int FreeCapacity  { get; set; }

    // ─── Magic level ──────────────────────────────────────────────────────────

    public int MagicLevel        { get; set; }
    public int MagicLevelPercent { get; set; }

    // ─── Combat skills ────────────────────────────────────────────────────────

    private static readonly int SkillCount = Enum.GetValues<SkillType>().Length;

    private readonly int[] _skillLevel   = new int[Enum.GetValues<SkillType>().Length];
    private readonly int[] _skillPercent = new int[Enum.GetValues<SkillType>().Length];

    /// <summary>Returns the level of a combat skill.</summary>
    public int GetSkillLevel(SkillType skill)   => _skillLevel[(int)skill];

    /// <summary>Returns the percent progress (0–100) of a combat skill.</summary>
    public int GetSkillPercent(SkillType skill) => _skillPercent[(int)skill];

    /// <summary>Sets the level and percent progress of a combat skill.</summary>
    public void SetSkill(SkillType skill, int level, int percent)
    {
        _skillLevel[(int)skill]   = level;
        _skillPercent[(int)skill] = percent;
    }

    // ─── Vocations ────────────────────────────────────────────────────────────

    public int Vocation { get; set; }

    // ─── Gold ─────────────────────────────────────────────────────────────────

    public long Gold { get; set; }
}

// ─── LocalPlayer ──────────────────────────────────────────────────────────────

/// <summary>Fight mode selected by the player.</summary>
public enum FightMode : byte { Offensive = 1, Balanced = 2, Defensive = 3 }

/// <summary>Chase mode selected by the player.</summary>
public enum ChaseMode : byte { ChaseOpponent = 0, DontChase = 1 }

/// <summary>PvP mode (available in newer protocols).</summary>
public enum PvpMode : byte { WhiteDove = 0, WhiteHand = 1, YellowHand = 2, RedFist = 3 }

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

    // ─── Combat modes ─────────────────────────────────────────────────────────

    public FightMode FightMode { get; set; } = FightMode.Balanced;
    public ChaseMode ChaseMode { get; set; } = ChaseMode.DontChase;
    public bool      SafeMode  { get; set; } = true;
    public PvpMode   PvpMode   { get; set; } = PvpMode.WhiteDove;

    // ─── Premium ──────────────────────────────────────────────────────────────

    public bool IsPremium { get; set; }

    // ─── Known / visible ──────────────────────────────────────────────────────

    /// <summary>
    /// Set to <c>true</c> once the server has confirmed login and the first
    /// map data arrived.
    /// </summary>
    public bool IsKnown { get; set; }
}

// ─── Monster ──────────────────────────────────────────────────────────────────

/// <summary>
/// A monster or summon creature.
/// Maps to <c>src/client/monster.h</c>.
/// Task T01.
/// </summary>
public sealed class Monster : Creature { }

// ─── Npc ──────────────────────────────────────────────────────────────────────

/// <summary>
/// A non-player character.
/// Maps to <c>src/client/npc.h</c>.
/// Task T01.
/// </summary>
public sealed class Npc : Creature { }
