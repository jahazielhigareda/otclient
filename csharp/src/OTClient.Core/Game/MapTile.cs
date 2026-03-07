using MoonSharp.Interpreter;
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
/// Task 8.10 / T40.
[MoonSharpUserData]
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

    /// <summary>
    /// Returns <c>true</c> when no item on this tile has the NotPathable flag.
    /// Used by pathfinding to distinguish between tiles that creatures can walk
    /// on but that the auto-walk algorithm should route around.
    /// Maps to <c>Tile::isPathable()</c>.
    /// Task T07.
    /// </summary>
    public bool IsPathable
        => !_items.Any(i => i.ThingType?.IsNotPathable == true)
        && (_ground?.ThingType?.IsNotPathable != true);

    /// <summary>
    /// Returns <c>true</c> when at least one creature occupies this tile.
    /// Maps to <c>Tile::hasCreatures()</c>.
    /// Task T07.
    /// </summary>
    public bool HasCreatures => _creatures.Count > 0;

    /// <summary>
    /// Returns the ground-tile movement speed (ms per tile).
    /// Defaults to 150 when there is no ground.
    /// Maps to <c>Tile::getGroundSpeed()</c>.
    /// Task T07.
    /// </summary>
    public int GetGroundSpeed()
        => _ground?.ThingType?.GroundSpeed ?? 150;

    // ─── Light ────────────────────────────────────────────────────────────────

    /// <summary>Maximum light level emitted from items on this tile.</summary>
    public int LightLevel
        => _items.Concat<Thing>(new[] { _ground! }.Where(x => x is not null))
                 .Max(t => t.ThingType?.LightLevel ?? 0);

    // ─── Sight / coverage (T25) ───────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when no item on this tile blocks projectiles
    /// (i.e. the tile allows line-of-sight through it).
    /// Maps to <c>Tile::isLookPossible()</c> in <c>src/client/tile.h</c>.
    /// Task T25.
    /// </summary>
    public bool IsLookPossible
        => (_ground?.ThingType?.IsBlockProjectile != true)
        && !_items.Any(i => i.ThingType?.IsBlockProjectile == true);

    /// <summary>
    /// Returns <c>true</c> when the tile is fully opaque (blocks top-down view).
    /// A tile is fully opaque when its ground is a full-ground tile or any thing
    /// on it has the opaque flag set.
    /// Maps to <c>Tile::isFullyOpaque()</c> in <c>src/client/tile.cpp</c>.
    /// Task T25.
    /// </summary>
    public bool IsFullyOpaque
        => (_ground?.ThingType?.IsFullGround == true)
        || (_ground?.ThingType?.IsOpaque == true)
        || _items.Any(i => i.ThingType?.IsOpaque == true);

    /// <summary>
    /// Returns <c>true</c> when the ground is a "top-ground" tile (covers the
    /// floor below when viewed from above).
    /// Maps to <c>Tile::hasTopGround()</c> in <c>src/client/tile.cpp</c>.
    /// Task T25.
    /// </summary>
    public bool HasTopGround
        => _ground?.ThingType?.IsGround == true && _ground.ThingType.IsFullGround;

    /// <summary>
    /// Total number of things on this tile (ground + items + creatures + effects).
    /// Used by <see cref="Map.IsSightClear"/> to detect tiles blocking cross-floor LoS.
    /// Maps to <c>Tile::getThingCount()</c>. Task T25.
    /// </summary>
    public int ThingCount
        => (_ground is not null ? 1 : 0) + _items.Count + _creatures.Count + _effects.Count;

    // ─── Empty ────────────────────────────────────────────────────────────────

    public bool IsEmpty
        => _ground is null && _items.Count == 0 && _creatures.Count == 0;

    // ─── Minimap color ────────────────────────────────────────────────────────

    public Color MinimapColor
        => _ground?.ThingType?.MinimapColor ?? Color.Black;

    // ─── Lua camelCase accessors (Task T40) ───────────────────────────────────

    /// <summary>Lua: <c>tile:getPosition()</c> — returns a Lua table {x, y, z}.</summary>
    public Table getPosition()
    {
        var t = new Table(null);
        t["x"] = Position.X;
        t["y"] = Position.Y;
        t["z"] = Position.Z;
        return t;
    }

    /// <summary>Lua: <c>tile:getGround()</c></summary>
    public object? getGround() => _ground;

    /// <summary>Lua: <c>tile:getItems()</c> — returns a Lua array table of items.</summary>
    public Table getItems()
    {
        var t = new Table(null);
        for (int i = 0; i < _items.Count; i++)
            t[i + 1] = _items[i];
        return t;
    }

    /// <summary>Lua: <c>tile:getCreatures()</c> — returns a Lua array table of creatures.</summary>
    public Table getCreatures()
    {
        var t = new Table(null);
        for (int i = 0; i < _creatures.Count; i++)
            t[i + 1] = _creatures[i];
        return t;
    }

    /// <summary>
    /// Lua: <c>tile:getThings()</c> — returns all things (ground, items, creatures)
    /// as a Lua array table in rendering order.
    /// </summary>
    public Table getThings()
    {
        var t = new Table(null);
        int idx = 1;
        if (_ground is not null)  t[idx++] = _ground;
        foreach (var item in _items)    t[idx++] = item;
        foreach (var c in _creatures)   t[idx++] = c;
        return t;
    }

    /// <summary>
    /// Lua: <c>tile:getThing(stackPos)</c> — thing at the given 0-based stack position
    /// (0 = ground).  Returns nil when out of range.
    /// </summary>
    public object? getThing(int stackPos) => GetThingAtStack(stackPos);

    /// <summary>
    /// Lua: <c>tile:getThingStackPos(thing)</c> — 0-based stack position of the given
    /// thing, or –1 if not found.
    /// </summary>
    public int getThingStackPos(Thing thing)
    {
        if (ReferenceEquals(thing, _ground)) return 0;
        int offset = _ground is not null ? 1 : 0;
        if (thing is Item item2)
        {
            int idx = _items.IndexOf(item2);
            if (idx >= 0) return offset + idx;
        }
        if (thing is Creature c)
        {
            int ci = _creatures.IndexOf(c);
            if (ci >= 0) return offset + _items.Count + ci;
        }
        return -1;
    }

    /// <summary>Lua: <c>tile:getThingCount()</c></summary>
    public int getThingCount() => ThingCount;

    /// <summary>
    /// Lua: <c>tile:getTopThing()</c> — topmost thing (creatures first, then
    /// top item, then ground).  Returns nil when tile is empty.
    /// </summary>
    public object? getTopThing()
    {
        if (_creatures.Count > 0) return _creatures[^1];
        if (_items.Count    > 0) return _items[^1];
        return _ground;
    }

    /// <summary>Lua: <c>tile:getTopLookThing()</c> — topmost thing a player can look at.</summary>
    public object? getTopLookThing()
    {
        if (_creatures.Count > 0) return _creatures[^1];
        for (int i = _items.Count - 1; i >= 0; i--)
            if (_items[i].IsPickupable || _items[i].IsNotWalkable)
                return _items[i];
        return _ground;
    }

    /// <summary>Lua: <c>tile:getTopUseThing()</c> — topmost item that can be used.</summary>
    public object? getTopUseThing()
    {
        for (int i = _items.Count - 1; i >= 0; i--)
            if (_items[i].IsPickupable || _items[i].IsNotWalkable)
                return _items[i];
        return _ground;
    }

    /// <summary>Lua: <c>tile:getTopCreature()</c> — topmost creature or nil.</summary>
    public object? getTopCreature()
        => _creatures.Count > 0 ? _creatures[^1] : null;

    /// <summary>Lua: <c>tile:getTopMoveThing()</c> — topmost moveable item.</summary>
    public object? getTopMoveThing()
    {
        for (int i = _items.Count - 1; i >= 0; i--)
            if (_items[i].IsPickupable)
                return _items[i];
        return null;
    }

    /// <summary>Lua: <c>tile:isWalkable()</c></summary>
    public bool isWalkable() => IsWalkable;

    /// <summary>Lua: <c>tile:isPathable()</c></summary>
    public bool isPathable() => IsPathable;

    /// <summary>Lua: <c>tile:isLookPossible()</c></summary>
    public bool isLookPossible() => IsLookPossible;

    /// <summary>Lua: <c>tile:isFullyOpaque()</c></summary>
    public bool isFullyOpaque() => IsFullyOpaque;

    /// <summary>Lua: <c>tile:isFullGround()</c></summary>
    public bool isFullGround() => _ground?.ThingType?.IsFullGround ?? false;

    /// <summary>Lua: <c>tile:isEmpty()</c></summary>
    public bool isEmpty() => IsEmpty;

    /// <summary>Lua: <c>tile:hasCreatures()</c></summary>
    public bool hasCreatures() => HasCreatures;

    /// <summary>Lua: <c>tile:clean()</c></summary>
    public void clean() => Clear();
}

// ─── Pathfinding result / flags (T07) ────────────────────────────────────────

/// <summary>
/// Outcome returned by <see cref="Map.FindPath"/>.
/// Maps to <c>Otc::PathFindResult</c> in <c>src/client/const.h</c>.
/// Task T07.
/// </summary>
public enum PathFindResult : byte
{
    Ok             = 0,
    SamePosition   = 1,
    Impossible     = 2,
    TooFar         = 3,
    NoWay          = 4,
}

/// <summary>
/// Control flags that modify pathfinding behaviour.
/// Maps to the <c>Otc::PathFind*</c> constants in <c>src/client/const.h</c>.
/// Task T07.
/// </summary>
[Flags]
public enum PathFindFlags : int
{
    None                = 0,
    AllowNotSeenTiles   = 1,
    AllowCreatures      = 2,
    AllowNonPathable    = 4,
    AllowNonWalkable    = 8,
    IgnoreCreatures     = 16,
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

    // ─── Pathfinding (T07) ────────────────────────────────────────────────────

    /// <summary>
    /// Computes a walking path from <paramref name="start"/> to
    /// <paramref name="goal"/> using the same weighted-Dijkstra algorithm as
    /// <c>Map::findPath</c> in <c>src/client/map.cpp</c>.
    /// </summary>
    /// <param name="start">Starting world position.</param>
    /// <param name="goal">Target world position.</param>
    /// <param name="maxComplexity">
    /// Maximum number of nodes explored before giving up
    /// (<c>PathFindResultTooFar</c>).  Typical value: 3500 (C++ default).
    /// </param>
    /// <param name="flags">Bit-set controlling walkability rules.</param>
    /// <returns>
    /// A tuple of (directions, result) where <c>directions</c> is the ordered
    /// list of steps from <paramref name="start"/> to <paramref name="goal"/>
    /// and <c>result</c> describes the outcome.  On failure,
    /// <c>directions</c> is empty.
    /// </returns>
    /// <remarks>
    /// The algorithm is an A*-style priority-queue search where the priority
    /// key is <c>g + h</c> (accumulated tile-speed cost + Chebyshev heuristic).
    /// Diagonal steps cost 3× the walk-factor; cardinal steps cost 1×.
    /// </remarks>
    /// Task T07.
    public (IReadOnlyList<Direction> Directions, PathFindResult Result) FindPath(
        Position start, Position goal,
        int maxComplexity = 3500,
        PathFindFlags flags = PathFindFlags.None)
    {
        if (start == goal)
            return ([], PathFindResult.SamePosition);

        if (start.Z != goal.Z)
            return ([], PathFindResult.Impossible);

        // Verify goal walkability
        var goalTile = Get(goal);
        bool ignoreCreatures = (flags & PathFindFlags.IgnoreCreatures) != 0;
        if (goalTile is not null && (flags & PathFindFlags.AllowNonWalkable) == 0)
        {
            if (!goalTile.IsWalkable)
                return ([], PathFindResult.NoWay);
        }

        // ── A* (weighted Dijkstra) ─────────────────────────────────────────────
        var nodes    = new Dictionary<Position, SearchNode>();
        var queue    = new PriorityQueue<SearchNode, float>();

        var root = new SearchNode(start);
        nodes[start] = root;
        queue.Enqueue(root, 0f);

        SearchNode? foundNode = null;

        while (queue.Count > 0)
        {
            if (nodes.Count > maxComplexity)
                return ([], PathFindResult.TooFar);

            var current = queue.Dequeue();

            // Already found a cheaper path to goal — done
            if (foundNode is not null && current.TotalCost >= foundNode.Cost)
                break;

            // Check for arrival
            if (current.Pos == goal && (foundNode is null || current.Cost < foundNode.Cost))
                foundNode = current;

            // Expand 8 neighbours
            for (int di = -1; di <= 1; di++)
            for (int dj = -1; dj <= 1; dj++)
            {
                if (di == 0 && dj == 0) continue;

                var npos = new Position(current.Pos.X + di, current.Pos.Y + dj, start.Z);
                if (npos.X < 0 || npos.Y < 0) continue;

                bool wasSeen        = false;
                bool hasCreature    = false;
                bool isNotWalkable  = false;   // unknown tiles: assume passable
                bool isNotPathable  = false;
                int  speed          = 100;

                var ntile = Get(npos);
                if (ntile is not null)
                {
                    wasSeen       = true;
                    hasCreature   = ntile.HasCreatures && !ignoreCreatures;
                    isNotWalkable = !ntile.IsWalkable;
                    isNotPathable = !ntile.IsPathable;
                    speed         = ntile.GetGroundSpeed();
                }

                bool isGoal = npos == goal;

                if (!isGoal)
                {
                    if ((flags & PathFindFlags.AllowNotSeenTiles) == 0 && !wasSeen)
                        continue;
                    if (wasSeen)
                    {
                        if ((flags & PathFindFlags.AllowCreatures) == 0 && hasCreature)
                            continue;
                        if ((flags & PathFindFlags.AllowNonPathable) == 0 && isNotPathable)
                            continue;
                        if ((flags & PathFindFlags.AllowNonWalkable) == 0 && isNotWalkable)
                            continue;
                    }
                }
                else
                {
                    if ((flags & PathFindFlags.AllowNotSeenTiles) == 0 && !wasSeen)
                        continue;
                    if (wasSeen && (flags & PathFindFlags.AllowNonWalkable) == 0 && isNotWalkable)
                        continue;
                }

                bool diagonal  = di != 0 && dj != 0;
                float walkFactor = diagonal ? 3.0f : 1.0f;
                float newCost  = current.Cost + (speed * walkFactor) / 100.0f;

                if (nodes.TryGetValue(npos, out var existing))
                {
                    if (existing.Cost <= newCost)
                        continue;
                    existing.Cost      = newCost;
                    existing.TotalCost = newCost + npos.ChebyshevDistance(goal);
                    existing.Prev      = current;
                    existing.Dir       = current.Pos.DirectionTo(npos);
                    queue.Enqueue(existing, existing.TotalCost);
                }
                else
                {
                    var nn = new SearchNode(npos)
                    {
                        Cost      = newCost,
                        TotalCost = newCost + npos.ChebyshevDistance(goal),
                        Prev      = current,
                        Dir       = current.Pos.DirectionTo(npos),
                    };
                    nodes[npos] = nn;
                    queue.Enqueue(nn, nn.TotalCost);
                }
            }
        }

        if (foundNode is null)
            return ([], PathFindResult.NoWay);

        // Reconstruct path (reverse)
        var dirs = new List<Direction>();
        var node = foundNode;
        while (node.Prev is not null)
        {
            dirs.Add(node.Dir);
            node = node.Prev;
        }
        dirs.Reverse();
        return (dirs, PathFindResult.Ok);
    }

    /// <summary>
    /// Asynchronous wrapper around <see cref="FindPath"/>.
    /// Runs the search on the thread pool and returns a <see cref="Task{T}"/>
    /// with the result.
    /// Maps to <c>Map::findPathAsync</c>.
    /// Task T07.
    /// </summary>
    public Task<(IReadOnlyList<Direction> Directions, PathFindResult Result)> FindPathAsync(
        Position start, Position goal,
        int maxComplexity = 3500,
        PathFindFlags flags = PathFindFlags.None,
        System.Threading.CancellationToken cancellationToken = default)
        => Task.Run(() => FindPath(start, goal, maxComplexity, flags), cancellationToken);

    // ── Internal: search node for FindPath ────────────────────────────────────

    private sealed class SearchNode(Position pos)
    {
        public Position    Pos       { get; } = pos;
        public float       Cost      { get; set; } = 0f;
        public float       TotalCost { get; set; } = 0f;
        public SearchNode? Prev      { get; set; }
        public Direction   Dir       { get; set; }
    }

    // ─── Aware floor helpers (T24) ────────────────────────────────────────────

    private const int MapSeaFloor              = 7;
    private const int MapMaxZ                  = 15;
    private const int MapAwareUndergroundRange = 2;

    private int GetFirstAwareFloor(int z)
        => z <= MapSeaFloor ? 0 : z - MapAwareUndergroundRange;

    private int GetLastAwareFloor(int z)
        => z <= MapSeaFloor ? MapSeaFloor
                            : Math.Min(z + MapAwareUndergroundRange, MapMaxZ);

    // ─── Spectator queries (T24) ──────────────────────────────────────────────

    /// <summary>
    /// Returns all known creatures within [minXRange,maxXRange] × [minYRange,maxYRange]
    /// around <paramref name="center"/>, optionally spanning multiple floors.
    /// Maps to <c>Map::getSpectatorsInRangeEx</c>.
    /// Task T24.
    /// </summary>
    private List<Creature> GetSpectatorsInRangeEx(
        Position center, bool multiFloor,
        int minXRange, int maxXRange,
        int minYRange, int maxYRange)
    {
        int startZ = multiFloor ? GetFirstAwareFloor(center.Z) : center.Z;
        int endZ   = multiFloor ? GetLastAwareFloor(center.Z)  : center.Z;

        int startY = center.Y - minYRange;
        int endY   = center.Y + maxYRange;
        int startX = center.X - minXRange;
        int endX   = center.X + maxXRange;

        var result  = new List<Creature>();
        var seenIds = new HashSet<uint>();

        for (int z = startZ; z <= endZ; z++)
        for (int y = startY; y <= endY; y++)
        for (int x = startX; x <= endX; x++)
        {
            var tile = Get(new Position(x, y, z));
            if (tile is null) continue;
            foreach (var c in tile.Creatures)
            {
                if (seenIds.Add(c.Id))
                    result.Add(c);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns all known creatures in the full aware range around
    /// <paramref name="center"/>.
    /// Maps to <c>Map::getSpectators</c>.
    /// Task T24.
    /// </summary>
    public IReadOnlyList<Creature> GetSpectators(Position center, bool multiFloor = false)
        => GetSpectatorsInRangeEx(center, multiFloor,
                AwareRange.Left,   AwareRange.Right,
                AwareRange.Top,    AwareRange.Bottom);

    /// <summary>
    /// Returns known creatures visible within the sight-spectator range.
    /// The ranges are asymmetric (<c>Left−1, Right−2, Top−1, Bottom−2</c>),
    /// matching the asymmetric viewport shape and the exact values used by the
    /// C++ <c>Map::getSightSpectators</c> implementation.
    /// Task T24.
    /// </summary>
    public IReadOnlyList<Creature> GetSightSpectators(Position center, bool multiFloor = false)
        => GetSpectatorsInRangeEx(center, multiFloor,
                // Asymmetric margin mirrors C++ Map::getSightSpectators:
                // left−1, right−2, top−1, bottom−2 (viewport is 18×14, not square).
                AwareRange.Left   - 1, AwareRange.Right  - 2,
                AwareRange.Top    - 1, AwareRange.Bottom - 2);

    /// <summary>
    /// Returns all known creatures within a symmetric
    /// <paramref name="xRange"/> × <paramref name="yRange"/> range.
    /// Maps to <c>Map::getSpectatorsInRange</c>.
    /// Task T24.
    /// </summary>
    public IReadOnlyList<Creature> GetSpectatorsInRange(
        Position center, bool multiFloor, int xRange, int yRange)
        => GetSpectatorsInRangeEx(center, multiFloor, xRange, xRange, yRange, yRange);

    // ─── Sight / coverage checks (T25) ────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the tile at <paramref name="pos"/> is visually
    /// covered by an opaque tile on a higher floor.
    /// Maps to <c>Map::isCovered</c> in <c>src/client/map.cpp</c>.
    /// Task T25.
    /// </summary>
    public bool IsCovered(Position pos, int firstFloor = 0)
    {
        var tilePos = pos;
        while (true)
        {
            var covered = tilePos.CoveredUp();
            if (covered == tilePos || covered.Z < firstFloor) break;
            tilePos = covered;

            var above = Get(tilePos);
            if (above is not null && above.IsFullyOpaque)
                return true;

            var diag = Get(tilePos.Offset(1, 1));
            if (diag is not null && diag.HasTopGround)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the line of sight between <paramref name="fromPos"/>
    /// and <paramref name="toPos"/> is unobstructed.
    /// Uses the same Bresenham-style algorithm as <c>Map::isSightClear</c> in
    /// <c>src/client/map.cpp</c>.
    /// Task T25.
    /// </summary>
    public bool IsSightClear(Position fromPos, Position toPos)
    {
        if (fromPos == toPos) return true;

        // Walk on the shallower-z end first (lower z = higher floor)
        var start       = fromPos.Z > toPos.Z ? toPos   : fromPos;
        var destination = fromPos.Z > toPos.Z ? fromPos : toPos;

        int mx = start.X < destination.X ? 1 : start.X == destination.X ? 0 : -1;
        int my = start.Y < destination.Y ? 1 : start.Y == destination.Y ? 0 : -1;

        long A = destination.Y - start.Y;
        long B = start.X - destination.X;
        long C = -(A * destination.X + B * destination.Y);

        // Walk the XY path on the same floor
        var cur = start;
        while (cur.X != destination.X || cur.Y != destination.Y)
        {
            long moveHor   = Math.Abs(A * (cur.X + mx) + B * cur.Y         + C);
            long moveVer   = Math.Abs(A * cur.X         + B * (cur.Y + my) + C);
            long moveCross = Math.Abs(A * (cur.X + mx)  + B * (cur.Y + my) + C);

            int nx = cur.X, ny = cur.Y;

            if (cur.Y != destination.Y && (cur.X == destination.X || moveHor > moveVer || moveHor > moveCross))
                ny += my;

            if (cur.X != destination.X && (cur.Y == destination.Y || moveVer > moveHor || moveVer > moveCross))
                nx += mx;

            cur = new Position(nx, ny, cur.Z);
            var tile = Get(cur);
            if (tile is not null && !tile.IsLookPossible)
                return false;
        }

        // Walk floor levels (z axis)
        while (cur.Z != destination.Z)
        {
            var tile = Get(cur);
            if (tile is not null && tile.ThingCount > 0)
                return false;
            cur = cur with { Z = cur.Z + 1 };
        }

        return true;
    }

    // ─── Map-level overlay collections (T17) ─────────────────────────────────

    private readonly List<AnimatedText> _animatedTexts = [];
    private readonly List<StaticText>   _staticTexts   = [];
    private readonly List<Missile>      _missiles      = [];

    /// <summary>All live floating damage/XP numbers.</summary>
    public IReadOnlyList<AnimatedText> AnimatedTexts => _animatedTexts;

    /// <summary>All live creature name / status labels.</summary>
    public IReadOnlyList<StaticText> StaticTexts => _staticTexts;

    /// <summary>All in-flight missile projectiles.</summary>
    public IReadOnlyList<Missile> Missiles => _missiles;

    /// <summary>Registers a new animated text on the map.</summary>
    public void AddAnimatedText(AnimatedText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _animatedTexts.Add(text);
    }

    /// <summary>Registers a new static label on the map.</summary>
    public void AddStaticText(StaticText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _staticTexts.Add(text);
    }

    /// <summary>Registers a new missile on the map.</summary>
    public void AddMissile(Missile missile)
    {
        ArgumentNullException.ThrowIfNull(missile);
        _missiles.Add(missile);
    }

    /// <summary>Removes expired animated texts.</summary>
    public void PruneAnimatedTexts() => _animatedTexts.RemoveAll(t => t.IsExpired);

    /// <summary>Removes expired static texts.</summary>
    public void PruneStaticTexts() => _staticTexts.RemoveAll(t => t.IsExpired);

    /// <summary>Removes finished missiles.</summary>
    public void PruneMissiles() => _missiles.RemoveAll(m => m.IsFinished);

    /// <summary>Clears all overlay collections.</summary>
    public void ClearOverlays()
    {
        _animatedTexts.Clear();
        _staticTexts.Clear();
        _missiles.Clear();
    }
}
