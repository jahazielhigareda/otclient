using System.IO;
using OTClient.Framework.Core;
using Xunit;

namespace OTClient.Tests;

public sealed class ConfigManagerTests : IDisposable
{
    private readonly ConfigManager _config = new();
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    // ─── Load / Save round-trip ───────────────────────────────────────────────

    [Fact]
    public void Load_MissingFile_StartsEmpty()
    {
        // Use a path that does not exist
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        _config.Load(missing);

        Assert.False(_config.Exists("anything"));
    }

    [Fact]
    public void Save_Then_Load_RestoresValues()
    {
        _config.Load(_tempFile);
        _config.Set("name",  "OTClient");
        _config.Set("port",  7171);
        _config.Set("debug", true);
        _config.Set("zoom",  2.5f);
        _config.Save();

        var config2 = new ConfigManager();
        config2.Load(_tempFile);

        Assert.Equal("OTClient", config2.GetString("name"));
        Assert.Equal(7171,       config2.GetInt("port"));
        Assert.True(             config2.GetBool("debug"));
        Assert.Equal(2.5f,       config2.GetFloat("zoom"));
    }

    // ─── Default values ───────────────────────────────────────────────────────

    [Fact]
    public void GetString_MissingKey_ReturnsDefault()
    {
        _config.Load(_tempFile);
        Assert.Equal("fallback", _config.GetString("missing", "fallback"));
    }

    [Fact]
    public void GetInt_MissingKey_ReturnsDefault()
    {
        _config.Load(_tempFile);
        Assert.Equal(42, _config.GetInt("missing", 42));
    }

    [Fact]
    public void GetBool_MissingKey_ReturnsDefault()
    {
        _config.Load(_tempFile);
        Assert.True(_config.GetBool("missing", true));
    }

    [Fact]
    public void GetFloat_MissingKey_ReturnsDefault()
    {
        _config.Load(_tempFile);
        Assert.Equal(3.14f, _config.GetFloat("missing", 3.14f));
    }

    // ─── Exists / Remove ─────────────────────────────────────────────────────

    [Fact]
    public void Exists_ReturnsTrueAfterSet()
    {
        _config.Load(_tempFile);
        _config.Set("key", "value");
        Assert.True(_config.Exists("key"));
    }

    [Fact]
    public void Remove_DeletesKey()
    {
        _config.Load(_tempFile);
        _config.Set("key", "value");
        _config.Remove("key");
        Assert.False(_config.Exists("key"));
    }

    // ─── Overwrite ────────────────────────────────────────────────────────────

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        _config.Load(_tempFile);
        _config.Set("port", 7171);
        _config.Set("port", 7172);
        Assert.Equal(7172, _config.GetInt("port"));
    }

    // ─── Terminate ────────────────────────────────────────────────────────────

    [Fact]
    public void Terminate_ClearsState()
    {
        _config.Load(_tempFile);
        _config.Set("key", "value");
        _config.Terminate();

        Assert.False(_config.Exists("key"));
    }
}
