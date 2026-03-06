using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OTClient.Framework.Core;

/// <summary>
/// Platform utilities — OS detection, command-line arguments, and process
/// restart.  Maps to <c>src/framework/platform/platform.*</c>.
/// Task 10.1.
/// </summary>
public static class Platform
{
    // ─── OS identification ────────────────────────────────────────────────────

    /// <summary>Human-readable OS name: <c>"Windows"</c>, <c>"Linux"</c>, or <c>"macOS"</c>.</summary>
    public static string OsName
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
         : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? "macOS"
         : "Linux";

    /// <summary>CPU architecture string, e.g. <c>"X64"</c>, <c>"Arm64"</c>.</summary>
    public static string Arch => RuntimeInformation.ProcessArchitecture.ToString();

    /// <summary>Full OS description including version, e.g. <c>"Windows 11 (10.0.22621)"</c>.</summary>
    public static string OsDescription => RuntimeInformation.OSDescription;

    /// <summary>Dotnet runtime version string.</summary>
    public static string RuntimeVersion => RuntimeInformation.FrameworkDescription;

    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux   => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsMacOS   => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    // ─── Process / executable ─────────────────────────────────────────────────

    /// <summary>Path to the currently executing assembly.</summary>
    public static string ExecutablePath
        => Process.GetCurrentProcess().MainModule?.FileName
           ?? AppContext.BaseDirectory;

    /// <summary>Command-line arguments passed to the process (skips argv[0]).</summary>
    public static IReadOnlyList<string> CommandLineArgs
        => Environment.GetCommandLineArgs().Skip(1).ToArray();

    /// <summary>
    /// Returns <c>true</c> if <paramref name="arg"/> (with or without leading
    /// <c>--</c>/<c>-</c>) is present in the command-line arguments.
    /// </summary>
    public static bool HasArg(string arg)
    {
        foreach (var a in CommandLineArgs)
        {
            if (string.Equals(a, arg, StringComparison.OrdinalIgnoreCase)
             || string.Equals(a, "--" + arg, StringComparison.OrdinalIgnoreCase)
             || string.Equals(a, "-"  + arg, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the value following <paramref name="key"/> in the form
    /// <c>--key value</c> or <c>--key=value</c>, or <c>null</c> if not found.
    /// </summary>
    public static string? GetArgValue(string key)
    {
        var args = CommandLineArgs;
        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            if (a.StartsWith("--" + key + "=", StringComparison.OrdinalIgnoreCase))
                return a[("--" + key + "=").Length..];
            if (string.Equals(a, "--" + key, StringComparison.OrdinalIgnoreCase)
             && i + 1 < args.Count)
                return args[i + 1];
        }
        return null;
    }

    // ─── Process restart ──────────────────────────────────────────────────────

    /// <summary>
    /// Restarts the current process with the same arguments.
    /// Returns immediately after launching the replacement process — the
    /// caller is responsible for shutting down the current process.
    /// </summary>
    public static void Restart()
    {
        var exe  = ExecutablePath;
        var args = Environment.GetCommandLineArgs().Skip(1);
        Process.Start(new ProcessStartInfo(exe, string.Join(" ", args))
        {
            UseShellExecute = false,
        });
    }

    /// <summary>
    /// Opens a URL in the system default browser.
    /// </summary>
    public static void OpenUrl(string url)
    {
        if (IsWindows)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        else if (IsMacOS)
            Process.Start("open", url);
        else
            Process.Start("xdg-open", url);
    }
}
