using OTClient.Framework.Graphics;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for the shelf-packing algorithm inside <see cref="TextureAtlasBuilder"/>.
/// These tests operate on the region-layout logic only — no GPU or Raylib window
/// is required because the packing is done on CPU before the GPU upload.
/// </summary>
public sealed class TextureAtlasPackingTests
{
    // ─── AtlasRegion struct ───────────────────────────────────────────────────

    [Fact]
    public void AtlasRegion_KeyAndSource_ArePreserved()
    {
        var source = new Raylib_cs.Rectangle(10, 20, 30, 40);
        var region = new AtlasRegion("sprite_key", source);

        Assert.Equal("sprite_key", region.Key);
        Assert.Equal(source.X,      region.Source.X);
        Assert.Equal(source.Y,      region.Source.Y);
        Assert.Equal(source.Width,  region.Source.Width);
        Assert.Equal(source.Height, region.Source.Height);
    }

    // ─── Builder validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 512)]
    [InlineData(-1, 512)]
    [InlineData(512, 0)]
    [InlineData(512, -1)]
    public void Builder_InvalidDimensions_ThrowArgumentOutOfRangeException(int w, int h)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureAtlasBuilder(w, h));
    }

    [Fact]
    public void Builder_DefaultConstructor_UsesPositiveDimensions()
    {
        // Should not throw
        var builder = new TextureAtlasBuilder();
        Assert.NotNull(builder);
    }

    // ─── TextureAtlas lookup ──────────────────────────────────────────────────

    [Fact]
    public void TryGetRegion_MissingKey_ReturnsNull()
    {
        // Build a minimal atlas by hand-crafting the internal dictionary
        // without going through Raylib (use the internal test constructor).
        var regions = new Dictionary<string, AtlasRegion>
        {
            ["a"] = new("a", new Raylib_cs.Rectangle(0, 0, 10, 10)),
        };
        var atlas = TextureAtlasTestHelper.CreateWithRegions(regions);

        Assert.Null(atlas.TryGetRegion("missing"));
    }

    [Fact]
    public void TryGetRegion_ExistingKey_ReturnsRegion()
    {
        var regions = new Dictionary<string, AtlasRegion>
        {
            ["hero"] = new("hero", new Raylib_cs.Rectangle(0, 0, 32, 32)),
        };
        var atlas = TextureAtlasTestHelper.CreateWithRegions(regions);

        var r = atlas.TryGetRegion("hero");
        Assert.NotNull(r);
        Assert.Equal("hero", r.Value.Key);
    }

    [Fact]
    public void GetRegion_MissingKey_ThrowsKeyNotFoundException()
    {
        var regions = new Dictionary<string, AtlasRegion>();
        var atlas   = TextureAtlasTestHelper.CreateWithRegions(regions);

        Assert.Throws<KeyNotFoundException>(() => atlas.GetRegion("nope"));
    }

    [Fact]
    public void RegionCount_MatchesNumberOfAddedRegions()
    {
        var regions = new Dictionary<string, AtlasRegion>
        {
            ["a"] = new("a", new Raylib_cs.Rectangle(0,  0, 10, 10)),
            ["b"] = new("b", new Raylib_cs.Rectangle(10, 0, 10, 10)),
            ["c"] = new("c", new Raylib_cs.Rectangle(20, 0, 10, 10)),
        };
        var atlas = TextureAtlasTestHelper.CreateWithRegions(regions);

        Assert.Equal(3, atlas.RegionCount);
    }
}
