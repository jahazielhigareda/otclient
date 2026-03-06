using OTClient.Framework.Game;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for <see cref="ThingType"/>, <see cref="ThingTypeManager"/>. Task 8.3.
/// </summary>
public sealed class ThingTypeTests
{
    // ─── ThingType defaults ───────────────────────────────────────────────────

    [Fact]
    public void NewThingType_DefaultFrameCount_IsOne()
    {
        var tt = new ThingType { Category = ThingCategory.Item, Id = 1 };
        Assert.Equal(1, tt.Frames);
    }

    [Fact]
    public void IsAnimated_TrueWhenFramesGtOne()
    {
        var tt = new ThingType { Category = ThingCategory.Item, Id = 1, Frames = 4 };
        Assert.True(tt.IsAnimated);
    }

    [Fact]
    public void Flags_Ground_ReturnsIsGround()
    {
        var tt = new ThingType { Flags = ThingTypeFlag.Ground };
        Assert.True(tt.IsGround);
    }

    [Fact]
    public void Flags_Stackable_ReturnsIsStackable()
    {
        var tt = new ThingType { Flags = ThingTypeFlag.Stackable };
        Assert.True(tt.IsStackable);
    }

    [Fact]
    public void Flags_NotWalkable_ReturnsIsNotWalkable()
    {
        var tt = new ThingType { Flags = ThingTypeFlag.NotWalkable };
        Assert.True(tt.IsNotWalkable);
    }

    // ─── ThingTypeManager ─────────────────────────────────────────────────────

    [Fact]
    public void Manager_Add_IncrementsCount()
    {
        var mgr = new ThingTypeManager();
        mgr.Add(new ThingType { Category = ThingCategory.Item, Id = 100 });
        Assert.Equal(1, mgr.Count);
    }

    [Fact]
    public void Manager_Get_ReturnsRegistered()
    {
        var mgr = new ThingTypeManager();
        var tt  = new ThingType { Category = ThingCategory.Item, Id = 200 };
        mgr.Add(tt);
        Assert.Same(tt, mgr.Get(ThingCategory.Item, 200));
    }

    [Fact]
    public void Manager_Get_ReturnsNull_WhenNotFound()
    {
        var mgr = new ThingTypeManager();
        Assert.Null(mgr.Get(ThingCategory.Item, 999));
    }

    [Fact]
    public void Manager_Contains_TrueWhenAdded()
    {
        var mgr = new ThingTypeManager();
        mgr.Add(new ThingType { Category = ThingCategory.Creature, Id = 1 });
        Assert.True(mgr.Contains(ThingCategory.Creature, 1));
    }

    [Fact]
    public void Manager_GetAll_FiltersByCategory()
    {
        var mgr = new ThingTypeManager();
        mgr.Add(new ThingType { Category = ThingCategory.Item, Id = 1 });
        mgr.Add(new ThingType { Category = ThingCategory.Item, Id = 2 });
        mgr.Add(new ThingType { Category = ThingCategory.Creature, Id = 1 });
        Assert.Equal(2, mgr.GetAll(ThingCategory.Item).Count());
    }
}
