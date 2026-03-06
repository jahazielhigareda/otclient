using Raylib_cs;

namespace OTClient.Framework.Graphics;

/// <summary>
/// Stateless 2-D draw API that wraps common Raylib draw calls and applies a
/// uniform tint colour and opacity.  All methods are self-contained — no state
/// is retained between calls.
/// <para>
/// Use <see cref="Painter"/> as a convenience layer on top of raw Raylib calls
/// when you need consistent colour/opacity handling across the whole scene.
/// </para>
/// Maps to the <c>Painter</c> abstraction referenced in
/// <c>src/framework/graphics/painter.{h,cpp}</c>.
/// </summary>
public sealed class Painter
{
    // ─── Global state ─────────────────────────────────────────────────────────

    /// <summary>
    /// Global tint multiplied into every draw call.
    /// Defaults to <see cref="Color.White"/> (no tint).
    /// </summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>
    /// Global opacity in the range [0, 1].  Applied on top of <see cref="Tint"/>'s
    /// alpha channel.
    /// </summary>
    public float Opacity
    {
        get => _opacity;
        set => _opacity = Math.Clamp(value, 0f, 1f);
    }

    private float _opacity = 1f;

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Combines <see cref="Tint"/> with <see cref="Opacity"/> into a final
    /// <see cref="Color"/> suitable for passing to Raylib draw functions.
    /// </summary>
    private Color EffectiveTint()
    {
        byte alpha = (byte)(Tint.A * _opacity);
        return new Color(Tint.R, Tint.G, Tint.B, alpha);
    }

    // ─── Rectangle ────────────────────────────────────────────────────────────

    /// <summary>Draws a filled rectangle.</summary>
    public void DrawRect(Rectangle rect) =>
        Raylib.DrawRectangleRec(rect, EffectiveTint());

    /// <summary>Draws a filled rectangle with an explicit colour.</summary>
    public void DrawRect(Rectangle rect, Color color) =>
        Raylib.DrawRectangleRec(rect, BlendColor(color));

    /// <summary>Draws a rectangle outline.</summary>
    public void DrawRectLines(Rectangle rect, float thickness) =>
        Raylib.DrawRectangleLinesEx(rect, thickness, EffectiveTint());

    // ─── Sprite ───────────────────────────────────────────────────────────────

    /// <summary>Draws a texture region to a destination rectangle.</summary>
    public void DrawSprite(
        Texture texture,
        Rectangle source,
        Rectangle dest,
        Vector2   origin   = default,
        float     rotation = 0f)
    {
        Raylib.DrawTexturePro(texture.Handle, source, dest, origin, rotation, EffectiveTint());
    }

    /// <summary>Draws an entire texture at the given screen position.</summary>
    public void DrawTexture(Texture texture, int x, int y) =>
        Raylib.DrawTexture(texture.Handle, x, y, EffectiveTint());

    // ─── Text ─────────────────────────────────────────────────────────────────

    /// <summary>Draws text using the Raylib default font.</summary>
    public void DrawText(string text, Vector2 position, int fontSize) =>
        Raylib.DrawText(text, (int)position.X, (int)position.Y, fontSize, EffectiveTint());

    /// <summary>Draws text using a loaded <see cref="BitmapFont"/>.</summary>
    public void DrawText(BitmapFont font, string text, Vector2 position, float spacing = 1f) =>
        Raylib.DrawTextEx(font.Handle, text, position, font.Size, spacing, EffectiveTint());

    // ─── Line / circle ────────────────────────────────────────────────────────

    /// <summary>Draws a line between two points.</summary>
    public void DrawLine(Vector2 start, Vector2 end, float thickness = 1f) =>
        Raylib.DrawLineEx(start, end, thickness, EffectiveTint());

    /// <summary>Draws a filled circle.</summary>
    public void DrawCircle(Vector2 center, float radius) =>
        Raylib.DrawCircleV(center, radius, EffectiveTint());

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Blends an explicit colour with the current tint and opacity.</summary>
    private Color BlendColor(Color c)
    {
        byte r = (byte)(c.R * Tint.R / 255);
        byte g = (byte)(c.G * Tint.G / 255);
        byte b = (byte)(c.B * Tint.B / 255);
        byte a = (byte)(c.A * Tint.A / 255 * _opacity);
        return new Color(r, g, b, a);
    }
}
