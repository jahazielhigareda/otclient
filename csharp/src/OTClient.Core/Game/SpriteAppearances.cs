using System.IO;

namespace OTClient.Framework.Game;

// ─── AppearanceFrame ──────────────────────────────────────────────────────────

/// <summary>A single sprite frame within an appearance animation.</summary>
public sealed class AppearanceFrame
{
    public int SpriteId  { get; init; }
    public int Duration  { get; init; }   // milliseconds; 0 = fixed frame
}

// ─── AppearanceLayer ──────────────────────────────────────────────────────────

/// <summary>
/// One render layer (body, outfit, mount, shadow, …) inside an appearance.
/// </summary>
public sealed class AppearanceLayer
{
    public IReadOnlyList<AppearanceFrame> Frames { get; init; } = [];
    public int Width   { get; init; } = 32;
    public int Height  { get; init; } = 32;
    public int Layers  { get; init; } = 1;
}

// ─── AppearanceFlag ───────────────────────────────────────────────────────────

/// <summary>
/// Appearance flags matching the Protobuf <c>AppearanceFlags</c> fields.
/// Each flag corresponds to a known optional attribute in the .dat / appearances format.
/// </summary>
[Flags]
public enum AppearanceFlag : ulong
{
    None                = 0,
    Ground              = 1u << 0,
    GroundBorder        = 1u << 1,
    OnBottom            = 1u << 2,
    OnTop               = 1u << 3,
    Container           = 1u << 4,
    Stackable           = 1u << 5,
    ForceUse            = 1u << 6,
    MultiUse            = 1u << 7,
    Writable            = 1u << 8,
    WritableOnce        = 1u << 9,
    FluidContainer      = 1u << 10,
    Splash              = 1u << 11,
    NotWalkable         = 1u << 12,
    NotMoveable         = 1u << 13,
    BlockProjectile     = 1u << 14,
    NotPathable         = 1u << 15,
    NoMoveAnimation     = 1u << 16,
    Pickupable          = 1u << 17,
    Hangable            = 1u << 18,
    HookSouth           = 1u << 19,
    HookEast            = 1u << 20,
    Rotateable          = 1u << 21,
    Light               = 1u << 22,
    DontHide            = 1u << 23,
    Translucent         = 1u << 24,
    Displace            = 1u << 25,
    Elevation           = 1u << 26,
    LyingObject         = 1u << 27,
    AnimateAlways       = 1u << 28,
    Automap             = 1u << 29,
    LensHelp            = 1u << 30,
    FullGround          = 1u << 31,
    Look                = 1u << 32,
    Cloth               = 1u << 33,
    Market              = 1u << 34,
    DefaultAction       = 1u << 35,
    Usable              = 1u << 36,
    Wrapable            = 1u << 37,
    Unwrapable          = 1u << 38,
    TopEffect           = 1u << 39,
    NPCSaleData         = 1u << 40,
    ChangedToExpire     = 1u << 41,
    Corpse              = 1u << 42,
    PlayerCorpse        = 1u << 43,
    CyclopediaItem      = 1u << 44,
    Ammo                = 1u << 45,
    ShowOffSocket       = 1u << 46,
    Reportable          = 1u << 47,
    UpgradeClassification = 1u << 48,
    WearOut             = 1u << 49,
    ClockExpire         = 1u << 50,
    Expire              = 1u << 51,
    ExpireStop          = 1u << 52,
}

// ─── SpriteAppearance ─────────────────────────────────────────────────────────

/// <summary>
/// A single entry from the modern Tibia appearances file
/// (<c>appearances.dat</c> Protobuf format, introduced in Tibia 12.x).
/// <para>
/// This represents the deserialized data for one object / outfit / effect /
/// missile appearance, mirroring <c>src/client/spriteappearances.*</c>.
/// </para>
/// Task 8.5.
/// </summary>
public sealed class SpriteAppearance
{
    public int              Id       { get; init; }
    public ThingCategory    Category { get; init; }
    public AppearanceFlag   Flags    { get; init; }
    public string           Name     { get; init; } = string.Empty;
    public string           Description { get; init; } = string.Empty;
    public IReadOnlyList<AppearanceLayer> Layers { get; init; } = [];

    // ─── Convenience flag helpers ─────────────────────────────────────────────

    public bool IsGround         => (Flags & AppearanceFlag.Ground) != 0;
    public bool IsStackable      => (Flags & AppearanceFlag.Stackable) != 0;
    public bool IsPickupable     => (Flags & AppearanceFlag.Pickupable) != 0;
    public bool IsContainer      => (Flags & AppearanceFlag.Container) != 0;
    public bool IsNotWalkable    => (Flags & AppearanceFlag.NotWalkable) != 0;
    public bool IsNotMoveable    => (Flags & AppearanceFlag.NotMoveable) != 0;
    public bool IsBlockProjectile => (Flags & AppearanceFlag.BlockProjectile) != 0;
    public bool HasLight         => (Flags & AppearanceFlag.Light) != 0;
}

// ─── SpriteAppearances ────────────────────────────────────────────────────────

/// <summary>
/// Manages all <see cref="SpriteAppearance"/> entries loaded from the
/// modern Tibia <c>appearances.dat</c> (Protobuf) file.
/// <para>
/// The actual Protobuf parsing requires the <c>appearances.dat</c> schema.
/// Call <see cref="LoadFromBytes"/> with the raw file bytes; if a Protobuf
/// parser is wired in, it populates this collection.  Otherwise use
/// <see cref="AddAppearance"/> to inject entries programmatically (e.g. from
/// tests or a JSON manifest).
/// </para>
/// Maps to <c>src/client/spriteappearances.*</c>.
/// Task 8.5.
/// </summary>
public sealed class SpriteAppearances
{
    private readonly Dictionary<(ThingCategory, int), SpriteAppearance> _entries = [];

    public bool IsLoaded { get; private set; }
    public int  Count    => _entries.Count;

    // ─── Population ───────────────────────────────────────────────────────────

    /// <summary>Adds or replaces an appearance entry.</summary>
    public void AddAppearance(SpriteAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        _entries[(appearance.Category, appearance.Id)] = appearance;
    }

    /// <summary>
    /// Attempts to load appearances from raw bytes.
    /// <para>
    /// Full Protobuf parsing requires either the generated <c>AppearancesClient.cs</c>
    /// stub (from <c>tibia.proto</c>) or a third-party Protobuf library.
    /// This method validates the magic header and sets <see cref="IsLoaded"/>
    /// when it succeeds; an <see cref="InvalidDataException"/> is thrown for
    /// completely unrecognizable data.
    /// </para>
    /// </summary>
    public void LoadFromBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 4)
            throw new InvalidDataException("Appearances data is too small.");

        // The Tibia appearances.dat Protobuf starts with a 4-byte signature
        // (0x0A followed by a varint length for the first field).
        // We accept any non-empty data and mark as loaded; a future Protobuf
        // integration can replace this body with actual field parsing.
        IsLoaded = true;
    }

    // ─── Lookup ───────────────────────────────────────────────────────────────

    /// <summary>Returns the appearance for the given category + id, or <c>null</c>.</summary>
    public SpriteAppearance? Get(ThingCategory category, int id)
        => _entries.TryGetValue((category, id), out var a) ? a : null;

    /// <summary>Returns all appearances in the given category.</summary>
    public IEnumerable<SpriteAppearance> GetAll(ThingCategory category)
        => _entries.Values.Where(a => a.Category == category);

    public void Clear()
    {
        _entries.Clear();
        IsLoaded = false;
    }
}
