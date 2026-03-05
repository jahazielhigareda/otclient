using Raylib_cs;

namespace OTClient.Framework.Core;

/// <summary>
/// Application lifecycle interface.  Implement this to receive per-frame
/// <see cref="Update"/> and <see cref="Render"/> callbacks from the main loop.
/// </summary>
public interface IApplicationLoop
{
    /// <summary>Called once per frame before rendering.</summary>
    void Update(double deltaSeconds);

    /// <summary>Called once per frame inside BeginDrawing/EndDrawing.</summary>
    void Render();
}

/// <summary>
/// Central application class that owns the Raylib window and drives the
/// <c>PollEvents → Update → Render</c> main loop.
/// <para>
/// Maps to <c>GraphicalApplication</c> in
/// <c>src/framework/core/graphicalapplication.{h,cpp}</c>.
/// </para>
/// </summary>
public sealed class Application : IDisposable
{
    // ─── Sub-systems (public for wiring) ─────────────────────────────────────
    public Logger Logger { get; }
    public Clock Clock { get; }
    public EventDispatcher Dispatcher { get; }
    public Scheduler Scheduler { get; }
    public ConfigManager Config { get; }
    public ModuleManager Modules { get; }
    public InputManager Input { get; }

    // ─── Window settings ─────────────────────────────────────────────────────
    private int _windowWidth;
    private int _windowHeight;
    private string _windowTitle;
    private int _targetFps;
    private bool _running;
    private bool _stopping;
    private bool _disposed;

    private IApplicationLoop? _loop;
    private double _lastFrameSeconds;

    public Application(
        string name = "OTClient",
        int windowWidth = 1280,
        int windowHeight = 720,
        int targetFps = 60)
    {
        _windowTitle = name;
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
        _targetFps = targetFps;

        Logger = new Logger();
        Clock = new Clock();
        Dispatcher = new EventDispatcher();
        Scheduler = new Scheduler(Dispatcher, Clock);
        Config = new ConfigManager();
        Modules = new ModuleManager(Logger);
        Input = new InputManager();
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    public string Name
    {
        get => _windowTitle;
        set
        {
            _windowTitle = value;
            if (_running) Raylib.SetWindowTitle(value);
        }
    }

    public int TargetFps
    {
        get => _targetFps;
        set
        {
            _targetFps = value;
            if (_running) Raylib.SetTargetFPS(value);
        }
    }

    public bool IsRunning => _running;
    public bool IsStopping => _stopping;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises all sub-systems and opens the Raylib window.
    /// Call this before <see cref="Run"/>.
    /// </summary>
    public void Init(IApplicationLoop? loop = null)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Application));
        _loop = loop;

        Logger.Info($"Initialising {_windowTitle}…");

        Dispatcher.Init();
        Clock.Update();

        // Open the Raylib window (audio device is opened later in Phase 4)
        Raylib.InitWindow(_windowWidth, _windowHeight, _windowTitle);
        Raylib.SetTargetFPS(_targetFps);

        _lastFrameSeconds = Clock.Seconds;
        _running = true;
        Logger.Info("Application initialised.");
    }

    /// <summary>
    /// Enters the main loop: <c>PollEvents → Update → Render</c>.
    /// Blocks until <see cref="Exit"/> is called or the window is closed.
    /// </summary>
    public void Run()
    {
        if (!_running) throw new InvalidOperationException("Call Init() before Run().");

        Logger.Info("Entering main loop.");

        while (!Raylib.WindowShouldClose() && !_stopping)
        {
            // 1. Update clock snapshot
            Clock.Update();
            double now = Clock.Seconds;
            double delta = now - _lastFrameSeconds;
            _lastFrameSeconds = now;

            // 2. Poll dispatcher (processes events queued from any thread)
            Dispatcher.Poll();

            // 3. Poll input (keyboard, mouse, text) for this frame
            Input.Poll();

            // 4. Game/logic update
            _loop?.Update(delta);

            // 5. Render
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            _loop?.Render();
            Raylib.EndDrawing();
        }

        Logger.Info("Main loop exited.");
    }

    /// <summary>
    /// Signals the main loop to stop at the end of the current frame.
    /// Thread-safe; may be called from any thread or from a Lua callback.
    /// </summary>
    public void Exit()
    {
        _stopping = true;
        Logger.Info("Application exit requested.");
    }

    /// <summary>
    /// Unloads modules, shuts down the dispatcher, and closes the Raylib window.
    /// Call after <see cref="Run"/> returns.
    /// </summary>
    public void Deinit()
    {
        Logger.Info("Deinitialising…");
        Modules.UnloadModules();
        Modules.Clear();
        Dispatcher.Shutdown();
        _running = false;
        _stopping = false;
    }

    /// <summary>
    /// Terminates sub-systems and closes the OS window.
    /// Call after <see cref="Deinit"/>.
    /// </summary>
    public void Terminate()
    {
        Config.Save();
        Scheduler.Dispose();
        Logger.Info("Goodbye.");
        if (Raylib.IsWindowReady())
            Raylib.CloseWindow();
        Logger.Dispose();
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Scheduler.Dispose();
        if (Raylib.IsWindowReady())
            Raylib.CloseWindow();
        Logger.Dispose();
    }
}
