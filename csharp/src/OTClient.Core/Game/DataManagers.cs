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
/// Tracks which equipment items are visually equipped in each slot.
/// Maps to <c>src/client/paperdoll.h</c>.
/// Task 8.25.
/// </summary>
public sealed class PaperDoll
{
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

    public void Equip(Slot slot, Item? item) => _slots[slot] = item;
    public Item? GetSlot(Slot slot) => _slots.TryGetValue(slot, out var i) ? i : null;
    public bool  IsSlotEmpty(Slot slot) => GetSlot(slot) is null;
    public void  Clear() => _slots.Clear();
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
