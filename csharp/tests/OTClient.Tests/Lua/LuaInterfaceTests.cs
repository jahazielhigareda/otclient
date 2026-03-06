using MoonSharp.Interpreter;
using OTClient.Framework.Lua;
using Xunit;

namespace OTClient.Tests.Lua;

/// <summary>
/// Unit tests for <see cref="LuaInterface"/> — the core Lua runtime wrapper.
/// All tests are purely in-memory; no files or network are required.
/// Tasks 6.2, 6.15.
/// </summary>
public sealed class LuaInterfaceTests : IDisposable
{
    private readonly LuaInterface _lua = new();

    public LuaInterfaceTests() => _lua.Init();

    public void Dispose() => _lua.Dispose();

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    [Fact]
    public void IsInitialised_True_AfterInit()
    {
        using var lua = new LuaInterface();
        lua.Init();
        Assert.True(lua.IsInitialised);
    }

    [Fact]
    public void IsInitialised_False_BeforeInit()
    {
        using var lua = new LuaInterface();
        Assert.False(lua.IsInitialised);
    }

    [Fact]
    public void Init_IsIdempotent()
    {
        _lua.Init(); // second call
        Assert.True(_lua.IsInitialised);
    }

    [Fact]
    public void Shutdown_SetsIsInitialisedToFalse()
    {
        using var lua = new LuaInterface();
        lua.Init();
        lua.Shutdown();
        Assert.False(lua.IsInitialised);
    }

    [Fact]
    public void DoString_BeforeInit_Throws()
    {
        using var lua = new LuaInterface();
        Assert.Throws<InvalidOperationException>(() => lua.DoString("return 1"));
    }

    // ─── DoString ─────────────────────────────────────────────────────────────

    [Fact]
    public void DoString_ReturnsTrueResult()
    {
        var result = _lua.DoString("return 2 + 2");
        Assert.Equal(DataType.Number, result.Type);
        Assert.Equal(4.0, result.Number);
    }

    [Fact]
    public void DoString_ReturnsFalseResult()
    {
        var result = _lua.DoString("return 1 > 2");
        Assert.Equal(DataType.Boolean, result.Type);
        Assert.False(result.Boolean);
    }

    [Fact]
    public void DoString_ReturnsString()
    {
        var result = _lua.DoString("return 'hello'");
        Assert.Equal(DataType.String, result.Type);
        Assert.Equal("hello", result.String);
    }

    [Fact]
    public void DoString_NoReturn_ReturnsNil()
    {
        var result = _lua.DoString("local x = 1");
        Assert.Equal(DataType.Void, result.Type);
    }

    [Fact]
    public void DoString_SyntaxError_ThrowsLuaException()
    {
        Assert.Throws<LuaException>(() => _lua.DoString("this is not valid lua !!!"));
    }

    [Fact]
    public void DoString_RuntimeError_ThrowsLuaException()
    {
        Assert.Throws<LuaException>(() => _lua.DoString("error('boom')"));
    }

    // ─── DoFile ───────────────────────────────────────────────────────────────

    [Fact]
    public void DoFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<System.IO.FileNotFoundException>(
            () => _lua.DoFile("/tmp/nonexistent_lua_script_xyz.lua"));
    }

    [Fact]
    public void DoFile_ValidScript_ExecutesAndReturns()
    {
        var tmp = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(tmp, "return 42");
            var result = _lua.DoFile(tmp);
            Assert.Equal(42.0, result.Number);
        }
        finally { System.IO.File.Delete(tmp); }
    }

    // ─── Globals ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetGlobal_And_GetGlobal_RoundTrip_Number()
    {
        _lua.SetGlobal("myNum", DynValue.NewNumber(99));
        var v = _lua.GetGlobal("myNum");
        Assert.Equal(99.0, v.Number);
    }

    [Fact]
    public void SetGlobal_And_GetGlobal_RoundTrip_String()
    {
        _lua.SetGlobal("greeting", DynValue.NewString("hello lua"));
        var v = _lua.GetGlobal("greeting");
        Assert.Equal("hello lua", v.String);
    }

    [Fact]
    public void GetGlobal_Undefined_ReturnsNil()
    {
        var v = _lua.GetGlobal("__not_set__");
        Assert.Equal(DataType.Nil, v.Type);
    }

    [Fact]
    public void SetGlobal_Object_IsAccessibleFromScript()
    {
        _lua.SetGlobal("answer", (object)42);
        var v = _lua.DoString("return answer");
        Assert.Equal(42.0, v.Number);
    }

    [Fact]
    public void RemoveGlobal_SetsGlobalToNil()
    {
        _lua.SetGlobal("tmp", DynValue.NewNumber(1));
        _lua.RemoveGlobal("tmp");
        Assert.Equal(DataType.Nil, _lua.GetGlobal("tmp").Type);
    }

    // ─── Script execution from multiple DoString calls ────────────────────────

    [Fact]
    public void MultipleDoString_ShareGlobalState()
    {
        _lua.DoString("counter = 0");
        _lua.DoString("counter = counter + 1");
        _lua.DoString("counter = counter + 1");
        var v = _lua.DoString("return counter");
        Assert.Equal(2.0, v.Number);
    }

    // ─── Coroutine ────────────────────────────────────────────────────────────

    [Fact]
    public void CreateCoroutine_NonFunction_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => _lua.CreateCoroutine(DynValue.NewNumber(1)));
    }

    // ─── Dispose guard ────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var lua = new LuaInterface();
        lua.Init();
        var ex = Record.Exception(() => lua.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var lua = new LuaInterface();
        lua.Dispose();
        var ex = Record.Exception(() => lua.Dispose());
        Assert.Null(ex);
    }
}
