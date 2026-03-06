using Raylib_cs;

namespace OTClient.Framework.UI;

/// <summary>
/// Positions children using anchor flags relative to the parent's content area.
/// This is the default layout engine used by <see cref="UIWidget"/>.
/// <para>
/// Children with <see cref="AnchorFlag.None"/> are placed at their explicit
/// <see cref="UIWidget.Position"/>.  Anchored children are stretched or centred
/// relative to <paramref name="available"/>.
/// </para>
/// Maps to <c>src/framework/ui/uilayout.*</c> (anchor mode).
/// Task 7.3.
/// </summary>
public static class AnchorLayout
{
    /// <summary>Lays out <paramref name="children"/> within <paramref name="available"/>.</summary>
    public static void Arrange(IEnumerable<UIWidget> children, Rectangle available)
    {
        foreach (var child in children)
            child.Layout(available);
    }
}

/// <summary>
/// Stacks children horizontally (left→right) or vertically (top→bottom).
/// Gap is inserted between siblings.
/// Maps to <c>UIHorizontalLayout</c> / <c>UIVerticalLayout</c> in OTClient.
/// Task 7.6.
/// </summary>
public sealed class BoxLayout(LayoutDirection direction = LayoutDirection.Horizontal, float gap = 0f)
{
    public LayoutDirection Direction { get; } = direction;
    public float Gap { get; } = gap;

    /// <summary>Arranges <paramref name="children"/> inside <paramref name="available"/>.</summary>
    public void Arrange(IReadOnlyList<UIWidget> children, Rectangle available)
    {
        float offset = 0f;
        foreach (var child in children)
        {
            if (!child.Visible) continue;

            float w = child.Size?.X ?? 0f;
            float h = child.Size?.Y ?? 0f;

            Rectangle slot;
            if (Direction == LayoutDirection.Horizontal)
            {
                slot = new Rectangle(available.X + offset, available.Y, w, available.Height);
                offset += w + Gap;
            }
            else
            {
                slot = new Rectangle(available.X, available.Y + offset, available.Width, h);
                offset += h + Gap;
            }

            child.Layout(slot);
        }
    }
}

/// <summary>
/// Flex-box style layout.  Children fill the main axis according to their
/// <see cref="UIWidget.Size"/>; when wrapping is enabled they overflow to a
/// new row/column.
/// Task 7.4.
/// </summary>
public sealed class FlexBoxLayout(
    LayoutDirection direction = LayoutDirection.Horizontal,
    float gap = 0f,
    bool wrap = false)
{
    public LayoutDirection Direction { get; } = direction;
    public float Gap  { get; } = gap;
    public bool  Wrap { get; } = wrap;

    public void Arrange(IReadOnlyList<UIWidget> children, Rectangle available)
    {
        float mainOffset   = 0f;
        float crossOffset  = 0f;
        float lineMax      = 0f;     // tallest / widest item on the current line

        foreach (var child in children)
        {
            if (!child.Visible) continue;

            float cw = child.Size?.X ?? 0f;
            float ch = child.Size?.Y ?? 0f;

            bool isH = Direction == LayoutDirection.Horizontal;
            float main  = isH ? cw : ch;
            float cross = isH ? ch : cw;

            // Wrap when we exceed the container's main-axis extent
            if (Wrap && mainOffset + main > (isH ? available.Width : available.Height) && mainOffset > 0)
            {
                mainOffset   = 0f;
                crossOffset += lineMax + Gap;
                lineMax      = 0f;
            }

            Rectangle slot;
            if (isH)
                slot = new Rectangle(available.X + mainOffset, available.Y + crossOffset, cw, ch);
            else
                slot = new Rectangle(available.X + crossOffset, available.Y + mainOffset, cw, ch);

            child.Layout(slot);
            mainOffset += main + Gap;
            lineMax = Math.Max(lineMax, cross);
        }
    }
}

/// <summary>
/// Positions children in a uniform grid with <see cref="Columns"/> columns.
/// Task 7.5.
/// </summary>
public sealed class GridLayout(int columns = 1, float cellWidth = 0f, float cellHeight = 0f, float gap = 0f)
{
    public int   Columns    { get; } = Math.Max(1, columns);
    public float CellWidth  { get; } = cellWidth;
    public float CellHeight { get; } = cellHeight;
    public float Gap        { get; } = gap;

    public void Arrange(IReadOnlyList<UIWidget> children, Rectangle available)
    {
        float cw = CellWidth  > 0 ? CellWidth  : (available.Width  - Gap * (Columns - 1)) / Columns;
        float ch = CellHeight > 0 ? CellHeight : cw; // square cells when not specified

        int col = 0, row = 0;
        foreach (var child in children)
        {
            if (!child.Visible) continue;

            float x = available.X + col * (cw + Gap);
            float y = available.Y + row * (ch + Gap);
            child.Layout(new Rectangle(x, y, cw, ch));

            if (++col >= Columns) { col = 0; row++; }
        }
    }
}
