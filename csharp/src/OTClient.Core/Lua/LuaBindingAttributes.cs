namespace OTClient.Framework.Lua;

/// <summary>
/// Marks a C# class for automatic registration with the Lua runtime by
/// <see cref="LuaBinder"/>.  The optional <paramref name="luaName"/> controls
/// the type name visible from Lua code.  When omitted the C# class name is used.
/// <para>
/// Usage:
/// <code>
/// [LuaBinding("Vector2")]
/// public sealed class Vector2Proxy { … }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class LuaBindingAttribute : Attribute
{
    /// <summary>Name used to identify the type in Lua (default: C# class name).</summary>
    public string? LuaName { get; }

    public LuaBindingAttribute(string? luaName = null) => LuaName = luaName;
}

/// <summary>
/// Marks a public method on a <see cref="LuaBindingAttribute"/>-decorated class
/// as callable from Lua.  The optional <paramref name="luaName"/> overrides the
/// method name exposed to scripts.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuaMethodAttribute : Attribute
{
    public string? LuaName { get; }
    public LuaMethodAttribute(string? luaName = null) => LuaName = luaName;
}

/// <summary>
/// Marks a public property on a <see cref="LuaBindingAttribute"/>-decorated class
/// as accessible from Lua.  The optional <paramref name="luaName"/> overrides the
/// property name exposed to scripts.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LuaPropertyAttribute : Attribute
{
    public string? LuaName { get; }
    public LuaPropertyAttribute(string? luaName = null) => LuaName = luaName;
}
