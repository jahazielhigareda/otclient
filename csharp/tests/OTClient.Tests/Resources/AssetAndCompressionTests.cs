using System.IO;
using System.IO.Compression;
using System.Text;
using OTClient.Framework.Resources;
using Xunit;

namespace OTClient.Tests.Resources;

/// <summary>
/// Tests for <see cref="AssetCipher"/> (task 9.7),
/// <see cref="CompressedStream"/> (task 9.3),
/// <see cref="ModuleDiscovery"/> (task 9.9).
/// </summary>
public sealed class AssetAndCompressionTests
{
    // ─── AssetCipher ─────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginal()
    {
        byte[] data     = Encoding.UTF8.GetBytes("hello encrypted world");
        byte[] cipher   = AssetCipher.Encrypt(data, "secretPassword");
        byte[] plain    = AssetCipher.Decrypt(cipher, "secretPassword");
        Assert.Equal(data, plain);
    }

    [Fact]
    public void Encrypt_ProducesLongerOutput()
    {
        byte[] data   = [1, 2, 3];
        byte[] cipher = AssetCipher.Encrypt(data, "key");
        Assert.Equal(data.Length + 4, cipher.Length);   // 4-byte magic header
    }

    [Fact]
    public void IsEncrypted_True_ForEncryptedData()
    {
        byte[] enc = AssetCipher.Encrypt([1, 2, 3], "pw");
        Assert.True(AssetCipher.IsEncrypted(enc));
    }

    [Fact]
    public void IsEncrypted_False_ForRawData()
    {
        Assert.False(AssetCipher.IsEncrypted([0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Decrypt_WrongPassword_ThrowsInvalidData()
    {
        byte[] enc = AssetCipher.Encrypt([1, 2, 3], "correctPw");
        // Scramble the magic
        enc[0] ^= 0xFF;
        Assert.Throws<InvalidDataException>(() => AssetCipher.Decrypt(enc, "wrongPw"));
    }

    [Fact]
    public void Encrypt_DifferentPasswords_ProduceDifferentCiphertext()
    {
        byte[] data   = [10, 20, 30, 40];
        byte[] a      = AssetCipher.Encrypt(data, "pw1");
        byte[] b      = AssetCipher.Encrypt(data, "pw2");
        // Bodies should differ (skip 4-byte header)
        Assert.False(a.Skip(4).SequenceEqual(b.Skip(4)));
    }

    [Fact]
    public void Encrypt_EmptyData_Works()
    {
        byte[] cipher = AssetCipher.Encrypt([], "pw");
        byte[] plain  = AssetCipher.Decrypt(cipher, "pw");
        Assert.Empty(plain);
    }

    // ─── CompressedStream ────────────────────────────────────────────────────

    [Fact]
    public void CompressedStream_Deflate_RoundTrip()
    {
        byte[] original   = Encoding.UTF8.GetBytes("Hello Deflate decompression!");
        byte[] compressed = CompressedStream.CompressDeflate(original);
        byte[] restored   = CompressedStream.DecompressAll(compressed, CompressionType.Deflate);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void CompressedStream_GZip_RoundTrip()
    {
        byte[] original = Encoding.UTF8.GetBytes("GZip test data 0123456789");
        using var ms  = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(original);
        ms.Position = 0;
        byte[] restored = CompressedStream.DecompressAll(ms, CompressionType.GZip);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void CompressedStream_CanRead_IsTrue()
    {
        byte[] compressed = CompressedStream.CompressDeflate([1, 2, 3]);
        using var ms  = new MemoryStream(compressed);
        using var cs  = new CompressedStream(ms, CompressionType.Deflate);
        Assert.True(cs.CanRead);
    }

    [Fact]
    public void CompressedStream_Lzma_Throws()
    {
        using var dummy = new MemoryStream([0, 1, 2]);
        Assert.Throws<NotSupportedException>(() =>
            new CompressedStream(dummy, CompressionType.Lzma));
    }

    // ─── ModuleDiscovery ─────────────────────────────────────────────────────

    [Fact]
    public void ModuleDiscovery_ParseOtmod_ReturnsInfo()
    {
        const string otml = """
            Module
              name: game_things
              description: Loads game things
              autoLoad: true
              version: 1.0
            """;
        var info = ModuleDiscovery.ParseOtmod(otml, "mods/game_things.otmod");
        Assert.NotNull(info);
        Assert.Equal("game_things", info.Name);
        Assert.Equal("Loads game things", info.Description);
        Assert.True(info.AutoLoad);
        Assert.Equal("1.0", info.Version);
        Assert.Equal("mods/game_things.otmod", info.SourcePath);
    }

    [Fact]
    public void ModuleDiscovery_ParseOtmod_NoModuleNode_ReturnsNull()
    {
        var info = ModuleDiscovery.ParseOtmod("key: value");
        Assert.Null(info);
    }

    [Fact]
    public void ModuleDiscovery_Scan_FindsDotOtmodFiles()
    {
        using var rm = new ResourceManager();
        rm.AddZipSource(MakeZip(
            ("game_things.otmod", ModuleOtml("game_things")),
            ("game_map.otmod",    ModuleOtml("game_map")),
            ("readme.txt",        "ignore me")));

        var mods = ModuleDiscovery.Scan(rm);
        Assert.Equal(2, mods.Count);
        Assert.Contains(mods, m => m.Name == "game_things");
        Assert.Contains(mods, m => m.Name == "game_map");
    }

    [Fact]
    public void ModuleDiscovery_Scan_IgnoresMalformedFiles()
    {
        using var rm = new ResourceManager();
        rm.AddZipSource(MakeZip(
            ("good.otmod",    ModuleOtml("good")),
            ("bad.otmod",     "this is not valid OTML with a Module node")));

        var mods = ModuleDiscovery.Scan(rm);
        Assert.Single(mods);
        Assert.Equal("good", mods[0].Name);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string ModuleOtml(string name) => $"""
        Module
          name: {name}
          description: Module {name}
        """;

    private static Stream MakeZip(params (string name, string content)[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var w = new StreamWriter(entry.Open());
                w.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }
}
