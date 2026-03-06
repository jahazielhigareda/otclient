namespace OTClient.Framework.Game;

// ─── Direction ────────────────────────────────────────────────────────────────

/// <summary>
/// Eight-directional compass used by creatures and projectiles.
/// Matches the Tibia protocol direction byte ordering.
/// </summary>
public enum Direction : byte
{
    North     = 0,
    East      = 1,
    South     = 2,
    West      = 3,
    NorthEast = 4,
    SouthEast = 5,
    SouthWest = 6,
    NorthWest = 7,
}

// ─── Position ─────────────────────────────────────────────────────────────────

/// <summary>
/// Immutable 3-D world coordinate: column (x), row (y), floor/z-layer (z).
/// Floor 0 is the highest (sky); floor 7 is ground level; 8–15 are underground.
/// Maps to <c>src/client/position.h</c>.
/// Task 8.1.
/// </summary>
public readonly record struct Position(int X, int Y, int Z)
{
    // ─── Constants ────────────────────────────────────────────────────────────

    public const int MaxFloors = 16;
    public const int GroundFloor = 7;

    /// <summary>Sentinel value used when no position is set.</summary>
    public static readonly Position Invalid = new(-1, -1, -1);
    public static readonly Position Zero    = new(0, 0, 0);

    // ─── Validity ─────────────────────────────────────────────────────────────

    public bool IsValid => X >= 0 && Y >= 0 && Z is >= 0 and < MaxFloors;

    // ─── Navigation helpers ───────────────────────────────────────────────────

    /// <summary>Returns the position one step in <paramref name="dir"/>.</summary>
    public Position Translated(Direction dir) => dir switch
    {
        Direction.North     => this with { Y = Y - 1 },
        Direction.East      => this with { X = X + 1 },
        Direction.South     => this with { Y = Y + 1 },
        Direction.West      => this with { X = X - 1 },
        Direction.NorthEast => new Position(X + 1, Y - 1, Z),
        Direction.SouthEast => new Position(X + 1, Y + 1, Z),
        Direction.SouthWest => new Position(X - 1, Y + 1, Z),
        Direction.NorthWest => new Position(X - 1, Y - 1, Z),
        _                   => this,
    };

    /// <summary>Returns the direction from this position towards <paramref name="other"/>.</summary>
    public Direction DirectionTo(Position other)
    {
        int dx = other.X - X;
        int dy = other.Y - Y;

        if (dx == 0 && dy < 0) return Direction.North;
        if (dx >  0 && dy < 0) return Direction.NorthEast;
        if (dx >  0 && dy == 0) return Direction.East;
        if (dx >  0 && dy > 0) return Direction.SouthEast;
        if (dx == 0 && dy > 0) return Direction.South;
        if (dx <  0 && dy > 0) return Direction.SouthWest;
        if (dx <  0 && dy == 0) return Direction.West;
        return Direction.NorthWest;
    }

    /// <summary>Chebyshev distance (max of |Δx|, |Δy|).</summary>
    public int ChebyshevDistance(Position other)
        => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    /// <summary>Manhattan distance (|Δx| + |Δy|, ignoring z).</summary>
    public int ManhattanDistance(Position other)
        => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>Returns a position offset by (dx, dy, dz).</summary>
    public Position Offset(int dx, int dy, int dz = 0) => new(X + dx, Y + dy, Z + dz);

    public override string ToString() => $"({X}, {Y}, {Z})";
}
