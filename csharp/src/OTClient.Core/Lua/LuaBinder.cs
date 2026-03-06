using MoonSharp.Interpreter;
using System.Reflection;

namespace OTClient.Framework.Lua;

/// <summary>
/// Reflection-based automatic type registrar for the Lua runtime.
/// <para>
/// Scans one or more assemblies for classes decorated with
/// <see cref="LuaBindingAttribute"/> and registers them with MoonSharp's
/// <see cref="UserData"/> system so they can be passed to/from Lua scripts.
/// </para>
/// Maps to the C++ template-based <c>LuaBinder</c> in
/// <c>src/framework/luaengine/luabinder.h</c>.
/// Task 6.3.
/// </summary>
public static class LuaBinder
{
    // ─── Assembly scanning ────────────────────────────────────────────────────

    /// <summary>
    /// Scans all types in <paramref name="assembly"/> for
    /// <see cref="LuaBindingAttribute"/> and registers each with MoonSharp.
    /// </summary>
    /// <param name="assembly">Assembly to scan. Defaults to the calling assembly.</param>
    public static void RegisterAssembly(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        foreach (var type in assembly.GetTypes())
            TryRegisterType(type);
    }

    /// <summary>
    /// Scans the assembly that contains <typeparamref name="T"/> for
    /// <see cref="LuaBindingAttribute"/>-decorated types and registers them.
    /// </summary>
    public static void RegisterAssembly<T>() => RegisterAssembly(typeof(T).Assembly);

    // ─── Explicit single-type registration ───────────────────────────────────

    /// <summary>
    /// Registers a single type with the Lua runtime, regardless of whether
    /// it carries <see cref="LuaBindingAttribute"/>.
    /// Idempotent: calling twice for the same type is safe.
    /// </summary>
    public static void RegisterType<T>()
    {
        if (!UserData.IsTypeRegistered(typeof(T)))
            UserData.RegisterType<T>();
    }

    /// <summary>
    /// Registers a single type instance with the Lua runtime.
    /// Idempotent: safe to call multiple times.
    /// </summary>
    public static void RegisterType(Type type)
    {
        if (!UserData.IsTypeRegistered(type))
            UserData.RegisterType(type);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void TryRegisterType(Type type)
    {
        if (type.GetCustomAttribute<LuaBindingAttribute>() is null) return;
        if (!UserData.IsTypeRegistered(type))
            UserData.RegisterType(type);
    }
}
