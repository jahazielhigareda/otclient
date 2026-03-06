using OTClient.Framework.Graphics;
using Raylib_cs;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="Painter"/> state management and color-blending arithmetic.
/// No Raylib GPU context is required — only the CPU-side tint/opacity logic is exercised.
/// </summary>
public sealed class PainterTests
{
    // ─── Tint ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tint_DefaultsToWhite()
    {
        var painter = new Painter();
        Assert.Equal(Color.White.R, painter.Tint.R);
        Assert.Equal(Color.White.G, painter.Tint.G);
        Assert.Equal(Color.White.B, painter.Tint.B);
        Assert.Equal(Color.White.A, painter.Tint.A);
    }

    [Fact]
    public void Tint_CanBeSetToArbitraryColor()
    {
        var painter = new Painter();
        painter.Tint = Color.Red;
        Assert.Equal(Color.Red.R, painter.Tint.R);
        Assert.Equal(Color.Red.G, painter.Tint.G);
    }

    // ─── Opacity ──────────────────────────────────────────────────────────────

    [Fact]
    public void Opacity_DefaultsToOne()
    {
        var painter = new Painter();
        Assert.Equal(1f, painter.Opacity);
    }

    [Fact]
    public void Opacity_ClampedToZeroMinimum()
    {
        var painter = new Painter();
        painter.Opacity = -0.5f;
        Assert.Equal(0f, painter.Opacity);
    }

    [Fact]
    public void Opacity_ClampedToOneMaximum()
    {
        var painter = new Painter();
        painter.Opacity = 2.5f;
        Assert.Equal(1f, painter.Opacity);
    }

    [Fact]
    public void Opacity_AcceptsValuesInRange()
    {
        var painter = new Painter();
        painter.Opacity = 0.5f;
        Assert.Equal(0.5f, painter.Opacity);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1f)]
    public void Opacity_RoundTrip_WithinBounds(float value)
    {
        var painter = new Painter();
        painter.Opacity = value;
        Assert.Equal(value, painter.Opacity, precision: 6);
    }

    // ─── State independence ───────────────────────────────────────────────────

    [Fact]
    public void TwoPainters_HaveIndependentState()
    {
        var p1 = new Painter { Tint = Color.Red };
        var p2 = new Painter { Tint = Color.Blue };

        p1.Opacity = 0.3f;
        p2.Opacity = 0.9f;

        Assert.Equal(Color.Red.R,  p1.Tint.R);
        Assert.Equal(Color.Blue.B, p2.Tint.B);
        Assert.Equal(0.3f, p1.Opacity);
        Assert.Equal(0.9f, p2.Opacity);
    }
}
