using Raylib_cs;

namespace OTClient.Framework.Graphics;

// ─── Draw command types ───────────────────────────────────────────────────────

/// <summary>Identifies which variant of draw command is stored.</summary>
public enum DrawCommandKind : byte
{
    Sprite,
    Rect,
    Text,
}

/// <summary>
/// A single batched draw instruction stored inside a <see cref="DrawPool"/>.
/// Kept as a class to allow polymorphic storage without unsafe code.
/// </summary>
public abstract class DrawCommand
{
    public abstract DrawCommandKind Kind { get; }
    public Color Tint { get; init; } = Color.White;
}

/// <summary>Draw a portion of a <see cref="Texture"/> to a destination rectangle.</summary>
public sealed class SpriteCommand : DrawCommand
{
    public override DrawCommandKind Kind => DrawCommandKind.Sprite;

    /// <summary>The source texture.</summary>
    public required Texture Texture { get; init; }

    /// <summary>Source rectangle within <see cref="Texture"/>.</summary>
    public Rectangle Source { get; init; }

    /// <summary>Destination rectangle on screen.</summary>
    public Rectangle Dest { get; init; }

    /// <summary>Rotation origin relative to <see cref="Dest"/> top-left.</summary>
    public Vector2 Origin { get; init; }

    /// <summary>Clockwise rotation in degrees.</summary>
    public float Rotation { get; init; }
}

/// <summary>Draw a filled or outlined rectangle.</summary>
public sealed class RectCommand : DrawCommand
{
    public override DrawCommandKind Kind => DrawCommandKind.Rect;
    public Rectangle Rect { get; init; }
}

/// <summary>Draw a UTF-8 string using a Raylib font.</summary>
public sealed class TextCommand : DrawCommand
{
    public override DrawCommandKind Kind => DrawCommandKind.Text;
    public required string Text { get; init; }
    public Vector2 Position { get; init; }
    public float FontSize { get; init; } = 16f;
    public float Spacing { get; init; } = 1f;
}

// ─── DrawPool ─────────────────────────────────────────────────────────────────

/// <summary>
/// An ordered list of draw commands that are accumulated during a frame and
/// flushed (rendered) in one pass.  Batching avoids GPU state changes between
/// individual draw calls scattered across game logic code.
/// <para>
/// Call <see cref="Add"/> to enqueue commands, then <see cref="Flush"/> at the
/// appropriate point in the frame (inside <c>BeginDrawing / EndDrawing</c>).
/// </para>
/// Maps to <c>src/framework/graphics/drawpool.{h,cpp}</c>.
/// </summary>
public sealed class DrawPool
{
    private readonly List<DrawCommand> _commands = [];
    private bool _locked;

    // ─── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Human-readable label (e.g. <c>"ground"</c>).</summary>
    public string Name { get; }

    /// <summary>Render layer this pool belongs to.</summary>
    public RenderLayer Layer { get; }

    public DrawPool(string name, RenderLayer layer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name  = name;
        Layer = layer;
    }

    // ─── Command submission ───────────────────────────────────────────────────

    /// <summary>Appends a draw command to the pool.</summary>
    public void Add(DrawCommand command)
    {
        if (_locked)
            throw new InvalidOperationException("DrawPool is locked while flushing.");
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
    }

    /// <summary>Number of commands currently queued.</summary>
    public int CommandCount => _commands.Count;

    /// <summary>Removes all queued commands without rendering them.</summary>
    public void Clear() => _commands.Clear();

    // ─── Rendering ────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes all queued draw commands using Raylib and then clears the pool.
    /// Must be called inside a <c>BeginDrawing / EndDrawing</c> block.
    /// </summary>
    public void Flush()
    {
        _locked = true;
        try
        {
            foreach (var cmd in _commands)
            {
                switch (cmd)
                {
                    case SpriteCommand s:
                        Raylib.DrawTexturePro(
                            s.Texture.Handle,
                            s.Source,
                            s.Dest,
                            s.Origin,
                            s.Rotation,
                            s.Tint);
                        break;

                    case RectCommand r:
                        Raylib.DrawRectangleRec(r.Rect, r.Tint);
                        break;

                    case TextCommand t:
                        Raylib.DrawText(t.Text, (int)t.Position.X, (int)t.Position.Y, (int)t.FontSize, t.Tint);
                        break;
                }
            }
        }
        finally
        {
            _commands.Clear();
            _locked = false;
        }
    }
}
