using OTClient.Framework.Game;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for <see cref="Tile"/> and <see cref="Map"/>. Tasks 8.10–8.11.
/// </summary>
public sealed class TileAndMapTests
{
    private static Item MakeItem(ThingTypeFlag flags = ThingTypeFlag.Ground) =>
        new Item
        {
            Id        = 1,
            ThingType = new ThingType { Category = ThingCategory.Item, Id = 1, Flags = flags },
        };

    // ─── Tile ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NewTile_IsEmpty()
    {
        var tile = new Tile(Position.Zero);
        Assert.True(tile.IsEmpty);
    }

    [Fact]
    public void SetGround_TileNotEmpty()
    {
        var tile = new Tile(Position.Zero);
        tile.SetGround(MakeItem());
        Assert.False(tile.IsEmpty);
    }

    [Fact]
    public void Tile_WithGround_IsWalkable()
    {
        var tile = new Tile(Position.Zero);
        tile.SetGround(MakeItem(ThingTypeFlag.Ground));
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void Tile_NotWalkableItem_MakesItNonWalkable()
    {
        var tile = new Tile(Position.Zero);
        tile.SetGround(MakeItem(ThingTypeFlag.Ground));
        tile.AddItem(MakeItem(ThingTypeFlag.NotWalkable));
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void AddItem_AppearsInItems()
    {
        var tile = new Tile(Position.Zero);
        var item = MakeItem(ThingTypeFlag.None);
        tile.AddItem(item);
        Assert.Contains(item, tile.Items);
    }

    [Fact]
    public void RemoveItem_RemovesFromItems()
    {
        var tile = new Tile(Position.Zero);
        var item = MakeItem(ThingTypeFlag.None);
        tile.AddItem(item);
        tile.RemoveItem(item);
        Assert.DoesNotContain(item, tile.Items);
    }

    [Fact]
    public void TopItem_ReturnsLastAdded()
    {
        var tile = new Tile(Position.Zero);
        var i1 = MakeItem(ThingTypeFlag.None);
        var i2 = MakeItem(ThingTypeFlag.None);
        tile.AddItem(i1);
        tile.AddItem(i2);
        Assert.Same(i2, tile.TopItem);
    }

    [Fact]
    public void AddCreature_AppearsInCreatures()
    {
        var tile = new Tile(Position.Zero);
        var c    = new Creature { Id = 1, Name = "Dragon" };
        tile.AddCreature(c);
        Assert.Contains(c, tile.Creatures);
    }

    [Fact]
    public void AddCreature_Duplicate_NotAddedTwice()
    {
        var tile = new Tile(Position.Zero);
        var c    = new Creature { Id = 1 };
        tile.AddCreature(c);
        tile.AddCreature(c);
        Assert.Single(tile.Creatures);
    }

    [Fact]
    public void PruneEffects_RemovesFinishedEffects()
    {
        var tile   = new Tile(Position.Zero);
        var effect = new Effect { TypeId = 1 };
        // advance animator past its single frame to finish it
        effect.Update(10_000);
        tile.AddEffect(effect);
        tile.PruneEffects();
        Assert.Empty(tile.Effects);
    }

    // ─── Map ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_GetOrCreate_ReturnsNewTile()
    {
        var map  = new Map();
        var pos  = new Position(100, 100, 7);
        var tile = map.GetOrCreate(pos);
        Assert.NotNull(tile);
        Assert.Equal(pos, tile.Position);
    }

    [Fact]
    public void Map_GetOrCreate_ReturnsSameInstance()
    {
        var map  = new Map();
        var pos  = new Position(1, 1, 7);
        var t1   = map.GetOrCreate(pos);
        var t2   = map.GetOrCreate(pos);
        Assert.Same(t1, t2);
    }

    [Fact]
    public void Map_Get_ReturnsNullForUnknownPos()
    {
        var map = new Map();
        Assert.Null(map.Get(new Position(0, 0, 0)));
    }

    [Fact]
    public void Map_Remove_DeletesTile()
    {
        var map = new Map();
        var pos = new Position(5, 5, 7);
        map.GetOrCreate(pos);
        map.Remove(pos);
        Assert.Null(map.Get(pos));
    }

    [Fact]
    public void Map_Clear_RemovesAllTiles()
    {
        var map = new Map();
        map.GetOrCreate(new Position(1, 1, 7));
        map.GetOrCreate(new Position(2, 2, 7));
        map.Clear();
        Assert.Equal(0, map.TileCount);
    }

    [Fact]
    public void Map_AddCreature_TracksIt()
    {
        var map = new Map();
        var c   = new Creature { Id = 42, Position = new Position(10, 10, 7) };
        map.AddCreature(c);
        Assert.Same(c, map.GetCreature(42));
    }

    [Fact]
    public void Map_RemoveCreature_RemovesIt()
    {
        var map = new Map();
        var c   = new Creature { Id = 7, Position = new Position(5, 5, 7) };
        map.AddCreature(c);
        map.RemoveCreature(7);
        Assert.Null(map.GetCreature(7));
    }

    [Fact]
    public void Map_GetViewport_ReturnsTilesInArea()
    {
        var map = new Map();
        map.GetOrCreate(new Position(10, 10, 7));
        map.GetOrCreate(new Position(11, 10, 7));
        map.GetOrCreate(new Position(20, 20, 7)); // out of range
        var tiles = map.GetViewport(10, 10, 7, 2, 2).ToList();
        Assert.Equal(2, tiles.Count);
    }

    // ─── Map.MoveCreature (T03) ───────────────────────────────────────────────

    [Fact]
    public void MoveCreature_UpdatesCreaturePositionAndTiles()
    {
        var map  = new Map();
        var from = new Position(10, 10, 7);
        var to   = new Position(11, 10, 7);
        var c    = new Creature { Id = 1, Position = from };
        map.AddCreature(c);

        bool moved = map.MoveCreature(1, to);

        Assert.True(moved);
        Assert.Equal(to, c.Position);
        Assert.DoesNotContain(c, map.Get(from)?.Creatures ?? []);
        Assert.Contains(c,  map.Get(to)!.Creatures);
    }

    [Fact]
    public void MoveCreature_ReturnsFalse_WhenCreatureNotKnown()
    {
        var map = new Map();
        Assert.False(map.MoveCreature(999, new Position(0, 0, 7)));
    }

    // ─── Creature.BaseSpeed (T03) ─────────────────────────────────────────────

    [Fact]
    public void Creature_BaseSpeed_DefaultsToSpeed()
    {
        var c = new Creature();
        Assert.Equal(c.Speed, c.BaseSpeed);
    }

    [Fact]
    public void Creature_BaseSpeed_CanBeSetIndependently()
    {
        var c = new Creature { Speed = 300, BaseSpeed = 220 };
        Assert.Equal(300, c.Speed);
        Assert.Equal(220, c.BaseSpeed);
    }

    // ─── T07: Tile walkability helpers ───────────────────────────────────────

    [Fact]
    public void Tile_IsPathable_TrueWithNormalGround()
    {
        var tile = new Tile(Position.Zero);
        tile.SetGround(MakeItem(ThingTypeFlag.Ground));
        Assert.True(tile.IsPathable);
    }

    [Fact]
    public void Tile_IsPathable_FalseWhenNotPathableItem()
    {
        var tile = new Tile(Position.Zero);
        tile.SetGround(MakeItem(ThingTypeFlag.Ground));
        tile.AddItem(MakeItem(ThingTypeFlag.NotPathable));
        Assert.False(tile.IsPathable);
    }

    [Fact]
    public void Tile_HasCreatures_FalseWhenEmpty()
    {
        var tile = new Tile(Position.Zero);
        Assert.False(tile.HasCreatures);
    }

    [Fact]
    public void Tile_HasCreatures_TrueAfterAddCreature()
    {
        var tile = new Tile(Position.Zero);
        tile.AddCreature(new Creature { Id = 1, Position = Position.Zero });
        Assert.True(tile.HasCreatures);
    }

    [Fact]
    public void Tile_GetGroundSpeed_DefaultsTo150WhenNoGround()
    {
        var tile = new Tile(Position.Zero);
        Assert.Equal(150, tile.GetGroundSpeed());
    }

    [Fact]
    public void Tile_GetGroundSpeed_ReturnsGroundThingTypeSpeed()
    {
        var tile = new Tile(Position.Zero);
        tile.SetGround(new Item
        {
            Id        = 1,
            ThingType = new ThingType { Category = ThingCategory.Item, Id = 1,
                Flags = ThingTypeFlag.Ground, GroundSpeed = 250 },
        });
        Assert.Equal(250, tile.GetGroundSpeed());
    }

    // ─── T07: PathFindResult / PathFindFlags enum values ─────────────────────

    [Fact] public void PathFindResult_Ok_IsZero()         => Assert.Equal(0, (int)PathFindResult.Ok);
    [Fact] public void PathFindResult_SamePosition_Is1()  => Assert.Equal(1, (int)PathFindResult.SamePosition);
    [Fact] public void PathFindResult_Impossible_Is2()    => Assert.Equal(2, (int)PathFindResult.Impossible);
    [Fact] public void PathFindResult_TooFar_Is3()        => Assert.Equal(3, (int)PathFindResult.TooFar);
    [Fact] public void PathFindResult_NoWay_Is4()         => Assert.Equal(4, (int)PathFindResult.NoWay);
    [Fact] public void PathFindFlags_AllowNotSeenTiles_Is1()  => Assert.Equal(1, (int)PathFindFlags.AllowNotSeenTiles);
    [Fact] public void PathFindFlags_AllowCreatures_Is2()     => Assert.Equal(2, (int)PathFindFlags.AllowCreatures);
    [Fact] public void PathFindFlags_AllowNonPathable_Is4()   => Assert.Equal(4, (int)PathFindFlags.AllowNonPathable);
    [Fact] public void PathFindFlags_AllowNonWalkable_Is8()   => Assert.Equal(8, (int)PathFindFlags.AllowNonWalkable);
    [Fact] public void PathFindFlags_IgnoreCreatures_Is16()   => Assert.Equal(16, (int)PathFindFlags.IgnoreCreatures);

    // ─── T07: FindPath – trivial cases ───────────────────────────────────────

    [Fact]
    public void FindPath_SamePosition_ReturnsSamePosition()
    {
        var map = new Map();
        var pos = new Position(100, 100, 7);
        var (dirs, result) = map.FindPath(pos, pos);
        Assert.Equal(PathFindResult.SamePosition, result);
        Assert.Empty(dirs);
    }

    [Fact]
    public void FindPath_DifferentZ_ReturnsImpossible()
    {
        var map = new Map();
        var start = new Position(100, 100, 7);
        var goal  = new Position(100, 100, 8);
        var (dirs, result) = map.FindPath(start, goal);
        Assert.Equal(PathFindResult.Impossible, result);
        Assert.Empty(dirs);
    }

    [Fact]
    public void FindPath_NotWalkableGoal_ReturnsNoWay()
    {
        var map = new Map();
        var start = new Position(100, 100, 7);
        var goal  = new Position(101, 100, 7);

        // Create start as walkable
        map.GetOrCreate(start).SetGround(MakeItem(ThingTypeFlag.Ground));

        // Create goal as NOT walkable (wall)
        var goalTile = map.GetOrCreate(goal);
        goalTile.SetGround(MakeItem(ThingTypeFlag.Ground | ThingTypeFlag.NotWalkable));

        var (dirs, result) = map.FindPath(start, goal, flags: PathFindFlags.AllowNotSeenTiles);
        Assert.Equal(PathFindResult.NoWay, result);
        Assert.Empty(dirs);
    }

    // ─── T07: FindPath – straight line ────────────────────────────────────────

    private static Map BuildCorridor(Position start, Position end)
    {
        var map = new Map();
        int x = start.X;
        int endX = end.X;
        int z = start.Z;
        for (int cx = Math.Min(x, endX); cx <= Math.Max(x, endX); cx++)
            map.GetOrCreate(new Position(cx, start.Y, z))
               .SetGround(MakeItem(ThingTypeFlag.Ground));
        return map;
    }

    [Fact]
    public void FindPath_StraightEast_ReturnsDirectionList()
    {
        var start = new Position(100, 100, 7);
        var goal  = new Position(103, 100, 7);
        var map   = BuildCorridor(start, goal);

        var (dirs, result) = map.FindPath(start, goal,
            flags: PathFindFlags.AllowNotSeenTiles);

        Assert.Equal(PathFindResult.Ok, result);
        Assert.Equal(3, dirs.Count);
        Assert.All(dirs, d => Assert.Equal(Direction.East, d));
    }

    [Fact]
    public void FindPath_MaxComplexityExceeded_ReturnsTooFar()
    {
        // Build a very large open map and cap complexity at 1
        var start = new Position(100, 100, 7);
        var goal  = new Position(110, 100, 7);
        var map   = BuildCorridor(start, goal);

        var (_, result) = map.FindPath(start, goal,
            maxComplexity: 1,
            flags: PathFindFlags.AllowNotSeenTiles);

        Assert.Equal(PathFindResult.TooFar, result);
    }

    [Fact]
    public void FindPath_BlockedByCreature_WithoutFlag_ReturnsNoWay()
    {
        // Only add the 3 corridor tiles. Without AllowNotSeenTiles the
        // pathfinder cannot leave to unseen neighbours.
        var start = new Position(100, 100, 7);
        var mid   = new Position(101, 100, 7);
        var goal  = new Position(102, 100, 7);
        var map   = BuildCorridor(start, goal);

        // Place a creature on the middle tile
        map.GetOrCreate(mid).AddCreature(new Creature { Id = 99, Position = mid });

        // Without AllowCreatures, the only "seen" path through mid is blocked.
        var (_, noCreaturesResult) = map.FindPath(start, goal);
        Assert.Equal(PathFindResult.NoWay, noCreaturesResult);

        // With AllowCreatures the path IS found because the creature tile is open.
        var (dirs, okResult) = map.FindPath(start, goal,
            flags: PathFindFlags.AllowCreatures);
        Assert.Equal(PathFindResult.Ok, okResult);
        Assert.NotEmpty(dirs);
    }

    // ─── T07: FindPathAsync ───────────────────────────────────────────────────

    [Fact]
    public async System.Threading.Tasks.Task FindPathAsync_SamePosition_ReturnsSamePosition()
    {
        var map = new Map();
        var pos = new Position(50, 50, 7);
        var (dirs, result) = await map.FindPathAsync(pos, pos);
        Assert.Equal(PathFindResult.SamePosition, result);
        Assert.Empty(dirs);
    }

    [Fact]
    public async System.Threading.Tasks.Task FindPathAsync_StraightPath_ReturnsOk()
    {
        var start = new Position(200, 200, 7);
        var goal  = new Position(202, 200, 7);
        var map   = BuildCorridor(start, goal);

        var (dirs, result) = await map.FindPathAsync(start, goal,
            flags: PathFindFlags.AllowNotSeenTiles);

        Assert.Equal(PathFindResult.Ok, result);
        Assert.Equal(2, dirs.Count);
    }
}
