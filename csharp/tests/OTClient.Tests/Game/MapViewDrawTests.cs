using System.Collections.Generic;
using System.Linq;
using OTClient.Framework.Game;
using OTClient.Framework.UI;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for <see cref="MapView.Draw"/> (T17) and the supporting
/// <see cref="Map"/> overlay collections (AnimatedTexts, StaticTexts, Missiles).
/// </summary>
public sealed class MapViewDrawTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static MapView MakeView(int x = 100, int y = 100, int z = 7)
    {
        var mv = new MapView();
        mv.Resize(15, 11, 32);
        mv.CenterOn(new Position(x, y, z));
        return mv;
    }

    // ─── Map overlay collections ──────────────────────────────────────────────

    [Fact]
    public void Map_AddAnimatedText_Visible()
    {
        var map  = new Map();
        var text = new AnimatedText { Text = "150", Position = new Position(100, 100, 7) };
        map.AddAnimatedText(text);
        Assert.Single(map.AnimatedTexts);
    }

    [Fact]
    public void Map_AddStaticText_Visible()
    {
        var map  = new Map();
        var text = new StaticText { Text = "Player", Position = new Position(100, 100, 7) };
        map.AddStaticText(text);
        Assert.Single(map.StaticTexts);
    }

    [Fact]
    public void Map_AddMissile_Visible()
    {
        var map     = new Map();
        var missile = new Missile { From = new Position(99, 100, 7), To = new Position(101, 100, 7) };
        map.AddMissile(missile);
        Assert.Single(map.Missiles);
    }

    [Fact]
    public void Map_PruneAnimatedTexts_RemovesExpired()
    {
        var map = new Map();
        var t1  = new AnimatedText { Text = "1", Position = new Position(100, 100, 7), DurationMs = 0.001f };
        var t2  = new AnimatedText { Text = "2", Position = new Position(100, 100, 7), DurationMs = 100_000f };
        map.AddAnimatedText(t1);
        map.AddAnimatedText(t2);
        t1.Update(1000f); // advance 1000 ms >> 0.001 ms lifetime → t1 expires
        map.PruneAnimatedTexts();
        Assert.Single(map.AnimatedTexts);
        Assert.Equal("2", map.AnimatedTexts[0].Text);
    }

    [Fact]
    public void Map_PruneMissiles_RemovesFinished()
    {
        var map = new Map();
        var m1  = new Missile { From = new Position(99, 100, 7), To = new Position(99, 100, 7) };
        var m2  = new Missile { From = new Position(99, 100, 7), To = new Position(101, 100, 7) };
        map.AddMissile(m1);
        map.AddMissile(m2);
        m1.Update(100f); // finish instantly (zero distance → progress = 1)
        map.PruneMissiles();
        Assert.Single(map.Missiles); // m2 still in flight
    }

    [Fact]
    public void Map_ClearOverlays_RemovesAll()
    {
        var map = new Map();
        map.AddAnimatedText(new AnimatedText { Text = "1", Position = new Position(100, 100, 7) });
        map.AddStaticText(new StaticText { Text = "P", Position = new Position(100, 100, 7) });
        map.AddMissile(new Missile { From = new Position(99, 100, 7), To = new Position(101, 100, 7) });
        map.ClearOverlays();
        Assert.Empty(map.AnimatedTexts);
        Assert.Empty(map.StaticTexts);
        Assert.Empty(map.Missiles);
    }

    // ─── MapView.Draw — empty map ─────────────────────────────────────────────

    [Fact]
    public void Draw_EmptyMap_EmitsNoCommands()
    {
        var view     = MakeView();
        var map      = new Map();
        var commands = new List<UIDrawCommand>();
        view.Draw(map, commands);
        Assert.Empty(commands);
    }

    // ─── MapView.Draw — tile pass ─────────────────────────────────────────────

    [Fact]
    public void Draw_TileOnCurrentFloor_EmitsFillRect()
    {
        var view = MakeView(100, 100, 7);
        var map  = new Map();
        map.GetOrCreate(new Position(100, 100, 7)); // centre tile

        var cmds = new List<UIDrawCommand>();
        view.Draw(map, cmds);

        Assert.Contains(cmds, c => c is UIDrawCommand.FillRect);
    }

    [Fact]
    public void Draw_TileOnOtherFloor_NotEmitted()
    {
        var view = MakeView(100, 100, 7);
        var map  = new Map();
        // Only a tile on floor 6, not 7
        map.GetOrCreate(new Position(100, 100, 6));

        var cmds = new List<UIDrawCommand>();
        view.Draw(map, cmds);

        // No FillRect should be emitted (wrong floor)
        Assert.Empty(cmds);
    }

    // ─── MapView.Draw — creature pass ─────────────────────────────────────────

    [Fact]
    public void Draw_CreatureOnVisibleTile_EmitsTwoRects()
    {
        // One FillRect for the tile background, one for the creature
        var view = MakeView(100, 100, 7);
        var map  = new Map();
        var tile = map.GetOrCreate(new Position(100, 100, 7));
        tile.AddCreature(new Player { Id = 1, Position = new Position(100, 100, 7) });

        var cmds = new List<UIDrawCommand>();
        view.Draw(map, cmds);

        var rects = cmds.OfType<UIDrawCommand.FillRect>().ToList();
        Assert.True(rects.Count >= 2, "Expected at least tile + creature rect.");
    }

    // ─── MapView.Draw — effect pass ───────────────────────────────────────────

    [Fact]
    public void Draw_ActiveEffectOnTile_EmitsEffectRect()
    {
        var view = MakeView(100, 100, 7);
        var map  = new Map();
        var tile = map.GetOrCreate(new Position(100, 100, 7));
        var eff  = new Effect();
        // Do not call Update so it stays at frame 0 (not finished yet)
        tile.AddEffect(eff);

        var cmds = new List<UIDrawCommand>();
        view.Draw(map, cmds);

        // At minimum tile rect + effect rect
        var rects = cmds.OfType<UIDrawCommand.FillRect>().ToList();
        Assert.True(rects.Count >= 2, "Expected tile and effect rects.");
    }

    // ─── MapView.Draw — missile overlay ───────────────────────────────────────

    [Fact]
    public void Draw_InFlightMissile_EmitsMissileFillRect()
    {
        var view    = MakeView(100, 100, 7);
        var map     = new Map();
        var missile = new Missile { From = new Position(99, 100, 7), To = new Position(101, 100, 7) };
        missile.Update(0.05f); // partial progress
        map.GetOrCreate(new Position(100, 100, 7)); // ensure tile exists so tile rect is separate
        map.AddMissile(missile);

        var cmds = new List<UIDrawCommand>();
        view.Draw(map, cmds);

        Assert.Contains(cmds, c => c is UIDrawCommand.FillRect);
    }

    // ─── MapView.Draw — animated-text overlay ─────────────────────────────────

    [Fact]
    public void Draw_AnimatedText_EmitsDrawText()
    {
        var view = MakeView(100, 100, 7);
        var map  = new Map();
        map.AddAnimatedText(new AnimatedText
        {
            Text       = "50",
            Position   = new Position(100, 100, 7),
            DurationMs = 5000f,
        });

        var cmds = new List<UIDrawCommand>();
        view.Draw(map, cmds);

        Assert.Contains(cmds, c => c is UIDrawCommand.DrawText dt && dt.Text == "50");
    }

    // ─── MapView.Draw — static-text overlay ───────────────────────────────────

    [Fact]
    public void Draw_StaticText_EmitsDrawText()
    {
        var view = MakeView(100, 100, 7);
        var map  = new Map();
        map.AddStaticText(new StaticText
        {
            Text       = "PlayerName",
            Position   = new Position(100, 100, 7),
            LifetimeMs = 5000f,
        });

        var cmds = new List<UIDrawCommand>();
        view.Draw(map, cmds);

        Assert.Contains(cmds, c => c is UIDrawCommand.DrawText dt && dt.Text == "PlayerName");
    }

    // ─── MapView.Draw — off-screen text skipped ───────────────────────────────

    [Fact]
    public void Draw_AnimatedTextOffScreen_Skipped()
    {
        var view = MakeView(100, 100, 7);
        var map  = new Map();
        map.AddAnimatedText(new AnimatedText
        {
            Text       = "999",
            Position   = new Position(200, 200, 7), // far off-screen
            DurationMs = 5000f,
        });

        var cmds = new List<UIDrawCommand>();
        view.Draw(map, cmds);

        Assert.DoesNotContain(cmds, c => c is UIDrawCommand.DrawText);
    }
}
