namespace OTClient.Framework.Resources;

// ─── OtmodInfo ────────────────────────────────────────────────────────────────

/// <summary>
/// Parsed metadata from a <c>.otmod</c> module descriptor file.
/// </summary>
public sealed class OtmodInfo
{
    public string  Name        { get; init; } = string.Empty;
    public string  Description { get; init; } = string.Empty;
    public string? Author      { get; init; }
    public string? Version     { get; init; }
    public string? Website     { get; init; }
    public bool    AutoLoad    { get; init; }
    public int     SandboxType { get; init; }
    /// <summary>Full virtual path to the .otmod file this was loaded from.</summary>
    public string  SourcePath  { get; init; } = string.Empty;
}

// ─── ModuleDiscovery ──────────────────────────────────────────────────────────

/// <summary>
/// Scans VFS sources for <c>.otmod</c> files and parses their OTML metadata.
/// Maps to <c>src/framework/core/modulemanager.*</c> — module discovery portion.
/// Task 9.9.
/// </summary>
public static class ModuleDiscovery
{
    // ─── Scan ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all <c>.otmod</c> files in the given virtual directory and
    /// returns parsed <see cref="OtmodInfo"/> records.
    /// Files that fail to parse are skipped (not thrown).
    /// </summary>
    public static IReadOnlyList<OtmodInfo> Scan(
        ResourceManager resources,
        string virtualDir = "")
    {
        ArgumentNullException.ThrowIfNull(resources);

        var results = new List<OtmodInfo>();
        var files   = resources.ListFiles(virtualDir);

        foreach (var fileName in files)
        {
            if (!fileName.EndsWith(".otmod", StringComparison.OrdinalIgnoreCase))
                continue;

            string fullPath = string.IsNullOrEmpty(virtualDir)
                ? fileName
                : virtualDir.TrimEnd('/') + "/" + fileName;

            try
            {
                string text = resources.ReadAllText(fullPath);
                var    info = ParseOtmod(text, fullPath);
                if (info is not null)
                    results.Add(info);
            }
            catch { /* skip malformed .otmod files */ }
        }

        return results;
    }

    // ─── Parsing ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a single .otmod OTML file into an <see cref="OtmodInfo"/>.
    /// Returns <c>null</c> when the file has no "Module" top-level node.
    /// </summary>
    public static OtmodInfo? ParseOtmod(string text, string sourcePath = "")
    {
        var doc    = OTMLDocument.Parse(text, sourcePath);
        var module = doc.Get("Module");
        if (module is null) return null;

        return new OtmodInfo
        {
            Name        = module.ReadAt<string>("name"),
            Description = module.ReadAtOrDefault<string>("description") ?? string.Empty,
            Author      = module.Get("author")?.Value,
            Version     = module.Get("version")?.Value,
            Website     = module.Get("website")?.Value,
            AutoLoad    = module.ReadAtOrDefault<bool>("autoLoad"),
            SandboxType = module.ReadAtOrDefault<int>("sandboxType"),
            SourcePath  = sourcePath,
        };
    }
}
