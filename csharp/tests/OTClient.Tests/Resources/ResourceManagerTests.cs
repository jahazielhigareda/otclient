using System.IO;
using System.IO.Compression;
using System.Text;
using OTClient.Framework.Resources;
using Xunit;

namespace OTClient.Tests.Resources;

/// <summary>
/// Tests for <see cref="ResourceManager"/> — zip source, folder source,
/// priority, listing, and error handling.  Tasks 9.1, 9.2.
/// </summary>
public sealed class ResourceManagerTests : IDisposable
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private readonly string _tmpDir;
    private readonly ResourceManager _rm = new();

    public ResourceManagerTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "OTClientTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        _rm.Dispose();
        Directory.Delete(_tmpDir, recursive: true);
    }

    // ─── Creates a tiny in-memory zip ─────────────────────────────────────────

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

    // ─── Zip source ───────────────────────────────────────────────────────────

    [Fact]
    public void ZipSource_FileExists_True()
    {
        _rm.AddZipSource(MakeZip(("hello.txt", "world")));
        Assert.True(_rm.FileExists("hello.txt"));
    }

    [Fact]
    public void ZipSource_FileExists_False_ForMissing()
    {
        _rm.AddZipSource(MakeZip(("a.txt", "data")));
        Assert.False(_rm.FileExists("missing.txt"));
    }

    [Fact]
    public void ZipSource_ReadAllText_ReturnsContent()
    {
        _rm.AddZipSource(MakeZip(("data.txt", "Hello Phase 9")));
        Assert.Equal("Hello Phase 9", _rm.ReadAllText("data.txt"));
    }

    [Fact]
    public void ZipSource_ReadAllBytes_ReturnsCorrectBytes()
    {
        var expected = Encoding.UTF8.GetBytes("binary content");
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var e = zip.CreateEntry("bin.dat");
            using var s = e.Open();
            s.Write(expected);
        }
        ms.Position = 0;
        _rm.AddZipSource(ms);
        Assert.Equal(expected, _rm.ReadAllBytes("bin.dat"));
    }

    [Fact]
    public void ZipSource_OpenStream_ReturnsReadableStream()
    {
        _rm.AddZipSource(MakeZip(("readme.txt", "readme content")));
        using var stream = _rm.OpenStream("readme.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("readme content", reader.ReadToEnd());
    }

    [Fact]
    public void ZipSource_ListFiles_ReturnsEntryNames()
    {
        _rm.AddZipSource(MakeZip(("a.txt", ""), ("b.otml", "")));
        var files = _rm.ListFiles();
        Assert.Contains("a.txt", files);
        Assert.Contains("b.otml", files);
    }

    // ─── Folder source ────────────────────────────────────────────────────────

    [Fact]
    public void FolderSource_FileExists_True()
    {
        File.WriteAllText(Path.Combine(_tmpDir, "test.txt"), "content");
        _rm.AddFolderSource(_tmpDir);
        Assert.True(_rm.FileExists("test.txt"));
    }

    [Fact]
    public void FolderSource_ReadAllText_ReturnsFileContent()
    {
        File.WriteAllText(Path.Combine(_tmpDir, "config.otml"), "key: value");
        _rm.AddFolderSource(_tmpDir);
        Assert.Equal("key: value", _rm.ReadAllText("config.otml"));
    }

    [Fact]
    public void FolderSource_ListFiles_IncludesWrittenFile()
    {
        File.WriteAllText(Path.Combine(_tmpDir, "mod.otmod"), "Module");
        _rm.AddFolderSource(_tmpDir);
        Assert.Contains("mod.otmod", _rm.ListFiles());
    }

    // ─── Priority (LIFO) ──────────────────────────────────────────────────────

    [Fact]
    public void LastAddedSource_TakesPriority()
    {
        // Source 1 (lower priority)
        _rm.AddZipSource(MakeZip(("data.txt", "from zip")));
        // Source 2 (higher priority)
        File.WriteAllText(Path.Combine(_tmpDir, "data.txt"), "from folder");
        _rm.AddFolderSource(_tmpDir);

        Assert.Equal("from folder", _rm.ReadAllText("data.txt"));
    }

    // ─── Missing file ─────────────────────────────────────────────────────────

    [Fact]
    public void OpenStream_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => _rm.OpenStream("nonexistent.txt"));
    }

    // ─── Dispose ─────────────────────────────────────────────────────────────

    [Fact]
    public void AfterDispose_FileExists_Throws()
    {
        _rm.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _rm.FileExists("x.txt"));
    }

    // ─── FindFiles ────────────────────────────────────────────────────────────

    [Fact]
    public void FindFiles_ByExtension_FiltersCorrectly()
    {
        _rm.AddZipSource(MakeZip(
            ("a.otmod", ""),
            ("b.otml", ""),
            ("c.otmod", "")));
        var mods = _rm.FindFiles(".otmod");
        Assert.Equal(2, mods.Count);
        Assert.All(mods, f => Assert.EndsWith(".otmod", f));
    }
}
