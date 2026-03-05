using OTClient.Framework.Graphics;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="DrawPoolManager"/> pool management (no GPU required).
/// </summary>
public sealed class DrawPoolManagerTests
{
    // ─── Layer coverage ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(RenderLayer.Ground)]
    [InlineData(RenderLayer.Items)]
    [InlineData(RenderLayer.Creatures)]
    [InlineData(RenderLayer.Effects)]
    [InlineData(RenderLayer.Ui)]
    public void GetPool_ReturnsPoolForEachLayer(RenderLayer layer)
    {
        var manager = new DrawPoolManager();
        var pool = manager.GetPool(layer);
        Assert.NotNull(pool);
        Assert.Equal(layer, pool.Layer);
    }

    [Fact]
    public void Indexer_ReturnsSamePoolAsGetPool()
    {
        var manager = new DrawPoolManager();
        Assert.Same(manager.GetPool(RenderLayer.Ui), manager[RenderLayer.Ui]);
    }

    // ─── TotalCommandCount ────────────────────────────────────────────────────

    [Fact]
    public void TotalCommandCount_StartsAtZero()
    {
        var manager = new DrawPoolManager();
        Assert.Equal(0, manager.TotalCommandCount);
    }

    [Fact]
    public void TotalCommandCount_SumsAcrossLayers()
    {
        var manager = new DrawPoolManager();
        manager[RenderLayer.Ground].Add(new RectCommand { Rect = new(0, 0, 1, 1) });
        manager[RenderLayer.Ui].Add(new RectCommand    { Rect = new(0, 0, 2, 2) });
        manager[RenderLayer.Ui].Add(new RectCommand    { Rect = new(0, 0, 3, 3) });
        Assert.Equal(3, manager.TotalCommandCount);
    }

    // ─── ClearAll ─────────────────────────────────────────────────────────────

    [Fact]
    public void ClearAll_ResetsAllPools()
    {
        var manager = new DrawPoolManager();
        manager[RenderLayer.Ground].Add(new RectCommand { Rect = new(0, 0, 1, 1) });
        manager[RenderLayer.Items].Add(new RectCommand  { Rect = new(0, 0, 2, 2) });

        manager.ClearAll();

        Assert.Equal(0, manager.TotalCommandCount);
    }
}
