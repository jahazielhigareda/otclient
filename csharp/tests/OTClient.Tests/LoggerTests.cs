using System.IO;
using OTClient.Framework.Core;
using Xunit;

namespace OTClient.Tests;

public sealed class LoggerTests : IDisposable
{
    private readonly Logger _logger = new();

    public void Dispose() => _logger.Dispose();

    // ─── Log level filtering ──────────────────────────────────────────────────

    [Fact]
    public void Log_BelowConfiguredLevel_IsNotRecorded()
    {
        _logger.Level = LogLevel.Warning;
        _logger.Debug("debug message");
        _logger.Info("info message");

        Assert.Empty(_logger.GetHistory());
    }

    [Fact]
    public void Log_AtOrAboveConfiguredLevel_IsRecorded()
    {
        _logger.Level = LogLevel.Warning;
        _logger.Warning("warn message");
        _logger.Error("error message");
        _logger.Fatal("fatal message");

        Assert.Equal(3, _logger.GetHistory().Count);
    }

    [Fact]
    public void Log_DefaultLevel_IsDebug()
    {
        _logger.Debug("debug");
        _logger.Info("info");
        _logger.Warning("warn");
        _logger.Error("error");
        _logger.Fatal("fatal");

        Assert.Equal(5, _logger.GetHistory().Count);
    }

    // ─── Shortcut methods ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Info)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Fatal)]
    public void Shortcut_Method_RecordsCorrectLevel(LogLevel level)
    {
        Action<string> log = level switch
        {
            LogLevel.Debug   => _logger.Debug,
            LogLevel.Info    => _logger.Info,
            LogLevel.Warning => _logger.Warning,
            LogLevel.Error   => _logger.Error,
            LogLevel.Fatal   => _logger.Fatal,
            _                => throw new ArgumentOutOfRangeException()
        };

        log("test message");
        var history = _logger.GetHistory();

        Assert.Single(history);
        Assert.Equal(level, history[0].Level);
    }

    // ─── History ring-buffer ──────────────────────────────────────────────────

    [Fact]
    public void GetHistory_ReturnsMessageText()
    {
        _logger.Info("hello world");
        var history = _logger.GetHistory();
        Assert.Single(history);
        Assert.Equal("hello world", history[0].Message);
    }

    [Fact]
    public void GetHistory_CapsBeyondMaxHistory()
    {
        // Logger caps at 1 000 messages internally; write 1 002 and verify cap
        for (int i = 0; i < 1002; i++)
            _logger.Info($"msg {i}");

        var history = _logger.GetHistory();
        Assert.Equal(1000, history.Count);
        // Most recent message should be present
        Assert.Equal("msg 1001", history[^1].Message);
    }

    // ─── OnLog callback ───────────────────────────────────────────────────────

    [Fact]
    public void SetOnLog_CallbackInvokedForEachMessage()
    {
        var calls = new List<(LogLevel, string)>();
        _logger.SetOnLog((lvl, msg, _) => calls.Add((lvl, msg)));

        _logger.Info("first");
        _logger.Warning("second");

        Assert.Equal(2, calls.Count);
        Assert.Equal((LogLevel.Info,    "first"),  calls[0]);
        Assert.Equal((LogLevel.Warning, "second"), calls[1]);
    }

    [Fact]
    public void SetOnLog_NullCallback_DisablesCallback()
    {
        int count = 0;
        _logger.SetOnLog((_, _, _) => count++);
        _logger.Info("one");

        _logger.SetOnLog(null);
        _logger.Info("two");

        Assert.Equal(1, count);
    }

    // ─── File sink ────────────────────────────────────────────────────────────

    [Fact]
    public void SetLogFile_WritesMessagesToFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            _logger.SetLogFile(path);
            _logger.Info("written to file");
            _logger.SetLogFile(null); // flush/close

            var content = File.ReadAllText(path);
            Assert.Contains("written to file", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ─── Dispose ─────────────────────────────────────────────────────────────

    [Fact]
    public void AfterDispose_LogCallsAreIgnored()
    {
        _logger.Dispose();
        // Should not throw
        _logger.Info("post-dispose");
        Assert.Empty(_logger.GetHistory());
    }
}
