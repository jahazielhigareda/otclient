using System.Numerics;
using OTClient.Framework.UI;
using Raylib_cs;

namespace OTClient.Framework.Game;

// ─── MapView ──────────────────────────────────────────────────────────────────

/// <summary>
/// Manages the game viewport — which portion of the map is visible — and
/// calculates the screen position for any given world <see cref="Position"/>.
/// Supports smooth camera scrolling.
/// Maps to <c>src/client/mapview.h</c>.
/// Task 8.12.
/// </summary>
public sealed class MapView
{
    // ─── Viewport dimensions ──────────────────────────────────────────────────

    /// <summary>Number of tiles visible horizontally.</summary>
    public int TilesX { get; private set; } = 15;
    /// <summary>Number of tiles visible vertically.</summary>
    public int TilesY { get; private set; } = 11;
    /// <summary>Tile size in pixels.</summary>
    public int TileSize { get; private set; } = 32;
    /// <summary>Current floor being rendered.</summary>
    public int Floor    { get; set; } = Position.GroundFloor;

    // ─── Camera centre ────────────────────────────────────────────────────────

    private Vector2 _cameraTarget;   // world coordinates (float for smooth scroll)
    private Vector2 _cameraActual;   // smoothed position

    /// <summary>World tile coordinates at the centre of the screen.</summary>
    public Vector2 CameraTarget => _cameraTarget;

    /// <summary>Smoothed camera position used for rendering.</summary>
    public Vector2 CameraActual => _cameraActual;

    // ─── Configuration ────────────────────────────────────────────────────────

    /// <summary>Smooth scroll speed (0 = instant, 1 = very slow).</summary>
    public float SmoothFactor { get; set; } = 0.15f;

    public void Resize(int tilesX, int tilesY, int tileSize)
    {
        TilesX   = tilesX;
        TilesY   = tilesY;
        TileSize = tileSize;
    }

    // ─── Camera ───────────────────────────────────────────────────────────────

    /// <summary>Centres the camera immediately on <paramref name="pos"/>.</summary>
    public void CenterOn(Position pos)
    {
        _cameraTarget = new Vector2(pos.X, pos.Y);
        _cameraActual = _cameraTarget;
        Floor         = pos.Z;
    }

    /// <summary>Moves the camera target towards <paramref name="pos"/>.</summary>
    public void MoveTo(Position pos)
    {
        _cameraTarget = new Vector2(pos.X, pos.Y);
        Floor         = pos.Z;
    }

    /// <summary>
    /// Advances the smooth camera interpolation by <paramref name="deltaSeconds"/>.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        float t = Math.Clamp(SmoothFactor * deltaSeconds * 60f, 0f, 1f);
        _cameraActual = Vector2.Lerp(_cameraActual, _cameraTarget, t);
    }

    // ─── Coordinate mapping ───────────────────────────────────────────────────

    /// <summary>
    /// Converts a world <see cref="Position"/> to a screen pixel position
    /// using the current (smoothed) camera.
    /// </summary>
    public Vector2 WorldToScreen(Position pos)
    {
        float offX = pos.X - _cameraActual.X;
        float offY = pos.Y - _cameraActual.Y;
        float screenX = (TilesX / 2f + offX) * TileSize;
        float screenY = (TilesY / 2f + offY) * TileSize;
        return new Vector2(screenX, screenY);
    }

    /// <summary>
    /// Converts screen pixel coordinates back to a world <see cref="Position"/>
    /// on the current <see cref="Floor"/>.
    /// </summary>
    public Position ScreenToWorld(Vector2 screen)
    {
        float tileX = screen.X / TileSize - TilesX / 2f + _cameraActual.X;
        float tileY = screen.Y / TileSize - TilesY / 2f + _cameraActual.Y;
        return new Position((int)MathF.Round(tileX), (int)MathF.Round(tileY), Floor);
    }

    // ─── Visibility culling ───────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> when <paramref name="pos"/> is inside the viewport.</summary>
    public bool IsVisible(Position pos)
    {
        if (pos.Z != Floor) return false;
        float halfX = TilesX / 2f;
        float halfY = TilesY / 2f;
        return Math.Abs(pos.X - _cameraActual.X) <= halfX + 1
            && Math.Abs(pos.Y - _cameraActual.Y) <= halfY + 1;
    }

    // ─── Visible tile range ───────────────────────────────────────────────────

    /// <summary>Returns the top-left world position of the viewport.</summary>
    public Position ViewportTopLeft
        => new((int)(_cameraActual.X - TilesX / 2f),
               (int)(_cameraActual.Y - TilesY / 2f),
               Floor);

    // ─── Draw pipeline (T17) ──────────────────────────────────────────────────

    // Tile placeholder color (blue-grey, mirrors C++ tile fill)
    private static readonly Color TileGroundColor    = new(60,  80,  100, 255);
    private static readonly Color TileCreatureColor  = new(0,   200, 80,  255);
    private static readonly Color TileEffectColor    = new(255, 165, 0,   200);

    /// <summary>
    /// Builds the full draw-command list for one frame.
    /// <para>
    /// Render order (matches C++ mapview.cpp):
    /// <list type="number">
    ///   <item>Ground/item tiles for the current floor (visible only)</item>
    ///   <item>Tile-anchored effects</item>
    ///   <item>Creatures on each tile</item>
    ///   <item>Map-level missile overlay</item>
    ///   <item>Map-level animated-text overlay</item>
    ///   <item>Map-level static-text overlay</item>
    /// </list>
    /// </para>
    /// All coordinates use pixel-space; culling to the viewport is applied before
    /// adding each command so the caller receives only visible draw calls.
    /// Task T17.
    /// </summary>
    public void Draw(Map map, List<UIDrawCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(commands);

        float tileF = TileSize;

        // ── Pass 1: ground tiles + effects + creatures ─────────────────────────
        for (int dy = -TilesY / 2 - 1; dy <= TilesY / 2 + 1; dy++)
        {
            for (int dx = -TilesX / 2 - 1; dx <= TilesX / 2 + 1; dx++)
            {
                var pos   = new Position(
                    (int)_cameraActual.X + dx,
                    (int)_cameraActual.Y + dy,
                    Floor);
                var tile  = map.Get(pos);

                if (tile is null) continue;

                var screen = WorldToScreen(pos);
                var rect   = new Rectangle(screen.X, screen.Y, tileF, tileF);

                // 1a. Ground placeholder
                commands.Add(new UIDrawCommand.FillRect(rect, TileGroundColor));

                // 1b. Effects (tile-anchored)
                foreach (var effect in tile.Effects)
                {
                    if (effect.IsFinished) continue;
                    var eRect = new Rectangle(screen.X + 2, screen.Y + 2, tileF - 4, tileF - 4);
                    commands.Add(new UIDrawCommand.FillRect(eRect, TileEffectColor));
                }

                // 1c. Creatures
                foreach (var creature in tile.Creatures)
                {
                    var cRect = new Rectangle(screen.X + 4, screen.Y + 4, tileF - 8, tileF - 8);
                    commands.Add(new UIDrawCommand.FillRect(cRect, TileCreatureColor));
                }
            }
        }

        // ── Pass 2: missiles ──────────────────────────────────────────────────
        foreach (var missile in map.Missiles)
            missile.Draw(commands, this);

        // ── Pass 3: animated texts ─────────────────────────────────────────────
        foreach (var text in map.AnimatedTexts)
            text.Draw(commands, this);

        // ── Pass 4: static texts ───────────────────────────────────────────────
        foreach (var text in map.StaticTexts)
            text.Draw(commands, this);
    }
}

// ─── LightView ────────────────────────────────────────────────────────────────

/// <summary>
/// Tracks per-tile ambient and dynamic light contributions.
/// The final lighting pass blends these with the framebuffer to produce the
/// characteristic Tibia dark-floor effect.
/// Maps to <c>src/client/lightview.h</c>.
/// Task 8.13.
/// </summary>
public sealed class LightView
{
    /// <summary>Global ambient light level (0 = pitch black, 255 = full daylight).</summary>
    public byte AmbientLight { get; set; } = 215;

    /// <summary>Global ambient colour (usually white).</summary>
    public Color AmbientColor { get; set; } = Color.White;

    // ─── Light sources ────────────────────────────────────────────────────────

    private readonly List<LightSource> _sources = [];
    public  IReadOnlyList<LightSource> Sources  => _sources;

    public void AddSource(LightSource src)
    {
        ArgumentNullException.ThrowIfNull(src);
        _sources.Add(src);
    }

    public void Clear() => _sources.Clear();

    /// <summary>
    /// Returns the combined light level at <paramref name="pos"/>.
    /// </summary>
    public float GetLightAt(Position pos)
    {
        float ambient = AmbientLight / 255f;
        float dynamic = 0f;
        foreach (var s in _sources)
        {
            int dist = pos.ChebyshevDistance(s.Position);
            if (dist <= s.Radius)
                dynamic = Math.Max(dynamic, s.Level / 255f * (1f - (float)dist / (s.Radius + 1)));
        }
        return Math.Clamp(ambient + dynamic, 0f, 1f);
    }
}

/// <summary>A point light emitted by a creature, item, or effect.</summary>
public sealed class LightSource
{
    public Position Position { get; set; }
    /// <summary>Intensity 0–255.</summary>
    public int Level  { get; set; }
    /// <summary>Radius in tiles.</summary>
    public int Radius { get; set; }
}
