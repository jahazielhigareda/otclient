using OTClient.Framework.Graphics;
using Xunit;

namespace OTClient.Tests.Graphics;

/// <summary>
/// Tests for <see cref="FrameBuffer"/> constructor argument validation and
/// state-machine logic.  No GPU or Raylib window is required for argument guard tests.
/// </summary>
public sealed class FrameBufferTests
{
    // ─── Constructor argument validation ──────────────────────────────────────

    [Fact]
    public void Constructor_ZeroWidth_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameBuffer(0, 64));
    }

    [Fact]
    public void Constructor_NegativeWidth_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameBuffer(-1, 64));
    }

    [Fact]
    public void Constructor_ZeroHeight_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameBuffer(64, 0));
    }

    [Fact]
    public void Constructor_NegativeHeight_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameBuffer(64, -1));
    }
}
