using OTClient.Framework.Graphics;
using Raylib_cs;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Creates <see cref="TextureAtlas"/> instances without a GPU context for unit testing.
/// Uses a zero-ID <see cref="Texture2D"/> handle (never passed to Raylib draw calls).
/// </summary>
internal static class TextureAtlasTestHelper
{
    internal static TextureAtlas CreateWithRegions(Dictionary<string, AtlasRegion> regions)
    {
        // Texture.FromHandle(default) creates a wrapper around a zero-ID handle.
        // This is safe for read-only region lookup tests as long as we don't call Flush().
        var texture = Texture.FromHandle(default(Texture2D));
        return new TextureAtlas(texture, regions);
    }
}
