using System.IO;
using System.Text;

namespace OTClient.Framework.Core;

/// <summary>
/// Log severity levels, ordered from least to most severe.
/// Maps to the C++ Fw::LogLevel enum in src/framework/global.h.
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Fatal = 4,
}

/// <summary>
/// A single log entry kept in the in-memory history ring-buffer.
/// </summary>
public sealed record LogMessage(LogLevel Level, string Message, DateTimeOffset When);

/// <summary>
/// Thread-safe logger with configurable severity, in-memory history, optional file sink
/// and an optional delegate callback (used by the UI / Lua layer to display messages).
/// Maps to <c>src/framework/core/logger.{h,cpp}</c>.
/// </summary>
public sealed class Logger : IDisposable
{
    private const int MaxLogHistory = 1000;

    private readonly object _lock = new();
    private readonly Queue<LogMessage> _history = new(MaxLogHistory + 1);
    private StreamWriter? _fileWriter;
    private Action<LogLevel, string, long>? _onLog;
    private LogLevel _level = LogLevel.Debug;
    private bool _disposed;

    // ─── Configuration ────────────────────────────────────────────────────────

    /// <summary>Sets the minimum level that will be recorded/emitted.</summary>
    public LogLevel Level
    {
        get => _level;
        set { lock (_lock) { _level = value; } }
    }

    /// <summary>
    /// Opens (or replaces) the log file. Pass <c>null</c> to close the current file.
    /// </summary>
    public void SetLogFile(string? path)
    {
        lock (_lock)
        {
            _fileWriter?.Dispose();
            _fileWriter = null;
            if (path is not null)
                _fileWriter = new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
        }
    }

    /// <summary>Registers a callback invoked for every accepted log message.</summary>
    public void SetOnLog(Action<LogLevel, string, long>? callback)
    {
        lock (_lock) { _onLog = callback; }
    }

    // ─── Logging API ──────────────────────────────────────────────────────────

    public void Log(LogLevel level, string message)
    {
        lock (_lock)
        {
            if (level < _level || _disposed)
                return;

            var now = DateTimeOffset.UtcNow;
            var entry = new LogMessage(level, message, now);

            // Maintain capped history
            if (_history.Count >= MaxLogHistory)
                _history.Dequeue();
            _history.Enqueue(entry);

            // Console sink
            var prefix = LevelPrefix(level);
            var line = $"[{now:HH:mm:ss.fff}] {prefix} {message}";
            WriteToConsole(level, line);

            // File sink
            _fileWriter?.WriteLine(line);

            // Callback
            _onLog?.Invoke(level, message, now.ToUnixTimeMilliseconds());
        }
    }

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    public void Fatal(string message) => Log(LogLevel.Fatal, message);

    // ─── History ──────────────────────────────────────────────────────────────

    /// <summary>Returns a snapshot of buffered log messages.</summary>
    public IReadOnlyList<LogMessage> GetHistory()
    {
        lock (_lock)
            return [.. _history];
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _fileWriter?.Dispose();
            _fileWriter = null;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string LevelPrefix(LogLevel level) => level switch
    {
        LogLevel.Debug   => "[DEBUG]",
        LogLevel.Info    => "[INFO ]",
        LogLevel.Warning => "[WARN ]",
        LogLevel.Error   => "[ERROR]",
        LogLevel.Fatal   => "[FATAL]",
        _                => "[?????]",
    };

    private static void WriteToConsole(LogLevel level, string line)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = level switch
        {
            LogLevel.Debug   => ConsoleColor.Gray,
            LogLevel.Info    => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error   => ConsoleColor.Red,
            LogLevel.Fatal   => ConsoleColor.Magenta,
            _                => prev,
        };
        if (level >= LogLevel.Error)
            Console.Error.WriteLine(line);
        else
            Console.WriteLine(line);
        Console.ForegroundColor = prev;
    }
}
