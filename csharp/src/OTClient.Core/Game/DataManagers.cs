namespace OTClient.Framework.Game;

// ─── Town ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents a town/city accessible in the game world.
/// Maps to <c>src/client/towns.h</c>.
/// Task 8.23.
/// </summary>
public sealed class Town
{
    public int      Id       { get; init; }
    public string   Name     { get; init; } = string.Empty;
    public Position TemplePos { get; init; }
}

/// <summary>Registry of all towns.</summary>
public sealed class TownManager
{
    private readonly Dictionary<int, Town> _towns = [];

    public void Add(Town town)
    {
        ArgumentNullException.ThrowIfNull(town);
        _towns[town.Id] = town;
    }

    public Town? Get(int id) => _towns.TryGetValue(id, out var t) ? t : null;
    public IEnumerable<Town> All => _towns.Values;
    public int Count => _towns.Count;
}

// ─── House ────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents a purchasable house in the game world.
/// Task 8.23.
/// </summary>
public sealed class House
{
    public int      Id       { get; init; }
    public string   Name     { get; init; } = string.Empty;
    public int      TownId   { get; init; }
    public Position EntryPos { get; init; }
    public int      Rent     { get; init; }
    public string   Owner    { get; init; } = string.Empty;
}

/// <summary>Registry of all houses.</summary>
public sealed class HouseManager
{
    private readonly Dictionary<int, House> _houses = [];

    public void Add(House house)
    {
        ArgumentNullException.ThrowIfNull(house);
        _houses[house.Id] = house;
    }

    public House? Get(int id) => _houses.TryGetValue(id, out var h) ? h : null;
    public IEnumerable<House> All => _houses.Values;
    public int Count => _houses.Count;
}

// ─── CreatureData ─────────────────────────────────────────────────────────────

/// <summary>
/// Static creature data loaded from XML / appearances
/// (e.g. NPC / monster name → type mapping).
/// Task 8.23.
/// </summary>
public sealed class CreatureData
{
    public string Name { get; init; } = string.Empty;
    public int    TypeId { get; init; }
    public bool   IsNpc  { get; init; }
}

/// <summary>Registry of known creature data.</summary>
public sealed class CreatureDataManager
{
    private readonly Dictionary<string, CreatureData> _data
        = new(StringComparer.OrdinalIgnoreCase);

    public void Add(CreatureData cd)
    {
        ArgumentNullException.ThrowIfNull(cd);
        _data[cd.Name] = cd;
    }

    public CreatureData? Get(string name)
        => _data.TryGetValue(name, out var d) ? d : null;

    public IEnumerable<CreatureData> All => _data.Values;
    public int Count => _data.Count;
}

// ─── PaperDoll ────────────────────────────────────────────────────────────────

/// <summary>
/// Visual equipment-overlay layer rendered on top of a creature sprite
/// (wings, extra armour graphics, etc.).
/// Expanded in T34 to match <c>src/client/paperdoll.h</c> fully.
/// </summary>
public sealed class PaperDoll
{
    // ── equipment slots (original simple tracker, kept for compatibility) ─────
    public enum Slot
    {
        Head   = 0,
        Neck   = 1,
        Back   = 2,
        Body   = 3,
        RightHand = 4,
        LeftHand  = 5,
        Legs   = 6,
        Feet   = 7,
        Ring   = 8,
        Ammo   = 9,
    }

    private readonly Dictionary<Slot, Item?> _slots = [];

    public void  Equip(Slot slot, Item? item) => _slots[slot] = item;
    public Item? GetSlot(Slot slot) => _slots.TryGetValue(slot, out var i) ? i : null;
    public bool  IsSlotEmpty(Slot slot) => GetSlot(slot) is null;
    public void  Clear() => _slots.Clear();

    // ── visual identity (T34 additions) ───────────────────────────────────────
    public ushort Id     { get; set; }
    public ushort ThingId { get; set; }

    // ── visual properties ─────────────────────────────────────────────────────
    /// <summary>Playback speed multiplier × 100 (100 = 1×).</summary>
    public byte  Speed      { get; set; } = 100;
    /// <summary>Opacity × 100 (100 = fully opaque).</summary>
    public byte  Opacity    { get; set; } = 100;
    /// <summary>Scale factor relative to the owner sprite.</summary>
    public float SizeFactor { get; set; } = 1.0f;

    // ── addon mask ────────────────────────────────────────────────────────────
    public uint Addons   { get; set; }
    public bool OnlyAddon { get; set; }

    public bool HasAddon(uint addon) => (Addons & addon) == addon;
    public void SetAddon(uint addon)    => Addons |=  addon;
    public void RemoveAddon(uint addon) => Addons &= ~addon;

    // ── outfit colour channels ────────────────────────────────────────────────
    public byte HeadColor { get; set; }
    public byte BodyColor { get; set; }
    public byte LegsColor { get; set; }
    public byte FeetColor { get; set; }

    public void SetColor(byte c) { HeadColor = BodyColor = LegsColor = FeetColor = c; }

    // ── mount flags ───────────────────────────────────────────────────────────
    public bool UseMountPattern { get; set; }
    public bool ShowOnMount     { get; set; } = true;

    // ── draw options ──────────────────────────────────────────────────────────
    public byte Priority   { get; set; } = 1;
    public bool CanDrawOnUI { get; set; } = true;

    // ── per-direction offsets (2 arrays: normal + mount, indexed by Direction cast to int, 8 values) ─
    private readonly DirControl[][] _offsetDirections = [ new DirControl[8], new DirControl[8] ];

    public PaperDoll()
    {
        for (int m = 0; m < 2; m++)
            for (int d = 0; d < 8; d++)
                _offsetDirections[m][d] = new DirControl { OnTop = true };
    }

    public DirControl GetDirControl(int mountIdx, Direction dir) => _offsetDirections[mountIdx][(int)dir];

    public void SetOnTop(bool onTop)
    {
        foreach (var dc in _offsetDirections[0]) dc.OnTop = onTop;
    }

    public void SetOffset(int x, int y)
    {
        foreach (var dc in _offsetDirections[0]) { dc.OffsetX = x; dc.OffsetY = y; }
    }

    public void SetMountOffset(int x, int y)
    {
        foreach (var dc in _offsetDirections[1]) { dc.OffsetX = x; dc.OffsetY = y; }
    }

    public void SetDirOffset(Direction dir, int x, int y, bool onTop = true)
    {
        var dc = _offsetDirections[0][(int)dir];
        dc.OnTop = onTop; dc.OffsetX = x; dc.OffsetY = y;
    }

    public void SetMountDirOffset(Direction dir, int x, int y, bool onTop = true)
    {
        var dc = _offsetDirections[1][(int)dir];
        dc.OnTop = onTop; dc.OffsetX = x; dc.OffsetY = y;
    }

    /// <summary>Deep-copy this paperdoll (mirrors <c>clone()</c> in C++).</summary>
    public PaperDoll Clone()
    {
        var c = new PaperDoll
        {
            Id = Id, ThingId = ThingId,
            Speed = Speed, Opacity = Opacity, SizeFactor = SizeFactor,
            Addons = Addons, OnlyAddon = OnlyAddon,
            HeadColor = HeadColor, BodyColor = BodyColor, LegsColor = LegsColor, FeetColor = FeetColor,
            UseMountPattern = UseMountPattern, ShowOnMount = ShowOnMount,
            Priority = Priority, CanDrawOnUI = CanDrawOnUI,
        };
        for (int m = 0; m < 2; m++)
            for (int d = 0; d < 8; d++)
            {
                c._offsetDirections[m][d].OnTop   = _offsetDirections[m][d].OnTop;
                c._offsetDirections[m][d].OffsetX = _offsetDirections[m][d].OffsetX;
                c._offsetDirections[m][d].OffsetY = _offsetDirections[m][d].OffsetY;
            }
        // copy slots
        foreach (var kv in _slots) c._slots[kv.Key] = kv.Value;
        return c;
    }

    /// <summary>Reset to defaults.</summary>
    public void Reset()
    {
        Speed = 100; Opacity = 100; SizeFactor = 1.0f;
        Addons = 0; OnlyAddon = false;
        HeadColor = BodyColor = LegsColor = FeetColor = 0;
        UseMountPattern = false; ShowOnMount = true;
        Priority = 1; CanDrawOnUI = true;
        foreach (var arr in _offsetDirections)
            foreach (var dc in arr) { dc.OnTop = true; dc.OffsetX = dc.OffsetY = 0; }
        _slots.Clear();
    }
}

/// <summary>
/// Registry of all <see cref="PaperDoll"/> descriptors loaded from data files.
/// Maps to <c>src/client/paperdollmanager.h</c>.
/// Task T34.
/// </summary>
public sealed class PaperDollManager
{
    private readonly Dictionary<ushort, PaperDoll> _paperdolls = [];

    /// <summary>Create and register a paper doll with the given id/thingId.</summary>
    public PaperDoll Set(ushort id, ushort thingId)
    {
        var pd = new PaperDoll { Id = id, ThingId = thingId };
        _paperdolls[id] = pd;
        return pd;
    }

    public PaperDoll? GetById(ushort id)
        => _paperdolls.TryGetValue(id, out var pd) ? pd : null;

    public void Remove(ushort id) => _paperdolls.Remove(id);
    public void Clear()           => _paperdolls.Clear();

    public IEnumerable<PaperDoll> All => _paperdolls.Values;
    public int Count => _paperdolls.Count;
}

// ─── SpriteManager ────────────────────────────────────────────────────────────

/// <summary>
/// Manages sprite metadata loaded from a .spr file or appearances.
/// In a full implementation this maps sprite IDs to
/// <see cref="OTClient.Framework.Graphics.Texture"/> regions.
/// Task 8.4.
/// </summary>
public sealed class SpriteManager
{
    // ─── Sprite index ─────────────────────────────────────────────────────────

    private readonly Dictionary<int, SpriteInfo> _sprites = [];

    public int Count => _sprites.Count;

    public void Register(SpriteInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _sprites[info.Id] = info;
    }

    public SpriteInfo? Get(int id)
        => _sprites.TryGetValue(id, out var s) ? s : null;

    public bool HasSprite(int id) => _sprites.ContainsKey(id);
}

/// <summary>
/// Lightweight descriptor for a single sprite within an atlas.
/// </summary>
public sealed class SpriteInfo
{
    public int Id      { get; init; }
    public int AtlasId { get; init; }
    public int X       { get; init; }
    public int Y       { get; init; }
    public int Width   { get; init; } = 32;
    public int Height  { get; init; } = 32;
}
