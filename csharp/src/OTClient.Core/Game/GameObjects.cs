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

// ─── InventorySlot ────────────────────────────────────────────────────────────

/// <summary>
/// Equipment slots on the player's paper-doll.  Wire values match the C++
/// <c>Otc::InventorySlot</c> enum (1-based).
/// Task T04.
/// </summary>
public enum InventorySlot : byte
{
    Head      = 1,
    Necklace  = 2,
    Backpack  = 3,
    Armor     = 4,
    Right     = 5,
    Left      = 6,
    Legs      = 7,
    Feet      = 8,
    Ring      = 9,
    Ammo      = 10,
    Purse     = 11,
    Ext1      = 12,
    Ext2      = 13,
    Ext3      = 14,
    Ext4      = 15,
    /// <summary>Sentinel value — not a real slot; equals the total slot count + 1.</summary>
    MaxValue  = 16,
}

// ─── Container ────────────────────────────────────────────────────────────────

/// <summary>
/// An ordered collection of <see cref="Item"/> objects (backpack / container item).
/// Mirrors <c>src/client/container.h</c>.
/// Task T08.
/// </summary>
public sealed class Container
{
    /// <summary>Wire container ID (0–63).</summary>
    public int    Id           { get; init; }

    /// <summary>Display name shown in the container window title.</summary>
    public string Name         { get; init; } = string.Empty;

    /// <summary>Maximum number of items this container can hold.</summary>
    public int    Capacity     { get; init; } = 20;

    /// <summary>
    /// The item that represents the container itself (e.g. a backpack item).
    /// Mirrors <c>m_containerItem</c>.
    /// </summary>
    public Item?  ContainerItem { get; init; }

    /// <summary>
    /// <c>true</c> when this container was opened from inside another container.
    /// Mirrors <c>m_hasParent</c>.
    /// </summary>
    public bool HasParent    { get; init; }

    /// <summary>
    /// <c>true</c> when items can be dragged into/out of this container.
    /// Mirrors <c>m_unlocked</c> (GameContainerPagination).
    /// </summary>
    public bool IsUnlocked   { get; init; } = true;

    /// <summary>
    /// <c>true</c> when the container supports pagination (large bags).
    /// Mirrors <c>m_hasPages</c>.
    /// </summary>
    public bool HasPages     { get; init; }

    /// <summary>
    /// Total number of slots in the container (may exceed <see cref="Capacity"/>
    /// for paginated bags).  Mirrors <c>m_size</c>.
    /// </summary>
    public int  Size         { get; init; }

    /// <summary>
    /// First visible slot index (non-zero for paginated bags scrolled down).
    /// Mirrors <c>m_firstIndex</c>.
    /// </summary>
    public int  FirstIndex   { get; init; }

    /// <summary><c>true</c> when the container has been closed by the server.</summary>
    public bool IsClosed     { get; private set; }

    // ─── Contents ─────────────────────────────────────────────────────────────

    private readonly List<Item> _contents = [];

    /// <summary>The ordered list of items currently in this container.</summary>
    public  IReadOnlyList<Item> Contents  => _contents;

    /// <summary>Number of items currently in this container.</summary>
    public  int                 Count     => _contents.Count;

    public  bool IsFull  => _contents.Count >= Capacity;
    public  bool IsEmpty => _contents.Count == 0;

    // ─── Mutation methods ─────────────────────────────────────────────────────

    /// <summary>
    /// Appends all items in <paramref name="items"/> to the container
    /// (used when the server sends the initial item list in <c>OpenContainer</c>).
    /// </summary>
    public void AddItems(IEnumerable<Item> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
            _contents.Add(item);
    }

    /// <summary>
    /// Inserts <paramref name="item"/> at position <paramref name="slot"/>,
    /// clamping to the end if <paramref name="slot"/> is out of range.
    /// Mirrors <c>Container::onAddItem</c> with the paginated slot.
    /// </summary>
    public void AddItem(Item item, int slot = -1)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (slot < 0 || slot >= _contents.Count)
            _contents.Add(item);
        else
            _contents.Insert(slot, item);
    }

    /// <summary>
    /// Replaces the item at <paramref name="slot"/> with <paramref name="newItem"/>.
    /// Returns <c>true</c> on success, <c>false</c> when <paramref name="slot"/>
    /// is out of range.
    /// Mirrors <c>Container::onUpdateItem</c>.
    /// </summary>
    public bool UpdateAt(int slot, Item newItem)
    {
        ArgumentNullException.ThrowIfNull(newItem);
        if (slot < 0 || slot >= _contents.Count) return false;
        _contents[slot] = newItem;
        return true;
    }

    /// <summary>
    /// Removes the item at <paramref name="slot"/>, optionally appending
    /// <paramref name="lastItem"/> to the end (pagination: last page item
    /// moves forward after removal).
    /// Returns <c>false</c> when <paramref name="slot"/> is out of range.
    /// Mirrors <c>Container::onRemoveItem</c>.
    /// </summary>
    public bool RemoveAt(int slot, Item? lastItem = null)
    {
        if (slot < 0 || slot >= _contents.Count) return false;
        _contents.RemoveAt(slot);
        if (lastItem is not null)
            _contents.Add(lastItem);
        return true;
    }

    /// <summary>Returns the item at <paramref name="slot"/>, or <c>null</c>.</summary>
    public Item? GetAt(int slot)
        => slot >= 0 && slot < _contents.Count ? _contents[slot] : null;

    /// <summary>Marks the container as closed.</summary>
    public void Close() => IsClosed = true;
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

// ─── NpcTradeItem ─────────────────────────────────────────────────────────────

/// <summary>
/// A single entry in an NPC's trade list as received from the server.
/// Carries the item prototype, its display name, weight, and buy/sell prices.
/// Maps to the tuple elements in <c>parseOpenNpcTrade</c> /
/// <c>Game::processOpenNpcTrade</c>.
/// Task T15.
/// </summary>
public sealed record NpcTradeItem(
    Item   Item,
    string Name,
    uint   Weight,
    uint   BuyPrice,
    uint   SellPrice);

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
