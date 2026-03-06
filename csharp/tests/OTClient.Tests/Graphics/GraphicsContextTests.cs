using OTClient.Framework.Graphics;
using Raylib_cs;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="GraphicsContext"/> state-machine logic.
/// No Raylib GPU window is initialised — only CPU-side state is exercised.
/// </summary>
public sealed class GraphicsContextTests
{
    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void IsInFrame_IsFalseBeforeBeginFrame()
    {
        var ctx = new GraphicsContext();
        Assert.False(ctx.IsInFrame);
    }

    [Fact]
    public void ClearColor_DefaultsToBlack()
    {
        var ctx = new GraphicsContext();
        Assert.Equal(Color.Black.R, ctx.ClearColor.R);
        Assert.Equal(Color.Black.G, ctx.ClearColor.G);
        Assert.Equal(Color.Black.B, ctx.ClearColor.B);
    }

    [Fact]
    public void ClearColor_CanBeChanged()
    {
        var ctx = new GraphicsContext();
        ctx.ClearColor = Color.Red;
        Assert.Equal(Color.Red.R, ctx.ClearColor.R);
    }

    // ─── Width / Height without window ────────────────────────────────────────

    [Fact]
    public void Width_ReturnsZero_WhenNoWindowOpen()
    {
        var ctx = new GraphicsContext();
        // Raylib.IsWindowReady() is false → should return 0
        Assert.Equal(0, ctx.Width);
    }

    [Fact]
    public void Height_ReturnsZero_WhenNoWindowOpen()
    {
        var ctx = new GraphicsContext();
        Assert.Equal(0, ctx.Height);
    }

    // ─── FullScreenRect ───────────────────────────────────────────────────────

    [Fact]
    public void FullScreenRect_OriginIsZero()
    {
        var ctx = new GraphicsContext();
        var rect = ctx.FullScreenRect();
        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
    }

    [Fact]
    public void FullScreenRect_DimensionsMatchWidthAndHeight()
    {
        var ctx = new GraphicsContext();
        var rect = ctx.FullScreenRect();
        Assert.Equal(ctx.Width,  rect.Width);
        Assert.Equal(ctx.Height, rect.Height);
    }
}
