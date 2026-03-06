using Raylib_cs;

namespace OTClient.Framework.Game;

// ─── Tile ─────────────────────────────────────────────────────────────────────

/// <summary>
/// A single cell on the game map.  Holds a sorted stack of <see cref="Thing"/>
/// objects (ground, ground-border, on-bottom items, creatures, on-top items).
/// </summary>
/// <remarks>
/// Stack ordering (back-to-front for rendering):
/// 1. Ground
/// 2. Ground-border items
/// 3. On-bottom items
/// 4. Creatures
/// 5. Items (regular)
/// 6. On-top items
/// 7. Effects
/// </remarks>
/// Maps to <c>src/client/tile.h</c>.
/// Task 8.10.
public sealed class Tile
{
    public Position Position { get; }

    public Tile(Position pos) => Position = pos;

    // ─── Things ───────────────────────────────────────────────────────────────

    private Item?         _ground;
    private readonly List<Item>     _items     = [];
    private readonly List<Creature> _creatures = [];
    private readonly List<Effect>   _effects   = [];

    public Item?         Ground    => _ground;
    public IReadOnlyList<Item>     Items     => _items;
    public IReadOnlyList<Creature> Creatures => _creatures;
    public IReadOnlyList<Effect>   Effects   => _effects;

    // ─── Ground ───────────────────────────────────────────────────────────────

    public void SetGround(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _ground = item;
    }

    // ─── Items ────────────────────────────────────────────────────────────────

    public void AddItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    public bool RemoveItem(Item item) => _items.Remove(item);

    public void ClearItems() => _items.Clear();

    /// <summary>Returns the top-most non-ground item, or <c>null</c>.</summary>
    public Item? TopItem => _items.Count > 0 ? _items[^1] : null;

    // ─── Creatures ────────────────────────────────────────────────────────────

    public void AddCreature(Creature c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (!_creatures.Contains(c))
            _creatures.Add(c);
    }

    public bool RemoveCreature(Creature c) => _creatures.Remove(c);

    // ─── Effects ──────────────────────────────────────────────────────────────

    public void AddEffect(Effect e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _effects.Add(e);
    }

    public void PruneEffects() => _effects.RemoveAll(e => e.IsFinished);

    // ─── Walkability ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when no thing on this tile blocks movement.
    /// A tile is walkable when it has a ground and no not-walkable items.
    /// </summary>
    public bool IsWalkable
        => _ground is not null
        && !_items.Any(i => i.IsNotWalkable)
        && !(_ground.IsNotWalkable);

    // ─── Light ────────────────────────────────────────────────────────────────

    /// <summary>Maximum light level emitted from items on this tile.</summary>
    public int LightLevel
        => _items.Concat<Thing>(new[] { _ground! }.Where(x => x is not null))
                 .Max(t => t.ThingType?.LightLevel ?? 0);

    // ─── Empty ────────────────────────────────────────────────────────────────

    public bool IsEmpty
        => _ground is null && _items.Count == 0 && _creatures.Count == 0;

    // ─── Minimap color ────────────────────────────────────────────────────────

    public Color MinimapColor
        => _ground?.ThingType?.MinimapColor ?? Color.Black;
}

// ─── Map ──────────────────────────────────────────────────────────────────────

/// <summary>
/// The 3-D tile grid (x, y, z) covering all 16 floors.
/// Tiles are stored on-demand: only known tiles are kept in memory.
/// Maps to <c>src/client/map.h</c>.
/// Task 8.11.
/// </summary>
public sealed class Map
{
    // ─── Tile storage ─────────────────────────────────────────────────────────

    private readonly Dictionary<Position, Tile> _tiles = [];

    /// <summary>Number of tiles currently known.</summary>
    public int TileCount => _tiles.Count;

    // ─── Tile access ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tile at <paramref name="pos"/>, creating an empty one if
    /// it does not exist yet.
    /// </summary>
    public Tile GetOrCreate(Position pos)
    {
        if (!_tiles.TryGetValue(pos, out var tile))
        {
            tile = new Tile(pos);
            _tiles[pos] = tile;
        }
        return tile;
    }

    /// <summary>Returns the tile at <paramref name="pos"/> or <c>null</c>.</summary>
    public Tile? Get(Position pos)
        => _tiles.TryGetValue(pos, out var tile) ? tile : null;

    /// <summary>Removes a tile from the grid.</summary>
    public bool Remove(Position pos) => _tiles.Remove(pos);

    /// <summary>Clears all tiles.</summary>
    public void Clear() => _tiles.Clear();

    // ─── Creature tracking ────────────────────────────────────────────────────

    private readonly Dictionary<uint, Creature> _knownCreatures = [];

    public void AddCreature(Creature c)
    {
        ArgumentNullException.ThrowIfNull(c);
        _knownCreatures[c.Id] = c;
        GetOrCreate(c.Position).AddCreature(c);
    }

    public void RemoveCreature(uint id)
    {
        if (!_knownCreatures.TryGetValue(id, out var c)) return;
        Get(c.Position)?.RemoveCreature(c);
        _knownCreatures.Remove(id);
    }

    public Creature? GetCreature(uint id)
        => _knownCreatures.TryGetValue(id, out var c) ? c : null;

    public IReadOnlyDictionary<uint, Creature> KnownCreatures => _knownCreatures;

    // ─── Visibility helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Returns all tiles in the rectangular viewport on the given floor.
    /// </summary>
    public IEnumerable<Tile> GetViewport(int x, int y, int z, int width, int height)
    {
        for (int row = y; row < y + height; row++)
        for (int col = x; col < x + width;  col++)
        {
            var t = Get(new Position(col, row, z));
            if (t is not null) yield return t;
        }
    }
}
