using Raylib_cs;

namespace OTClient.Framework.Game;

// ─── AwareRange ───────────────────────────────────────────────────────────────

/// <summary>
/// Defines how many tiles around the central position the server streams to
/// the client in each direction.
/// Maps to <c>AwareRange</c> in <c>src/client/staticdata.h</c>.
/// Task T01.
/// </summary>
public readonly record struct AwareRange(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Width of the streamed area in tiles (Left + Right + 1).</summary>
    public int Horizontal => Left + Right + 1;

    /// <summary>Height of the streamed area in tiles (Top + Bottom + 1).</summary>
    public int Vertical => Top + Bottom + 1;

    /// <summary>
    /// Default aware range matching the C++ game config defaults:
    /// mapViewPort = {8, 6}, so left=8, top=6, right=9 (8+1), bottom=7 (6+1).
    /// Horizontal = 18, Vertical = 14.
    /// </summary>
    public static readonly AwareRange Default = new(Left: 8, Top: 6, Right: 9, Bottom: 7);
}

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

    /// <summary>
    /// Returns the thing at wire <paramref name="stackPos"/> from this tile's
    /// virtual stack: position 0 = ground, then items in order, then creatures.
    /// Returns <c>null</c> when the index is out of range.
    /// </summary>
    public Thing? GetThingAtStack(int stackPos)
    {
        if (stackPos == 0 && _ground is not null) return _ground;
        int idx = stackPos - (_ground is not null ? 1 : 0);
        if (idx >= 0 && idx < _items.Count) return _items[idx];
        idx -= _items.Count;
        if (idx >= 0 && idx < _creatures.Count) return _creatures[idx];
        return null;
    }

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

    // ─── Protocol stack operations (T01/T02) ─────────────────────────────────

    /// <summary>
    /// Adds <paramref name="thing"/> to the tile at wire <paramref name="stackPos"/>.
    /// <list type="bullet">
    ///   <item><description>Ground items (stackPos 0, IsGround flag) go in the ground slot.</description></item>
    ///   <item><description>Creatures are added to the creature list.</description></item>
    ///   <item><description>All other items are appended to <see cref="Items"/>.</description></item>
    /// </list>
    /// </summary>
    public void AddThing(Thing thing, int stackPos)
    {
        switch (thing)
        {
            case Creature c:
                AddCreature(c);
                break;
            case Item item when item.ThingType?.IsGround == true || stackPos == 0:
                SetGround(item);
                break;
            case Item item:
                // Insert at stackPos to keep order; clamp to list bounds
                int idx = Math.Clamp(stackPos, 0, _items.Count);
                _items.Insert(idx, item);
                break;
        }
    }

    /// <summary>
    /// Removes <paramref name="thing"/> from whichever sub-list it belongs to.
    /// </summary>
    public bool RemoveThing(Thing thing)
    {
        switch (thing)
        {
            case Creature c:
                return RemoveCreature(c);
            case Item item when ReferenceEquals(item, _ground):
                _ground = null;
                return true;
            case Item item:
                return _items.Remove(item);
            default:
                return false;
        }
    }

    /// <summary>
    /// Removes all items (ground + item stack) from this tile but keeps its
    /// creature list.  Mirrors <c>g_map.cleanTile()</c>.
    /// </summary>
    public void Clear()
    {
        _ground = null;
        _items.Clear();
        _effects.Clear();
    }

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

    /// <summary>
    /// Moves a known creature to <paramref name="newPos"/>, updating the tile
    /// it occupies on both the old and new tile.
    /// </summary>
    public bool MoveCreature(uint id, Position newPos)
    {
        if (!_knownCreatures.TryGetValue(id, out var creature)) return false;
        Get(creature.Position)?.RemoveCreature(creature);
        creature.Position = newPos;
        GetOrCreate(newPos).AddCreature(creature);
        return true;
    }

    public IReadOnlyDictionary<uint, Creature> KnownCreatures => _knownCreatures;

    // ─── Map navigation state (T01) ───────────────────────────────────────────

    /// <summary>
    /// The position of the tile at the centre of the current view (the player's
    /// position on the default 18×14 viewport).
    /// Mirrors <c>g_map.getCentralPosition()</c>.
    /// </summary>
    public Position CentralPosition { get; set; } = Position.Invalid;

    /// <summary>
    /// How many tiles the server streams around <see cref="CentralPosition"/>
    /// in each direction.  Defaults to the C++ <c>gameConfig.mapViewPort {8,6}</c>
    /// values: Left=8, Top=6, Right=9, Bottom=7 (18 wide × 14 tall).
    /// </summary>
    public AwareRange AwareRange { get; set; } = AwareRange.Default;

    // ─── Thing-level map operations (T01/T02) ────────────────────────────────

    /// <summary>
    /// Adds <paramref name="thing"/> to the tile at <paramref name="pos"/> at
    /// wire stack position <paramref name="stackPos"/>.  Creatures are also
    /// registered in the known-creature table.
    /// Mirrors <c>g_map.addThing(thing, pos, stackPos)</c>.
    /// </summary>
    public void AddThing(Thing thing, Position pos, int stackPos)
    {
        ArgumentNullException.ThrowIfNull(thing);
        thing.Position = pos;
        var tile = GetOrCreate(pos);
        tile.AddThing(thing, stackPos);

        if (thing is Creature creature)
            _knownCreatures[creature.Id] = creature;
    }

    /// <summary>
    /// Removes <paramref name="thing"/> from whichever tile it currently occupies.
    /// Creatures are also removed from the known-creature table.
    /// Mirrors <c>g_map.removeThing(thing)</c>.
    /// </summary>
    public bool RemoveThing(Thing thing)
    {
        ArgumentNullException.ThrowIfNull(thing);
        var tile = Get(thing.Position);
        bool removed = tile?.RemoveThing(thing) ?? false;

        if (removed && thing is Creature creature)
            _knownCreatures.Remove(creature.Id);

        return removed;
    }

    /// <summary>
    /// Clears all non-creature things from the tile at <paramref name="pos"/>.
    /// If the tile does not exist, it is created (empty).
    /// Mirrors <c>g_map.cleanTile(pos)</c>.
    /// </summary>
    public void CleanTile(Position pos) => GetOrCreate(pos).Clear();

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
