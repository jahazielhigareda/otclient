using System.Text;

namespace OTClient.Framework.Resources;

// ─── OTMLNode ─────────────────────────────────────────────────────────────────

/// <summary>
/// A single node in an OTML document — a key/tag with an optional string value
/// and an ordered list of child nodes.
/// Maps to <c>src/framework/otml/otmlnode.*</c>.
/// Task 9.6.
/// </summary>
public sealed class OTMLNode
{
    // ─── Properties ───────────────────────────────────────────────────────────

    public string   Tag      { get; set; } = string.Empty;
    public string?  Value    { get; set; }
    public bool     IsNull   { get; set; }
    public string   Source   { get; set; } = string.Empty;   // file:line

    private readonly List<OTMLNode> _children = [];
    public IReadOnlyList<OTMLNode> Children => _children;

    // ─── Derived flags ────────────────────────────────────────────────────────

    public bool HasTag      => !string.IsNullOrEmpty(Tag);
    public bool HasValue    => Value is not null && !IsNull;
    public bool HasChildren => _children.Count > 0;
    public int  Count       => _children.Count;

    // ─── Factories ────────────────────────────────────────────────────────────

    public static OTMLNode Create(string tag = "")                 => new() { Tag = tag };
    public static OTMLNode Create(string tag, string? value)       => new() { Tag = tag, Value = value };
    public static OTMLNode CreateNull(string tag)                  => new() { Tag = tag, IsNull = true };

    // ─── Child management ─────────────────────────────────────────────────────

    public void AddChild(OTMLNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Add(child);
    }

    public void RemoveChild(OTMLNode child) => _children.Remove(child);
    public void ClearChildren()             => _children.Clear();

    // ─── Lookup ───────────────────────────────────────────────────────────────

    /// <summary>Returns the first child with the given tag, or <c>null</c>.</summary>
    public OTMLNode? Get(string tag)
        => _children.FirstOrDefault(c => c.Tag == tag);

    /// <summary>
    /// Returns the first child with the given tag.
    /// Throws <see cref="KeyNotFoundException"/> if not found.
    /// </summary>
    public OTMLNode At(string tag)
        => Get(tag) ?? throw new KeyNotFoundException(
            $"OTML node '{Tag}' has no child '{tag}'. Source: {Source}");

    /// <summary>Returns child by zero-based index, or <c>null</c>.</summary>
    public OTMLNode? GetAt(int index)
        => index >= 0 && index < _children.Count ? _children[index] : null;

    /// <summary>
    /// Returns child by index; throws <see cref="IndexOutOfRangeException"/> when out of range.
    /// </summary>
    public OTMLNode AtIndex(int index)
        => GetAt(index) ?? throw new IndexOutOfRangeException(
            $"OTML node '{Tag}' has {_children.Count} children; index {index} is out of range.");

    public bool HasChild(string tag) => Get(tag) is not null;

    // ─── Typed value reading ──────────────────────────────────────────────────

    /// <summary>Returns <see cref="Value"/> cast to <typeparamref name="T"/>.</summary>
    public T Read<T>() => Convert<T>(Value);

    /// <summary>Returns the value of child <paramref name="tag"/> cast to <typeparamref name="T"/>.</summary>
    public T ReadAt<T>(string tag)
    {
        var node = Get(tag);
        return node is null ? default! : node.Read<T>();
    }

    /// <summary>Returns the value of child <paramref name="tag"/> or <paramref name="defaultValue"/> when absent.</summary>
    public T ReadAtOrDefault<T>(string tag, T defaultValue = default!)
    {
        var node = Get(tag);
        if (node is null || !node.HasValue) return defaultValue;
        try { return node.Read<T>(); }
        catch { return defaultValue; }
    }

    /// <summary>Returns <see cref="Value"/> cast to <typeparamref name="T"/>, or <paramref name="defaultValue"/>.</summary>
    public T ReadOrDefault<T>(T defaultValue = default!)
    {
        if (!HasValue) return defaultValue;
        try { return Read<T>(); }
        catch { return defaultValue; }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static T Convert<T>(string? raw)
    {
        if (raw is null) return default!;

        Type t = typeof(T);
        // Unwrap Nullable<X>
        if (Nullable.GetUnderlyingType(t) is { } inner)
        {
            if (string.IsNullOrEmpty(raw)) return default!;
            t = inner;
        }

        if (t == typeof(string))  return (T)(object)raw;
        if (t == typeof(bool))    return (T)(object)ParseBool(raw);
        if (t == typeof(int))     return (T)(object)int.Parse(raw.Trim());
        if (t == typeof(long))    return (T)(object)long.Parse(raw.Trim());
        if (t == typeof(float))   return (T)(object)float.Parse(raw.Trim(), System.Globalization.CultureInfo.InvariantCulture);
        if (t == typeof(double))  return (T)(object)double.Parse(raw.Trim(), System.Globalization.CultureInfo.InvariantCulture);
        if (t == typeof(uint))    return (T)(object)uint.Parse(raw.Trim());

        throw new InvalidCastException($"Cannot convert OTML value '{raw}' to type {typeof(T).Name}");
    }

    private static bool ParseBool(string s)
    {
        s = s.Trim().ToLowerInvariant();
        return s is "true" or "yes" or "1" or "on";
    }

    // ─── Equality (structural) ────────────────────────────────────────────────

    public override string ToString()
        => HasValue ? $"{Tag}: {Value}" : Tag;
}

// ─── OTMLDocument ─────────────────────────────────────────────────────────────

/// <summary>
/// Root container for an OTML parse result.
/// Maps to <c>src/framework/otml/otmldocument.*</c>.
/// Task 9.6.
/// </summary>
public sealed class OTMLDocument
{
    /// <summary>Virtual root node — children are the top-level nodes of the document.</summary>
    public OTMLNode Root { get; } = OTMLNode.Create("__root__");

    public string SourcePath { get; set; } = string.Empty;

    public IReadOnlyList<OTMLNode> Children => Root.Children;

    /// <summary>Returns the first top-level child with the given tag, or <c>null</c>.</summary>
    public OTMLNode? Get(string tag) => Root.Get(tag);

    /// <summary>Parses OTML from a string.</summary>
    public static OTMLDocument Parse(string text, string sourcePath = "")
        => OTMLParser.Parse(text, sourcePath);

    /// <summary>Parses OTML from a stream (UTF-8).</summary>
    public static OTMLDocument Parse(System.IO.Stream stream, string sourcePath = "")
    {
        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
        return OTMLParser.Parse(reader.ReadToEnd(), sourcePath);
    }
}
