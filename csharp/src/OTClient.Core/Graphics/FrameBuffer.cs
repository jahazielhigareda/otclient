using Raylib_cs;

namespace OTClient.Framework.Graphics;

/// <summary>
/// Wraps a Raylib <see cref="RenderTexture2D"/> (off-screen framebuffer) and
/// provides <see cref="Begin"/> / <see cref="End"/> helpers for render-to-texture
/// passes (e.g. light maps, post-processing effects, map layers).
/// <para>
/// Dispose to release GPU memory.
/// </para>
/// Maps to <c>src/framework/graphics/framebuffer.{h,cpp}</c>.
/// </summary>
public sealed class FrameBuffer : IDisposable
{
    private RenderTexture2D _handle;
    private bool _active;
    private bool _disposed;

    // ─── Constructor ──────────────────────────────────────────────────────────

    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    public FrameBuffer(int width, int height)
    {
        if (width  <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        _handle = Raylib.LoadRenderTexture(width, height);
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>Underlying Raylib handle.</summary>
    public RenderTexture2D Handle => _handle;

    /// <summary>The colour texture produced by this framebuffer.</summary>
    public Texture2D Texture => _handle.Texture;

    /// <summary>Width in pixels.</summary>
    public int Width => _handle.Texture.Width;

    /// <summary>Height in pixels.</summary>
    public int Height => _handle.Texture.Height;

    /// <summary><c>true</c> when a render-to-texture pass is active.</summary>
    public bool IsActive => _active;

    // ─── Render-to-texture ────────────────────────────────────────────────────

    /// <summary>
    /// Redirects subsequent Raylib draw calls into this framebuffer.
    /// Clears the buffer with <paramref name="clearColor"/>.
    /// Must be paired with <see cref="End"/>.
    /// </summary>
    public void Begin(Color clearColor = default)
    {
        if (_active)
            throw new InvalidOperationException("FrameBuffer.Begin already called.");
        Raylib.BeginTextureMode(_handle);
        Raylib.ClearBackground(clearColor);
        _active = true;
    }

    /// <summary>Restores normal screen rendering.</summary>
    public void End()
    {
        if (!_active)
            throw new InvalidOperationException("FrameBuffer.End called without matching Begin.");
        Raylib.EndTextureMode();
        _active = false;
    }

    // ─── Convenience drawing ──────────────────────────────────────────────────

    /// <summary>
    /// Draws this framebuffer's texture to the screen at position
    /// (<paramref name="x"/>, <paramref name="y"/>), flipped vertically as
    /// required by Raylib's OpenGL texture coordinate convention.
    /// </summary>
    public void DrawToScreen(int x, int y, Color tint = default)
    {
        // Raylib render textures are flipped vertically, so we negate the height
        // of the source rectangle to flip it back when drawing to screen.
        var source = new Rectangle(0, 0, Width, -Height);
        var dest   = new Rectangle(x, y, Width, Height);
        Raylib.DrawTexturePro(_handle.Texture, source, dest, Vector2.Zero, 0f,
            tint.A == 0 ? Color.White : tint);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_active)
        {
            Raylib.EndTextureMode();
            _active = false;
        }
        Raylib.UnloadRenderTexture(_handle);
    }
}
