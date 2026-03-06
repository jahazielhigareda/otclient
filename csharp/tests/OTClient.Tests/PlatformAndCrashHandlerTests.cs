using OTClient.Framework.Core;
using OTClient.Framework.Game;
using Xunit;

namespace OTClient.Tests;

/// <summary>
/// Tests for <see cref="Platform"/> (task 10.1), <see cref="CrashHandler"/> (task 10.2),
/// and <see cref="SpriteAppearances"/> (task 8.5).
/// </summary>
public sealed class PlatformAndCrashHandlerTests
{
    // ─── Platform (10.1) ─────────────────────────────────────────────────────

    [Fact]
    public void Platform_OsName_IsKnownOS()
    {
        var os = Platform.OsName;
        Assert.True(os == "Windows" || os == "Linux" || os == "macOS",
            $"Unexpected OS name: {os}");
    }

    [Fact]
    public void Platform_Arch_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(Platform.Arch));
    }

    [Fact]
    public void Platform_OsDescription_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(Platform.OsDescription));
    }

    [Fact]
    public void Platform_RuntimeVersion_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(Platform.RuntimeVersion));
    }

    [Fact]
    public void Platform_BoolFlags_ExactlyOneIsTrue()
    {
        int trueCount = (Platform.IsWindows ? 1 : 0)
                      + (Platform.IsLinux   ? 1 : 0)
                      + (Platform.IsMacOS   ? 1 : 0);
        Assert.Equal(1, trueCount);
    }

    [Fact]
    public void Platform_ExecutablePath_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(Platform.ExecutablePath));
    }

    [Fact]
    public void Platform_CommandLineArgs_IsNotNull()
    {
        Assert.NotNull(Platform.CommandLineArgs);
    }

    [Fact]
    public void Platform_HasArg_MissingReturnsFalse()
    {
        Assert.False(Platform.HasArg("this-arg-does-not-exist-xyzzy"));
    }

    [Fact]
    public void Platform_GetArgValue_MissingReturnsNull()
    {
        Assert.Null(Platform.GetArgValue("this-key-does-not-exist-xyzzy"));
    }

    // ─── CrashHandler (10.2) ─────────────────────────────────────────────────

    [Fact]
    public void CrashHandler_Install_ThenUninstall_DoesNotThrow()
    {
        CrashHandler.Install();
        CrashHandler.Uninstall();
    }

    [Fact]
    public void CrashHandler_Install_Idempotent()
    {
        CrashHandler.Install();
        CrashHandler.Install(); // second call should be no-op
        CrashHandler.Uninstall();
    }

    [Fact]
    public void CrashHandler_WriteCrashReport_NullException_WritesFile()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crash_test_{Guid.NewGuid()}.log");
        try
        {
            CrashHandler.Install(logPath: path);
            CrashHandler.WriteCrashReport(null, isTerminating: false);
            Assert.True(System.IO.File.Exists(path));
            string content = System.IO.File.ReadAllText(path);
            Assert.Contains("OTClient Crash Report", content);
        }
        finally
        {
            CrashHandler.Uninstall();
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void CrashHandler_WriteCrashReport_WithException_IncludesMessage()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crash_test_{Guid.NewGuid()}.log");
        try
        {
            CrashHandler.Install(logPath: path);
            var ex = new InvalidOperationException("test crash message");
            CrashHandler.WriteCrashReport(ex, isTerminating: false);

            string content = System.IO.File.ReadAllText(path);
            Assert.Contains("test crash message", content);
            Assert.Contains("InvalidOperationException", content);
        }
        finally
        {
            CrashHandler.Uninstall();
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void CrashHandler_WriteCrashReport_IncludesOsInfo()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crash_test_{Guid.NewGuid()}.log");
        try
        {
            CrashHandler.Install(logPath: path);
            CrashHandler.WriteCrashReport(null);

            string content = System.IO.File.ReadAllText(path);
            Assert.Contains(Platform.OsName, content);
            Assert.Contains(Platform.Arch, content);
        }
        finally
        {
            CrashHandler.Uninstall();
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }
}

/// <summary>Tests for <see cref="SpriteAppearances"/> (task 8.5).</summary>
public sealed class SpriteAppearancesTests
{
    [Fact]
    public void SpriteAppearances_InitiallyEmpty()
    {
        var sa = new SpriteAppearances();
        Assert.Equal(0, sa.Count);
        Assert.False(sa.IsLoaded);
    }

    [Fact]
    public void AddAppearance_IncrementsCount()
    {
        var sa = new SpriteAppearances();
        sa.AddAppearance(new SpriteAppearance { Id = 1, Category = ThingCategory.Item });
        Assert.Equal(1, sa.Count);
    }

    [Fact]
    public void Get_ExistingAppearance_ReturnsIt()
    {
        var sa = new SpriteAppearances();
        var a  = new SpriteAppearance { Id = 42, Category = ThingCategory.Creature, Name = "Dragon" };
        sa.AddAppearance(a);

        var found = sa.Get(ThingCategory.Creature, 42);
        Assert.NotNull(found);
        Assert.Equal("Dragon", found.Name);
    }

    [Fact]
    public void Get_MissingAppearance_ReturnsNull()
    {
        var sa = new SpriteAppearances();
        Assert.Null(sa.Get(ThingCategory.Item, 9999));
    }

    [Fact]
    public void GetAll_Category_FiltersCorrectly()
    {
        var sa = new SpriteAppearances();
        sa.AddAppearance(new SpriteAppearance { Id = 1, Category = ThingCategory.Item });
        sa.AddAppearance(new SpriteAppearance { Id = 2, Category = ThingCategory.Item });
        sa.AddAppearance(new SpriteAppearance { Id = 3, Category = ThingCategory.Creature });

        var items = sa.GetAll(ThingCategory.Item).ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, a => Assert.Equal(ThingCategory.Item, a.Category));
    }

    [Fact]
    public void AddAppearance_Duplicate_Overwrites()
    {
        var sa  = new SpriteAppearances();
        sa.AddAppearance(new SpriteAppearance { Id = 1, Category = ThingCategory.Item, Name = "Old" });
        sa.AddAppearance(new SpriteAppearance { Id = 1, Category = ThingCategory.Item, Name = "New" });
        Assert.Equal("New", sa.Get(ThingCategory.Item, 1)!.Name);
        Assert.Equal(1, sa.Count);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var sa = new SpriteAppearances();
        sa.AddAppearance(new SpriteAppearance { Id = 1, Category = ThingCategory.Item });
        sa.Clear();
        Assert.Equal(0, sa.Count);
        Assert.False(sa.IsLoaded);
    }

    [Fact]
    public void LoadFromBytes_ValidData_SetsIsLoaded()
    {
        var sa   = new SpriteAppearances();
        var data = new byte[] { 0x0A, 0x01, 0x00, 0x00 };
        sa.LoadFromBytes(data);
        Assert.True(sa.IsLoaded);
    }

    [Fact]
    public void LoadFromBytes_TooSmall_ThrowsInvalidData()
    {
        var sa = new SpriteAppearances();
        Assert.Throws<InvalidDataException>(() => sa.LoadFromBytes([1, 2]));
    }

    [Fact]
    public void AppearanceFlag_IsNotWalkable_TrueForNotWalkable()
    {
        var a = new SpriteAppearance
        {
            Id       = 1,
            Category = ThingCategory.Item,
            Flags    = AppearanceFlag.NotWalkable | AppearanceFlag.Stackable,
        };
        Assert.True(a.IsNotWalkable);
        Assert.True(a.IsStackable);
        Assert.False(a.IsPickupable);
    }
}
