using MoonSharp.Interpreter;
using System.IO;

namespace OTClient.Framework.Lua;

/// <summary>
/// Core Lua runtime wrapper built on MoonSharp (pure-C# Lua 5.2 implementation).
/// <para>
/// Provides <see cref="DoString"/>, <see cref="DoFile"/>, global get/set,
/// coroutine creation, and type registration helpers.  All errors surface as
/// <see cref="LuaException"/>.
/// </para>
/// Maps to <c>src/framework/luaengine/luainterface.{h,cpp}</c>.
/// Task 6.2.
/// </summary>
public sealed class LuaInterface : IDisposable
{
    private Script? _script;
    private bool _disposed;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary><c>true</c> once <see cref="Init"/> has been called successfully.</summary>
    public bool IsInitialised => _script is not null && !_disposed;

    /// <summary>
    /// Initialises the Lua runtime and registers the standard libraries.
    /// </summary>
    public void Init()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_script is not null) return; // idempotent

        _script = new Script(CoreModules.Preset_SoftSandbox
                           | CoreModules.LoadMethods
                           | CoreModules.OS_Time
                           | CoreModules.String
                           | CoreModules.Table
                           | CoreModules.Math
                           | CoreModules.Basic);
    }

    /// <summary>
    /// Shuts down the Lua runtime and releases resources.
    /// </summary>
    public void Shutdown()
    {
        _script = null;
    }

    // ─── Execution ────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a Lua code string and returns the first return value (or Nil).
    /// </summary>
    /// <exception cref="LuaException">On Lua syntax or runtime error.</exception>
    public DynValue DoString(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        EnsureReady();
        try
        {
            return _script!.DoString(code);
        }
        catch (ScriptRuntimeException ex)
        {
            throw new LuaException(ex.DecoratedMessage, ex);
        }
        catch (SyntaxErrorException ex)
        {
            throw new LuaException(ex.DecoratedMessage, ex);
        }
    }

    /// <summary>
    /// Loads and executes a Lua script from the file system.
    /// </summary>
    /// <exception cref="FileNotFoundException">When the file does not exist.</exception>
    /// <exception cref="LuaException">On Lua syntax or runtime error.</exception>
    public DynValue DoFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        EnsureReady();

        if (!File.Exists(path))
            throw new FileNotFoundException($"Lua script not found: {path}", path);

        string code = File.ReadAllText(path);
        try
        {
            return _script!.DoString(code, globalContext: null, codeFriendlyName: path);
        }
        catch (ScriptRuntimeException ex)
        {
            throw new LuaException(ex.DecoratedMessage, ex);
        }
        catch (SyntaxErrorException ex)
        {
            throw new LuaException(ex.DecoratedMessage, ex);
        }
    }

    // ─── Global get / set ─────────────────────────────────────────────────────

    /// <summary>
    /// Reads a named global from the Lua environment.
    /// Returns <see cref="DynValue.Nil"/> when the global is not set.
    /// </summary>
    public DynValue GetGlobal(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureReady();
        return _script!.Globals.Get(name);
    }

    /// <summary>Sets (or replaces) a named global in the Lua environment.</summary>
    public void SetGlobal(string name, DynValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureReady();
        _script!.Globals.Set(name, value);
    }

    /// <summary>
    /// Registers a C# object as a named global in the Lua environment.
    /// MoonSharp automatically exposes public methods and properties as members.
    /// </summary>
    public void SetGlobal(string name, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureReady();
        _script!.Globals[name] = value;
    }

    /// <summary>Removes a named global from the Lua environment (sets it to nil).</summary>
    public void RemoveGlobal(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureReady();
        _script!.Globals.Set(name, DynValue.Nil);
    }

    // ─── Type registration ────────────────────────────────────────────────────

    /// <summary>
    /// Registers a C# type with MoonSharp so it can be passed to/from Lua.
    /// Idempotent: safe to call multiple times.
    /// </summary>
    public void RegisterType<T>() => LuaBinder.RegisterType<T>();

    /// <summary>
    /// Registers all <see cref="LuaBindingAttribute"/>-decorated types in
    /// <paramref name="assembly"/>.
    /// </summary>
    public void RegisterAssembly(System.Reflection.Assembly assembly)
        => LuaBinder.RegisterAssembly(assembly);

    // ─── Coroutine helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new Lua coroutine from a Lua function value.
    /// Returns the coroutine <see cref="DynValue"/> that can be resumed from Lua.
    /// </summary>
    public DynValue CreateCoroutine(DynValue function)
    {
        EnsureReady();
        if (function.Type != DataType.Function)
            throw new ArgumentException("Expected a Lua function.", nameof(function));
        return _script!.CreateCoroutine(function);
    }

    // ─── Raw Script access ───────────────────────────────────────────────────

    /// <summary>
    /// Provides direct access to the underlying MoonSharp <see cref="Script"/>.
    /// Use this for advanced operations not covered by the public API.
    /// </summary>
    public Script RawScript
    {
        get
        {
            EnsureReady();
            return _script!;
        }
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private void EnsureReady()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_script is null)
            throw new InvalidOperationException(
                "LuaInterface has not been initialised. Call Init() first.");
    }
}

/// <summary>
/// Thrown when a Lua script causes a syntax or runtime error.
/// The <see cref="Exception.Message"/> contains the Lua-decorated error string.
/// </summary>
public sealed class LuaException : Exception
{
    public LuaException(string message) : base(message) { }
    public LuaException(string message, Exception inner) : base(message, inner) { }
}
