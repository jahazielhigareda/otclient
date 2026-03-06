using MoonSharp.Interpreter;

namespace OTClient.Framework.Lua;

/// <summary>
/// Marshalling helpers that convert between common C# types and MoonSharp
/// <see cref="DynValue"/> objects.
/// <para>
/// These casts are used by <see cref="LuaInterface"/> when passing values to /
/// receiving values from Lua scripts.
/// </para>
/// Maps to <c>src/framework/luaengine/luavaluecasts.*</c>.
/// Task 6.4.
/// </summary>
public static class LuaValueCasts
{
    // ─── C# → DynValue ────────────────────────────────────────────────────────

    /// <summary>Converts a C# <see cref="bool"/> to a Lua boolean.</summary>
    public static DynValue FromBool(bool v)    => DynValue.NewBoolean(v);

    /// <summary>Converts a C# <see cref="int"/> to a Lua number.</summary>
    public static DynValue FromInt(int v)      => DynValue.NewNumber(v);

    /// <summary>Converts a C# <see cref="long"/> to a Lua number.</summary>
    public static DynValue FromLong(long v)    => DynValue.NewNumber(v);

    /// <summary>Converts a C# <see cref="float"/> to a Lua number.</summary>
    public static DynValue FromFloat(float v)  => DynValue.NewNumber(v);

    /// <summary>Converts a C# <see cref="double"/> to a Lua number.</summary>
    public static DynValue FromDouble(double v) => DynValue.NewNumber(v);

    /// <summary>Converts a C# <see cref="string"/> to a Lua string (nil if null).</summary>
    public static DynValue FromString(string? v)
        => v is null ? DynValue.Nil : DynValue.NewString(v);

    /// <summary>
    /// Converts a <see cref="Vector2"/> (System.Numerics) to a Lua table
    /// with fields <c>x</c> and <c>y</c>.
    /// </summary>
    public static DynValue FromVector2(Vector2 v, Script script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var t = new Table(script);
        t["x"] = DynValue.NewNumber(v.X);
        t["y"] = DynValue.NewNumber(v.Y);
        return DynValue.NewTable(t);
    }

    /// <summary>
    /// Converts a <see cref="Vector4"/> (System.Numerics) to a Lua table with
    /// fields <c>r</c>, <c>g</c>, <c>b</c>, <c>a</c>.  Useful for colour values.
    /// </summary>
    public static DynValue FromColor(Vector4 color, Script script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var t = new Table(script);
        t["r"] = DynValue.NewNumber((int)(color.X * 255));
        t["g"] = DynValue.NewNumber((int)(color.Y * 255));
        t["b"] = DynValue.NewNumber((int)(color.Z * 255));
        t["a"] = DynValue.NewNumber((int)(color.W * 255));
        return DynValue.NewTable(t);
    }

    /// <summary>
    /// Converts a rectangle (x, y, width, height) to a Lua table.
    /// </summary>
    public static DynValue FromRect(int x, int y, int w, int h, Script script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var t = new Table(script);
        t["x"]      = DynValue.NewNumber(x);
        t["y"]      = DynValue.NewNumber(y);
        t["width"]  = DynValue.NewNumber(w);
        t["height"] = DynValue.NewNumber(h);
        return DynValue.NewTable(t);
    }

    // ─── DynValue → C# ────────────────────────────────────────────────────────

    /// <summary>Extracts a boolean from a <see cref="DynValue"/> (any truthy value).</summary>
    public static bool ToBool(DynValue v)
    {
        if (v.Type == DataType.Boolean) return v.Boolean;
        return v.Type != DataType.Nil && v.Type != DataType.Void;
    }

    /// <summary>Extracts an integer from a numeric <see cref="DynValue"/>.</summary>
    /// <exception cref="InvalidCastException">When the value is not a number.</exception>
    public static int ToInt(DynValue v)
    {
        if (v.Type != DataType.Number)
            throw new InvalidCastException($"Expected number, got {v.Type}.");
        return (int)v.Number;
    }

    /// <summary>Extracts a double from a numeric <see cref="DynValue"/>.</summary>
    /// <exception cref="InvalidCastException">When the value is not a number.</exception>
    public static double ToDouble(DynValue v)
    {
        if (v.Type != DataType.Number)
            throw new InvalidCastException($"Expected number, got {v.Type}.");
        return v.Number;
    }

    /// <summary>Extracts a string from a string <see cref="DynValue"/>.</summary>
    /// <exception cref="InvalidCastException">When the value is not a string.</exception>
    public static string ToString(DynValue v)
    {
        if (v.Type != DataType.String)
            throw new InvalidCastException($"Expected string, got {v.Type}.");
        return v.String;
    }

    /// <summary>Extracts an optional string from a <see cref="DynValue"/>; returns null for nil.</summary>
    public static string? ToStringOrNull(DynValue v)
        => v.Type == DataType.Nil || v.Type == DataType.Void ? null : v.CastToString();

    /// <summary>
    /// Reads <c>x</c> and <c>y</c> numeric fields from a Lua table into a
    /// <see cref="Vector2"/>.
    /// </summary>
    /// <exception cref="InvalidCastException">When the value is not a table.</exception>
    public static Vector2 ToVector2(DynValue v)
    {
        if (v.Type != DataType.Table)
            throw new InvalidCastException($"Expected table, got {v.Type}.");
        var t = v.Table;
        float x = (float)(t.Get("x").CastToNumber() ?? 0);
        float y = (float)(t.Get("y").CastToNumber() ?? 0);
        return new Vector2(x, y);
    }
}
