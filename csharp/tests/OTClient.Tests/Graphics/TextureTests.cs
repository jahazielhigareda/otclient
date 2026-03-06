using OTClient.Framework.Graphics;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="Texture"/> factory argument validation.
/// GPU-dependent factories (FromFile, FromMemory, FromColor) are only tested for
/// their argument-guard paths — no Raylib window or GPU context is required.
/// </summary>
public sealed class TextureTests
{
    // ─── FromFile argument validation ─────────────────────────────────────────

    [Fact]
    public void FromFile_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Texture.FromFile(null!));
    }

    [Fact]
    public void FromFile_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Texture.FromFile(""));
    }

    [Fact]
    public void FromFile_WhitespacePath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Texture.FromFile("   "));
    }

    // ─── FromMemory argument validation ───────────────────────────────────────

    [Fact]
    public void FromMemory_NullFileType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Texture.FromMemory(null!, [1, 2, 3]));
    }

    [Fact]
    public void FromMemory_EmptyFileType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Texture.FromMemory("", [1, 2, 3]));
    }

    [Fact]
    public void FromMemory_NullData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Texture.FromMemory("png", null!));
    }

    // ─── FromColor argument validation ────────────────────────────────────────

    [Fact]
    public void FromColor_ZeroWidth_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Texture.FromColor(0, 16, Raylib_cs.Color.Red));
    }

    [Fact]
    public void FromColor_NegativeWidth_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Texture.FromColor(-1, 16, Raylib_cs.Color.Red));
    }

    [Fact]
    public void FromColor_ZeroHeight_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Texture.FromColor(16, 0, Raylib_cs.Color.Red));
    }

    [Fact]
    public void FromColor_NegativeHeight_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Texture.FromColor(16, -1, Raylib_cs.Color.Red));
    }

    // ─── FromHandle (zero handle) ─────────────────────────────────────────────

    [Fact]
    public void FromHandle_ZeroId_IsNotValid()
    {
        // A default (zero) Texture2D handle is not a real GPU texture.
        var texture = Texture.FromHandle(default);
        Assert.False(texture.IsValid);
    }

    [Fact]
    public void FromHandle_ZeroId_Width_IsZero()
    {
        var texture = Texture.FromHandle(default);
        Assert.Equal(0, texture.Width);
    }

    [Fact]
    public void FromHandle_ZeroId_Height_IsZero()
    {
        var texture = Texture.FromHandle(default);
        Assert.Equal(0, texture.Height);
    }

    [Fact]
    public void FromHandle_ZeroId_SourceRect_IsZero()
    {
        var texture = Texture.FromHandle(default);
        var rect = texture.SourceRect();
        Assert.Equal(0, rect.Width);
        Assert.Equal(0, rect.Height);
    }

    // ─── Dispose ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_ZeroHandle_DoesNotThrow()
    {
        // Disposing a zero-ID texture (no GPU resource) should be a no-op.
        var texture = Texture.FromHandle(default);
        texture.Dispose(); // Should not throw
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var texture = Texture.FromHandle(default);
        texture.Dispose();
        texture.Dispose(); // Idempotent
    }
}
