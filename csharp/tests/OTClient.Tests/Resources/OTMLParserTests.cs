using System.IO;
using System.IO.Compression;
using OTClient.Framework.Resources;
using Xunit;

namespace OTClient.Tests.Resources;

/// <summary>
/// Tests for <see cref="OTMLParser"/>, <see cref="OTMLNode"/>,
/// <see cref="OTMLDocument"/>. Tasks 9.5, 9.6.
/// </summary>
public sealed class OTMLParserTests
{
    // ─── Basic key-value ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_SimpleKeyValue_ReturnsNode()
    {
        var doc = OTMLParser.Parse("key: hello");
        var node = doc.Get("key");
        Assert.NotNull(node);
        Assert.Equal("hello", node.Value);
    }

    [Fact]
    public void Parse_IntegerValue_CanBeRead()
    {
        var doc = OTMLParser.Parse("count: 42");
        Assert.Equal(42, doc.Get("count")!.Read<int>());
    }

    [Fact]
    public void Parse_BoolTrue_CanBeRead()
    {
        var doc = OTMLParser.Parse("flag: true");
        Assert.True(doc.Get("flag")!.Read<bool>());
    }

    [Fact]
    public void Parse_BoolYes_CanBeRead()
    {
        var doc = OTMLParser.Parse("flag: yes");
        Assert.True(doc.Get("flag")!.Read<bool>());
    }

    [Fact]
    public void Parse_FloatValue_CanBeRead()
    {
        var doc = OTMLParser.Parse("speed: 1.5");
        Assert.Equal(1.5f, doc.Get("speed")!.Read<float>(), precision: 5);
    }

    [Fact]
    public void Parse_QuotedString_StripsQuotes()
    {
        var doc = OTMLParser.Parse("name: \"My Module\"");
        Assert.Equal("My Module", doc.Get("name")!.Value);
    }

    [Fact]
    public void Parse_NullValue_SetsIsNull()
    {
        var doc = OTMLParser.Parse("val: null");
        var node = doc.Get("val");
        Assert.NotNull(node);
        Assert.True(node.IsNull);
    }

    // ─── Comments ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_LineComment_IsIgnored()
    {
        var doc = OTMLParser.Parse("// this is a comment\nkey: value");
        Assert.Single(doc.Children);
        Assert.Equal("key", doc.Children[0].Tag);
    }

    [Fact]
    public void Parse_HashComment_IsIgnored()
    {
        var doc = OTMLParser.Parse("# comment\nkey: value");
        Assert.Single(doc.Children);
    }

    [Fact]
    public void Parse_InlineComment_IsStripped()
    {
        var doc = OTMLParser.Parse("key: value // inline");
        Assert.Equal("value", doc.Get("key")!.Value);
    }

    // ─── Nesting ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Nested_ChildAccessible()
    {
        const string otml = """
            parent:
              child: 99
            """;
        var doc    = OTMLParser.Parse(otml);
        var parent = doc.Get("parent");
        Assert.NotNull(parent);
        Assert.Equal(99, parent.ReadAt<int>("child"));
    }

    [Fact]
    public void Parse_DeepNesting_Works()
    {
        const string otml = """
            a:
              b:
                c: deep
            """;
        var doc = OTMLParser.Parse(otml);
        var c   = doc.Get("a")!.Get("b")!.Get("c");
        Assert.NotNull(c);
        Assert.Equal("deep", c.Value);
    }

    [Fact]
    public void Parse_MultipleTopLevel_AllPresent()
    {
        const string otml = """
            x: 1
            y: 2
            z: 3
            """;
        var doc = OTMLParser.Parse(otml);
        Assert.Equal(3, doc.Children.Count);
    }

    // ─── Aliases ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_AliasDefinition_NotAddedAsNode()
    {
        var doc = OTMLParser.Parse("&myAlias: hello\nkey: *myAlias");
        // &myAlias line should not appear as a child node
        Assert.Null(doc.Get("&myAlias"));
    }

    [Fact]
    public void Parse_AliasReference_IsResolved()
    {
        var doc = OTMLParser.Parse("&myVal: 123\nresult: *myVal");
        Assert.Equal("123", doc.Get("result")!.Value);
    }

    // ─── OTMLNode API ─────────────────────────────────────────────────────────

    [Fact]
    public void At_ThrowsKeyNotFound_WhenMissing()
    {
        var node = OTMLNode.Create("parent");
        Assert.Throws<KeyNotFoundException>(() => node.At("missing"));
    }

    [Fact]
    public void AtIndex_ThrowsOutOfRange_WhenInvalid()
    {
        var node = OTMLNode.Create("parent");
        Assert.Throws<IndexOutOfRangeException>(() => node.AtIndex(0));
    }

    [Fact]
    public void HasChild_TrueAfterAddingChild()
    {
        var parent = OTMLNode.Create("parent");
        parent.AddChild(OTMLNode.Create("child", "val"));
        Assert.True(parent.HasChild("child"));
    }

    [Fact]
    public void ReadAtOrDefault_ReturnsFallback_WhenMissing()
    {
        var node = OTMLNode.Create("root");
        Assert.Equal(99, node.ReadAtOrDefault<int>("absent", 99));
    }

    [Fact]
    public void ReadOrDefault_ReturnsFallback_WhenNoValue()
    {
        var node = OTMLNode.Create("key");  // no value
        Assert.Equal("fallback", node.ReadOrDefault<string>("fallback"));
    }

    // ─── OTMLDocument.Parse(stream) ───────────────────────────────────────────

    [Fact]
    public void Parse_Stream_Works()
    {
        using var ms = new System.IO.MemoryStream(
            System.Text.Encoding.UTF8.GetBytes("key: streamValue"));
        var doc = OTMLDocument.Parse(ms, "test.otml");
        Assert.Equal("streamValue", doc.Get("key")!.Value);
    }

    // ─── Source path ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SourcePath_IsStored()
    {
        var doc = OTMLParser.Parse("k: v", "my/file.otml");
        Assert.Equal("my/file.otml", doc.SourcePath);
    }

    [Fact]
    public void Node_SourceField_ContainsLineInfo()
    {
        var doc = OTMLParser.Parse("// comment\nkey: val", "src.otml");
        var node = doc.Get("key");
        Assert.NotNull(node);
        Assert.Contains("src.otml", node.Source);
    }

    // ─── .otmod module block ──────────────────────────────────────────────────

    [Fact]
    public void Parse_OtmodBlock_ModuleNodePresent()
    {
        const string otml = """
            Module
              name: game_things
              description: Loads things
              autoLoad: true
            """;
        var doc = OTMLParser.Parse(otml);
        var mod = doc.Get("Module");
        Assert.NotNull(mod);
        Assert.Equal("game_things", mod.ReadAt<string>("name"));
        Assert.True(mod.ReadAt<bool>("autoLoad"));
    }
}
