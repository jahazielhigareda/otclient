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
/// Tasks 6.5–6.12.
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
    /// <param name="fontManager">Font manager (becomes <c>g_fonts</c>); may be null.</param>
    /// <param name="game">Game singleton (becomes <c>g_game</c>); may be null.</param>
    /// <param name="uiManager">UI manager (becomes <c>g_ui</c>); may be null.</param>
    public static void Register(
        LuaInterface           lua,
        Logger?                logger      = null,
        ModuleManager?         modules     = null,
        EventDispatcher?       dispatcher  = null,
        Scheduler?             scheduler   = null,
        Net.ProtocolHttp?      http        = null,
        Graphics.FontManager?  fontManager = null,
        Game.Game?             game        = null,
        UI.UIManager?          uiManager   = null)
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

        // ── g_graphics (task 6.6) ─────────────────────────────────────────────
        lua.SetGlobal("g_graphics", new LuaGraphicsProxy());

        // ── g_textures (task 6.6) ─────────────────────────────────────────────
        lua.SetGlobal("g_textures", new LuaTexturesProxy());

        // ── g_fonts (task 6.6) ────────────────────────────────────────────────
        lua.SetGlobal("g_fonts", new LuaFontsProxy(fontManager ?? new Graphics.FontManager()));

        // ── g_drawpool (task 6.6) ─────────────────────────────────────────────
        lua.SetGlobal("g_drawpool", new LuaDrawPoolProxy());

        // ── g_keyboard (task 6.7) ─────────────────────────────────────────────
        lua.SetGlobal("g_keyboard", new LuaKeyboardProxy());

        // ── g_mouse (task 6.7) ────────────────────────────────────────────────
        lua.SetGlobal("g_mouse", new LuaMouseProxy());

        // ── g_game, g_map, g_minimap, g_things, g_sprites, g_creatures, g_client (task 6.10 / T36)
        var g = game ?? new Game.Game();
        lua.SetGlobal("g_game",      new LuaGameProxy(g));
        lua.SetGlobal("g_map",       new LuaMapProxy(g.Map));
        lua.SetGlobal("g_minimap",   new LuaMinimapProxy(g.Minimap));
        lua.SetGlobal("g_things",    new LuaThingsProxy(g.Things));
        lua.SetGlobal("g_sprites",   new LuaSpritesProxy());
        lua.SetGlobal("g_creatures", new LuaCreaturesProxy(g.Creatures));
        lua.SetGlobal("g_client",    new LuaClientProxy());

        // ── ThingCategory integer constants (T37) ─────────────────────────────
        // Mirrors the C++ Lua bindings in luafunctions.cpp.
        lua.SetGlobal("ThingCategoryItem",     (int)Game.ThingCategory.Item);
        lua.SetGlobal("ThingCategoryCreature", (int)Game.ThingCategory.Creature);
        lua.SetGlobal("ThingCategoryEffect",   (int)Game.ThingCategory.Effect);
        lua.SetGlobal("ThingCategoryMissile",  (int)Game.ThingCategory.Missile);

        // ── g_ui (task 6.11) ──────────────────────────────────────────────────
        lua.SetGlobal("g_ui", new LuaUiProxy(uiManager ?? new UI.UIManager()));

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

        // Task 6.6 — graphics
        LuaBinder.RegisterType<LuaGraphicsProxy>();
        LuaBinder.RegisterType<LuaTexturesProxy>();
        LuaBinder.RegisterType<LuaFontsProxy>();
        LuaBinder.RegisterType<LuaDrawPoolProxy>();

        // Task 6.7 — input
        LuaBinder.RegisterType<LuaKeyboardProxy>();
        LuaBinder.RegisterType<LuaMouseProxy>();

        // Task 6.10 — game globals
        LuaBinder.RegisterType<LuaGameProxy>();
        LuaBinder.RegisterType<LuaMapProxy>();
        LuaBinder.RegisterType<LuaMinimapProxy>();
        LuaBinder.RegisterType<LuaThingsProxy>();
        LuaBinder.RegisterType<LuaSpritesProxy>();
        LuaBinder.RegisterType<LuaCreaturesProxy>();
        LuaBinder.RegisterType<LuaClientProxy>();

        // T37 — ThingType as first-class Lua userdata
        LuaBinder.RegisterType<Game.ThingType>();

        // T38 — Creature hierarchy as Lua userdata
        LuaBinder.RegisterType<Game.Creature>();
        LuaBinder.RegisterType<Game.Player>();
        LuaBinder.RegisterType<Game.LocalPlayer>();
        LuaBinder.RegisterType<Game.Monster>();
        LuaBinder.RegisterType<Game.Npc>();

        // Task 6.11 — UI
        LuaBinder.RegisterType<LuaUiProxy>();
        LuaBinder.RegisterType<UI.UIWidget>();
        LuaBinder.RegisterType<UI.UIButton>();
        LuaBinder.RegisterType<UI.UILabel>();
        LuaBinder.RegisterType<UI.UITextEdit>();
        LuaBinder.RegisterType<UI.UICheckBox>();
        LuaBinder.RegisterType<UI.UIScrollBar>();
        LuaBinder.RegisterType<UI.UIScrollArea>();
        LuaBinder.RegisterType<UI.UIProgressBar>();
        LuaBinder.RegisterType<UI.UIWindow>();
        LuaBinder.RegisterType<UI.UITabBar>();
        LuaBinder.RegisterType<UI.UIMap>();
        LuaBinder.RegisterType<UI.UIItem>();
        LuaBinder.RegisterType<UI.UICreature>();
        LuaBinder.RegisterType<UI.UIMinimap>();
        LuaBinder.RegisterType<UI.UISprite>();
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
