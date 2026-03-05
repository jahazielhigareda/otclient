using MoonSharp.Interpreter;

namespace OTClient.Framework.Lua;

/// <summary>
/// Registers all engine <c>g_*</c> global singletons into a
/// <see cref="LuaInterface"/> and installs the Lua coroutine / event helpers
/// (<c>addEvent</c>, <c>scheduleEvent</c>, <c>cycleEvent</c>).
/// <para>
/// Call <see cref="Register"/> once after <see cref="LuaInterface.Init"/>
/// and before running any Lua scripts.
/// </para>
/// Tasks 6.5–6.9, 6.12.
/// </summary>
public static class LuaGlobals
{
    // ─── Registration entry point ─────────────────────────────────────────────

    /// <summary>
    /// Registers all engine types and global singletons.
    /// </summary>
    /// <param name="lua">Initialised <see cref="LuaInterface"/> instance.</param>
    /// <param name="logger">Engine logger (becomes <c>g_logger</c>).</param>
    /// <param name="modules">Module manager (becomes <c>g_modules</c>).</param>
    /// <param name="dispatcher">Event dispatcher (becomes <c>g_dispatcher</c>).</param>
    /// <param name="scheduler">Scheduler (becomes <c>g_scheduler</c>).</param>
    /// <param name="http">HTTP client (becomes <c>g_http</c>); may be null.</param>
    public static void Register(
        LuaInterface     lua,
        Logger?          logger     = null,
        ModuleManager?   modules    = null,
        EventDispatcher? dispatcher = null,
        Scheduler?       scheduler  = null,
        Net.ProtocolHttp? http      = null)
    {
        ArgumentNullException.ThrowIfNull(lua);

        // ── Register MoonSharp types ──────────────────────────────────────────
        RegisterTypes();

        // ── g_app ─────────────────────────────────────────────────────────────
        lua.SetGlobal("g_app", new LuaAppProxy());

        // ── g_logger ──────────────────────────────────────────────────────────
        var log = logger ?? new Logger();
        lua.SetGlobal("g_logger", new LuaLoggerProxy(log));

        // ── g_resources ───────────────────────────────────────────────────────
        lua.SetGlobal("g_resources", new LuaResourcesProxy());

        // ── g_platform ────────────────────────────────────────────────────────
        lua.SetGlobal("g_platform", new LuaPlatformProxy());

        // ── g_modules ─────────────────────────────────────────────────────────
        var mgr = modules ?? new ModuleManager(log);
        lua.SetGlobal("g_modules", new LuaModulesProxy(mgr));

        // ── g_dispatcher ──────────────────────────────────────────────────────
        lua.SetGlobal("g_dispatcher", dispatcher ?? new EventDispatcher());

        // ── g_scheduler ───────────────────────────────────────────────────────
        if (scheduler is not null)
            lua.SetGlobal("g_scheduler", scheduler);

        // ── g_sounds ──────────────────────────────────────────────────────────
        lua.SetGlobal("g_sounds", new Sound.SoundManager());

        // ── g_http ────────────────────────────────────────────────────────────
        lua.SetGlobal("g_http", http ?? new Net.ProtocolHttp());

        // ── Coroutine / event helpers (task 6.12) ─────────────────────────────
        RegisterEventHelpers(lua, dispatcher);
    }

    // ─── Type registration ────────────────────────────────────────────────────

    /// <summary>
    /// Registers all Lua-facing proxy types with MoonSharp's UserData system.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public static void RegisterTypes()
    {
        LuaBinder.RegisterType<LuaAppProxy>();
        LuaBinder.RegisterType<LuaLoggerProxy>();
        LuaBinder.RegisterType<LuaResourcesProxy>();
        LuaBinder.RegisterType<LuaPlatformProxy>();
        LuaBinder.RegisterType<LuaModulesProxy>();
        LuaBinder.RegisterType<EventDispatcher>();
        LuaBinder.RegisterType<Sound.SoundManager>();
        LuaBinder.RegisterType<Net.ProtocolHttp>();
    }

    // ─── Coroutine / event helpers ────────────────────────────────────────────

    private static void RegisterEventHelpers(LuaInterface lua, EventDispatcher? dispatcher)
    {
        var script = lua.RawScript;

        // Event-ID → CancellationTokenSource registry (for removeEvent)
        var pendingEvents = new Dictionary<int, CancellationTokenSource>();
        int nextEventId = 1;

        // addEvent(fn)  — run fn on the next dispatcher Poll
        script.Globals["addEvent"] = (Action<DynValue>)(fn =>
        {
            if (dispatcher is null) return;
            if (fn.Type == DataType.Function)
                dispatcher.AddEvent(() =>
                {
                    try { script.Call(fn); }
                    catch (Exception) { /* swallow Lua errors from addEvent callbacks */ }
                });
        });

        // scheduleEvent(fn, delayMs)  — run fn after delayMs.
        // Returns an integer event-ID that can be passed to removeEvent() to cancel.
        script.Globals["scheduleEvent"] = (Func<DynValue, int, int>)((fn, delay) =>
        {
            int id = nextEventId++;
            var cts = new CancellationTokenSource();
            pendingEvents[id] = cts;

            if (fn.Type == DataType.Function)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(Math.Max(0, delay), cts.Token);
                        if (dispatcher is not null)
                            dispatcher.AddEvent(() =>
                            {
                                try { script.Call(fn); }
                                catch (Exception) { /* swallow */ }
                            });
                        else
                            script.Call(fn);
                    }
                    catch (OperationCanceledException) { /* clean cancel */ }
                    catch (Exception) { /* swallow Lua errors */ }
                    finally { pendingEvents.Remove(id); }
                }, cts.Token);
            }

            return id;
        });

        // cycleEvent(fn, intervalMs)  — repeatedly run fn every intervalMs.
        // Returns an integer event-ID that can be passed to removeEvent() to stop the cycle.
        script.Globals["cycleEvent"] = (Func<DynValue, int, int>)((fn, interval) =>
        {
            int id = nextEventId++;
            var cts = new CancellationTokenSource();
            pendingEvents[id] = cts;

            if (fn.Type == DataType.Function)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(Math.Max(1, interval), cts.Token);
                            if (dispatcher is not null)
                                dispatcher.AddEvent(() =>
                                {
                                    try { script.Call(fn); }
                                    catch (Exception) { /* swallow */ }
                                });
                            else
                                script.Call(fn);
                        }
                    }
                    catch (OperationCanceledException) { /* clean cancel */ }
                    catch (Exception) { /* swallow Lua errors */ }
                    finally { pendingEvents.Remove(id); }
                }, cts.Token);
            }

            return id;
        });

        // removeEvent(id)  — cancels a pending scheduleEvent or cycleEvent.
        script.Globals["removeEvent"] = (Action<int>)(id =>
        {
            if (pendingEvents.TryGetValue(id, out var cts))
            {
                cts.Cancel();
                pendingEvents.Remove(id);
            }
        });
    }
}
