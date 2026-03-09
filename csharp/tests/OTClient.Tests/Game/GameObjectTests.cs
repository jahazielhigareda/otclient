using OTClient.Framework.Game;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for <see cref="Animator"/>, <see cref="Item"/>,
/// <see cref="Effect"/>, <see cref="Missile"/>,
/// <see cref="AnimatedText"/>, <see cref="StaticText"/>,
/// <see cref="Container"/>, <see cref="Outfit"/>,
/// <see cref="SpriteManager"/>.
/// Tasks 8.6, 8.14–8.17, 8.19, 8.22.
/// </summary>
public sealed class GameObjectTests
{
    // ─── Animator ─────────────────────────────────────────────────────────────

    [Fact]
    public void Animator_DefaultFrame_IsZero()
    {
        var a = new Animator(4);
        Assert.Equal(0, a.CurrentFrame);
    }

    [Fact]
    public void Animator_Advances_AfterDuration()
    {
        var a = new Animator(4, minDurationMs: 100, maxDurationMs: 100, loop: true);
        a.Update(150);
        Assert.Equal(1, a.CurrentFrame);
    }

    [Fact]
    public void Animator_Loops_BackToZero()
    {
        var a = new Animator(2, 100, 100, loop: true);
        a.Update(250);   // should be at frame 0 after one full loop
        Assert.Equal(0, a.CurrentFrame);
    }

    [Fact]
    public void Animator_NonLoop_FinishesAtLastFrame()
    {
        var a = new Animator(3, 100, 100, loop: false);
        a.Update(1000);
        Assert.True(a.IsFinished);
        Assert.Equal(2, a.CurrentFrame);
    }

    [Fact]
    public void Animator_Reset_GoesBackToStart()
    {
        var a = new Animator(3, 100, 100, loop: true);
        a.Update(250);
        a.Reset();
        Assert.Equal(0, a.CurrentFrame);
        Assert.False(a.IsFinished);
    }

    [Fact]
    public void Animator_SingleFrame_NeverAdvances_WhenLooping()
    {
        var a = new Animator(1, loop: true);   // looping — stays at 0 forever
        a.Update(10_000);
        Assert.Equal(0, a.CurrentFrame);
        Assert.False(a.IsFinished);
    }

    // ─── Item ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Item_Category_IsItem()
    {
        Assert.Equal(ThingCategory.Item, new Item().Category);
    }

    [Fact]
    public void Item_Create_SetsIdAndCount()
    {
        var item = Item.Create(42, 10);
        Assert.Equal(42, item.Id);
        Assert.Equal(10, item.Count);
    }

    [Fact]
    public void Item_IsStackable_DependsOnThingType()
    {
        var item = Item.Create(1);
        item.ThingType = new ThingType { Flags = ThingTypeFlag.Stackable };
        Assert.True(item.IsStackable);
    }

    // ─── Effect ───────────────────────────────────────────────────────────────

    [Fact]
    public void Effect_Category_IsEffect()
    {
        Assert.Equal(ThingCategory.Effect, new Effect().Category);
    }

    [Fact]
    public void Effect_IsNotFinished_Initially()
    {
        // A fresh non-looping effect has not yet received an Update
        var e = new Effect();
        Assert.False(e.IsFinished);
    }

    [Fact]
    public void Effect_Update_AdvancesAnimator()
    {
        // After one update, a non-looping single-frame effect finishes
        var e = new Effect();
        e.Update(1);
        Assert.True(e.IsFinished);
    }

    // ─── Missile ──────────────────────────────────────────────────────────────

    [Fact]
    public void Missile_Category_IsMissile()
    {
        Assert.Equal(ThingCategory.Missile, new Missile().Category);
    }

    [Fact]
    public void Missile_Progress_StartsAtZero()
    {
        var m = new Missile { From = Position.Zero, To = new Position(5, 0, 7) };
        Assert.Equal(0f, m.Progress);
    }

    [Fact]
    public void Missile_Update_ReachesDestination()
    {
        var m = new Missile
        {
            From = new Position(0, 0, 7),
            To   = new Position(5, 0, 7),
        };
        m.Update(10f);  // 10 seconds at 10 tiles/s — well past destination
        Assert.True(m.IsFinished);
    }

    [Fact]
    public void Missile_CurrentX_Interpolates()
    {
        var m = new Missile
        {
            From = new Position(0, 0, 7),
            To   = new Position(10, 0, 7),
        };
        // One tile per 0.1 s at 10 tiles/s
        m.Update(0.5f);
        // After 0.5 s, 5 tiles traveled: progress = 5/10 = 0.5
        Assert.Equal(5f, m.CurrentX, precision: 2);
    }

    // ─── AnimatedText ─────────────────────────────────────────────────────────

    [Fact]
    public void AnimatedText_NotExpired_Initially()
    {
        var t = new AnimatedText { Text = "+100" };
        Assert.False(t.IsExpired);
    }

    [Fact]
    public void AnimatedText_Expires_AfterDuration()
    {
        var t = new AnimatedText { DurationMs = 500 };
        t.Update(600);
        Assert.True(t.IsExpired);
    }

    [Fact]
    public void AnimatedText_FloatOffset_IncreasesOverTime()
    {
        var t = new AnimatedText { DurationMs = 1000 };
        t.Update(500);
        Assert.True(t.FloatOffset > 0f);
    }

    // ─── StaticText ───────────────────────────────────────────────────────────

    [Fact]
    public void StaticText_NotExpired_Initially()
    {
        var t = new StaticText { Text = "Hello" };
        Assert.False(t.IsExpired);
    }

    [Fact]
    public void StaticText_Expires_AfterLifetime()
    {
        var t = new StaticText { LifetimeMs = 1000 };
        t.Update(1100);
        Assert.True(t.IsExpired);
    }

    // ─── Container ────────────────────────────────────────────────────────────

    [Fact]
    public void Container_Empty_Initially()
    {
        Assert.True(new Container { Capacity = 5 }.IsEmpty);
    }

    [Fact]
    public void Container_AddItem_IncrementsCount()
    {
        var c = new Container { Capacity = 10 };
        c.AddItem(Item.Create(1));
        Assert.Equal(1, c.Count);
    }

    [Fact]
    public void Container_Full_HasIsFull_WhenAtCapacity()
    {
        var c = new Container { Capacity = 1 };
        c.AddItem(Item.Create(1));
        Assert.True(c.IsFull);
    }

    [Fact]
    public void Container_RemoveAt_RemovesSlot()
    {
        var c = new Container { Capacity = 10 };
        c.AddItem(Item.Create(1));
        c.RemoveAt(0);
        Assert.True(c.IsEmpty);
    }

    [Fact]
    public void Container_GetAt_ReturnsCorrectItem()
    {
        var c    = new Container { Capacity = 10 };
        var item = Item.Create(99);
        c.AddItem(item);
        Assert.Same(item, c.GetAt(0));
    }

    // ─── Outfit ───────────────────────────────────────────────────────────────

    [Fact]
    public void Outfit_Default_Id_Is128()
    {
        Assert.Equal(128, Outfit.Default.Id);
    }

    [Fact]
    public void Outfit_HasAddon1_TrueWhenBit0Set()
    {
        var o = new Outfit { Addons = 1 };
        Assert.True(o.HasAddon1);
    }

    [Fact]
    public void Outfit_HasAddon2_TrueWhenBit1Set()
    {
        var o = new Outfit { Addons = 2 };
        Assert.True(o.HasAddon2);
    }

    [Fact]
    public void Outfit_IsMounted_TrueWhenMountIdNonZero()
    {
        var o = new Outfit { MountId = 5 };
        Assert.True(o.IsMounted);
    }

    // ─── SpriteManager ────────────────────────────────────────────────────────

    [Fact]
    public void SpriteManager_Register_IncreasesCount()
    {
        var mgr = new SpriteManager();
        mgr.Register(new SpriteInfo { Id = 1 });
        Assert.Equal(1, mgr.Count);
    }

    [Fact]
    public void SpriteManager_Get_ReturnsRegistered()
    {
        var mgr  = new SpriteManager();
        var info = new SpriteInfo { Id = 42, Width = 64 };
        mgr.Register(info);
        Assert.Same(info, mgr.Get(42));
    }

    [Fact]
    public void SpriteManager_Get_NullForUnknown()
    {
        Assert.Null(new SpriteManager().Get(999));
    }
}
