namespace OTClient.Framework.UI;

/// <summary>
/// Stub widget types for client-specific rendering.
/// These extend <see cref="UIWidget"/> and act as placeholders for
/// game-world rendering that requires the full game state (Tasks 7.18–7.22).
/// </summary>

// ─── UIMap (7.18) ─────────────────────────────────────────────────────────────

/// <summary>
/// Renders the game map as a UI widget.
/// Full implementation requires the map/tile system (Phase 8+).
/// Task 7.18.
/// </summary>
public class UIMap : UIWidget
{
    public override string TypeName => "UIMap";

    /// <summary>Floor (z-level) currently shown.</summary>
    public int Floor { get; set; } = 7;

    /// <summary>Zoom level multiplier (1.0 = normal).</summary>
    public float Zoom { get; set; } = 1.0f;

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);
        // Map tiles drawn here when tile data is available (Phase 8)
    }
}

// ─── UIItem (7.19) ────────────────────────────────────────────────────────────

/// <summary>
/// Renders a single Tibia item sprite identified by its client ID.
/// Full sprite data requires the SPR/DAT loader (Phase 8+).
/// Task 7.19.
/// </summary>
public class UIItem : UIWidget
{
    public override string TypeName => "UIItem";

    /// <summary>Client item ID (0 = empty slot).</summary>
    public int ItemId { get; set; } = 0;

    /// <summary>Stack count shown as a number overlay.</summary>
    public int Count { get; set; } = 1;

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);
        // Item sprite drawn here when SPR data is available
    }
}

// ─── UICreature (7.20) ────────────────────────────────────────────────────────

/// <summary>
/// Renders a creature outfit animation.
/// Task 7.20.
/// </summary>
public class UICreature : UIWidget
{
    public override string TypeName => "UICreature";

    /// <summary>Outfit type ID (0 = empty).</summary>
    public int OutfitId { get; set; } = 0;

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);
        // Outfit sprite drawn here when outfit data is available
    }
}

// ─── UIMinimap (7.21) ────────────────────────────────────────────────────────

/// <summary>
/// Renders a minimap texture.
/// Task 7.21.
/// </summary>
public class UIMinimap : UIWidget
{
    public override string TypeName => "UIMinimap";

    /// <summary>Scale of one minimap pixel relative to the map tile grid.</summary>
    public float Scale { get; set; } = 1f;

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);
        // Minimap texture drawn here when map data is available
    }
}

// ─── UISprite (7.22) ──────────────────────────────────────────────────────────

/// <summary>
/// Renders a raw sprite by its SPR file index.
/// Task 7.22.
/// </summary>
public class UISprite : UIWidget
{
    public override string TypeName => "UISprite";

    /// <summary>Sprite ID within the loaded SPR file.</summary>
    public int SpriteId { get; set; } = 0;

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);
        // Sprite drawn here when SPR data is available
    }
}
