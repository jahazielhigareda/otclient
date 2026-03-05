using OTClient.Framework.Core;
using OTClient.Framework.Lua;
using Xunit;

namespace OTClient.Tests.Lua;

/// <summary>
/// Tests for <see cref="LuaGlobals"/> — g_* singleton registration — and the
/// <see cref="LuaGlobalProxies"/> types (LuaAppProxy, LuaLoggerProxy, etc.).
/// Tasks 6.5–6.9, 6.12, 6.15.
/// </summary>
public sealed class LuaGlobalsTests : IDisposable
{
    private readonly LuaInterface _lua = new();
    private readonly Logger _logger    = new();
    private readonly ModuleManager _modules;

    public LuaGlobalsTests()
    {
        _modules = new ModuleManager(_logger);
        _lua.Init();
        LuaGlobals.Register(_lua, _logger, _modules);
    }

    public void Dispose() => _lua.Dispose();

    // ─── g_app ────────────────────────────────────────────────────────────────

    [Fact]
    public void GApp_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(g_app)");
        Assert.Equal("userdata", v.String);
    }

    [Fact]
    public void GApp_GetName_DefaultsToOTClient()
    {
        var v = _lua.DoString("return g_app:getName()");
        Assert.Equal("OTClient", v.String);
    }

    [Fact]
    public void GApp_SetName_VisibleFromLua()
    {
        _lua.DoString("g_app:setName('TestGame')");
        var v = _lua.DoString("return g_app:getName()");
        Assert.Equal("TestGame", v.String);
    }

    [Fact]
    public void GApp_GetCompactName_DefaultsToOtclient()
    {
        var v = _lua.DoString("return g_app:getCompactName()");
        Assert.Equal("otclient", v.String);
    }

    [Fact]
    public void GApp_GetVersion_ReturnsString()
    {
        var v = _lua.DoString("return type(g_app:getVersion())");
        Assert.Equal("string", v.String);
    }

    [Fact]
    public void GApp_HasUpdater_ReturnsFalse()
    {
        var v = _lua.DoString("return g_app:hasUpdater()");
        Assert.False(v.Boolean);
    }

    [Fact]
    public void GApp_GetBuildArch_ReturnsNonEmptyString()
    {
        var v = _lua.DoString("return g_app:getBuildArch()");
        Assert.NotEmpty(v.String);
    }

    // ─── g_logger ─────────────────────────────────────────────────────────────

    [Fact]
    public void GLogger_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(g_logger)");
        Assert.Equal("userdata", v.String);
    }

    [Fact]
    public void GLogger_Info_CallDoesNotThrow()
    {
        var ex = Record.Exception(() => _lua.DoString("g_logger:info('test message')"));
        Assert.Null(ex);
    }

    [Fact]
    public void GLogger_Info_WritesToEngineLogger()
    {
        _lua.DoString("g_logger:info('lua-test-marker')");
        var history = _logger.GetHistory();
        Assert.Contains(history, m => m.Message.Contains("lua-test-marker"));
    }

    [Fact]
    public void GLogger_Warning_WritesToEngineLogger()
    {
        _lua.DoString("g_logger:warning('warn-marker')");
        var history = _logger.GetHistory();
        Assert.Contains(history, m => m.Message.Contains("warn-marker"));
    }

    // ─── g_resources ──────────────────────────────────────────────────────────

    [Fact]
    public void GResources_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(g_resources)");
        Assert.Equal("userdata", v.String);
    }

    [Fact]
    public void GResources_GetWorkDir_ReturnsNonEmptyString()
    {
        var v = _lua.DoString("return g_resources:getWorkDir()");
        Assert.Equal(MoonSharp.Interpreter.DataType.String, v.Type);
        Assert.NotEmpty(v.String);
    }

    [Fact]
    public void GResources_AddSearchPath_ReturnsBool()
    {
        var v = _lua.DoString("return g_resources:addSearchPath('/tmp', true)");
        Assert.Equal(MoonSharp.Interpreter.DataType.Boolean, v.Type);
    }

    // ─── g_platform ───────────────────────────────────────────────────────────

    [Fact]
    public void GPlatform_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(g_platform)");
        Assert.Equal("userdata", v.String);
    }

    [Fact]
    public void GPlatform_GetOSName_ReturnsKnownString()
    {
        var v = _lua.DoString("return g_platform:getOSName()");
        var known = new[] { "Windows", "Linux", "macOS" };
        Assert.Contains(v.String, known);
    }

    // ─── g_modules ────────────────────────────────────────────────────────────

    [Fact]
    public void GModules_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(g_modules)");
        Assert.Equal("userdata", v.String);
    }

    [Fact]
    public void GModules_EnsureModuleLoaded_LoadsModule()
    {
        bool loaded = false;
        _modules.Register(new Module("lua-test-mod", onLoad: () => loaded = true));
        _lua.DoString("g_modules:ensureModuleLoaded('lua-test-mod')");
        Assert.True(loaded);
    }

    [Fact]
    public void GModules_IsModuleLoaded_ReturnsFalse_WhenNotLoaded()
    {
        _modules.Register(new Module("unloaded-mod"));
        var v = _lua.DoString("return g_modules:isModuleLoaded('unloaded-mod')");
        Assert.False(v.Boolean);
    }

    [Fact]
    public void GModules_IsModuleLoaded_ReturnsTrue_AfterEnsureLoaded()
    {
        _modules.Register(new Module("auto-load-mod"));
        _lua.DoString("g_modules:ensureModuleLoaded('auto-load-mod')");
        var v = _lua.DoString("return g_modules:isModuleLoaded('auto-load-mod')");
        Assert.True(v.Boolean);
    }

    // ─── g_sounds ─────────────────────────────────────────────────────────────

    [Fact]
    public void GSounds_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(g_sounds)");
        Assert.Equal("userdata", v.String);
    }

    // ─── g_http ───────────────────────────────────────────────────────────────

    [Fact]
    public void GHttp_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(g_http)");
        Assert.Equal("userdata", v.String);
    }

    // ─── addEvent (task 6.12) ─────────────────────────────────────────────────

    [Fact]
    public void AddEvent_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(addEvent)");
        Assert.Equal("function", v.String);
    }

    [Fact]
    public void AddEvent_CallableWithFunction()
    {
        var ex = Record.Exception(() => _lua.DoString("addEvent(function() end)"));
        Assert.Null(ex);
    }

    // ─── scheduleEvent (task 6.12) ────────────────────────────────────────────

    [Fact]
    public void ScheduleEvent_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(scheduleEvent)");
        Assert.Equal("function", v.String);
    }

    [Fact]
    public void ScheduleEvent_CallableWithFunctionAndDelay()
    {
        // scheduleEvent now returns an integer event-ID
        var ex = Record.Exception(() => _lua.DoString("local id = scheduleEvent(function() end, 100)"));
        Assert.Null(ex);
    }

    [Fact]
    public void ScheduleEvent_ReturnsInteger()
    {
        var v = _lua.DoString("return scheduleEvent(function() end, 9999)");
        Assert.Equal(MoonSharp.Interpreter.DataType.Number, v.Type);
        Assert.True(v.Number >= 1);
    }

    [Fact]
    public void RemoveEvent_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(removeEvent)");
        Assert.Equal("function", v.String);
    }

    [Fact]
    public void RemoveEvent_CancelsPendingEvent()
    {
        var ex = Record.Exception(() => _lua.DoString(
            "local id = scheduleEvent(function() end, 5000); removeEvent(id)"));
        Assert.Null(ex);
    }

    // ─── cycleEvent (task 6.12) ───────────────────────────────────────────────

    [Fact]
    public void CycleEvent_IsRegistered_AsGlobal()
    {
        var v = _lua.DoString("return type(cycleEvent)");
        Assert.Equal("function", v.String);
    }

    [Fact]
    public void CycleEvent_ReturnsIntegerEventId()
    {
        var v = _lua.DoString("local id = cycleEvent(function() end, 9999); removeEvent(id); return id");
        Assert.Equal(MoonSharp.Interpreter.DataType.Number, v.Type);
        Assert.True(v.Number >= 1);
    }

    // ─── Integration: multi-global Lua script ─────────────────────────────────

    [Fact]
    public void Integration_LuaScript_UsesMultipleGlobals()
    {
        const string script = @"
            local arch = g_platform:getOSName()
            local name = g_app:getName()
            g_logger:info('Running on ' .. arch .. ' as ' .. name)
            return arch ~= nil and name ~= nil
        ";
        var result = _lua.DoString(script);
        Assert.True(result.Boolean);
    }
}
