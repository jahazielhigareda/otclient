using OTClient.Framework.UI;
using Raylib_cs;
using Xunit;

namespace OTClient.Tests.UI;

/// <summary>
/// Tests for <see cref="OtuiParser"/> — OTUI style file parsing and property coercions.
/// Task 7.7, 7.24.
/// </summary>
public sealed class OtuiParserTests
{
    // ─── Parse ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SingleBlock_ReturnsOneBlock()
    {
        const string input = """
            UILabel
              text: Hello
            """;
        var blocks = OtuiParser.Parse(input);
        Assert.Single(blocks);
        Assert.Equal("UILabel", blocks[0].TypeName);
    }

    [Fact]
    public void Parse_BlockWithId_ExtractsId()
    {
        const string input = """
            UIButton#myBtn
              text: Click
            """;
        var blocks = OtuiParser.Parse(input);
        Assert.Equal("UIButton", blocks[0].TypeName);
        Assert.Equal("myBtn",    blocks[0].Id);
    }

    [Fact]
    public void Parse_BlockWithoutId_HasNullId()
    {
        const string input = """
            UILabel
              text: Hello
            """;
        var blocks = OtuiParser.Parse(input);
        Assert.Null(blocks[0].Id);
    }

    [Fact]
    public void Parse_MultipleBlocks_AllReturned()
    {
        const string input = """
            UILabel
              text: A

            UIButton
              text: B
            """;
        var blocks = OtuiParser.Parse(input);
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void Parse_Properties_ReadCorrectly()
    {
        const string input = """
            UIWidget
              width: 100
              height: 50
              visible: true
            """;
        var block = OtuiParser.Parse(input)[0];
        Assert.Equal("100",  block.Properties["width"]);
        Assert.Equal("50",   block.Properties["height"]);
        Assert.Equal("true", block.Properties["visible"]);
    }

    [Fact]
    public void Parse_CommentLines_AreIgnored()
    {
        const string input = """
            UILabel
              // this is a comment
              text: Real value
            """;
        var block = OtuiParser.Parse(input)[0];
        Assert.Equal("Real value", block.Properties["text"]);
        Assert.False(block.Properties.ContainsKey("// this is a comment"));
    }

    [Fact]
    public void Parse_BlankLines_AreIgnored()
    {
        const string input = "\n\n\nUILabel\n  text: hi\n\n";
        var blocks = OtuiParser.Parse(input);
        Assert.Single(blocks);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyList()
    {
        var blocks = OtuiParser.Parse("");
        Assert.Empty(blocks);
    }

    [Fact]
    public void Parse_PropertyWithColonInValue_FullValuePreserved()
    {
        const string input = """
            UILabel
              text: Hello: World
            """;
        var block = OtuiParser.Parse(input)[0];
        Assert.Equal("Hello: World", block.Properties["text"]);
    }

    // ─── GetInt ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetInt_PresentKey_ReturnsValue()
    {
        var props = new Dictionary<string, string> { ["x"] = "42" };
        Assert.Equal(42, OtuiParser.GetInt(props, "x"));
    }

    [Fact]
    public void GetInt_MissingKey_ReturnsDefault()
    {
        var props = new Dictionary<string, string>();
        Assert.Equal(99, OtuiParser.GetInt(props, "missing", 99));
    }

    // ─── GetFloat ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetFloat_PresentKey_ReturnsValue()
    {
        var props = new Dictionary<string, string> { ["opacity"] = "0.75" };
        Assert.Equal(0.75f, OtuiParser.GetFloat(props, "opacity"), precision: 5);
    }

    // ─── GetBool ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetBool_True_ReturnsTrue()
    {
        var props = new Dictionary<string, string> { ["visible"] = "true" };
        Assert.True(OtuiParser.GetBool(props, "visible"));
    }

    [Fact]
    public void GetBool_False_ReturnsFalse()
    {
        var props = new Dictionary<string, string> { ["visible"] = "false" };
        Assert.False(OtuiParser.GetBool(props, "visible"));
    }

    [Fact]
    public void GetBool_Missing_ReturnsDefault()
    {
        var props = new Dictionary<string, string>();
        Assert.True(OtuiParser.GetBool(props, "x", defaultValue: true));
    }

    // ─── GetString ────────────────────────────────────────────────────────────

    [Fact]
    public void GetString_PresentKey_ReturnsValue()
    {
        var props = new Dictionary<string, string> { ["text"] = "hello" };
        Assert.Equal("hello", OtuiParser.GetString(props, "text"));
    }

    [Fact]
    public void GetString_Missing_ReturnsDefault()
    {
        var props = new Dictionary<string, string>();
        Assert.Equal("fallback", OtuiParser.GetString(props, "x", "fallback"));
    }

    // ─── ParseColor ───────────────────────────────────────────────────────────

    [Fact]
    public void ParseColor_WhiteHex_ReturnsWhite()
    {
        var c = OtuiParser.ParseColor("#ffffff");
        Assert.Equal(255, c.R);
        Assert.Equal(255, c.G);
        Assert.Equal(255, c.B);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void ParseColor_RedHex()
    {
        var c = OtuiParser.ParseColor("#ff0000");
        Assert.Equal(255, c.R);
        Assert.Equal(0,   c.G);
        Assert.Equal(0,   c.B);
    }

    [Fact]
    public void ParseColor_WithAlpha()
    {
        var c = OtuiParser.ParseColor("#00ff0080");
        Assert.Equal(0,   c.R);
        Assert.Equal(255, c.G);
        Assert.Equal(0,   c.B);
        Assert.Equal(128, c.A);
    }

    [Fact]
    public void ParseColor_Invalid_ReturnsFallback()
    {
        var fallback = new Color(1, 2, 3, 4);
        var c = OtuiParser.ParseColor("not-a-color", fallback);
        Assert.Equal(fallback, c);
    }

    [Fact]
    public void ParseColor_Empty_ReturnsFallback()
    {
        var fallback = new Color(7, 8, 9, 10);
        var c = OtuiParser.ParseColor("", fallback);
        Assert.Equal(fallback, c);
    }
}
