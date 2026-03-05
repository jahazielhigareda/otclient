using System.IO;

namespace OTClient.Framework.Core;

/// <summary>
/// Represents the load state of a <see cref="Module"/>.
/// </summary>
public enum ModuleState
{
    Unloaded,
    Loaded,
}

/// <summary>
/// Describes a single C# engine module that can be loaded and unloaded at runtime.
/// Lua modules are surfaced through this same interface once a Lua host is wired up
/// in Phase 6.  Maps to <c>src/framework/core/module.{h,cpp}</c>.
/// </summary>
public sealed class Module
{
    /// <param name="name">Unique module identifier, e.g. <c>"game_features"</c>.</param>
    /// <param name="description">Human-readable description shown in the module list.</param>
    /// <param name="onLoad">Action invoked when the module is loaded.</param>
    /// <param name="onUnload">Action invoked when the module is unloaded.</param>
    public Module(
        string name,
        string description = "",
        Action? onLoad = null,
        Action? onUnload = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Description = description;
        _onLoad = onLoad;
        _onUnload = onUnload;
    }

    public string Name { get; }
    public string Description { get; }
    public ModuleState State { get; private set; } = ModuleState.Unloaded;

    private readonly Action? _onLoad;
    private readonly Action? _onUnload;

    internal void Load()
    {
        if (State == ModuleState.Loaded) return;
        _onLoad?.Invoke();
        State = ModuleState.Loaded;
    }

    internal void Unload()
    {
        if (State == ModuleState.Unloaded) return;
        _onUnload?.Invoke();
        State = ModuleState.Unloaded;
    }
}

/// <summary>
/// Registry for engine and game modules.
/// Maps to <c>src/framework/core/modulemanager.{h,cpp}</c>.
/// </summary>
public sealed class ModuleManager
{
    private readonly Dictionary<string, Module> _modules = [];
    private readonly object _lock = new();
    private readonly Logger _logger;

    public ModuleManager(Logger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    // ─── Registration ─────────────────────────────────────────────────────────

    /// <summary>Registers a module.  Throws if a module with the same name already exists.</summary>
    public void Register(Module module)
    {
        ArgumentNullException.ThrowIfNull(module);
        lock (_lock)
        {
            if (_modules.ContainsKey(module.Name))
                throw new InvalidOperationException($"Module '{module.Name}' is already registered.");
            _modules[module.Name] = module;
        }
    }

    /// <summary>Removes a module from the registry (unloads it first if necessary).</summary>
    public void Unregister(string name)
    {
        lock (_lock)
        {
            if (!_modules.TryGetValue(name, out var m)) return;
            m.Unload();
            _modules.Remove(name);
        }
    }

    // ─── Load / Unload ────────────────────────────────────────────────────────

    /// <summary>Loads a previously registered module by name.</summary>
    public void LoadModule(string name)
    {
        Module? m;
        lock (_lock) _modules.TryGetValue(name, out m);
        if (m is null)
        {
            _logger.Warning($"ModuleManager: module '{name}' not found.");
            return;
        }
        _logger.Info($"Loading module '{name}'…");
        m.Load();
        _logger.Info($"Module '{name}' loaded.");
    }

    /// <summary>Unloads a loaded module by name.</summary>
    public void UnloadModule(string name)
    {
        Module? m;
        lock (_lock) _modules.TryGetValue(name, out m);
        if (m is null) return;
        _logger.Info($"Unloading module '{name}'…");
        m.Unload();
        _logger.Info($"Module '{name}' unloaded.");
    }

    /// <summary>Loads all registered modules that are not yet loaded.</summary>
    public void LoadModules()
    {
        Module[] snapshot;
        lock (_lock) snapshot = [.. _modules.Values];
        foreach (var m in snapshot)
            LoadModule(m.Name);
    }

    /// <summary>Unloads all currently loaded modules.</summary>
    public void UnloadModules()
    {
        Module[] snapshot;
        lock (_lock) snapshot = [.. _modules.Values];
        foreach (var m in snapshot)
            UnloadModule(m.Name);
    }

    /// <summary>Unloads all modules and clears the registry.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var m in _modules.Values)
                m.Unload();
            _modules.Clear();
        }
    }

    // ─── Query ────────────────────────────────────────────────────────────────

    public Module? GetModule(string name)
    {
        lock (_lock)
            return _modules.GetValueOrDefault(name);
    }

    public IReadOnlyList<Module> GetModules()
    {
        lock (_lock)
            return [.. _modules.Values];
    }
}
