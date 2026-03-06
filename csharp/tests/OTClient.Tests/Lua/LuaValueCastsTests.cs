using System.Numerics;
using MoonSharp.Interpreter;
using OTClient.Framework.Lua;
using Xunit;

namespace OTClient.Tests.Lua;

/// <summary>
/// Tests for <see cref="LuaValueCasts"/> — C# ↔ Lua type marshalling.
/// Task 6.4, 6.15.
/// </summary>
public sealed class LuaValueCastsTests
{
    private readonly Script _script = new();

    // ─── C# → DynValue ────────────────────────────────────────────────────────

    [Fact]
    public void FromBool_True_ProducesLuaTrue()
    {
        var v = LuaValueCasts.FromBool(true);
        Assert.Equal(DataType.Boolean, v.Type);
        Assert.True(v.Boolean);
    }

    [Fact]
    public void FromBool_False_ProducesLuaFalse()
    {
        var v = LuaValueCasts.FromBool(false);
        Assert.False(v.Boolean);
    }

    [Fact]
    public void FromInt_ProducesLuaNumber()
    {
        var v = LuaValueCasts.FromInt(42);
        Assert.Equal(DataType.Number, v.Type);
        Assert.Equal(42.0, v.Number);
    }

    [Fact]
    public void FromLong_ProducesLuaNumber()
    {
        var v = LuaValueCasts.FromLong(long.MaxValue);
        Assert.Equal(DataType.Number, v.Type);
    }

    [Fact]
    public void FromFloat_ProducesLuaNumber()
    {
        var v = LuaValueCasts.FromFloat(3.14f);
        Assert.Equal(DataType.Number, v.Type);
        Assert.Equal((double)3.14f, v.Number, precision: 5);
    }

    [Fact]
    public void FromDouble_ProducesLuaNumber()
    {
        var v = LuaValueCasts.FromDouble(2.718281828);
        Assert.Equal(2.718281828, v.Number, precision: 8);
    }

    [Fact]
    public void FromString_NonNull_ProducesLuaString()
    {
        var v = LuaValueCasts.FromString("hello");
        Assert.Equal(DataType.String, v.Type);
        Assert.Equal("hello", v.String);
    }

    [Fact]
    public void FromString_Null_ProducesNil()
    {
        var v = LuaValueCasts.FromString(null);
        Assert.Equal(DataType.Nil, v.Type);
    }

    [Fact]
    public void FromVector2_ProducesTableWithXAndY()
    {
        var v = LuaValueCasts.FromVector2(new Vector2(3f, 7f), _script);
        Assert.Equal(DataType.Table, v.Type);
        Assert.Equal(3.0, v.Table.Get("x").Number);
        Assert.Equal(7.0, v.Table.Get("y").Number);
    }

    [Fact]
    public void FromColor_ProducesTableWithRGBA()
    {
        // white: all components = 1.0 → 255
        var v = LuaValueCasts.FromColor(new Vector4(1f, 1f, 1f, 1f), _script);
        Assert.Equal(DataType.Table, v.Type);
        Assert.Equal(255.0, v.Table.Get("r").Number);
        Assert.Equal(255.0, v.Table.Get("g").Number);
        Assert.Equal(255.0, v.Table.Get("b").Number);
        Assert.Equal(255.0, v.Table.Get("a").Number);
    }

    [Fact]
    public void FromRect_ProducesTableWithXYWidthHeight()
    {
        var v = LuaValueCasts.FromRect(10, 20, 100, 50, _script);
        Assert.Equal(10.0,  v.Table.Get("x").Number);
        Assert.Equal(20.0,  v.Table.Get("y").Number);
        Assert.Equal(100.0, v.Table.Get("width").Number);
        Assert.Equal(50.0,  v.Table.Get("height").Number);
    }

    // ─── DynValue → C# ────────────────────────────────────────────────────────

    [Fact]
    public void ToBool_LuaTrue_ReturnsTrue()
    {
        Assert.True(LuaValueCasts.ToBool(DynValue.NewBoolean(true)));
    }

    [Fact]
    public void ToBool_LuaFalse_ReturnsFalse()
    {
        Assert.False(LuaValueCasts.ToBool(DynValue.NewBoolean(false)));
    }

    [Fact]
    public void ToBool_Nil_ReturnsFalse()
    {
        Assert.False(LuaValueCasts.ToBool(DynValue.Nil));
    }

    [Fact]
    public void ToBool_Number_ReturnsTrue()
    {
        // Non-nil, non-false values are truthy in Lua
        Assert.True(LuaValueCasts.ToBool(DynValue.NewNumber(0)));
    }

    [Fact]
    public void ToInt_Number_ReturnsInt()
    {
        Assert.Equal(7, LuaValueCasts.ToInt(DynValue.NewNumber(7.9)));
    }

    [Fact]
    public void ToInt_NonNumber_Throws()
    {
        Assert.Throws<InvalidCastException>(
            () => LuaValueCasts.ToInt(DynValue.NewString("oops")));
    }

    [Fact]
    public void ToDouble_Number_ReturnsDouble()
    {
        Assert.Equal(3.14, LuaValueCasts.ToDouble(DynValue.NewNumber(3.14)), precision: 10);
    }

    [Fact]
    public void ToDouble_NonNumber_Throws()
    {
        Assert.Throws<InvalidCastException>(
            () => LuaValueCasts.ToDouble(DynValue.Nil));
    }

    [Fact]
    public void ToString_String_ReturnsString()
    {
        Assert.Equal("world", LuaValueCasts.ToString(DynValue.NewString("world")));
    }

    [Fact]
    public void ToString_NonString_Throws()
    {
        Assert.Throws<InvalidCastException>(
            () => LuaValueCasts.ToString(DynValue.NewNumber(1)));
    }

    [Fact]
    public void ToStringOrNull_Nil_ReturnsNull()
    {
        Assert.Null(LuaValueCasts.ToStringOrNull(DynValue.Nil));
    }

    [Fact]
    public void ToStringOrNull_String_ReturnsString()
    {
        Assert.Equal("hi", LuaValueCasts.ToStringOrNull(DynValue.NewString("hi")));
    }

    [Fact]
    public void ToVector2_TableWithXY_ReturnsVector2()
    {
        var t = new Table(_script);
        t["x"] = DynValue.NewNumber(5);
        t["y"] = DynValue.NewNumber(8);
        var v = LuaValueCasts.ToVector2(DynValue.NewTable(t));
        Assert.Equal(5f, v.X);
        Assert.Equal(8f, v.Y);
    }

    [Fact]
    public void ToVector2_NonTable_Throws()
    {
        Assert.Throws<InvalidCastException>(
            () => LuaValueCasts.ToVector2(DynValue.NewNumber(1)));
    }
}
