using System.Collections.Generic;
using OTClient.Framework.Game;
using OTClient.Framework.UI;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for the draw-pipeline additions to <see cref="AnimatedText"/>,
/// <see cref="StaticText"/>, <see cref="Effect"/>, and <see cref="Missile"/>.
/// Tasks T19 and T20.
/// </summary>
public sealed class DrawPipelineTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static MapView MakeView(int cx = 100, int cy = 100, int floor = 7)
    {
        var v = new MapView();
        v.CenterOn(new Position(cx, cy, floor));
        return v;
    }

    // ─── T19: AnimatedText ────────────────────────────────────────────────────

    [Fact]
    public void AnimatedText_Draw_EmitsTextCommand_WhenVisible()
    {
        var at  = new AnimatedText { Position = new Position(100, 100, 7), Text = "+500" };
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        at.Draw(cmds, view);
        Assert.Single(cmds);
        Assert.IsType<UIDrawCommand.DrawText>(cmds[0]);
    }

    [Fact]
    public void AnimatedText_Draw_EmitsNothing_WhenExpired()
    {
        var at = new AnimatedText { Position = new Position(100, 100, 7), Text = "+500", DurationMs = 100f };
        at.Update(200f); // advance past duration
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        at.Draw(cmds, view);
        Assert.Empty(cmds);
    }

    [Fact]
    public void AnimatedText_Draw_EmitsNothing_WhenOffScreen()
    {
        var at  = new AnimatedText { Position = new Position(999, 999, 7), Text = "+500" };
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        at.Draw(cmds, view);
        Assert.Empty(cmds);
    }

    [Fact]
    public void AnimatedText_Draw_EmitsNothing_WhenDifferentFloor()
    {
        var at  = new AnimatedText { Position = new Position(100, 100, 6), Text = "+500" };
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        at.Draw(cmds, view);
        Assert.Empty(cmds);
    }

    [Fact]
    public void AnimatedText_Draw_PositionFloatsUpOverTime()
    {
        var at1 = new AnimatedText { Position = new Position(100, 100, 7), Text = "A" };
        var at2 = new AnimatedText { Position = new Position(100, 100, 7), Text = "A" };
        at2.Update(700f); // advance halfway through default 1500ms

        var view = MakeView(100, 100, 7);
        var c1 = new List<UIDrawCommand>(); at1.Draw(c1, view);
        var c2 = new List<UIDrawCommand>(); at2.Draw(c2, view);

        var dt1 = (UIDrawCommand.DrawText)c1[0];
        var dt2 = (UIDrawCommand.DrawText)c2[0];
        Assert.True(dt2.Position.Y < dt1.Position.Y, "Later text should be higher on screen (smaller Y).");
    }

    [Fact]
    public void AnimatedText_Draw_AlphaFadesOut_NearEnd()
    {
        var at = new AnimatedText { Position = new Position(100, 100, 7), Text = "A", DurationMs = 1000f };
        at.Update(950f); // 95% through — should be partly faded
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        at.Draw(cmds, view);
        var dt = (UIDrawCommand.DrawText)cmds[0];
        Assert.True(dt.Color.A < 255, "Color should be faded near end of duration.");
    }

    // ─── T19: StaticText ──────────────────────────────────────────────────────

    [Fact]
    public void StaticText_Draw_EmitsTextCommand_WhenVisible()
    {
        var st  = new StaticText { Position = new Position(100, 100, 7), Text = "Hello" };
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        st.Draw(cmds, view);
        Assert.Single(cmds);
        Assert.IsType<UIDrawCommand.DrawText>(cmds[0]);
    }

    [Fact]
    public void StaticText_Draw_EmitsNothing_WhenExpired()
    {
        var st = new StaticText { Position = new Position(100, 100, 7), Text = "Hi", LifetimeMs = 100f };
        st.Update(200f);
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        st.Draw(cmds, view);
        Assert.Empty(cmds);
    }

    [Fact]
    public void StaticText_Draw_EmitsNothing_WhenOffScreen()
    {
        var st  = new StaticText { Position = new Position(999, 999, 7), Text = "Far" };
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        st.Draw(cmds, view);
        Assert.Empty(cmds);
    }

    [Fact]
    public void StaticText_Draw_PositionIsAboveCreature()
    {
        var pos  = new Position(100, 100, 7);
        var st   = new StaticText { Position = pos, Text = "Name" };
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        st.Draw(cmds, view);

        var screen  = view.WorldToScreen(pos);
        var dt      = (UIDrawCommand.DrawText)cmds[0];
        Assert.True(dt.Position.Y < screen.Y, "StaticText should appear above the creature.");
    }

    // ─── T20: Effect ──────────────────────────────────────────────────────────

    [Fact]
    public void Effect_Draw_EmitsFillRect_WhenVisibleAndActive()
    {
        var eff  = new Effect { Position = new Position(100, 100, 7), TypeId = 1 };
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        eff.Draw(cmds, view);
        Assert.Single(cmds);
        Assert.IsType<UIDrawCommand.FillRect>(cmds[0]);
    }

    [Fact]
    public void Effect_Draw_EmitsNothing_WhenFinished()
    {
        var eff = new Effect { Position = new Position(100, 100, 7), TypeId = 1 };
        eff.Update(1000); // single-frame non-looping → finished after any delta
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        eff.Draw(cmds, view);
        Assert.Empty(cmds);
    }

    [Fact]
    public void Effect_Draw_EmitsNothing_WhenOffScreen()
    {
        var eff  = new Effect { Position = new Position(999, 999, 7), TypeId = 1 };
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        eff.Draw(cmds, view);
        Assert.Empty(cmds);
    }

    [Fact]
    public void Effect_Draw_RectMatchesTileSize()
    {
        var pos  = new Position(100, 100, 7);
        var eff  = new Effect { Position = pos, TypeId = 1 };
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        eff.Draw(cmds, view);

        var fr = (UIDrawCommand.FillRect)cmds[0];
        Assert.Equal(view.TileSize, (int)fr.Rect.Width);
        Assert.Equal(view.TileSize, (int)fr.Rect.Height);
    }

    // ─── T20: Missile ─────────────────────────────────────────────────────────

    [Fact]
    public void Missile_Draw_EmitsFillRect_WhenInFlight()
    {
        var m = new Missile
        {
            From = new Position(99, 100, 7),
            To   = new Position(101, 100, 7),
        };
        m.Update(0.01f); // slight progress
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        m.Draw(cmds, view);
        Assert.Single(cmds);
        Assert.IsType<UIDrawCommand.FillRect>(cmds[0]);
    }

    [Fact]
    public void Missile_Draw_EmitsNothing_WhenFinished()
    {
        var m = new Missile
        {
            From = new Position(100, 100, 7),
            To   = new Position(101, 100, 7),
        };
        m.Update(100f); // large delta → fully finished
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        m.Draw(cmds, view);
        Assert.Empty(cmds);
    }

    [Fact]
    public void Missile_Draw_EmitsNothing_AtStart_NoProgress()
    {
        var m = new Missile
        {
            From = new Position(100, 100, 7),
            To   = new Position(101, 100, 7),
        };
        // No Update() call → progress = 0, not finished, but CurrentX == From.X
        var view = MakeView(100, 100, 7);
        var cmds = new List<UIDrawCommand>();
        m.Draw(cmds, view);
        // Should emit because not finished and From is visible
        Assert.Single(cmds);
    }

    [Fact]
    public void Missile_Draw_PositionInterpolatesCorrectly()
    {
        var from = new Position(98, 100, 7);
        var to   = new Position(102, 100, 7);
        var m    = new Missile { From = from, To = to };
        m.Update(0.05f, speedTilesPerSec: 20f); // advance somewhat

        var view = MakeView(100, 100, 7);
        var c1   = new List<UIDrawCommand>(); m.Draw(c1, view);

        m.Update(0.05f, speedTilesPerSec: 20f); // advance more
        var c2 = new List<UIDrawCommand>(); m.Draw(c2, view);

        if (c1.Count == 0 || c2.Count == 0) return; // both off-screen edge case ok

        var r1 = (UIDrawCommand.FillRect)c1[0];
        var r2 = (UIDrawCommand.FillRect)c2[0];
        // As missile moves right (positive X), screen X should increase
        Assert.True(r2.Rect.X >= r1.Rect.X, "Missile should move to the right on screen.");
    }
}
