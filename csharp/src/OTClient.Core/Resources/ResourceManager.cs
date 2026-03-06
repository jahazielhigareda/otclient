using System.IO;
using System.IO.Compression;
using System.Text;

namespace OTClient.Framework.Resources;

// ─── IVfsSource ───────────────────────────────────────────────────────────────

/// <summary>
/// Abstraction over a single Virtual File System (VFS) source.
/// Sources are searched in registration order — first match wins.
/// </summary>
internal interface IVfsSource : IDisposable
{
    bool FileExists(string virtualPath);
    Stream OpenStream(string virtualPath);
    IReadOnlyList<string> ListFiles(string virtualPath);
}

// ─── FolderSource ─────────────────────────────────────────────────────────────

/// <summary>
/// VFS source backed by the real filesystem — used during development.
/// Task 9.2.
/// </summary>
internal sealed class FolderSource : IVfsSource
{
    private readonly string _root;

    public FolderSource(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _root = Path.GetFullPath(rootPath);
    }

    private string Resolve(string virtualPath)
        => Path.Combine(_root, virtualPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));

    public bool FileExists(string virtualPath)
    {
        var full = Resolve(virtualPath);
        return File.Exists(full);
    }

    public Stream OpenStream(string virtualPath)
    {
        var full = Resolve(virtualPath);
        return File.OpenRead(full);
    }

    public IReadOnlyList<string> ListFiles(string virtualPath)
    {
        var dir = Resolve(virtualPath);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir)
                        .Select(f => Path.GetFileName(f))
                        .ToArray();
    }

    public void Dispose() { /* nothing to dispose */ }
}

// ─── ZipSource ────────────────────────────────────────────────────────────────

/// <summary>
/// VFS source backed by a .zip / .otpkg file via
/// <see cref="ZipArchive"/>.
/// Task 9.1.
/// </summary>
internal sealed class ZipSource : IVfsSource
{
    private readonly ZipArchive _archive;

    public ZipSource(string zipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        var stream = File.OpenRead(zipPath);
        _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
    }

    public ZipSource(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
    }

    private static string Normalize(string virtualPath)
        => virtualPath.TrimStart('/', '\\').Replace('\\', '/');

    public bool FileExists(string virtualPath)
        => _archive.GetEntry(Normalize(virtualPath)) is not null;

    public Stream OpenStream(string virtualPath)
    {
        var entry = _archive.GetEntry(Normalize(virtualPath))
            ?? throw new FileNotFoundException($"Entry not found in zip: {virtualPath}");
        return entry.Open();
    }

    public IReadOnlyList<string> ListFiles(string virtualPath)
    {
        string prefix = Normalize(virtualPath);
        if (prefix.Length > 0 && !prefix.EndsWith('/')) prefix += '/';

        return _archive.Entries
            .Where(e => !e.FullName.EndsWith('/'))               // skip dir entries
            .Where(e => prefix.Length == 0
                        || e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(e => prefix.Length == 0
                         ? e.FullName
                         : e.FullName[prefix.Length..])
            .Where(n => !n.Contains('/'))                        // top-level only
            .ToArray();
    }

    public void Dispose() => _archive.Dispose();
}

// ─── ResourceManager ─────────────────────────────────────────────────────────

/// <summary>
/// Virtual File System that aggregates multiple sources (zip files, plain
/// directories).  Sources are searched in LIFO order — the last added source
/// has highest priority.
/// <para>Replaces PhysFS from the original C++ client.</para>
/// Maps to <c>src/framework/core/resourcemanager.*</c>.
/// Tasks 9.1, 9.2.
/// </summary>
public sealed class ResourceManager : IDisposable
{
    private readonly List<IVfsSource> _sources = [];
    private bool _disposed;

    // ─── Source registration ──────────────────────────────────────────────────

    /// <summary>
    /// Adds a plain-filesystem directory as a VFS source.
    /// Used during development to read assets directly from disk.
    /// </summary>
    public void AddFolderSource(string folderPath)
    {
        ThrowIfDisposed();
        _sources.Add(new FolderSource(folderPath));
    }

    /// <summary>
    /// Adds a .zip / .otpkg file as a VFS source.
    /// </summary>
    public void AddZipSource(string zipPath)
    {
        ThrowIfDisposed();
        _sources.Add(new ZipSource(zipPath));
    }

    /// <summary>
    /// Adds an in-memory zip as a VFS source (useful in tests).
    /// </summary>
    public void AddZipSource(Stream zipStream, bool leaveOpen = false)
    {
        ThrowIfDisposed();
        _sources.Add(new ZipSource(zipStream, leaveOpen));
    }

    // ─── File operations ──────────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> when <paramref name="virtualPath"/> exists in any source.</summary>
    public bool FileExists(string virtualPath)
    {
        ThrowIfDisposed();
        return SourceFor(virtualPath) is not null;
    }

    /// <summary>
    /// Opens a <see cref="Stream"/> for reading.
    /// Sources are searched from last-added (highest priority) to first.
    /// </summary>
    /// <exception cref="FileNotFoundException">When no source contains the path.</exception>
    public Stream OpenStream(string virtualPath)
    {
        ThrowIfDisposed();
        var src = SourceFor(virtualPath)
            ?? throw new FileNotFoundException($"Virtual file not found: {virtualPath}");
        return src.OpenStream(virtualPath);
    }

    /// <summary>Reads all bytes from a virtual file.</summary>
    public byte[] ReadAllBytes(string virtualPath)
    {
        using var stream = OpenStream(virtualPath);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Reads all text (UTF-8) from a virtual file.</summary>
    public string ReadAllText(string virtualPath)
        => Encoding.UTF8.GetString(ReadAllBytes(virtualPath));

    // ─── Directory listing ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the top-level file names in <paramref name="virtualPath"/> from all
    /// sources (deduplicated).
    /// </summary>
    public IReadOnlyList<string> ListFiles(string virtualPath = "")
    {
        ThrowIfDisposed();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        // Iterate reversed so highest-priority source wins
        for (int i = _sources.Count - 1; i >= 0; i--)
        {
            foreach (var f in _sources[i].ListFiles(virtualPath))
            {
                if (seen.Add(f))
                    result.Add(f);
            }
        }
        return result;
    }

    /// <summary>Finds all files with the given extension across all sources.</summary>
    public IReadOnlyList<string> FindFiles(string extension)
    {
        return ListFiles().Where(f =>
            f.EndsWith(extension, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    // ─── Internal helpers ─────────────────────────────────────────────────────

    private IVfsSource? SourceFor(string virtualPath)
    {
        // Search LIFO — last-registered has highest priority
        for (int i = _sources.Count - 1; i >= 0; i--)
        {
            if (_sources[i].FileExists(virtualPath))
                return _sources[i];
        }
        return null;
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    // ─── IDisposable ─────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var src in _sources)
            src.Dispose();
        _sources.Clear();
    }
}
