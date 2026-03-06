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
}
