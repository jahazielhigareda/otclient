using Raylib_cs;

namespace OTClient.Framework.Graphics;

/// <summary>
/// Render layer identifiers used by <see cref="DrawPoolManager"/>.
/// Layers are drawn bottom-to-top in numeric order.
/// Maps to the draw-pool type enum in <c>src/framework/graphics/drawpoolmanager.h</c>.
/// </summary>
public enum RenderLayer
{
    Ground   = 0,
    Items    = 1,
    Creatures = 2,
    Effects  = 3,
    Ui       = 4,
}

/// <summary>
/// Manages the Raylib drawing state for a single frame: clear color, viewport,
/// and the <c>BeginDrawing / EndDrawing</c> bracket.
/// <para>
/// This class does <em>not</em> create or destroy the Raylib window — that is
/// <see cref="OTClient.Framework.Core.Application"/>'s responsibility.
/// Obtain a <see cref="GraphicsContext"/> from the application and call
/// <see cref="BeginFrame"/> / <see cref="EndFrame"/> in your render callback.
/// </para>
/// Maps to <c>GraphicContext</c> in <c>src/framework/graphics/graphiccontext.*</c>.
/// </summary>
public sealed class GraphicsContext
{
    private bool _inFrame;

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>Background colour applied by <see cref="BeginFrame"/>.</summary>
    public Color ClearColor { get; set; } = Color.Black;

    /// <summary>Current width of the Raylib window in pixels.</summary>
    public int Width => Raylib.IsWindowReady() ? Raylib.GetScreenWidth()  : 0;

    /// <summary>Current height of the Raylib window in pixels.</summary>
    public int Height => Raylib.IsWindowReady() ? Raylib.GetScreenHeight() : 0;

    /// <summary><c>true</c> between <see cref="BeginFrame"/> and <see cref="EndFrame"/>.</summary>
    public bool IsInFrame => _inFrame;

    // ─── Frame control ────────────────────────────────────────────────────────

    /// <summary>
    /// Calls <c>Raylib.BeginDrawing()</c> and clears the back-buffer with
    /// <see cref="ClearColor"/>.  Must be paired with <see cref="EndFrame"/>.
    /// </summary>
    public void BeginFrame()
    {
        if (_inFrame) throw new InvalidOperationException("BeginFrame already called; call EndFrame first.");
        Raylib.BeginDrawing();
        Raylib.ClearBackground(ClearColor);
        _inFrame = true;
    }

    /// <summary>Calls <c>Raylib.EndDrawing()</c>, presenting the frame.</summary>
    public void EndFrame()
    {
        if (!_inFrame) throw new InvalidOperationException("EndFrame called without a matching BeginFrame.");
        Raylib.EndDrawing();
        _inFrame = false;
    }

    // ─── Viewport helpers ─────────────────────────────────────────────────────

    /// <summary>Sets a 2-D scissor rectangle.  Must be called inside a frame.</summary>
    public void BeginScissor(int x, int y, int width, int height)
    {
        Raylib.BeginScissorMode(x, y, width, height);
    }

    /// <summary>Ends the current scissor rectangle.</summary>
    public void EndScissor()
    {
        Raylib.EndScissorMode();
    }

    /// <summary>Returns a <see cref="Rectangle"/> covering the full screen.</summary>
    public Rectangle FullScreenRect() =>
        new(0, 0, Width, Height);
}
