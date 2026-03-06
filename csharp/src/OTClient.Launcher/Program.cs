using OTClient.Framework.Core;

// ── Bootstrap sequence mirrors src/main.cpp ──────────────────────────────────
//
//   Init → LoadConfig → LoadModules → Run → Deinit → Terminate
//
// The Launcher project is the thin entry point that wires everything together.
// Game-specific logic will be added in later phases via the IApplicationLoop
// interface and the ModuleManager.
// ─────────────────────────────────────────────────────────────────────────────

var app = new Application(
    name: "OTClient – Redemption",
    windowWidth: 1280,
    windowHeight: 720,
    targetFps: 60);

// Load config (creates the file with defaults if missing)
app.Config.Load("config.json");

// Register built-in modules here (game modules added in later phases)
// app.Modules.Register(new Module("example", onLoad: () => { … }));

// Initialise window + subsystems
app.Init();

app.Logger.Info("OTClient starting…");

try
{
    // Load all registered modules
    app.Modules.LoadModules();

    // Enter main loop (blocks until window closed or app.Exit() called)
    app.Run();
}
finally
{
    app.Deinit();
    app.Terminate();
}
