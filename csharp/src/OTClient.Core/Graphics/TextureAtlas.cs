using Raylib_cs;

namespace OTClient.Framework.Graphics;

/// <summary>
/// Describes a single sprite region that has been packed into a
/// <see cref="TextureAtlas"/>.
/// </summary>
public readonly struct AtlasRegion
{
    /// <summary>The sprite's source rectangle inside the atlas texture.</summary>
    public readonly Rectangle Source;

    /// <summary>The key used when the sprite was added.</summary>
    public readonly string Key;

    internal AtlasRegion(string key, Rectangle source)
    {
        Key = key;
        Source = source;
    }
}

/// <summary>
/// Packs multiple <see cref="Raylib_cs.Image"/> frames into a single GPU texture
/// using a shelf-based bin-packing algorithm (also known as next-fit decreasing
/// height).  This reduces draw-call batching overhead by avoiding texture switches.
/// <para>
/// Usage:
/// <code>
///   var builder = new TextureAtlasBuilder(1024, 1024);
///   builder.Add("hero",  heroImage);
///   builder.Add("sword", swordImage);
///   TextureAtlas atlas = builder.Build();
///   // Use atlas.GetRegion("hero") to retrieve the source rectangle.
/// </code>
/// </para>
/// Maps to <c>src/framework/graphics/textureatlas.*</c>.
/// </summary>
public sealed class TextureAtlasBuilder
{
    private readonly int _atlasWidth;
    private readonly int _atlasHeight;
    private readonly int _padding;

    // Pending frames before Build() is called
    private readonly List<(string key, Image image)> _pending = [];

    /// <param name="atlasWidth">Width of the output atlas texture in pixels.</param>
    /// <param name="atlasHeight">Height of the output atlas texture in pixels.</param>
    /// <param name="padding">Pixel gap inserted between sprites to prevent bleeding.</param>
    public TextureAtlasBuilder(int atlasWidth = 1024, int atlasHeight = 1024, int padding = 1)
    {
        if (atlasWidth  <= 0) throw new ArgumentOutOfRangeException(nameof(atlasWidth));
        if (atlasHeight <= 0) throw new ArgumentOutOfRangeException(nameof(atlasHeight));
        _atlasWidth  = atlasWidth;
        _atlasHeight = atlasHeight;
        _padding     = padding;
    }

    /// <summary>Adds a Raylib <see cref="Image"/> to the pending list.</summary>
    public void Add(string key, Image image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _pending.Add((key, image));
    }

    /// <summary>
    /// Performs shelf-packing and produces a <see cref="TextureAtlas"/>.
    /// Throws <see cref="InvalidOperationException"/> if the sprites do not fit.
    /// </summary>
    public TextureAtlas Build()
    {
        // Sort by height descending to minimise wasted shelf space
        var items = _pending
            .OrderByDescending(p => p.image.Height)
            .ToList();

        // Packing state
        int cursorX = _padding;
        int cursorY = _padding;
        int shelfHeight = 0;

        var canvas = Raylib.GenImageColor(_atlasWidth, _atlasHeight, Color.Blank);
        var regions = new Dictionary<string, AtlasRegion>(items.Count);

        foreach (var (key, src) in items)
        {
            int w = src.Width  + _padding;
            int h = src.Height + _padding;

            // Advance to a new shelf if the sprite doesn't fit horizontally
            if (cursorX + w > _atlasWidth)
            {
                cursorX = _padding;
                cursorY += shelfHeight + _padding;
                shelfHeight = 0;
            }

            if (cursorY + h > _atlasHeight)
                throw new InvalidOperationException(
                    $"Atlas size {_atlasWidth}×{_atlasHeight} is too small to fit all sprites.");

            var destRect = new Rectangle(cursorX, cursorY, src.Width, src.Height);
            Raylib.ImageDraw(ref canvas, src, new Rectangle(0, 0, src.Width, src.Height), destRect, Color.White);

            regions[key] = new AtlasRegion(key, destRect);

            cursorX    += w;
            shelfHeight = Math.Max(shelfHeight, src.Height);
        }

        var gpuTexture = Raylib.LoadTextureFromImage(canvas);
        Raylib.UnloadImage(canvas);

        return new TextureAtlas(Texture.FromHandle(gpuTexture), regions);
    }
}

/// <summary>
/// A read-only GPU texture that contains multiple packed sprite regions.
/// Dispose to release GPU memory.
/// </summary>
public sealed class TextureAtlas : IDisposable
{
    private readonly Texture _texture;
    private readonly IReadOnlyDictionary<string, AtlasRegion> _regions;
    private bool _disposed;

    internal TextureAtlas(Texture texture, Dictionary<string, AtlasRegion> regions)
    {
        _texture = texture;
        _regions = regions;
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>The underlying GPU texture holding all packed sprites.</summary>
    public Texture Texture => _texture;

    /// <summary>Number of sprite regions packed into this atlas.</summary>
    public int RegionCount => _regions.Count;

    // ─── Region lookup ────────────────────────────────────────────────────────

    /// <summary>Returns the region for <paramref name="key"/>, or <c>null</c> if not found.</summary>
    public AtlasRegion? TryGetRegion(string key) =>
        _regions.TryGetValue(key, out var r) ? r : null;

    /// <summary>Returns the region for <paramref name="key"/>.</summary>
    /// <exception cref="KeyNotFoundException"/>
    public AtlasRegion GetRegion(string key)
    {
        if (!_regions.TryGetValue(key, out var r))
            throw new KeyNotFoundException($"Atlas region '{key}' not found.");
        return r;
    }

    /// <summary>All regions packed in this atlas.</summary>
    public IEnumerable<AtlasRegion> Regions => _regions.Values;

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _texture.Dispose();
    }
}
