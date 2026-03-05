using Raylib_cs;

namespace OTClient.Framework.Graphics;

/// <summary>
/// A loaded Raylib font with cached size metadata.
/// Dispose to release GPU memory.
/// </summary>
public sealed class BitmapFont : IDisposable
{
    private Font _handle;
    private bool _disposed;

    internal BitmapFont(Font handle, int size)
    {
        _handle = handle;
        Size    = size;
    }

    /// <summary>Underlying Raylib font handle.</summary>
    public Font Handle => _handle;

    /// <summary>The font size in points used when loading.</summary>
    public int Size { get; }

    /// <summary>Measures how much space <paramref name="text"/> will occupy.</summary>
    public Vector2 MeasureText(string text, float spacing = 1f) =>
        Raylib.MeasureTextEx(_handle, text, Size, spacing);

    /// <summary>Draws <paramref name="text"/> at the given position.</summary>
    public void Draw(string text, Vector2 position, Color color, float spacing = 1f) =>
        Raylib.DrawTextEx(_handle, text, position, Size, spacing, color);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle.BaseSize != 0)
        {
            Raylib.UnloadFont(_handle);
            _handle = default;
        }
    }
}

/// <summary>
/// Registry of <see cref="BitmapFont"/> instances loaded from TTF/OTF files.
/// <para>
/// Fonts are keyed by a developer-defined name so that the rest of the engine
/// can look them up without holding file path references.
/// </para>
/// Maps to <c>src/framework/graphics/fontmanager.{h,cpp}</c> and
/// <c>bitmapfont.{h,cpp}</c>.
/// </summary>
public sealed class FontManager : IDisposable
{
    private readonly Dictionary<string, BitmapFont> _fonts = [];
    private BitmapFont? _default;
    private bool _disposed;

    // ─── Loading ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a TrueType / OpenType font from <paramref name="filePath"/> at
    /// <paramref name="size"/> points and registers it under <paramref name="name"/>.
    /// </summary>
    /// <param name="setAsDefault">If <c>true</c>, this becomes the default font.</param>
    public BitmapFont LoadFont(string name, string filePath, int size, bool setAsDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

        if (_fonts.TryGetValue(name, out var existing))
            return existing;

        var handle = Raylib.LoadFontEx(filePath, size, null, 0);
        var font   = new BitmapFont(handle, size);
        _fonts[name] = font;

        if (setAsDefault || _default is null)
            _default = font;

        return font;
    }

    // ─── Lookup ───────────────────────────────────────────────────────────────

    /// <summary>Returns the font registered under <paramref name="name"/>, or <c>null</c>.</summary>
    public BitmapFont? TryGet(string name) =>
        _fonts.TryGetValue(name, out var f) ? f : null;

    /// <summary>Returns the font registered under <paramref name="name"/>.</summary>
    /// <exception cref="KeyNotFoundException"/>
    public BitmapFont Get(string name)
    {
        if (!_fonts.TryGetValue(name, out var f))
            throw new KeyNotFoundException($"Font '{name}' is not loaded.");
        return f;
    }

    /// <summary>
    /// Returns the default font, or <c>null</c> if none has been loaded yet.
    /// </summary>
    public BitmapFont? Default => _default;

    /// <summary>Sets the default font by registered name.</summary>
    public void SetDefault(string name) => _default = Get(name);

    // ─── Unload ───────────────────────────────────────────────────────────────

    /// <summary>Unloads a single font and removes it from the registry.</summary>
    public void Unload(string name)
    {
        if (!_fonts.Remove(name, out var font)) return;
        if (ReferenceEquals(_default, font)) _default = null;
        font.Dispose();
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var font in _fonts.Values)
            font.Dispose();
        _fonts.Clear();
    }
}
