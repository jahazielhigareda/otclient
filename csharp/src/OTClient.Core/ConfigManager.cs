using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OTClient.Framework.Core;

/// <summary>
/// Key/value configuration store that persists settings to a JSON file.
/// Maps to <c>src/framework/core/configmanager.{h,cpp}</c> and replaces
/// the C++ <c>inih</c>-based INI parser with <see cref="System.Text.Json"/>.
/// </summary>
public sealed class ConfigManager
{
    private readonly object _lock = new();
    private JsonObject _data = [];
    private string? _filePath;

    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
    };

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads configuration from <paramref name="filePath"/>.
    /// Creates the file with default values if it does not exist.
    /// </summary>
    public void Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        lock (_lock)
        {
            _filePath = filePath;
            if (!File.Exists(filePath))
            {
                _data = [];
                return;
            }

            var text = File.ReadAllText(filePath);
            _data = JsonNode.Parse(text)?.AsObject() ?? [];
        }
    }

    /// <summary>Saves the current configuration to the file specified in <see cref="Load"/>.</summary>
    public void Save()
    {
        lock (_lock)
        {
            if (_filePath is null) return;
            var json = _data.ToJsonString(_writeOptions);
            File.WriteAllText(_filePath, json);
        }
    }

    /// <summary>Releases the file path without saving.</summary>
    public void Terminate()
    {
        lock (_lock)
        {
            _filePath = null;
            _data = [];
        }
    }

    // ─── Read ─────────────────────────────────────────────────────────────────

    public string GetString(string key, string defaultValue = "")
    {
        lock (_lock)
            return _data[key]?.GetValue<string>() ?? defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        lock (_lock)
            return _data[key]?.GetValue<int>() ?? defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        lock (_lock)
            return _data[key]?.GetValue<bool>() ?? defaultValue;
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        lock (_lock)
            return _data[key]?.GetValue<float>() ?? defaultValue;
    }

    public bool Exists(string key)
    {
        lock (_lock)
            return _data.ContainsKey(key);
    }

    // ─── Write ────────────────────────────────────────────────────────────────

    public void Set(string key, string value)
    {
        lock (_lock) _data[key] = value;
    }

    public void Set(string key, int value)
    {
        lock (_lock) _data[key] = value;
    }

    public void Set(string key, bool value)
    {
        lock (_lock) _data[key] = value;
    }

    public void Set(string key, float value)
    {
        lock (_lock) _data[key] = value;
    }

    public void Remove(string key)
    {
        lock (_lock) _data.Remove(key);
    }
}
