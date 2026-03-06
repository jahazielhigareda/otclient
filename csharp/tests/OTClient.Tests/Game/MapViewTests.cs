using System.Numerics;
using OTClient.Framework.Game;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for <see cref="MapView"/> — camera, smooth scroll, coordinate mapping,
/// visibility culling.  Task 8.12.
/// </summary>
public sealed class MapViewTests
{
    // ─── CenterOn ─────────────────────────────────────────────────────────────

    [Fact]
    public void CenterOn_SetsCameraTarget()
    {
        var mv = new MapView();
        mv.CenterOn(new Position(50, 60, 7));
        Assert.Equal(new Vector2(50, 60), mv.CameraTarget);
    }

    [Fact]
    public void CenterOn_SetsFloor()
    {
        var mv = new MapView();
        mv.CenterOn(new Position(0, 0, 3));
        Assert.Equal(3, mv.Floor);
    }

    [Fact]
    public void CenterOn_SetsActualImmediately()
    {
        var mv = new MapView();
        mv.CenterOn(new Position(100, 200, 7));
        Assert.Equal(mv.CameraTarget, mv.CameraActual);
    }

    // ─── IsVisible ────────────────────────────────────────────────────────────

    [Fact]
    public void IsVisible_CentrePos_IsTrue()
    {
        var mv = new MapView();
        mv.CenterOn(new Position(50, 50, 7));
        Assert.True(mv.IsVisible(new Position(50, 50, 7)));
    }

    [Fact]
    public void IsVisible_FarPos_IsFalse()
    {
        var mv = new MapView();
        mv.CenterOn(new Position(50, 50, 7));
        Assert.False(mv.IsVisible(new Position(200, 200, 7)));
    }

    [Fact]
    public void IsVisible_WrongFloor_IsFalse()
    {
        var mv = new MapView();
        mv.CenterOn(new Position(50, 50, 7));
        Assert.False(mv.IsVisible(new Position(50, 50, 6)));
    }

    // ─── WorldToScreen / ScreenToWorld ────────────────────────────────────────

    [Fact]
    public void WorldToScreen_CentrePos_ReturnsScreenCentre()
    {
        var mv = new MapView();
        mv.Resize(15, 11, 32);
        mv.CenterOn(new Position(100, 100, 7));

        var screen = mv.WorldToScreen(new Position(100, 100, 7));
        // (15/2) * 32 = 240, (11/2) * 32 = 176
        Assert.Equal(240f, screen.X, precision: 3);
        Assert.Equal(176f, screen.Y, precision: 3);
    }

    [Fact]
    public void ScreenToWorld_RoundTrip()
    {
        var mv = new MapView();
        mv.Resize(15, 11, 32);
        mv.CenterOn(new Position(100, 100, 7));

        var screen = mv.WorldToScreen(new Position(102, 98, 7));
        var world  = mv.ScreenToWorld(screen);
        Assert.Equal(102, world.X);
        Assert.Equal(98,  world.Y);
        Assert.Equal(7,   world.Z);
    }

    // ─── Update (smooth scroll) ───────────────────────────────────────────────

    [Fact]
    public void Update_MovesCameraActualTowardsTarget()
    {
        var mv = new MapView { SmoothFactor = 1f };  // instant
        mv.CenterOn(new Position(0, 0, 7));
        mv.MoveTo(new Position(10, 10, 7));
        mv.Update(1f / 60f);   // one frame
        // With factor = 1 the camera should reach the target immediately
        Assert.Equal(mv.CameraTarget.X, mv.CameraActual.X, precision: 3);
    }

    // ─── ViewportTopLeft ─────────────────────────────────────────────────────

    [Fact]
    public void ViewportTopLeft_CorrectForCenteredCamera()
    {
        var mv = new MapView();
        mv.Resize(15, 11, 32);
        mv.CenterOn(new Position(50, 50, 7));

        var tl = mv.ViewportTopLeft;
        // (int)(50 - 15/2.0) = (int)(50 - 7.5) = 42  (float truncation)
        Assert.Equal(42, tl.X);
        Assert.Equal(44, tl.Y);   // (int)(50 - 11/2.0) = (int)(50-5.5) = 44
    }
}
