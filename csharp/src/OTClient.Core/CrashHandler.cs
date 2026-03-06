using System.IO;

namespace OTClient.Framework.Core;

/// <summary>
/// Installs a process-wide unhandled-exception handler that writes a crash
/// report to a log file before the process exits.
/// <para>
/// Call <see cref="Install"/> once at application startup, before
/// <see cref="Application.Init"/>.
/// </para>
/// Maps to the crash-handler described in task 10.2.
/// </summary>
public static class CrashHandler
{
    private static string _logPath = "crash.log";
    private static Logger? _logger;
    private static bool    _installed;

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Installs the handler.  Safe to call multiple times (idempotent).
    /// </summary>
    /// <param name="logger">Optional logger — crash details are also written here.</param>
    /// <param name="logPath">Path for the crash log file (default: <c>crash.log</c>).</param>
    public static void Install(Logger? logger = null, string logPath = "crash.log")
    {
        if (_installed) return;
        _installed = true;
        _logger    = logger;
        _logPath   = logPath;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException       += OnUnobservedTaskException;
    }

    /// <summary>Uninstalls the handlers (for test teardown).</summary>
    public static void Uninstall()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException       -= OnUnobservedTaskException;
        _installed = false;
    }

    // ─── Handlers ─────────────────────────────────────────────────────────────

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        WriteCrashReport(ex, isTerminating: e.IsTerminating);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashReport(e.Exception, isTerminating: false);
        e.SetObserved();  // prevent process termination for unobserved task exceptions
    }

    // ─── Report writing ───────────────────────────────────────────────────────

    /// <summary>
    /// Writes a crash report for <paramref name="ex"/> to the log file and
    /// (optionally) to the engine logger.
    /// </summary>
    public static void WriteCrashReport(Exception? ex, bool isTerminating = false)
    {
        string message = BuildReport(ex, isTerminating);

        _logger?.Fatal(message);

        try
        {
            File.WriteAllText(_logPath, message);
        }
        catch
        {
            // If we can't write the crash log there's nothing more we can do.
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string BuildReport(Exception? ex, bool isTerminating)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== OTClient Crash Report ===");
        sb.AppendLine($"Timestamp  : {DateTimeOffset.UtcNow:O}");
        sb.AppendLine($"OS         : {Platform.OsName} ({Platform.OsDescription})");
        sb.AppendLine($"Arch       : {Platform.Arch}");
        sb.AppendLine($"Runtime    : {Platform.RuntimeVersion}");
        sb.AppendLine($"Terminating: {isTerminating}");
        sb.AppendLine();

        if (ex is null)
        {
            sb.AppendLine("(No exception details available)");
        }
        else
        {
            sb.AppendLine($"Exception  : {ex.GetType().FullName}");
            sb.AppendLine($"Message    : {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(ex.StackTrace);

            Exception? inner = ex.InnerException;
            while (inner is not null)
            {
                sb.AppendLine();
                sb.AppendLine("--- Inner Exception ---");
                sb.AppendLine($"Exception  : {inner.GetType().FullName}");
                sb.AppendLine($"Message    : {inner.Message}");
                sb.AppendLine(inner.StackTrace);
                inner = inner.InnerException;
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== End of Report ===");
        return sb.ToString();
    }
}
