using OTClient.Framework.Graphics;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="DrawPool"/> and related draw command types.
/// No Raylib GPU context is required — only the command-list logic is exercised.
/// </summary>
public sealed class DrawPoolTests
{
    private static DrawPool MakePool(RenderLayer layer = RenderLayer.Ui) =>
        new("test", layer);

    // ─── Command submission ───────────────────────────────────────────────────

    [Fact]
    public void NewPool_HasZeroCommands()
    {
        var pool = MakePool();
        Assert.Equal(0, pool.CommandCount);
    }

    [Fact]
    public void Add_IncreasesCommandCount()
    {
        var pool = MakePool();
        pool.Add(new RectCommand { Rect = new(0, 0, 10, 10), Tint = Raylib_cs.Color.Red });
        pool.Add(new RectCommand { Rect = new(20, 20, 5, 5), Tint = Raylib_cs.Color.Blue });
        Assert.Equal(2, pool.CommandCount);
    }

    [Fact]
    public void Clear_RemovesAllCommands()
    {
        var pool = MakePool();
        pool.Add(new RectCommand { Rect = new(0, 0, 1, 1) });
        pool.Add(new RectCommand { Rect = new(0, 0, 2, 2) });
        pool.Clear();
        Assert.Equal(0, pool.CommandCount);
    }

    [Fact]
    public void Add_NullCommand_ThrowsArgumentNullException()
    {
        var pool = MakePool();
        Assert.Throws<ArgumentNullException>(() => pool.Add(null!));
    }

    // ─── Identity ─────────────────────────────────────────────────────────────

    [Fact]
    public void Pool_Name_IsPreserved()
    {
        var pool = new DrawPool("ground_layer", RenderLayer.Ground);
        Assert.Equal("ground_layer", pool.Name);
    }

    [Fact]
    public void Pool_Layer_IsPreserved()
    {
        var pool = new DrawPool("creatures", RenderLayer.Creatures);
        Assert.Equal(RenderLayer.Creatures, pool.Layer);
    }

    // ─── Command kind ─────────────────────────────────────────────────────────

    [Fact]
    public void RectCommand_HasCorrectKind()
    {
        var cmd = new RectCommand { Rect = new(0, 0, 1, 1) };
        Assert.Equal(DrawCommandKind.Rect, cmd.Kind);
    }

    [Fact]
    public void SpriteCommand_Kind_EnumValueIsDistinctFromOtherKinds()
    {
        // SpriteCommand requires a live Texture (GPU resource) so we cannot
        // instantiate it in unit tests. Instead we verify the enum discriminator
        // value is unique across all DrawCommandKind members.
        Assert.NotEqual(DrawCommandKind.Sprite, DrawCommandKind.Rect);
        Assert.NotEqual(DrawCommandKind.Sprite, DrawCommandKind.Text);
    }

    [Fact]
    public void TextCommand_HasCorrectKind()
    {
        var cmd = new TextCommand { Text = "hello", Position = new(0, 0), FontSize = 14 };
        Assert.Equal(DrawCommandKind.Text, cmd.Kind);
    }
}
