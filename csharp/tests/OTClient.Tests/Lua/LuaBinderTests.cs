using MoonSharp.Interpreter;
using OTClient.Framework.Lua;
using Xunit;

namespace OTClient.Tests.Lua;

/// <summary>
/// Tests for <see cref="LuaBinder"/> — reflection-based type registration.
/// Task 6.3, 6.15.
/// </summary>
public sealed class LuaBinderTests
{
    // ─── [LuaBinding] attribute ───────────────────────────────────────────────

    [Fact]
    public void LuaBindingAttribute_StoresLuaName()
    {
        var attr = new LuaBindingAttribute("MyType");
        Assert.Equal("MyType", attr.LuaName);
    }

    [Fact]
    public void LuaBindingAttribute_DefaultLuaName_IsNull()
    {
        var attr = new LuaBindingAttribute();
        Assert.Null(attr.LuaName);
    }

    [Fact]
    public void LuaMethodAttribute_StoresLuaName()
    {
        var attr = new LuaMethodAttribute("myMethod");
        Assert.Equal("myMethod", attr.LuaName);
    }

    [Fact]
    public void LuaPropertyAttribute_StoresLuaName()
    {
        var attr = new LuaPropertyAttribute("myProp");
        Assert.Equal("myProp", attr.LuaName);
    }

    // ─── RegisterType<T> ─────────────────────────────────────────────────────

    [Fact]
    public void RegisterType_Generic_IsIdempotent()
    {
        LuaBinder.RegisterType<LuaAppProxy>();
        LuaBinder.RegisterType<LuaAppProxy>(); // second call must not throw
        Assert.True(UserData.IsTypeRegistered(typeof(LuaAppProxy)));
    }

    [Fact]
    public void RegisterType_ByType_RegistersSuccessfully()
    {
        LuaBinder.RegisterType(typeof(LuaLoggerProxy));
        Assert.True(UserData.IsTypeRegistered(typeof(LuaLoggerProxy)));
    }

    // ─── RegisterAssembly ─────────────────────────────────────────────────────

    [Fact]
    public void RegisterAssembly_RegistersLuaBindingDecoratedTypes()
    {
        // All proxy types in OTClient.Core carry [LuaBinding]
        LuaBinder.RegisterAssembly(typeof(LuaAppProxy).Assembly);

        Assert.True(UserData.IsTypeRegistered(typeof(LuaAppProxy)));
        Assert.True(UserData.IsTypeRegistered(typeof(LuaLoggerProxy)));
        Assert.True(UserData.IsTypeRegistered(typeof(LuaResourcesProxy)));
        Assert.True(UserData.IsTypeRegistered(typeof(LuaPlatformProxy)));
        Assert.True(UserData.IsTypeRegistered(typeof(LuaModulesProxy)));
    }

    [Fact]
    public void RegisterAssembly_Generic_RegistersTypes()
    {
        LuaBinder.RegisterAssembly<LuaAppProxy>();
        Assert.True(UserData.IsTypeRegistered(typeof(LuaAppProxy)));
    }

    // ─── Registered type is callable from Lua ────────────────────────────────

    [Fact]
    public void RegisteredType_MethodCallable_FromLua()
    {
        LuaBinder.RegisterType<LuaAppProxy>();

        using var lua = new LuaInterface();
        lua.Init();
        lua.SetGlobal("app", new LuaAppProxy());

        var result = lua.DoString("return app:getName()");
        Assert.Equal(DataType.String, result.Type);
        Assert.Equal("OTClient", result.String);
    }

    [Fact]
    public void RegisteredType_SetterCallable_FromLua()
    {
        LuaBinder.RegisterType<LuaAppProxy>();

        using var lua = new LuaInterface();
        lua.Init();
        var proxy = new LuaAppProxy();
        lua.SetGlobal("app", proxy);

        lua.DoString("app:setName('MyGame')");
        Assert.Equal("MyGame", proxy.getName());
    }
}
