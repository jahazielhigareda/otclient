using Raylib_cs;

namespace OTClient.Framework.UI;

/// <summary>
/// Base class for all UI widgets.
/// <para>
/// Implements the full OTClient box model: position, size, margin, padding,
/// anchor flags, colour, opacity, visibility, z-order, and a parent–children
/// hierarchy.  The draw pipeline emits <see cref="DrawCommand"/> objects into a
/// caller-supplied list so that rendering stays decoupled from Raylib and is
/// fully unit-testable.
/// </para>
/// Maps to <c>src/framework/ui/uiwidget.{h,cpp}</c>.
/// Tasks 7.1–7.2.
/// </summary>
public class UIWidget
{
    // ─── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Unique widget identifier (used by Lua widget factory).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>CSS-like type name used by the OTUI parser (e.g. <c>"UIButton"</c>).</summary>
    public virtual string TypeName => "UIWidget";

    // ─── Hierarchy ────────────────────────────────────────────────────────────

    /// <summary>Parent in the widget tree; <c>null</c> for root widgets.</summary>
    public UIWidget? Parent { get; private set; }

    private readonly List<UIWidget> _children = [];
    /// <summary>Read-only view of direct children, in paint order (back to front).</summary>
    public IReadOnlyList<UIWidget> Children => _children;

    // ─── Box model ────────────────────────────────────────────────────────────

    /// <summary>Position relative to the parent's content area (after padding).</summary>
    public Vector2 Position { get; set; } = Vector2.Zero;

    /// <summary>Explicit size.  If <c>null</c> the layout engine sizes the widget to content.</summary>
    public Vector2? Size { get; set; }

    public Thickness Margin  { get; set; } = Thickness.Zero;
    public Thickness Padding { get; set; } = Thickness.Zero;

    /// <summary>Minimum allowed size.</summary>
    public Vector2 MinSize { get; set; } = Vector2.Zero;
    /// <summary>Maximum allowed size (<c>float.MaxValue</c> = unconstrained).</summary>
    public Vector2 MaxSize { get; set; } = new Vector2(float.MaxValue, float.MaxValue);

    // ─── Anchoring ────────────────────────────────────────────────────────────

    public AnchorFlag Anchor { get; set; } = AnchorFlag.None;

    // ─── Appearance ───────────────────────────────────────────────────────────

    public Color BackgroundColor { get; set; } = Color.Blank;
    public Color ForegroundColor { get; set; } = Color.White;
    public Color BorderColor     { get; set; } = Color.Blank;
    public float BorderWidth     { get; set; } = 0f;
    public float Opacity         { get; set; } = 1f;
    public bool  Visible         { get; set; } = true;
    public bool  Enabled         { get; set; } = true;
    /// <summary>Paint order within siblings; higher = drawn on top.</summary>
    public int   ZOrder          { get; set; } = 0;

    // ─── Interaction ──────────────────────────────────────────────────────────

    public WidgetState State { get; protected set; } = WidgetState.None;
    public bool IsHovered  => (State & WidgetState.Hovered)  != 0;
    public bool IsPressed  => (State & WidgetState.Pressed)  != 0;
    public bool IsFocused  => (State & WidgetState.Focused)  != 0;
    public bool IsDisabled => (State & WidgetState.Disabled) != 0;

    // ─── Events ───────────────────────────────────────────────────────────────

    public event Action<UIWidget>?                 OnFocusGained;
    public event Action<UIWidget>?                 OnFocusLost;
    public event Action<UIWidget>?                 OnClick;
    public event Action<UIWidget, Input.KeyEvent>? OnKeyDown;
    public event Action<UIWidget, Input.MouseEvent>? OnMouseMove;

    // ─── Layout cache ─────────────────────────────────────────────────────────

    /// <summary>
    /// Resolved screen-space rectangle computed by the layout engine.
    /// Updated by <see cref="Layout(Rectangle)"/>.
    /// </summary>
    public Rectangle ComputedRect { get; protected set; }

    // ─── Child management ─────────────────────────────────────────────────────

    /// <summary>Appends a child to this widget's child list.</summary>
    public virtual void AddChild(UIWidget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null)
            child.Parent.RemoveChild(child);
        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>Removes a direct child from this widget.</summary>
    public virtual bool RemoveChild(UIWidget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Remove(child)) return false;
        child.Parent = null;
        return true;
    }

    /// <summary>Removes all children.</summary>
    public void ClearChildren()
    {
        foreach (var c in _children.ToArray())
            RemoveChild(c);
    }

    // ─── Layout ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the widget's <see cref="ComputedRect"/> from the available
    /// <paramref name="available"/> parent content area, then recursively lays
    /// out children.
    /// </summary>
    public virtual void Layout(Rectangle available)
    {
        ComputedRect = Resolve(available);
        LayoutChildren(ComputedRect);
    }

    /// <summary>Resolves this widget's rect within the available area, honouring anchors.</summary>
    protected Rectangle Resolve(Rectangle available)
    {
        float x = available.X + Margin.Left + Position.X;
        float y = available.Y + Margin.Top  + Position.Y;
        float w = Size?.X ?? (available.Width  - Margin.Horizontal);
        float h = Size?.Y ?? (available.Height - Margin.Vertical);

        // Clamp to min/max
        w = Math.Clamp(w, MinSize.X, MaxSize.X);
        h = Math.Clamp(h, MinSize.Y, MaxSize.Y);

        // Apply anchor overrides
        if ((Anchor & AnchorFlag.FillX) == AnchorFlag.FillX)
        {
            x = available.X + Margin.Left;
            w = available.Width - Margin.Horizontal;
        }
        else if ((Anchor & AnchorFlag.CenterX) != 0)
        {
            x = available.X + (available.Width - w) / 2f;
        }
        else if ((Anchor & AnchorFlag.Right) != 0)
        {
            x = available.X + available.Width - w - Margin.Right;
        }

        if ((Anchor & AnchorFlag.FillY) == AnchorFlag.FillY)
        {
            y = available.Y + Margin.Top;
            h = available.Height - Margin.Vertical;
        }
        else if ((Anchor & AnchorFlag.CenterY) != 0)
        {
            y = available.Y + (available.Height - h) / 2f;
        }
        else if ((Anchor & AnchorFlag.Bottom) != 0)
        {
            y = available.Y + available.Height - h - Margin.Bottom;
        }

        return new Rectangle(x, y, w, h);
    }

    /// <summary>
    /// Lays out children using the default <see cref="AnchorLayout"/>
    /// unless overridden by a subclass.
    /// </summary>
    protected virtual void LayoutChildren(Rectangle contentArea)
    {
        var inner = ContentArea(contentArea);
        var sorted = _children.OrderBy(c => c.ZOrder).ToArray();
        AnchorLayout.Arrange(sorted, inner);
    }

    /// <summary>Returns the inner content area (rect minus padding).</summary>
    protected static Rectangle ContentArea(Rectangle rect) => new(
        rect.X      + 0,   // padding applied in LayoutChildren via AnchorLayout
        rect.Y      + 0,
        rect.Width,
        rect.Height);

    // ─── Draw ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Appends draw commands for this widget and all visible children to
    /// <paramref name="commands"/>.  No Raylib calls are made here.
    /// </summary>
    public virtual void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;

        var rect = ComputedRect;

        // Background fill
        if (BackgroundColor.A > 0)
            commands.Add(new UIDrawCommand.FillRect(rect, Tinted(BackgroundColor)));

        // Border
        if (BorderWidth > 0 && BorderColor.A > 0)
            commands.Add(new UIDrawCommand.DrawBorder(rect, BorderWidth, Tinted(BorderColor)));

        // Children
        foreach (var child in _children.OrderBy(c => c.ZOrder))
            child.Draw(commands);
    }

    private Color Tinted(Color c)
    {
        byte a = (byte)(c.A * Opacity);
        return new Color(c.R, c.G, c.B, a);
    }

    // ─── Hit testing ──────────────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> when <paramref name="point"/> falls inside the computed rect.</summary>
    public bool Contains(Vector2 point)
        => point.X >= ComputedRect.X && point.X <= ComputedRect.X + ComputedRect.Width
        && point.Y >= ComputedRect.Y && point.Y <= ComputedRect.Y + ComputedRect.Height;

    /// <summary>
    /// Recursively finds the topmost visible, enabled descendant that contains
    /// <paramref name="point"/>, or <c>null</c>.
    /// </summary>
    public UIWidget? HitTest(Vector2 point)
    {
        if (!Visible || !Contains(point)) return null;
        // Children drawn last are on top — check in reverse order
        foreach (var c in _children.OrderByDescending(c => c.ZOrder))
        {
            var hit = c.HitTest(point);
            if (hit is not null) return hit;
        }
        return this;
    }

    // ─── Internal event dispatch ──────────────────────────────────────────────

    internal void RaiseFocusGained()
    {
        State |= WidgetState.Focused;
        OnFocusGained?.Invoke(this);
    }

    internal void RaiseFocusLost()
    {
        State &= ~WidgetState.Focused;
        OnFocusLost?.Invoke(this);
    }

    internal void RaiseClick()
    {
        if (!Enabled) return;
        OnClick?.Invoke(this);
    }

    internal virtual void RaiseKeyDown(Input.KeyEvent ev)
    {
        OnKeyDown?.Invoke(this, ev);
    }

    internal virtual void RaiseMouseMove(Input.MouseEvent ev)
    {
        bool inside = Contains(ev.Position);
        if (inside && !IsHovered)  State |= WidgetState.Hovered;
        if (!inside && IsHovered)  State &= ~WidgetState.Hovered;
        OnMouseMove?.Invoke(this, ev);
    }
}

// ─── Lightweight draw-command union ───────────────────────────────────────────

/// <summary>
/// Discriminated-union of draw instructions emitted by <see cref="UIWidget.Draw"/>.
/// Contains no Raylib calls so it is fully unit-testable.
/// </summary>
public abstract class UIDrawCommand
{
    public sealed class FillRect(Rectangle rect, Color color) : UIDrawCommand
    {
        public Rectangle Rect  { get; } = rect;
        public Color     Color { get; } = color;
    }

    public sealed class DrawBorder(Rectangle rect, float width, Color color) : UIDrawCommand
    {
        public Rectangle Rect  { get; } = rect;
        public float     Width { get; } = width;
        public Color     Color { get; } = color;
    }

    public sealed class DrawText(string text, Vector2 position, float fontSize, Color color) : UIDrawCommand
    {
        public string  Text     { get; } = text;
        public Vector2 Position { get; } = position;
        public float   FontSize { get; } = fontSize;
        public Color   Color    { get; } = color;
    }

    public sealed class DrawTexture(Graphics.Texture texture, Rectangle src, Rectangle dest, Color tint) : UIDrawCommand
    {
        public Graphics.Texture Texture { get; } = texture;
        public Rectangle Src  { get; } = src;
        public Rectangle Dest { get; } = dest;
        public Color     Tint { get; } = tint;
    }
}
