using OTClient.Framework.Game;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for T34: expanded AttachedEffect / AttachedEffectManager /
/// AttachableObject / Paperdoll / PaperdollManager.
/// </summary>
public sealed class GameObjectsT34Tests
{
    // ─── AttachedEffect ───────────────────────────────────────────────────────

    [Fact]
    public void AttachedEffect_DefaultValues()
    {
        var e = new AttachedEffect { Id = 1, Name = "Test" };
        Assert.Equal(100, e.Speed);
        Assert.Equal(100, e.Opacity);
        Assert.True(e.Permanent);
        Assert.True(e.CanDrawOnUI);
        Assert.Equal(DrawOrder.Third, e.DrawOrder);
        Assert.Equal(Direction.North, e.Direction);
        Assert.Equal(-1, e.Loop);
        Assert.Equal(0, e.Duration);
    }

    [Fact]
    public void AttachedEffect_SetFlags()
    {
        var e = new AttachedEffect { Id = 2, Name = "Wings" };
        e.HideOwner = true;
        e.Transform = true;
        e.DisableWalkAnimation = true;
        e.FollowOwner = true;
        Assert.True(e.HideOwner);
        Assert.True(e.Transform);
        Assert.True(e.DisableWalkAnimation);
        Assert.True(e.FollowOwner);
    }

    [Fact]
    public void AttachedEffect_Bounce_PulseFadeDescriptors()
    {
        var e = new AttachedEffect { Id = 3 };
        e.Bounce = new BounceControl { MinHeight = 2, Height = 10, Speed = 200 };
        e.Pulse  = new BounceControl { MinHeight = 1, Height = 5,  Speed = 100 };
        e.Fade   = new BounceControl { MinHeight = 0, Height = 100, Speed = 50 };

        Assert.Equal(10,  e.Bounce.Height);
        Assert.Equal(5,   e.Pulse.Height);
        Assert.Equal(100, e.Fade.Height);
    }

    [Fact]
    public void AttachedEffect_DirControl_SetOffset()
    {
        var e = new AttachedEffect { Id = 4 };
        e.SetOffset(3, -5);
        var dc = e.GetDirControl(Direction.East);
        Assert.Equal(3,  dc.OffsetX);
        Assert.Equal(-5, dc.OffsetY);
    }

    [Fact]
    public void AttachedEffect_SetDirOffset_PerDirection()
    {
        var e = new AttachedEffect { Id = 5 };
        e.SetDirOffset(Direction.South, 1, 2, onTop: true);
        var dc = e.GetDirControl(Direction.South);
        Assert.Equal(1, dc.OffsetX);
        Assert.Equal(2, dc.OffsetY);
        Assert.True(dc.OnTop);
    }

    [Fact]
    public void AttachedEffect_Clone_IsDeepCopy()
    {
        var e = new AttachedEffect { Id = 6, Name = "Aura", Speed = 50, Opacity = 80, HideOwner = true };
        e.SetDirOffset(Direction.West, 10, 20, onTop: false);

        var c = e.Clone();
        Assert.Equal(50,  c.Speed);
        Assert.Equal(80,  c.Opacity);
        Assert.True(c.HideOwner);
        Assert.Equal(10, c.GetDirControl(Direction.West).OffsetX);

        // Mutate original; clone should not change
        e.Speed = 99;
        Assert.Equal(50, c.Speed);
    }

    // ─── AttachedEffectManager ────────────────────────────────────────────────

    [Fact]
    public void AttachedEffectManager_Register_And_Get()
    {
        var mgr = new AttachedEffectManager();
        var eff = new AttachedEffect { Id = 7, Name = "Wings" };
        mgr.Register(eff);
        Assert.Same(eff, mgr.Get(7));
    }

    [Fact]
    public void AttachedEffectManager_GetNull_WhenMissing()
    {
        var mgr = new AttachedEffectManager();
        Assert.Null(mgr.Get(999));
    }

    [Fact]
    public void AttachedEffectManager_Remove()
    {
        var mgr = new AttachedEffectManager();
        mgr.Register(new AttachedEffect { Id = 8 });
        mgr.Remove(8);
        Assert.Null(mgr.Get(8));
        Assert.Equal(0, mgr.Count);
    }

    [Fact]
    public void AttachedEffectManager_Clear()
    {
        var mgr = new AttachedEffectManager();
        mgr.Register(new AttachedEffect { Id = 9  });
        mgr.Register(new AttachedEffect { Id = 10 });
        mgr.Clear();
        Assert.Equal(0, mgr.Count);
    }

    [Fact]
    public void AttachedEffectManager_RegisterByThing_Returns_Effect()
    {
        var mgr = new AttachedEffectManager();
        var eff = mgr.RegisterByThing(11, "Fire", thingId: 42, category: "effect");
        Assert.NotNull(eff);
        Assert.NotNull(mgr.Get(11));
    }

    [Fact]
    public void AttachedEffectManager_RegisterByImage_Returns_Effect()
    {
        var mgr = new AttachedEffectManager();
        var eff = mgr.RegisterByImage(12, "Glow", "images/glow.png", smooth: true);
        Assert.NotNull(eff);
        Assert.NotNull(mgr.Get(12));
    }

    // ─── AttachableObject ─────────────────────────────────────────────────────

    private sealed class TestAttachable : AttachableObject { }

    [Fact]
    public void AttachableObject_AttachAndDetect()
    {
        var obj = new TestAttachable();
        var eff = new AttachedEffect { Id = 1 };
        obj.AttachEffect(eff);
        Assert.True(obj.HasAttachedEffects);
        Assert.Single(obj.AttachedEffects);
    }

    [Fact]
    public void AttachableObject_AttachEffect_ClonesIt()
    {
        var obj = new TestAttachable();
        var eff = new AttachedEffect { Id = 2, Speed = 77 };
        obj.AttachEffect(eff);
        // Mutate original
        eff.Speed = 1;
        Assert.Equal(77, obj.AttachedEffects[0].Speed);
    }

    [Fact]
    public void AttachableObject_DetachEffectById()
    {
        var obj = new TestAttachable();
        obj.AttachEffect(new AttachedEffect { Id = 3 });
        Assert.True(obj.DetachEffectById(3));
        Assert.False(obj.HasAttachedEffects);
    }

    [Fact]
    public void AttachableObject_DetachEffectById_ReturnsFalse_WhenMissing()
    {
        var obj = new TestAttachable();
        Assert.False(obj.DetachEffectById(99));
    }

    [Fact]
    public void AttachableObject_ClearTemporaryEffects()
    {
        var obj = new TestAttachable();
        obj.AttachEffect(new AttachedEffect { Id = 4, Permanent = true  });
        obj.AttachEffect(new AttachedEffect { Id = 5, Permanent = false });
        obj.ClearTemporaryAttachedEffects();
        Assert.Single(obj.AttachedEffects);
        Assert.Equal(4, obj.AttachedEffects[0].Id);
    }

    [Fact]
    public void AttachableObject_ClearPermanentEffects()
    {
        var obj = new TestAttachable();
        obj.AttachEffect(new AttachedEffect { Id = 6, Permanent = true  });
        obj.AttachEffect(new AttachedEffect { Id = 7, Permanent = false });
        obj.ClearPermanentAttachedEffects();
        Assert.Single(obj.AttachedEffects);
        Assert.Equal(7, obj.AttachedEffects[0].Id);
    }

    [Fact]
    public void AttachableObject_IsOwnerHidden_WhenEffectSetsHideOwner()
    {
        var obj = new TestAttachable();
        obj.AttachEffect(new AttachedEffect { Id = 8, HideOwner = true });
        Assert.True(obj.IsOwnerHidden);
    }

    [Fact]
    public void AttachableObject_GetAttachedEffectById()
    {
        var obj = new TestAttachable();
        obj.AttachEffect(new AttachedEffect { Id = 9, Name = "Halo" });
        var found = obj.GetAttachedEffectById(9);
        Assert.NotNull(found);
        Assert.Equal("Halo", found!.Name);
    }

    // ─── PaperDoll (T34 additions) ────────────────────────────────────────────

    [Fact]
    public void PaperDoll_DefaultVisualProperties()
    {
        var pd = new PaperDoll();
        Assert.Equal(100, pd.Speed);
        Assert.Equal(100, pd.Opacity);
        Assert.Equal(1.0f, pd.SizeFactor);
        Assert.True(pd.ShowOnMount);
        Assert.True(pd.CanDrawOnUI);
        Assert.Equal(1, pd.Priority);
    }

    [Fact]
    public void PaperDoll_Addon_Management()
    {
        var pd = new PaperDoll();
        pd.SetAddon(0x01u);
        pd.SetAddon(0x02u);
        Assert.True(pd.HasAddon(0x01u));
        Assert.True(pd.HasAddon(0x02u));
        Assert.True(pd.HasAddon(0x03u)); // HasAddon checks (Addons & mask)==mask, so both bits must be present
        pd.RemoveAddon(0x01u);
        Assert.False(pd.HasAddon(0x01u));
        Assert.True(pd.HasAddon(0x02u));
    }

    [Fact]
    public void PaperDoll_SetColor_AllChannels()
    {
        var pd = new PaperDoll();
        pd.SetColor(42);
        Assert.Equal(42, pd.HeadColor);
        Assert.Equal(42, pd.BodyColor);
        Assert.Equal(42, pd.LegsColor);
        Assert.Equal(42, pd.FeetColor);
    }

    [Fact]
    public void PaperDoll_DirOffset_Normal()
    {
        var pd = new PaperDoll();
        pd.SetDirOffset(Direction.North, 5, 10, onTop: false);
        var dc = pd.GetDirControl(0, Direction.North);
        Assert.Equal(5,  dc.OffsetX);
        Assert.Equal(10, dc.OffsetY);
        Assert.False(dc.OnTop);
    }

    [Fact]
    public void PaperDoll_DirOffset_Mount()
    {
        var pd = new PaperDoll();
        pd.SetMountDirOffset(Direction.East, -3, 7);
        var dc = pd.GetDirControl(1, Direction.East);
        Assert.Equal(-3, dc.OffsetX);
        Assert.Equal(7,  dc.OffsetY);
    }

    [Fact]
    public void PaperDoll_Clone_IsDeepCopy()
    {
        var pd = new PaperDoll { Speed = 50, HeadColor = 77 };
        pd.Equip(PaperDoll.Slot.Head, Item.Create(100));
        pd.SetAddon(0x02u);

        var c = pd.Clone();
        Assert.Equal(50, c.Speed);
        Assert.Equal(77, c.HeadColor);
        Assert.True(c.HasAddon(0x02u));
        Assert.NotNull(c.GetSlot(PaperDoll.Slot.Head));

        // Mutate original
        pd.Speed = 99;
        Assert.Equal(50, c.Speed);
    }

    [Fact]
    public void PaperDoll_Reset_ClearsAll()
    {
        var pd = new PaperDoll { Speed = 50, HeadColor = 100 };
        pd.Equip(PaperDoll.Slot.Feet, Item.Create(1));
        pd.SetAddon(0x01u);
        pd.Reset();
        Assert.Equal(100, pd.Speed);
        Assert.Equal(0, pd.HeadColor);
        Assert.Equal(0u, pd.Addons);
        Assert.True(pd.IsSlotEmpty(PaperDoll.Slot.Feet));
    }

    // ─── PaperdollManager ─────────────────────────────────────────────────────

    [Fact]
    public void PaperdollManager_Set_And_GetById()
    {
        var mgr = new PaperDollManager();
        var pd  = mgr.Set(1, 1001);
        Assert.NotNull(pd);
        Assert.Equal((ushort)1,    pd.Id);
        Assert.Equal((ushort)1001, pd.ThingId);
        Assert.Same(pd, mgr.GetById(1));
    }

    [Fact]
    public void PaperdollManager_GetById_Null_WhenMissing()
    {
        var mgr = new PaperDollManager();
        Assert.Null(mgr.GetById(99));
    }

    [Fact]
    public void PaperdollManager_Remove()
    {
        var mgr = new PaperDollManager();
        mgr.Set(2, 2000);
        mgr.Remove(2);
        Assert.Null(mgr.GetById(2));
        Assert.Equal(0, mgr.Count);
    }

    [Fact]
    public void PaperdollManager_Clear()
    {
        var mgr = new PaperDollManager();
        mgr.Set(3, 3000);
        mgr.Set(4, 4000);
        mgr.Clear();
        Assert.Equal(0, mgr.Count);
    }
}
