using Raylib_cs;

namespace OTClient.Framework.Graphics;

/// <summary>
/// Wraps a Raylib <see cref="Texture2D"/> and exposes a high-level API for
/// loading from a file path, a raw byte buffer, or a generated solid colour.
/// <para>
/// Dispose when the texture is no longer needed to release GPU memory.
/// </para>
/// Maps to <c>src/framework/graphics/texture.{h,cpp}</c>.
/// </summary>
public sealed class Texture : IDisposable
{
    private Texture2D _handle;
    private bool _disposed;

    // ─── Constructors / factories ─────────────────────────────────────────────

    private Texture(Texture2D handle)
    {
        _handle = handle;
    }

    /// <summary>Creates a <see cref="Texture"/> from a raw <see cref="Texture2D"/> handle (internal use).</summary>
    internal static Texture FromHandle(Texture2D handle) => new(handle);

    /// <summary>Loads a texture from a file on disk (.png, .bmp, .jpg, …).</summary>
    public static Texture FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var handle = Raylib.LoadTexture(path);
        return new Texture(handle);
    }

    /// <summary>Loads a texture from raw image bytes in memory.</summary>
    /// <param name="fileType">File extension without dot, e.g. <c>"png"</c>.</param>
    public static Texture FromMemory(string fileType, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileType);
        ArgumentNullException.ThrowIfNull(data);

        var image = Raylib.LoadImageFromMemory($".{fileType}", data);
        var handle = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        return new Texture(handle);
    }

    /// <summary>Creates a solid-colour texture of the given dimensions.</summary>
    public static Texture FromColor(int width, int height, Color color)
    {
        if (width  <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        var image = Raylib.GenImageColor(width, height, color);
        var handle = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        return new Texture(handle);
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>Underlying Raylib handle.  Use with Raylib draw calls.</summary>
    public Texture2D Handle => _handle;

    /// <summary>Width in pixels.</summary>
    public int Width => _handle.Width;

    /// <summary>Height in pixels.</summary>
    public int Height => _handle.Height;

    /// <summary>Returns a <see cref="Rectangle"/> covering the full texture.</summary>
    public Rectangle SourceRect() => new(0, 0, Width, Height);

    /// <summary><c>true</c> when the underlying GPU texture ID is non-zero.</summary>
    public bool IsValid => _handle.Id != 0;

    // ─── Texture filter / wrap ────────────────────────────────────────────────

    /// <summary>Sets the texture filter mode (e.g. nearest for pixel art).</summary>
    public void SetFilter(TextureFilter filter)
    {
        Raylib.SetTextureFilter(_handle, filter);
    }

    /// <summary>Sets the texture wrap mode.</summary>
    public void SetWrap(TextureWrap wrap)
    {
        Raylib.SetTextureWrap(_handle, wrap);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle.Id != 0)
        {
            Raylib.UnloadTexture(_handle);
            _handle = default;
        }
    }
}
