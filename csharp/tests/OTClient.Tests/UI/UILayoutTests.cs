using System.Numerics;
using OTClient.Framework.UI;
using Raylib_cs;
using Xunit;

namespace OTClient.Tests.UI;

/// <summary>
/// Tests for layout engines: <see cref="BoxLayout"/>, <see cref="FlexBoxLayout"/>,
/// <see cref="GridLayout"/>, and <see cref="AnchorLayout"/>.
/// Tasks 7.3–7.6, 7.24.
/// </summary>
public sealed class UILayoutTests
{
    private static UIWidget MakeChild(float w, float h) =>
        new UIWidget { Size = new Vector2(w, h) };

    private static Rectangle Area(float w = 400, float h = 300) => new(0, 0, w, h);

    // ─── AnchorLayout ─────────────────────────────────────────────────────────

    [Fact]
    public void AnchorLayout_Arrange_CallsChildLayout()
    {
        var parent = new UIWidget();
        var child  = MakeChild(50, 50);
        parent.AddChild(child);
        parent.Layout(Area());
        // Child should have a ComputedRect with its size
        Assert.Equal(50f, child.ComputedRect.Width);
    }

    [Fact]
    public void AnchorLayout_MultipleChildren_EachPositionedCorrectly()
    {
        var parent = new UIWidget();
        var c1 = MakeChild(100, 50);
        var c2 = MakeChild(200, 80);
        c1.Anchor = AnchorFlag.None;
        c2.Anchor = AnchorFlag.None;
        parent.AddChild(c1);
        parent.AddChild(c2);
        parent.Layout(Area());
        Assert.Equal(100f, c1.ComputedRect.Width);
        Assert.Equal(200f, c2.ComputedRect.Width);
    }

    // ─── BoxLayout — Horizontal ───────────────────────────────────────────────

    [Fact]
    public void BoxLayout_Horizontal_OffsetsByWidth()
    {
        var layout = new BoxLayout(LayoutDirection.Horizontal, gap: 0);
        var c1 = MakeChild(50, 30);
        var c2 = MakeChild(80, 30);
        layout.Arrange([c1, c2], Area());

        Assert.Equal(0f,  c1.ComputedRect.X);
        Assert.Equal(50f, c2.ComputedRect.X);
    }

    [Fact]
    public void BoxLayout_Horizontal_WithGap()
    {
        var layout = new BoxLayout(LayoutDirection.Horizontal, gap: 10);
        var c1 = MakeChild(50, 30);
        var c2 = MakeChild(80, 30);
        layout.Arrange([c1, c2], Area());

        Assert.Equal(0f,  c1.ComputedRect.X);
        Assert.Equal(60f, c2.ComputedRect.X);   // 50 + 10
    }

    // ─── BoxLayout — Vertical ─────────────────────────────────────────────────

    [Fact]
    public void BoxLayout_Vertical_OffsetsByHeight()
    {
        var layout = new BoxLayout(LayoutDirection.Vertical, gap: 0);
        var c1 = MakeChild(80, 40);
        var c2 = MakeChild(80, 60);
        layout.Arrange([c1, c2], Area());

        Assert.Equal(0f,  c1.ComputedRect.Y);
        Assert.Equal(40f, c2.ComputedRect.Y);
    }

    [Fact]
    public void BoxLayout_Vertical_WithGap()
    {
        var layout = new BoxLayout(LayoutDirection.Vertical, gap: 5);
        var c1 = MakeChild(80, 40);
        var c2 = MakeChild(80, 60);
        layout.Arrange([c1, c2], Area());

        Assert.Equal(45f, c2.ComputedRect.Y);   // 40 + 5
    }

    [Fact]
    public void BoxLayout_SkipsInvisibleChildren()
    {
        var layout = new BoxLayout(LayoutDirection.Horizontal, gap: 0);
        var c1 = MakeChild(50, 30);
        var c2 = new UIWidget { Size = new Vector2(80, 30), Visible = false };
        var c3 = MakeChild(60, 30);
        layout.Arrange([c1, c2, c3], Area());

        // c3 should be placed at x=50, not 130
        Assert.Equal(50f, c3.ComputedRect.X);
    }

    // ─── GridLayout ───────────────────────────────────────────────────────────

    [Fact]
    public void GridLayout_OneColumn_StacksVertically()
    {
        var layout = new GridLayout(columns: 1, cellWidth: 100, cellHeight: 50, gap: 0);
        var c1 = MakeChild(0, 0);
        var c2 = MakeChild(0, 0);
        layout.Arrange([c1, c2], Area());

        Assert.Equal(0f,  c1.ComputedRect.Y);
        Assert.Equal(50f, c2.ComputedRect.Y);
    }

    [Fact]
    public void GridLayout_TwoColumns_ArrangesIntoGrid()
    {
        var layout = new GridLayout(columns: 2, cellWidth: 100, cellHeight: 50, gap: 0);
        var c1 = MakeChild(0, 0);
        var c2 = MakeChild(0, 0);
        var c3 = MakeChild(0, 0);
        var c4 = MakeChild(0, 0);
        layout.Arrange([c1, c2, c3, c4], Area());

        // Row 0: c1(0,0)  c2(100,0)
        // Row 1: c3(0,50) c4(100,50)
        Assert.Equal(0f,   c1.ComputedRect.X);  Assert.Equal(0f,  c1.ComputedRect.Y);
        Assert.Equal(100f, c2.ComputedRect.X);  Assert.Equal(0f,  c2.ComputedRect.Y);
        Assert.Equal(0f,   c3.ComputedRect.X);  Assert.Equal(50f, c3.ComputedRect.Y);
        Assert.Equal(100f, c4.ComputedRect.X);  Assert.Equal(50f, c4.ComputedRect.Y);
    }

    [Fact]
    public void GridLayout_WithGap_OffsetsCells()
    {
        var layout = new GridLayout(columns: 2, cellWidth: 100, cellHeight: 50, gap: 10);
        var c1 = MakeChild(0, 0);
        var c2 = MakeChild(0, 0);
        layout.Arrange([c1, c2], Area());

        Assert.Equal(110f, c2.ComputedRect.X);  // 100 + 10
    }

    // ─── FlexBoxLayout ────────────────────────────────────────────────────────

    [Fact]
    public void FlexBoxLayout_Horizontal_PlacesItems()
    {
        var layout = new FlexBoxLayout(LayoutDirection.Horizontal, gap: 0, wrap: false);
        var c1 = MakeChild(100, 50);
        var c2 = MakeChild(150, 50);
        layout.Arrange([c1, c2], Area());

        Assert.Equal(0f,   c1.ComputedRect.X);
        Assert.Equal(100f, c2.ComputedRect.X);
    }

    [Fact]
    public void FlexBoxLayout_Wrap_MovesToNextLine()
    {
        var layout = new FlexBoxLayout(LayoutDirection.Horizontal, gap: 0, wrap: true);
        var c1 = MakeChild(300, 50);
        var c2 = MakeChild(300, 50);   // overflows → new row
        layout.Arrange([c1, c2], new Rectangle(0, 0, 400, 300));

        Assert.Equal(0f,  c1.ComputedRect.X);
        Assert.Equal(0f,  c2.ComputedRect.X);  // wrapped to new row
        Assert.Equal(50f, c2.ComputedRect.Y);  // below c1
    }
}
