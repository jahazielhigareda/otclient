using OTClient.Framework.Graphics;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="Shader"/> factory argument validation.
/// GPU-dependent operations are only covered through their argument-guard paths —
/// no Raylib window or GPU context is required.
/// </summary>
public sealed class ShaderTests
{
    // ─── FromFiles argument validation ────────────────────────────────────────

    [Fact]
    public void FromFiles_NullFragmentPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Shader.FromFiles(null, null!));
    }

    [Fact]
    public void FromFiles_EmptyFragmentPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Shader.FromFiles(null, ""));
    }

    [Fact]
    public void FromFiles_WhitespaceFragmentPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Shader.FromFiles(null, "   "));
    }

    // ─── FromCode argument validation ─────────────────────────────────────────

    [Fact]
    public void FromCode_NullFragmentSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Shader.FromCode(null, null!));
    }

    [Fact]
    public void FromCode_EmptyFragmentSource_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Shader.FromCode(null, ""));
    }

    [Fact]
    public void FromCode_WhitespaceFragmentSource_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Shader.FromCode(null, "   "));
    }

    // ─── Dispose ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_ZeroHandle_DoesNotThrow()
    {
        // Use reflection to construct a Shader with default (zero-ID) handle.
        // A zero-ID shader has no GPU resource to release.
        var shader = CreateZeroShader();
        shader.Dispose(); // Should not throw
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var shader = CreateZeroShader();
        shader.Dispose();
        shader.Dispose(); // Idempotent
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="Shader"/> wrapping a zero-ID handle via reflection,
    /// bypassing the GPU factory methods so tests can run without a display.
    /// </summary>
    private static Shader CreateZeroShader()
    {
        // Shader has a private constructor: Shader(Raylib_cs.Shader handle)
        var ctor = typeof(Shader).GetConstructors(
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance)[0];

        return (Shader)ctor.Invoke([default(Raylib_cs.Shader)]);
    }
}
