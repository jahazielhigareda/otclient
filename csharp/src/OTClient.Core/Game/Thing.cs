using System.Numerics;
using OTClient.Framework.UI;
using Raylib_cs;

namespace OTClient.Framework.Game;

// ─── Thing ────────────────────────────────────────────────────────────────────

/// <summary>
/// Abstract base for every object that can exist in the world: items,
/// creatures, effects, and missiles.
/// Maps to <c>src/client/thing.h</c>.
/// Task 8.2.
/// </summary>
public abstract class Thing
{
    /// <summary>World position of this thing.</summary>
    public Position Position { get; set; } = Position.Invalid;

    /// <summary>The type descriptor loaded from .dat / appearances.</summary>
    public ThingType? ThingType { get; set; }

    public abstract ThingCategory Category { get; }

    public bool IsItem     => Category == ThingCategory.Item;
    public bool IsCreature => Category == ThingCategory.Creature;
    public bool IsEffect   => Category == ThingCategory.Effect;
    public bool IsMissile  => Category == ThingCategory.Missile;
}

// ─── Item ─────────────────────────────────────────────────────────────────────

/// <summary>
/// A single in-world or in-inventory item instance.
/// Maps to <c>src/client/item.h</c>.
/// Task 8.6.
/// </summary>
public sealed class Item : Thing
{
    public override ThingCategory Category => ThingCategory.Item;

    /// <summary>Client item type ID.</summary>
    public int Id { get; set; }

    /// <summary>Count / sub-type (stack amount, fluid type, etc.).</summary>
    public int Count { get; set; } = 1;

    /// <summary>Returns a new item with the given ID and count.</summary>
    public static Item Create(int id, int count = 1) => new() { Id = id, Count = count };

    // ─── Derived properties from ThingType ────────────────────────────────────

    public bool IsStackable   => ThingType?.IsStackable  ?? false;
    public bool IsPickupable  => ThingType?.IsPickupable ?? false;
    public bool IsContainer   => ThingType?.IsContainer  ?? false;
    public bool IsNotWalkable => ThingType?.IsNotWalkable ?? false;
}

// ─── ItemType ─────────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight metadata for a client item type (wraps <see cref="ThingType"/>
/// with game-specific convenience fields such as name and sell price).
/// Task 8.6.
/// </summary>
public sealed class ItemType
{
    public int    Id       { get; init; }
    public string Name     { get; init; } = string.Empty;
    public int    BuyPrice { get; init; }
    public int    SellPrice { get; init; }
    public ThingType? ThingType { get; init; }
}

// ─── Effect ───────────────────────────────────────────────────────────────────

/// <summary>
/// An animated visual effect placed on a tile (spell graphics, splashes, etc.).
/// Maps to <c>src/client/effect.h</c>.
/// Task 8.14.
/// </summary>
public sealed class Effect : Thing
{
    public override ThingCategory Category => ThingCategory.Effect;

    public int       TypeId   { get; set; }
    public Animator  Animator { get; init; } = new(1, 300, 300, loop: false);
    public bool      IsFinished => Animator.IsFinished;

    public void Update(int deltaMs) => Animator.Update(deltaMs);

    /// <summary>
    /// Emits draw commands to render this effect on the map.
    /// Emits a 32×32 filled rectangle tinted by <see cref="Position"/>.
    /// When the effect is finished or not visible in <paramref name="view"/>,
    /// nothing is emitted.
    /// Mirrors <c>Effect::draw</c>.
    /// Task T20.
    /// </summary>
    public void Draw(List<UIDrawCommand> commands, MapView view)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(view);
        if (IsFinished || !view.IsVisible(Position)) return;

        var screen = view.WorldToScreen(Position);
        int ts     = view.TileSize;
        var rect   = new Rectangle(screen.X, screen.Y, ts, ts);

        var color = new Color((byte)255, (byte)200, (byte)80, (byte)220); // amber placeholder
        commands.Add(new UIDrawCommand.FillRect(rect, color));
    }
}

// ─── Missile ──────────────────────────────────────────────────────────────────

/// <summary>
/// A projectile that travels between two world positions.
/// Maps to <c>src/client/missile.h</c>.
/// Task 8.15.
/// </summary>
public sealed class Missile : Thing
{
    public override ThingCategory Category => ThingCategory.Missile;

    public int      TypeId  { get; set; }
    public Position From    { get; set; }
    public Position To      { get; set; }

    private float _progress; // 0.0 → 1.0

    public float Progress   => _progress;
    public bool  IsFinished => _progress >= 1f;

    /// <summary>Updates flight progress; speed is in tiles per second.</summary>
    public void Update(float deltaSeconds, float speedTilesPerSec = 10f)
    {
        float dist = Math.Max(1f, From.ChebyshevDistance(To));
        _progress  = Math.Clamp(_progress + deltaSeconds * speedTilesPerSec / dist, 0f, 1f);
    }

    /// <summary>Interpolated world X.</summary>
    public float CurrentX => From.X + (To.X - From.X) * _progress;
    /// <summary>Interpolated world Y.</summary>
    public float CurrentY => From.Y + (To.Y - From.Y) * _progress;

    /// <summary>
    /// Emits a draw command for this missile at its interpolated screen position.
    /// When finished or the current position is outside the viewport, nothing is emitted.
    /// Mirrors <c>Missile::draw</c>.
    /// Task T20.
    /// </summary>
    public void Draw(List<UIDrawCommand> commands, MapView view)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(view);
        if (IsFinished) return;

        // Interpolated world position as a synthetic Position on the missile's floor
        var worldPos = new Position((int)MathF.Round(CurrentX), (int)MathF.Round(CurrentY), From.Z);
        if (!view.IsVisible(worldPos)) return;

        // Compute sub-tile screen position using exact float coords
        var fromScreen = view.WorldToScreen(From);
        var toScreen   = view.WorldToScreen(To);
        float sx = fromScreen.X + (toScreen.X - fromScreen.X) * _progress;
        float sy = fromScreen.Y + (toScreen.Y - fromScreen.Y) * _progress;

        int ts  = view.TileSize;
        var rect = new Rectangle(sx - ts / 4f, sy - ts / 4f, ts / 2f, ts / 2f);
        var color = new Color((byte)255, (byte)80, (byte)80, (byte)220); // red placeholder
        commands.Add(new UIDrawCommand.FillRect(rect, color));
    }
}

// ─── AnimatedText ─────────────────────────────────────────────────────────────

/// <summary>
/// Floating damage / heal number shown above a creature.
/// Maps to <c>src/client/animatedtext.h</c>.
/// Task 8.16.
/// </summary>
public sealed class AnimatedText
{
    // ─── Configuration constants ───────────────────────────────────────────────
    private const float FloatHeightPx = 32f;   // total pixels the text rises
    private const float FadeStartFrac = 0.75f;  // start fading after 75% of duration
    private const float DefaultFontSize = 14f;

    public Position   Position   { get; set; }
    public string     Text       { get; set; } = string.Empty;
    public Color      Color      { get; set; } = Color.White;
    public float      DurationMs { get; set; } = 1500f;
    public float      ElapsedMs  { get; private set; }
    public bool       IsExpired  => ElapsedMs >= DurationMs;

    /// <summary>Vertical offset (pixels) for the float-up animation.</summary>
    public float FloatOffset   => ElapsedMs / DurationMs * FloatHeightPx;

    public void Update(float deltaMs) => ElapsedMs = Math.Min(ElapsedMs + deltaMs, DurationMs);

    /// <summary>
    /// Emits a <see cref="UIDrawCommand.DrawText"/> command for this floating
    /// text at the correct world-to-screen position plus float-up offset.
    /// Nothing is emitted when expired or outside the viewport.
    /// Mirrors <c>AnimatedText::drawText</c>.
    /// Task T19.
    /// </summary>
    public void Draw(List<UIDrawCommand> commands, MapView view)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(view);
        if (IsExpired || !view.IsVisible(Position)) return;

        var screen = view.WorldToScreen(Position);

        // Float upward proportional to elapsed time
        float yOffset = -FloatOffset;

        // Fade out in the final portion of the animation
        float frac = ElapsedMs / DurationMs;
        byte  alpha = frac > FadeStartFrac
            ? (byte)((1f - (frac - FadeStartFrac) / (1f - FadeStartFrac)) * 255)
            : (byte)255;

        var color = new Color(Color.R, Color.G, Color.B, (byte)alpha);
        var pos   = new Vector2(screen.X, screen.Y + yOffset);
        commands.Add(new UIDrawCommand.DrawText(Text, pos, DefaultFontSize, color));
    }
}

// ─── StaticText ───────────────────────────────────────────────────────────────

/// <summary>
/// A chat/status message rendered above a creature's head.
/// Maps to <c>src/client/statictext.h</c>.
/// Task 8.17.
/// </summary>
public sealed class StaticText
{
    private const float DefaultFontSize = 12f;

    public Position   Position      { get; set; }
    public string     Text          { get; set; } = string.Empty;
    public Color      Color         { get; set; } = Color.White;
    public float      LifetimeMs    { get; set; } = 4000f;
    public float      ElapsedMs     { get; private set; }
    public bool       IsExpired     => ElapsedMs >= LifetimeMs;

    public void Update(float deltaMs) => ElapsedMs = Math.Min(ElapsedMs + deltaMs, LifetimeMs);

    /// <summary>
    /// Emits a <see cref="UIDrawCommand.DrawText"/> command for this static text
    /// centered above the creature at <see cref="Position"/>.
    /// Nothing is emitted when expired or outside the viewport.
    /// Mirrors <c>StaticText::drawText</c>.
    /// Task T19.
    /// </summary>
    public void Draw(List<UIDrawCommand> commands, MapView view)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(view);
        if (IsExpired || !view.IsVisible(Position)) return;

        var screen = view.WorldToScreen(Position);
        // Position the text origin one tile above the creature; horizontal
        // centering is left to the renderer which knows the actual text width.
        var pos = new Vector2(screen.X, screen.Y - view.TileSize);
        commands.Add(new UIDrawCommand.DrawText(Text, pos, DefaultFontSize, Color));
    }
}
