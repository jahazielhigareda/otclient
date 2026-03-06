namespace OTClient.Framework.Game;

// ─── ThingCategory ────────────────────────────────────────────────────────────

/// <summary>
/// Identifies which of the three main .dat / appearances sections a
/// <see cref="ThingType"/> belongs to.
/// </summary>
public enum ThingCategory
{
    Item    = 0,
    Creature = 1,
    Effect   = 2,
    Missile  = 3,
}

// ─── ThingTypeFlag ─────────────────────────────────────────────────────────────

/// <summary>
/// Bit-flags that describe a thing's properties, mirroring the flag bytes
/// in the Tibia .dat file and modern appearances format.
/// </summary>
[Flags]
public enum ThingTypeFlag : long
{
    None              = 0,
    Ground            = 1L << 0,
    GroundBorder      = 1L << 1,
    OnBottom          = 1L << 2,
    OnTop             = 1L << 3,
    Container         = 1L << 4,
    Stackable         = 1L << 5,
    MultiUse          = 1L << 6,
    Writable          = 1L << 7,
    WritableOnce      = 1L << 8,
    FluidContainer    = 1L << 9,
    Splash            = 1L << 10,
    NotWalkable       = 1L << 11,
    NotMoveable       = 1L << 12,
    BlockProjectile   = 1L << 13,
    NotPathable       = 1L << 14,
    Pickupable        = 1L << 15,
    Hangable          = 1L << 16,
    HookSouth         = 1L << 17,
    HookEast          = 1L << 18,
    Rotateable        = 1L << 19,
    Light             = 1L << 20,
    DontHide          = 1L << 21,
    Translucent       = 1L << 22,
    Displacement      = 1L << 23,
    Elevation         = 1L << 24,
    LyingCorpse       = 1L << 25,
    AnimateAlways     = 1L << 26,
    MinimapColor      = 1L << 27,
    LensHelp          = 1L << 28,
    FullGround        = 1L << 29,
    IgnoreLook        = 1L << 30,
    Cloth             = 1L << 31,
    Market            = 1L << 32,
    DefaultAction     = 1L << 33,
    Wrapable          = 1L << 34,
    Unwrapable        = 1L << 35,
    TopEffect         = 1L << 36,
}

// ─── ThingType ────────────────────────────────────────────────────────────────

/// <summary>
/// Metadata record for a single item/creature/effect type loaded from the
/// Tibia .dat file or Protobuf appearances file.
/// Maps to <c>src/client/thingtype.h</c>.
/// Task 8.3.
/// </summary>
public sealed class ThingType
{
    // ─── Identity ─────────────────────────────────────────────────────────────

    public ThingCategory Category { get; init; }
    public int           Id       { get; init; }

    // ─── Sprite layout ────────────────────────────────────────────────────────

    /// <summary>Width of one frame in tiles (usually 1; 2 for large creatures).</summary>
    public int Width    { get; init; } = 1;
    /// <summary>Height of one frame in tiles.</summary>
    public int Height   { get; init; } = 1;
    /// <summary>Number of X blend-layers (blend frames / outfit layers).</summary>
    public int Layers   { get; init; } = 1;
    /// <summary>Number of directional variants (1 or 4).</summary>
    public int PatternX { get; init; } = 1;
    /// <summary>Number of Y pattern variants (usually 1).</summary>
    public int PatternY { get; init; } = 1;
    /// <summary>Number of Z pattern variants (usually 1).</summary>
    public int PatternZ { get; init; } = 1;
    /// <summary>Number of animation frames.</summary>
    public int Frames   { get; init; } = 1;
    /// <summary>List of sprite IDs referenced, in layout order.</summary>
    public IReadOnlyList<int> SpriteIds { get; init; } = [];

    // ─── Flags ────────────────────────────────────────────────────────────────

    public ThingTypeFlag Flags { get; init; } = ThingTypeFlag.None;

    public bool IsGround         => (Flags & ThingTypeFlag.Ground)       != 0;
    public bool IsStackable      => (Flags & ThingTypeFlag.Stackable)    != 0;
    public bool IsContainer      => (Flags & ThingTypeFlag.Container)    != 0;
    public bool IsPickupable     => (Flags & ThingTypeFlag.Pickupable)   != 0;
    public bool IsNotWalkable    => (Flags & ThingTypeFlag.NotWalkable)  != 0;
    public bool IsNotPathable    => (Flags & ThingTypeFlag.NotPathable)  != 0;
    public bool IsBlockProjectile => (Flags & ThingTypeFlag.BlockProjectile) != 0;
    public bool IsFullGround     => (Flags & ThingTypeFlag.FullGround)   != 0;
    public bool IsAnimated       => Frames > 1;

    // ─── Minimap ──────────────────────────────────────────────────────────────

    public Raylib_cs.Color MinimapColor { get; init; } = Raylib_cs.Color.Black;

    // ─── Ground speed ─────────────────────────────────────────────────────────

    /// <summary>Ground movement speed modifier (ms to walk one tile).</summary>
    public int GroundSpeed { get; init; } = 150;

    // ─── Light ────────────────────────────────────────────────────────────────

    public int LightLevel  { get; init; } = 0;
    public int LightRadius { get; init; } = 0;
}

// ─── ThingTypeManager ─────────────────────────────────────────────────────────

/// <summary>
/// Registry of all <see cref="ThingType"/> records indexed by category and ID.
/// In a real client these are loaded from the Tibia .dat or appearances.dat file.
/// Programmatic registration (<see cref="Add"/>) is used in tests and
/// at run-time after parsing.
/// Maps to <c>src/client/thingtype.*</c>.
/// Task 8.3.
/// </summary>
public sealed class ThingTypeManager
{
    private readonly Dictionary<(ThingCategory, int), ThingType> _types = [];

    /// <summary>Total number of registered types.</summary>
    public int Count => _types.Count;

    /// <summary>Registers a <see cref="ThingType"/>.</summary>
    public void Add(ThingType tt)
    {
        ArgumentNullException.ThrowIfNull(tt);
        _types[(tt.Category, tt.Id)] = tt;
    }

    /// <summary>Looks up a <see cref="ThingType"/> by category and ID.</summary>
    public ThingType? Get(ThingCategory cat, int id)
        => _types.TryGetValue((cat, id), out var t) ? t : null;

    /// <summary>Returns all types in a given category.</summary>
    public IEnumerable<ThingType> GetAll(ThingCategory cat)
        => _types.Values.Where(t => t.Category == cat);

    /// <summary>Returns <c>true</c> when the given type is registered.</summary>
    public bool Contains(ThingCategory cat, int id)
        => _types.ContainsKey((cat, id));
}
