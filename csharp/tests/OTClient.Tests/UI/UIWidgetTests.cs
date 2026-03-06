using System.Numerics;
using OTClient.Framework.UI;
using Raylib_cs;
using Xunit;

namespace OTClient.Tests.UI;

/// <summary>
/// Unit tests for <see cref="UIWidget"/>: box model, layout, anchors,
/// child management, hit-testing, and draw commands.
/// Tasks 7.1–7.2, 7.24.
/// No Raylib GPU context is required.
/// </summary>
public sealed class UIWidgetTests
{
    private static Rectangle Screen => new(0, 0, 800, 600);

    // ─── Defaults ─────────────────────────────────────────────────────────────

    [Fact]
    public void NewWidget_HasNoChildren()
    {
        var w = new UIWidget();
        Assert.Empty(w.Children);
    }

    [Fact]
    public void NewWidget_IsVisible()
    {
        Assert.True(new UIWidget().Visible);
    }

    [Fact]
    public void NewWidget_IsEnabled()
    {
        Assert.True(new UIWidget().Enabled);
    }

    [Fact]
    public void NewWidget_HasNoParent()
    {
        Assert.Null(new UIWidget().Parent);
    }

    // ─── Child management ─────────────────────────────────────────────────────

    [Fact]
    public void AddChild_SetsParent()
    {
        var parent = new UIWidget();
        var child  = new UIWidget();
        parent.AddChild(child);
        Assert.Same(parent, child.Parent);
    }

    [Fact]
    public void AddChild_AppearsInChildren()
    {
        var parent = new UIWidget();
        var child  = new UIWidget();
        parent.AddChild(child);
        Assert.Contains(child, parent.Children);
    }

    [Fact]
    public void AddChild_Null_Throws()
    {
        var parent = new UIWidget();
        Assert.Throws<ArgumentNullException>(() => parent.AddChild(null!));
    }

    [Fact]
    public void RemoveChild_ClearsParent()
    {
        var parent = new UIWidget();
        var child  = new UIWidget();
        parent.AddChild(child);
        parent.RemoveChild(child);
        Assert.Null(child.Parent);
    }

    [Fact]
    public void RemoveChild_ReturnsTrue_WhenFound()
    {
        var parent = new UIWidget();
        var child  = new UIWidget();
        parent.AddChild(child);
        Assert.True(parent.RemoveChild(child));
    }

    [Fact]
    public void RemoveChild_ReturnsFalse_WhenNotFound()
    {
        var parent = new UIWidget();
        Assert.False(parent.RemoveChild(new UIWidget()));
    }

    [Fact]
    public void ClearChildren_RemovesAll()
    {
        var parent = new UIWidget();
        parent.AddChild(new UIWidget());
        parent.AddChild(new UIWidget());
        parent.ClearChildren();
        Assert.Empty(parent.Children);
    }

    [Fact]
    public void AddChild_ReparentsFromOldParent()
    {
        var p1 = new UIWidget();
        var p2 = new UIWidget();
        var c  = new UIWidget();
        p1.AddChild(c);
        p2.AddChild(c);   // should detach from p1 first
        Assert.DoesNotContain(c, p1.Children);
        Assert.Contains(c, p2.Children);
        Assert.Same(p2, c.Parent);
    }

    // ─── Layout — explicit size ───────────────────────────────────────────────

    [Fact]
    public void Layout_ExplicitSize_UsedInComputedRect()
    {
        var w = new UIWidget { Size = new Vector2(100, 50) };
        w.Layout(Screen);
        Assert.Equal(100f, w.ComputedRect.Width);
        Assert.Equal(50f,  w.ComputedRect.Height);
    }

    [Fact]
    public void Layout_NoSize_FillsAvailable()
    {
        var w = new UIWidget();
        w.Layout(new Rectangle(0, 0, 200, 100));
        Assert.Equal(200f, w.ComputedRect.Width);
        Assert.Equal(100f, w.ComputedRect.Height);
    }

    [Fact]
    public void Layout_Margin_ShrinksWidth()
    {
        var w = new UIWidget { Margin = new Thickness(10, 0, 10, 0) };
        w.Layout(new Rectangle(0, 0, 200, 100));
        Assert.Equal(180f, w.ComputedRect.Width);
    }

    [Fact]
    public void Layout_Position_OffsetsRect()
    {
        var w = new UIWidget { Size = new Vector2(50, 50), Position = new Vector2(20, 30) };
        w.Layout(new Rectangle(0, 0, 800, 600));
        Assert.Equal(20f, w.ComputedRect.X);
        Assert.Equal(30f, w.ComputedRect.Y);
    }

    // ─── Anchor layout ────────────────────────────────────────────────────────

    [Fact]
    public void AnchorFill_StretchesToParent()
    {
        var w = new UIWidget { Anchor = AnchorFlag.Fill };
        w.Layout(new Rectangle(0, 0, 400, 300));
        Assert.Equal(400f, w.ComputedRect.Width);
        Assert.Equal(300f, w.ComputedRect.Height);
    }

    [Fact]
    public void AnchorRight_AlignedToRightEdge()
    {
        var w = new UIWidget { Size = new Vector2(80, 40), Anchor = AnchorFlag.Right };
        w.Layout(new Rectangle(0, 0, 400, 300));
        Assert.Equal(400f - 80f, w.ComputedRect.X, precision: 3);
    }

    [Fact]
    public void AnchorBottom_AlignedToBottomEdge()
    {
        var w = new UIWidget { Size = new Vector2(80, 40), Anchor = AnchorFlag.Bottom };
        w.Layout(new Rectangle(0, 0, 400, 300));
        Assert.Equal(300f - 40f, w.ComputedRect.Y, precision: 3);
    }

    [Fact]
    public void AnchorCenterX_HorizontallyCentred()
    {
        var w = new UIWidget { Size = new Vector2(100, 50), Anchor = AnchorFlag.CenterX };
        w.Layout(new Rectangle(0, 0, 400, 300));
        Assert.Equal((400f - 100f) / 2f, w.ComputedRect.X, precision: 3);
    }

    [Fact]
    public void AnchorCenter_HV_BothCentred()
    {
        var w = new UIWidget { Size = new Vector2(100, 50), Anchor = AnchorFlag.Center };
        w.Layout(new Rectangle(0, 0, 400, 300));
        Assert.Equal((400f - 100f) / 2f, w.ComputedRect.X, precision: 3);
        Assert.Equal((300f - 50f)  / 2f, w.ComputedRect.Y, precision: 3);
    }

    [Fact]
    public void AnchorFillX_OnlyStretchesWidth()
    {
        var w = new UIWidget { Size = new Vector2(0, 50), Anchor = AnchorFlag.FillX };
        w.Layout(new Rectangle(0, 0, 400, 300));
        Assert.Equal(400f, w.ComputedRect.Width);
        Assert.Equal(50f,  w.ComputedRect.Height);
    }

    // ─── MinSize / MaxSize ────────────────────────────────────────────────────

    [Fact]
    public void MinSize_Respected()
    {
        var w = new UIWidget { Size = new Vector2(10, 10), MinSize = new Vector2(50, 50) };
        w.Layout(Screen);
        Assert.Equal(50f, w.ComputedRect.Width);
        Assert.Equal(50f, w.ComputedRect.Height);
    }

    [Fact]
    public void MaxSize_Respected()
    {
        var w = new UIWidget { MaxSize = new Vector2(30, 30) };
        w.Layout(new Rectangle(0, 0, 400, 300));
        Assert.Equal(30f, w.ComputedRect.Width);
        Assert.Equal(30f, w.ComputedRect.Height);
    }

    // ─── Hit testing ──────────────────────────────────────────────────────────

    [Fact]
    public void HitTest_InsideWidget_ReturnsSelf()
    {
        var w = new UIWidget { Size = new Vector2(100, 100) };
        w.Layout(new Rectangle(0, 0, 800, 600));
        Assert.Same(w, w.HitTest(new Vector2(50, 50)));
    }

    [Fact]
    public void HitTest_OutsideWidget_ReturnsNull()
    {
        var w = new UIWidget { Size = new Vector2(100, 100) };
        w.Layout(new Rectangle(0, 0, 800, 600));
        Assert.Null(w.HitTest(new Vector2(200, 200)));
    }

    [Fact]
    public void HitTest_PrefersTopChild()
    {
        var parent = new UIWidget();
        parent.Layout(new Rectangle(0, 0, 800, 600));

        var back  = new UIWidget { Size = new Vector2(200, 200), ZOrder = 0 };
        var front = new UIWidget { Size = new Vector2(200, 200), ZOrder = 1 };
        parent.AddChild(back);
        parent.AddChild(front);
        parent.Layout(new Rectangle(0, 0, 800, 600));

        var hit = parent.HitTest(new Vector2(50, 50));
        Assert.Same(front, hit);
    }

    [Fact]
    public void HitTest_InvisibleWidget_ReturnsNull()
    {
        var w = new UIWidget { Size = new Vector2(200, 200), Visible = false };
        w.Layout(Screen);
        Assert.Null(w.HitTest(new Vector2(50, 50)));
    }

    // ─── Draw commands ────────────────────────────────────────────────────────

    [Fact]
    public void Draw_InvisibleWidget_EmitsNoCommands()
    {
        var w = new UIWidget { Visible = false, BackgroundColor = Color.Red };
        w.Layout(Screen);
        var cmds = new List<UIDrawCommand>();
        w.Draw(cmds);
        Assert.Empty(cmds);
    }

    [Fact]
    public void Draw_BackgroundColor_EmitsFillRectCommand()
    {
        var w = new UIWidget { Size = new Vector2(100, 100), BackgroundColor = Color.Blue };
        w.Layout(Screen);
        var cmds = new List<UIDrawCommand>();
        w.Draw(cmds);
        Assert.Contains(cmds, c => c is UIDrawCommand.FillRect);
    }

    [Fact]
    public void Draw_TransparentBackground_EmitsNoFillRect()
    {
        var w = new UIWidget { Size = new Vector2(100, 100), BackgroundColor = Color.Blank };
        w.Layout(Screen);
        var cmds = new List<UIDrawCommand>();
        w.Draw(cmds);
        Assert.DoesNotContain(cmds, c => c is UIDrawCommand.FillRect);
    }

    [Fact]
    public void Draw_Border_EmitsBorderCommand()
    {
        var w = new UIWidget
        {
            Size = new Vector2(100, 100),
            BorderWidth = 2f,
            BorderColor = Color.White,
        };
        w.Layout(Screen);
        var cmds = new List<UIDrawCommand>();
        w.Draw(cmds);
        Assert.Contains(cmds, c => c is UIDrawCommand.DrawBorder);
    }

    [Fact]
    public void Draw_IncludesChildrenCommands()
    {
        var parent = new UIWidget { Size = new Vector2(200, 200), BackgroundColor = Color.Red };
        var child  = new UIWidget { Size = new Vector2(50,  50),  BackgroundColor = Color.Blue };
        parent.AddChild(child);
        parent.Layout(Screen);

        var cmds = new List<UIDrawCommand>();
        parent.Draw(cmds);

        // Parent rect + child rect
        var fills = cmds.OfType<UIDrawCommand.FillRect>().ToList();
        Assert.Equal(2, fills.Count);
    }

    // ─── Focus ────────────────────────────────────────────────────────────────

    [Fact]
    public void Focus_SetsIsFocused()
    {
        var w = new UIWidget();
        w.RaiseFocusGained();
        Assert.True(w.IsFocused);
    }

    [Fact]
    public void FocusLost_ClearsIsFocused()
    {
        var w = new UIWidget();
        w.RaiseFocusGained();
        w.RaiseFocusLost();
        Assert.False(w.IsFocused);
    }

    [Fact]
    public void Click_InvokesOnClick()
    {
        var w = new UIWidget();
        bool called = false;
        w.OnClick += _ => called = true;
        w.RaiseClick();
        Assert.True(called);
    }

    [Fact]
    public void Click_DisabledWidget_DoesNotInvokeOnClick()
    {
        var w = new UIWidget { Enabled = false };
        bool called = false;
        w.OnClick += _ => called = true;
        w.RaiseClick();
        Assert.False(called);
    }

    // ─── Thickness helper ─────────────────────────────────────────────────────

    [Fact]
    public void Thickness_Parse_Single()
    {
        var t = Thickness.Parse("8");
        Assert.Equal(new Thickness(8), t);
    }

    [Fact]
    public void Thickness_Parse_Pair()
    {
        var t = Thickness.Parse("4 8");
        Assert.Equal(new Thickness(4, 8), t);
    }

    [Fact]
    public void Thickness_Parse_Quad()
    {
        var t = Thickness.Parse("1 2 3 4");
        Assert.Equal(new Thickness(1, 2, 3, 4), t);
    }

    [Fact]
    public void Thickness_Horizontal_Sum()
    {
        var t = new Thickness(5, 10, 15, 20);
        Assert.Equal(20f, t.Horizontal);
    }
}
