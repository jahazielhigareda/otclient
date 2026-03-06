using MoonSharp.Interpreter;
using System.Runtime.InteropServices;

namespace OTClient.Framework.Lua;

// ─── g_app ────────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the application object, exposed as the <c>g_app</c> global.
/// Surfaces the subset of <c>Application</c> API that Lua modules call.
/// Task 6.5.
/// </summary>
[LuaBinding("g_app")]
[MoonSharpUserData]
public sealed class LuaAppProxy
{
    private string _name           = "OTClient";
    private string _compactName    = "otclient";
    private string _orgName        = "otcr";
    private string _version        = "0.0.1";
    private string _buildRevision  = "0";
    private string _buildCommit    = "unknown";
    private string _buildDate      = "unknown";
    private string _buildArch      = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    [LuaMethod] public string getName()           => _name;
    [LuaMethod] public string getCompactName()    => _compactName;
    [LuaMethod] public string getOrganizationName() => _orgName;
    [LuaMethod] public string getVersion()        => _version;
    [LuaMethod] public string getBuildRevision()  => _buildRevision;
    [LuaMethod] public string getBuildCommit()    => _buildCommit;
    [LuaMethod] public string getBuildDate()      => _buildDate;
    [LuaMethod] public string getBuildArch()      => _buildArch;

    [LuaMethod] public void setName(string name)            => _name        = name;
    [LuaMethod] public void setCompactName(string name)     => _compactName = name;
    [LuaMethod] public void setOrganizationName(string org) => _orgName     = org;
    [LuaMethod] public void setVersion(string v)            => _version     = v;

    [LuaMethod] public bool hasUpdater() => false;
    [LuaMethod] public void exit()       => Environment.Exit(0);
}

// ─── g_logger ─────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for the logger, exposed as the <c>g_logger</c> global.
/// Delegates to the engine <see cref="Logger"/>.
/// Task 6.5.
/// </summary>
[LuaBinding("g_logger")]
[MoonSharpUserData]
public sealed class LuaLoggerProxy
{
    private readonly Logger _logger;

    public LuaLoggerProxy(Logger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    [LuaMethod] public void debug(string msg)   => _logger.Debug(msg);
    [LuaMethod] public void info(string msg)    => _logger.Info(msg);
    [LuaMethod] public void warning(string msg) => _logger.Warning(msg);
    [LuaMethod] public void error(string msg)   => _logger.Error(msg);
    [LuaMethod] public void fatal(string msg)   => _logger.Fatal(msg);
    [LuaMethod] public void setLogFile(string path) => _logger.SetLogFile(path);
}

// ─── g_resources ──────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for resource / file-system access, exposed as <c>g_resources</c>.
/// Task 6.5.
/// </summary>
[LuaBinding("g_resources")]
[MoonSharpUserData]
public sealed class LuaResourcesProxy
{
    private readonly List<string> _searchPaths = [];
    private string _workDir = AppContext.BaseDirectory;

    [LuaMethod]
    public string getWorkDir() => _workDir.TrimEnd(System.IO.Path.DirectorySeparatorChar) +
                                   System.IO.Path.DirectorySeparatorChar;

    [LuaMethod]
    public bool addSearchPath(string path, bool pushFront = false)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string full = System.IO.Path.GetFullPath(path);
        if (_searchPaths.Contains(full)) return true;
        if (pushFront) _searchPaths.Insert(0, full);
        else           _searchPaths.Add(full);
        return true;
    }

    [LuaMethod] public void setupUserWriteDir(string dir) { /* stub */ }

    [LuaMethod]
    public void searchAndAddPackages(string dir, string ext, bool recursive) { /* stub */ }

    [LuaMethod]
    public bool fileExists(string path)
    {
        foreach (var sp in _searchPaths)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(sp, path)))
                return true;
        }
        return System.IO.File.Exists(path);
    }

    [LuaMethod] public IReadOnlyList<string> getSearchPaths() => _searchPaths.AsReadOnly();
}

// ─── g_platform ───────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for platform / OS information, exposed as <c>g_platform</c>.
/// Task 6.5.
/// </summary>
[LuaBinding("g_platform")]
[MoonSharpUserData]
public sealed class LuaPlatformProxy
{
    [LuaMethod]
    public string getOSName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     return "macOS";
        return "Linux";
    }

    [LuaMethod] public string getArch()  => RuntimeInformation.ProcessArchitecture.ToString();
    [LuaMethod] public bool isWindows()  => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    [LuaMethod] public bool isLinux()    => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    [LuaMethod] public bool isMacOS()    => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
}

// ─── g_modules ────────────────────────────────────────────────────────────────

/// <summary>
/// Lua proxy for module management, exposed as <c>g_modules</c>.
/// Delegates to the engine <see cref="ModuleManager"/>.
/// Task 6.5.
/// </summary>
[LuaBinding("g_modules")]
[MoonSharpUserData]
public sealed class LuaModulesProxy
{
    private readonly ModuleManager _modules;

    public LuaModulesProxy(ModuleManager modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = modules;
    }

    [LuaMethod] public void discoverModules()                     { /* stub */ }
    [LuaMethod] public void autoLoadModules(int priority)         { /* stub */ }
    [LuaMethod] public void ensureModuleLoaded(string name)       => _modules.LoadModule(name);
    [LuaMethod] public void loadModule(string name)               => _modules.LoadModule(name);
    [LuaMethod] public void unloadModule(string name)             => _modules.UnloadModule(name);

    [LuaMethod]
    public object? getModule(string name)
    {
        var m = _modules.GetModule(name);
        return m is null ? null : (object)m.Name;
    }

    [LuaMethod] public bool isModuleLoaded(string name)
        => _modules.GetModule(name)?.State == ModuleState.Loaded;
}
