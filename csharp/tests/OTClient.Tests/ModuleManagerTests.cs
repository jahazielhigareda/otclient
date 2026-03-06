using OTClient.Framework.Core;
using Xunit;

namespace OTClient.Tests;

public sealed class ModuleManagerTests
{
    private readonly Logger _logger = new();
    private ModuleManager CreateManager() => new(_logger);

    // ─── Register / Unregister ────────────────────────────────────────────────

    [Fact]
    public void Register_AddsModuleToRegistry()
    {
        var mgr = CreateManager();
        mgr.Register(new Module("core"));

        Assert.NotNull(mgr.GetModule("core"));
    }

    [Fact]
    public void Register_DuplicateName_ThrowsInvalidOperationException()
    {
        var mgr = CreateManager();
        mgr.Register(new Module("core"));

        Assert.Throws<InvalidOperationException>(() => mgr.Register(new Module("core")));
    }

    [Fact]
    public void Unregister_RemovesModule()
    {
        var mgr = CreateManager();
        mgr.Register(new Module("core"));
        mgr.Unregister("core");

        Assert.Null(mgr.GetModule("core"));
    }

    [Fact]
    public void Unregister_NonExistent_DoesNotThrow()
    {
        var mgr = CreateManager();
        // Should not throw
        mgr.Unregister("ghost");
    }

    // ─── Load / Unload ────────────────────────────────────────────────────────

    [Fact]
    public void LoadModule_InvokesOnLoadCallback()
    {
        var mgr = CreateManager();
        bool called = false;
        mgr.Register(new Module("m", onLoad: () => called = true));

        mgr.LoadModule("m");

        Assert.True(called);
    }

    [Fact]
    public void LoadModule_SetsStateToLoaded()
    {
        var mgr = CreateManager();
        mgr.Register(new Module("m"));
        mgr.LoadModule("m");

        Assert.Equal(ModuleState.Loaded, mgr.GetModule("m")!.State);
    }

    [Fact]
    public void LoadModule_CalledTwice_OnlyInvokesCallbackOnce()
    {
        var mgr = CreateManager();
        int count = 0;
        mgr.Register(new Module("m", onLoad: () => count++));

        mgr.LoadModule("m");
        mgr.LoadModule("m");

        Assert.Equal(1, count);
    }

    [Fact]
    public void UnloadModule_InvokesOnUnloadCallback()
    {
        var mgr = CreateManager();
        bool called = false;
        mgr.Register(new Module("m", onUnload: () => called = true));
        mgr.LoadModule("m");

        mgr.UnloadModule("m");

        Assert.True(called);
    }

    [Fact]
    public void UnloadModule_SetsStateToUnloaded()
    {
        var mgr = CreateManager();
        mgr.Register(new Module("m"));
        mgr.LoadModule("m");
        mgr.UnloadModule("m");

        Assert.Equal(ModuleState.Unloaded, mgr.GetModule("m")!.State);
    }

    [Fact]
    public void UnloadModule_CalledTwice_OnlyInvokesCallbackOnce()
    {
        var mgr = CreateManager();
        int count = 0;
        mgr.Register(new Module("m", onUnload: () => count++));
        mgr.LoadModule("m");

        mgr.UnloadModule("m");
        mgr.UnloadModule("m");

        Assert.Equal(1, count);
    }

    // ─── LoadModules / UnloadModules ──────────────────────────────────────────

    [Fact]
    public void LoadModules_LoadsAllRegisteredModules()
    {
        var mgr = CreateManager();
        mgr.Register(new Module("a"));
        mgr.Register(new Module("b"));
        mgr.Register(new Module("c"));

        mgr.LoadModules();

        Assert.All(mgr.GetModules(), m => Assert.Equal(ModuleState.Loaded, m.State));
    }

    [Fact]
    public void UnloadModules_UnloadsAllLoadedModules()
    {
        var mgr = CreateManager();
        mgr.Register(new Module("a"));
        mgr.Register(new Module("b"));
        mgr.LoadModules();

        mgr.UnloadModules();

        Assert.All(mgr.GetModules(), m => Assert.Equal(ModuleState.Unloaded, m.State));
    }

    // ─── Clear ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_UnloadsAndRemovesAllModules()
    {
        var mgr = CreateManager();
        int unloads = 0;
        mgr.Register(new Module("a", onUnload: () => unloads++));
        mgr.Register(new Module("b", onUnload: () => unloads++));
        mgr.LoadModules();

        mgr.Clear();

        Assert.Empty(mgr.GetModules());
        Assert.Equal(2, unloads);
    }

    // ─── GetModules ───────────────────────────────────────────────────────────

    [Fact]
    public void GetModules_ReturnsAllRegisteredModules()
    {
        var mgr = CreateManager();
        mgr.Register(new Module("x"));
        mgr.Register(new Module("y"));

        var modules = mgr.GetModules();

        Assert.Equal(2, modules.Count);
        Assert.Contains(modules, m => m.Name == "x");
        Assert.Contains(modules, m => m.Name == "y");
    }
}
