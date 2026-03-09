using MoonSharp.Interpreter;
using Raylib_cs;

namespace OTClient.Framework.Game;

// ─── Creature ─────────────────────────────────────────────────────────────────

/// <summary>
/// A living entity in the world: has an outfit, walks between tiles,
/// shows a health bar, and is visible on the minimap.
/// Maps to <c>src/client/creature.h</c>.
/// Task 8.8 / T38.
/// </summary>
[MoonSharpUserData]
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

        // Advance the walk animation phase (1..AnimPhaseCount, cycling).
        // Mirrors the C++ pattern: if (m_walkAnimationPhase == footAnimPhases)
        //   m_walkAnimationPhase = 1; else ++m_walkAnimationPhase;
        int count = Math.Max(1, AnimPhaseCount);
        AnimPhase = (byte)(AnimPhase >= count ? 1 : AnimPhase + 1);
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
            AnimPhase    = 0;   // idle — mirrors m_walkAnimationPhase = 0 in C++
        }
    }

    // ─── Health ───────────────────────────────────────────────────────────────

    public int  MaxHealth   { get; set; } = 100;
    public int  Health      { get; set; } = 100;
    public bool IsAlive     => Health > 0;
    public bool IsFullHealth => Health >= MaxHealth;
    public float HealthPercent => MaxHealth > 0 ? (float)Health / MaxHealth : 0f;

    // ─── Skull / party ────────────────────────────────────────────────────────

    public byte Skull  { get; set; }
    public byte Shield { get; set; }
    public byte Emblem { get; set; }

    // ─── Creature type / icon ─────────────────────────────────────────────────

    /// <summary>Creature type byte sent by the server (NPC, monster, etc.).</summary>
    public byte Type { get; set; }

    /// <summary>Icon byte displayed above the creature's head.</summary>
    public byte Icon { get; set; }

    // ─── Master / summon ──────────────────────────────────────────────────────

    /// <summary>Id of the master creature (0 = no master).</summary>
    public uint MasterId { get; set; }

    // ─── Visibility ───────────────────────────────────────────────────────────

    /// <summary>Whether the creature is currently invisible (e.g. with an invisibility rune).</summary>
    public bool IsInvisible { get; set; }

    // ─── Mana (base creature mana percent, 0–100) ─────────────────────────────

    /// <summary>Mana percentage (0–100). Carried here so monsters can show mana bars.</summary>
    public int ManaPercent { get; set; }

    // ─── Floating text ────────────────────────────────────────────────────────

    /// <summary>Text currently displayed above the creature's head (e.g. speech).</summary>
    public string Text { get; set; } = string.Empty;

    // ─── Typing indicator ─────────────────────────────────────────────────────

    /// <summary>Whether the creature has the typing indicator shown above its head.</summary>
    public bool IsTyping { get; set; }



    public int Speed     { get; set; } = 200;
    public int BaseSpeed { get; set; } = 200;

    // ─── Light emission ───────────────────────────────────────────────────────

    public int LightLevel  { get; set; }
    public int LightRadius { get; set; }

    // ─── Animation phase ──────────────────────────────────────────────────────

    /// <summary>
    /// Total number of walking animation frames for this creature type.
    /// The renderer uses this to cycle <see cref="AnimPhase"/> over the correct
    /// sprite sequence.  Matches <c>footAnimPhases</c> in <c>creature.cpp</c>.
    /// Default is 4, which covers most Tibia creature types.
    /// </summary>
    public int AnimPhaseCount { get; set; } = 4;

    /// <summary>
    /// Current walk animation frame.
    /// <c>0</c> = idle (not walking); <c>1..AnimPhaseCount</c> = walking frames.
    /// Advanced once per <see cref="Walk"/> call, reset to 0 when the walk
    /// completes.  Maps to <c>m_walkAnimationPhase</c> in <c>creature.h</c>.
    /// </summary>
    public byte AnimPhase { get; internal set; }

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

    // ─── Lua accessor methods (camelCase, T38) ────────────────────────────────

    [MoonSharpHidden] private static float ToPercent01(int num, int den)
        => den > 0 ? (float)num / den : 0f;

    public uint  getId()            => Id;
    public uint  getMasterId()      => MasterId;
    public string getName()         => Name;
    public float getHealthPercent() => HealthPercent;
    public int   getManaPercent()   => ManaPercent;
    public int   getSpeed()         => Speed;
    public int   getBaseSpeed()     => BaseSpeed;
    public byte  getSkull()         => Skull;
    public byte  getShield()        => Shield;
    public byte  getEmblem()        => Emblem;
    public byte  getType()          => Type;
    public byte  getIcon()          => Icon;
    public Outfit getOutfit()       => Outfit;
    public void  setOutfit(Outfit o) { Outfit = o; }
    public int   getDirection()     => (int)Direction;
    public void  setDirection(int d) { Direction = (Direction)d; }
    public bool  isWalking()        => IsWalking;
    public bool  isInvisible()      => IsInvisible;
    public bool  isDead()           => !IsAlive;
    public bool  isFullHealth()     => IsFullHealth;
    public string getText()         => Text;
    public void  setText(string t)  { Text = t ?? string.Empty; }
    public void  clearText()        { Text = string.Empty; }
    public bool  getTyping()        => IsTyping;
    public void  setTyping(bool v)  { IsTyping = v; }
    public virtual int getVocation() => 0;
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
/// Task 8.9 / T38.
/// </summary>
[MoonSharpUserData]
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

    private readonly int[] _skillLevel     = new int[Enum.GetValues<SkillType>().Length];
    private readonly int[] _skillBaseLevel = new int[Enum.GetValues<SkillType>().Length];
    private readonly int[] _skillPercent   = new int[Enum.GetValues<SkillType>().Length];

    /// <summary>Returns the level of a combat skill.</summary>
    public int GetSkillLevel(SkillType skill)     => _skillLevel[(int)skill];

    /// <summary>Returns the base level of a combat skill (before modifiers).</summary>
    public int GetSkillBaseLevel(SkillType skill)  => _skillBaseLevel[(int)skill];

    /// <summary>Returns the percent progress (0–100) of a combat skill.</summary>
    public int GetSkillPercent(SkillType skill)   => _skillPercent[(int)skill];

    /// <summary>Sets the level, base level, and percent progress of a combat skill.</summary>
    public void SetSkill(SkillType skill, int level, int percent, int baseLevel = 0)
    {
        _skillLevel[(int)skill]     = level;
        _skillBaseLevel[(int)skill] = baseLevel > 0 ? baseLevel : level;
        _skillPercent[(int)skill]   = percent;
    }

    // ─── Magic level base ─────────────────────────────────────────────────────

    public int BaseMagicLevel { get; set; }

    // ─── Total capacity ───────────────────────────────────────────────────────

    public int TotalCapacity { get; set; } = 400;

    // ─── Vocations ────────────────────────────────────────────────────────────

    public int Vocation { get; set; }

    // ─── Gold ─────────────────────────────────────────────────────────────────

    public long Gold { get; set; }

    // ─── Lua accessor methods (T41) ───────────────────────────────────────────

    public int   getLevel()              => Level;
    public int   getLevelPercent()       => LevelPercent;
    public ulong getExperience()         => Exp;
    public int   getMana()               => Mana;
    public int   getMaxMana()            => MaxMana;
    public int   getMagicLevel()         => MagicLevel;
    public int   getMagicLevelPercent()  => MagicLevelPercent;
    public int   getBaseMagicLevel()     => BaseMagicLevel;
    public int   getFreeCapacity()       => FreeCapacity;
    public int   getTotalCapacity()      => TotalCapacity;

    /// <summary>
    /// Returns the level of the given combat skill (0-based id matching <see cref="SkillType"/>).
    /// Out-of-range ids are clamped to the nearest valid id so Lua callers never receive an exception.
    /// </summary>
    public int   getSkillLevel(int skillId)
        => _skillLevel[Math.Clamp(skillId, 0, _skillLevel.Length - 1)];

    /// <summary>
    /// Returns the base level of the given combat skill (before modifiers).
    /// Out-of-range ids are clamped to the nearest valid id so Lua callers never receive an exception.
    /// </summary>
    public int   getSkillBaseLevel(int skillId)
        => _skillBaseLevel[Math.Clamp(skillId, 0, _skillBaseLevel.Length - 1)];

    /// <summary>
    /// Returns the percent progress (0–100) of the given combat skill.
    /// Out-of-range ids are clamped to the nearest valid id so Lua callers never receive an exception.
    /// </summary>
    public int   getSkillLevelPercent(int skillId)
        => _skillPercent[Math.Clamp(skillId, 0, _skillPercent.Length - 1)];

    // ─── Lua accessor overrides (T38) ─────────────────────────────────────────

    public override int getVocation() => Vocation;
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
/// Task 8.9 / T38.
/// </summary>
[MoonSharpUserData]
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

    // ─── Offline training / regeneration ─────────────────────────────────────

    public int OfflineTrainingTime { get; set; }
    public int RegenerationTime    { get; set; }

    // ─── Blessings ────────────────────────────────────────────────────────────

    /// <summary>
    /// Active blessings bitmask as received from the server.
    /// Maps to <c>LocalPlayer::setBlessings / getBlessings</c>.
    /// Task T46.
    /// </summary>
    public uint Blessings { get; set; }

    // ─── Spells ───────────────────────────────────────────────────────────────

    private IReadOnlyList<ushort> _spells = Array.Empty<ushort>();

    /// <summary>
    /// The list of spell ids available to the local player.
    /// Updated via <see cref="SetSpells"/>.
    /// Maps to <c>LocalPlayer::setSpells</c>.
    /// Task T47.
    /// </summary>
    public IReadOnlyList<ushort> Spells => _spells;

    /// <summary>Replaces the spell list with <paramref name="spells"/>.</summary>
    public void SetSpells(IReadOnlyList<ushort> spells) => _spells = spells;

    // ─── Lua accessor methods (T41) ───────────────────────────────────────────

    public int   getSoul()                => Soul;
    public int   getStamina()             => Stamina;
    public uint  getStates()              => Conditions;
    public int   getOfflineTrainingTime() => OfflineTrainingTime;
    public int   getRegenerationTime()    => RegenerationTime;
    public bool  isPremium()              => IsPremium;
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
