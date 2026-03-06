using Raylib_cs;

namespace OTClient.Framework.UI;

// ─── Anchor flags ─────────────────────────────────────────────────────────────

/// <summary>
/// Specifies which edges or axes of a parent widget a child should be anchored to.
/// Multiple flags may be combined.
/// Maps to the OTClient anchor system in <c>src/framework/ui/uilayout.h</c>.
/// </summary>
[Flags]
public enum AnchorFlag
{
    None   = 0,
    Left   = 1 << 0,
    Right  = 1 << 1,
    Top    = 1 << 2,
    Bottom = 1 << 3,
    /// <summary>Horizontally centre within the parent (overrides Left/Right).</summary>
    CenterX = 1 << 4,
    /// <summary>Vertically centre within the parent (overrides Top/Bottom).</summary>
    CenterY = 1 << 5,
    /// <summary>Fill the full horizontal extent of the parent.</summary>
    FillX  = Left | Right,
    /// <summary>Fill the full vertical extent of the parent.</summary>
    FillY  = Top | Bottom,
    /// <summary>Fill the entire parent area.</summary>
    Fill   = FillX | FillY,
    /// <summary>Centre within both axes of the parent.</summary>
    Center = CenterX | CenterY,
}

// ─── Layout direction ─────────────────────────────────────────────────────────

/// <summary>Axis direction used by <see cref="BoxLayout"/> and <see cref="FlexBoxLayout"/>.</summary>
public enum LayoutDirection { Horizontal, Vertical }

// ─── Widget state ─────────────────────────────────────────────────────────────

/// <summary>Combined interaction state flags for a widget.</summary>
[Flags]
public enum WidgetState
{
    None     = 0,
    Hovered  = 1 << 0,
    Pressed  = 1 << 1,
    Focused  = 1 << 2,
    Disabled = 1 << 3,
    Checked  = 1 << 4,
}

// ─── Thickness (margins / padding) ───────────────────────────────────────────

/// <summary>
/// Uniform four-sided thickness used for margins and padding.
/// </summary>
public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    public static readonly Thickness Zero = new(0, 0, 0, 0);

    public Thickness(float all) : this(all, all, all, all) { }
    public Thickness(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    /// <summary>Sum of horizontal (left + right) thickness.</summary>
    public float Horizontal => Left + Right;
    /// <summary>Sum of vertical (top + bottom) thickness.</summary>
    public float Vertical   => Top  + Bottom;

    public static Thickness Parse(string s)
    {
        var parts = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 => new Thickness(float.Parse(parts[0])),
            2 => new Thickness(float.Parse(parts[0]), float.Parse(parts[1])),
            4 => new Thickness(float.Parse(parts[0]), float.Parse(parts[1]),
                               float.Parse(parts[2]), float.Parse(parts[3])),
            _ => throw new FormatException($"Cannot parse Thickness from '{s}'.")
        };
    }
}
