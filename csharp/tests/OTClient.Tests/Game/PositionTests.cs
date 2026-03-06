using OTClient.Framework.Game;
using Xunit;

namespace OTClient.Tests.Game;

/// <summary>
/// Tests for <see cref="Position"/> — coordinates, direction helpers,
/// distance methods.  Task 8.1.
/// </summary>
public sealed class PositionTests
{
    // ─── Defaults ─────────────────────────────────────────────────────────────

    [Fact]
    public void Zero_IsValid()
    {
        Assert.True(Position.Zero.IsValid);
    }

    [Fact]
    public void Invalid_IsNotValid()
    {
        Assert.False(Position.Invalid.IsValid);
    }

    [Fact]
    public void Negative_Z_IsNotValid()
    {
        Assert.False(new Position(0, 0, -1).IsValid);
    }

    [Fact]
    public void Z_GreaterThan15_IsNotValid()
    {
        Assert.False(new Position(0, 0, 16).IsValid);
    }

    // ─── Translated ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Direction.North,     5, 4)]
    [InlineData(Direction.South,     5, 6)]
    [InlineData(Direction.East,      6, 5)]
    [InlineData(Direction.West,      4, 5)]
    [InlineData(Direction.NorthEast, 6, 4)]
    [InlineData(Direction.SouthEast, 6, 6)]
    [InlineData(Direction.SouthWest, 4, 6)]
    [InlineData(Direction.NorthWest, 4, 4)]
    public void Translated_CorrectForCardinals(Direction dir, int expX, int expY)
    {
        var pos = new Position(5, 5, 7);
        var result = pos.Translated(dir);
        Assert.Equal(expX, result.X);
        Assert.Equal(expY, result.Y);
    }

    // ─── DirectionTo ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, -1, Direction.North)]
    [InlineData(0,  1, Direction.South)]
    [InlineData(1,  0, Direction.East)]
    [InlineData(-1, 0, Direction.West)]
    public void DirectionTo_CardinalDirections(int dx, int dy, Direction expected)
    {
        var from = new Position(10, 10, 7);
        var to   = new Position(10 + dx, 10 + dy, 7);
        Assert.Equal(expected, from.DirectionTo(to));
    }

    // ─── Distances ───────────────────────────────────────────────────────────

    [Fact]
    public void ChebyshevDistance_SamePos_Zero()
    {
        var p = new Position(5, 5, 7);
        Assert.Equal(0, p.ChebyshevDistance(p));
    }

    [Fact]
    public void ChebyshevDistance_Diagonal()
    {
        var a = new Position(0, 0, 7);
        var b = new Position(3, 5, 7);
        Assert.Equal(5, a.ChebyshevDistance(b));   // max(3,5)
    }

    [Fact]
    public void ManhattanDistance_Diagonal()
    {
        var a = new Position(0, 0, 7);
        var b = new Position(3, 4, 7);
        Assert.Equal(7, a.ManhattanDistance(b));   // 3+4
    }

    // ─── Offset ───────────────────────────────────────────────────────────────

    [Fact]
    public void Offset_AddsCorrectly()
    {
        var p = new Position(10, 20, 7);
        var q = p.Offset(2, -3, 1);
        Assert.Equal(new Position(12, 17, 8), q);
    }

    // ─── ToString ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        Assert.Equal("(1, 2, 3)", new Position(1, 2, 3).ToString());
    }
}
