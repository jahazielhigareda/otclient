using OTClient.Framework.Graphics;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="FontManager"/> and <see cref="BitmapFont"/> registry operations.
/// No GPU or Raylib window is required — only the in-memory registry logic is exercised.
/// Actual font loading (which requires GPU) is not tested here.
/// </summary>
public sealed class FontManagerTests : IDisposable
{
    private readonly FontManager _manager = new();

    public void Dispose() => _manager.Dispose();

    // ─── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void Default_IsNull_WhenNoFontLoaded()
    {
        Assert.Null(_manager.Default);
    }

    // ─── TryGet / Get ─────────────────────────────────────────────────────────

    [Fact]
    public void TryGet_MissingName_ReturnsNull()
    {
        Assert.Null(_manager.TryGet("nonexistent"));
    }

    [Fact]
    public void Get_MissingName_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => _manager.Get("nonexistent"));
    }

    // ─── Unload ───────────────────────────────────────────────────────────────

    [Fact]
    public void Unload_NonexistentName_DoesNotThrow()
    {
        // Should silently do nothing when the key is not registered
        _manager.Unload("ghost");
    }

    // ─── LoadFont argument validation ─────────────────────────────────────────

    [Fact]
    public void LoadFont_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _manager.LoadFont(null!, "font.ttf", 16));
    }

    [Fact]
    public void LoadFont_EmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _manager.LoadFont("", "font.ttf", 16));
    }

    [Fact]
    public void LoadFont_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _manager.LoadFont("ui", null!, 16));
    }

    [Fact]
    public void LoadFont_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _manager.LoadFont("ui", "", 16));
    }

    [Fact]
    public void LoadFont_ZeroSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _manager.LoadFont("ui", "font.ttf", 0));
    }

    [Fact]
    public void LoadFont_NegativeSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _manager.LoadFont("ui", "font.ttf", -1));
    }

    // ─── SetDefault argument validation ───────────────────────────────────────

    [Fact]
    public void SetDefault_UnknownName_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => _manager.SetDefault("missing"));
    }

    // ─── Dispose ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var manager = new FontManager();
        manager.Dispose();
        manager.Dispose(); // Idempotent
    }
}
