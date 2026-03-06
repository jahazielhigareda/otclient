using Raylib_cs;

namespace OTClient.Framework.Game;

// ─── Minimap ──────────────────────────────────────────────────────────────────

/// <summary>
/// Records minimap colours for visited tiles and provides fast lookup.
/// Maps to <c>src/client/minimap.h</c>.
/// Task 8.18.
/// </summary>
public sealed class Minimap
{
    private readonly Dictionary<Position, Color> _colors = [];

    /// <summary>Number of recorded tiles.</summary>
    public int Count => _colors.Count;

    /// <summary>Records the colour of a tile at <paramref name="pos"/>.</summary>
    public void Record(Position pos, Color color) => _colors[pos] = color;

    /// <summary>Returns the recorded colour for <paramref name="pos"/>, or Black.</summary>
    public Color GetColor(Position pos)
        => _colors.TryGetValue(pos, out var c) ? c : Color.Black;

    /// <summary>Returns <c>true</c> when the tile has been visited.</summary>
    public bool IsKnown(Position pos) => _colors.ContainsKey(pos);

    /// <summary>Clears all recorded tiles.</summary>
    public void Clear() => _colors.Clear();

    /// <summary>Transfers tile colours from a map's visible tiles.</summary>
    public void Update(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        foreach (var (pos, tile) in map.KnownCreatures.Select(kv => (kv.Value.Position, (Tile?)null)))
            _ = pos; // creatures don't have minimap colour — handled by tile below

        // Record ground colour for each tile
        foreach (var tile in map.GetViewport(0, 0, Position.GroundFloor, int.MaxValue, int.MaxValue))
            Record(tile.Position, tile.MinimapColor);
    }
}

// ─── Container ────────────────────────────────────────────────────────────────

/// <summary>
/// An ordered collection of <see cref="Item"/> objects (backpack / container item).
/// Maps to <c>src/client/container.h</c>.
/// Task 8.22.
/// </summary>
public sealed class Container
{
    public int    Id       { get; init; }
    public string Name     { get; init; } = string.Empty;
    public int    Capacity { get; init; } = 20;
    public int    ItemId   { get; init; }    // appearance of the container itself

    private readonly List<Item> _contents = [];
    public  IReadOnlyList<Item> Contents  => _contents;
    public  int                 Count     => _contents.Count;
    public  bool                IsFull    => _contents.Count >= Capacity;
    public  bool                IsEmpty   => _contents.Count == 0;

    public bool AddItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsFull) return false;
        _contents.Add(item);
        return true;
    }

    public bool RemoveAt(int slot)
    {
        if (slot < 0 || slot >= _contents.Count) return false;
        _contents.RemoveAt(slot);
        return true;
    }

    public Item? GetAt(int slot)
        => slot >= 0 && slot < _contents.Count ? _contents[slot] : null;
}

// ─── AttachedEffect ────────────────────────────────────────────────────────────

/// <summary>
/// A visual effect attached permanently to a creature or item
/// (e.g. wings, auras, shader effects).
/// Maps to <c>src/client/attachedeffect.h</c>.
/// Task 8.20.
/// </summary>
public sealed class AttachedEffect
{
    public int      Id       { get; init; }
    public string   Name     { get; init; } = string.Empty;
    public Animator Animator { get; } = new(1);
    public bool     Permanent { get; init; } = true;
}

/// <summary>
/// Manages all <see cref="AttachedEffect"/> descriptors registered by the client.
/// Task 8.20.
/// </summary>
public sealed class AttachedEffectManager
{
    private readonly Dictionary<int, AttachedEffect> _effects = [];

    public void Register(AttachedEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _effects[effect.Id] = effect;
    }

    public AttachedEffect? Get(int id)
        => _effects.TryGetValue(id, out var e) ? e : null;

    public IEnumerable<AttachedEffect> All => _effects.Values;
}

// ─── GameConfig ────────────────────────────────────────────────────────────────

/// <summary>
/// Server-side feature flags and protocol configuration negotiated during login.
/// Maps to <c>src/client/gameconfig.h</c>.
/// Task 8.24.
/// </summary>
public sealed class GameConfig
{
    // ─── Protocol version ─────────────────────────────────────────────────────

    public int  ClientVersion  { get; set; } = 1281;
    public int  ProtocolVersion { get; set; } = 1281;

    // ─── Feature flags ────────────────────────────────────────────────────────

    public bool AttackCooldown     { get; set; }
    public bool ItemInspection     { get; set; }
    public bool Podium             { get; set; }
    public bool MarketStats        { get; set; }
    public bool HasTransparency    { get; set; }
    public bool HasContainerOpen   { get; set; }
    public bool NewProtocol        { get; set; } = true;

    // ─── Map dimensions ───────────────────────────────────────────────────────

    public int MapWidth  { get; set; } = 18;
    public int MapHeight { get; set; } = 14;
}
