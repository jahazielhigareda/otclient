// HtmlParserTests.cs — T32: tests for HtmlParser, HtmlNode, HtmlQuerySelector
using OTClient.Framework.UI;
using Xunit;

namespace OTClient.Tests.UI;

/// <summary>
/// Tests for the HTML/CSS parser ported from src/framework/html/.
/// Task T32.
/// </summary>
public sealed class HtmlParserTests
{
    // ─── Basic structure ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyString_ReturnsRootNode()
    {
        var root = HtmlParser.Parse("");
        Assert.Equal("root", root.Tag);
        Assert.Equal(NodeType.Element, root.Type);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Parse_SingleElement_HasCorrectTag()
    {
        var root = HtmlParser.Parse("<p>Hello</p>");
        Assert.Single(root.Children);
        Assert.Equal("p", root.Children[0].Tag);
    }

    [Fact]
    public void Parse_TagNormalisedToLowercase()
    {
        var root = HtmlParser.Parse("<DIV></DIV>");
        Assert.Equal("div", root.Children[0].Tag);
    }

    [Fact]
    public void Parse_TextNode_HasCorrectContent()
    {
        var root = HtmlParser.Parse("<p>Hello world</p>");
        var p = root.Children[0];
        Assert.NotEmpty(p.Children);
        Assert.Equal(NodeType.Text, p.Children[0].Type);
        Assert.Equal("Hello world", p.Children[0].GetText());
    }

    [Fact]
    public void Parse_NestedElements()
    {
        var root = HtmlParser.Parse("<div><span>text</span></div>");
        var div  = root.Children[0];
        Assert.Equal("div", div.Tag);
        var span = div.Children[0];
        Assert.Equal("span", span.Tag);
        Assert.Equal("text", span.TextContent());
    }

    // ─── Attributes ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_AttributeDoubleQuoted()
    {
        var root = HtmlParser.Parse("<a href=\"https://example.com\">link</a>");
        Assert.Equal("https://example.com", root.Children[0].GetAttr("href"));
    }

    [Fact]
    public void Parse_AttributeSingleQuoted()
    {
        var root = HtmlParser.Parse("<a href='page.html'>link</a>");
        Assert.Equal("page.html", root.Children[0].GetAttr("href"));
    }

    [Fact]
    public void Parse_AttributeNameLowercased()
    {
        var root = HtmlParser.Parse("<IMG SRC=\"img.png\" />");
        Assert.Equal("img.png", root.Children[0].GetAttr("src"));
    }

    [Fact]
    public void Parse_BooleanAttribute_HasEmptyValue()
    {
        var root = HtmlParser.Parse("<input disabled />");
        Assert.True(root.Children[0].HasAttr("disabled"));
        Assert.Equal(string.Empty, root.Children[0].GetAttr("disabled"));
    }

    // ─── Classes / id index ───────────────────────────────────────────────────

    [Fact]
    public void Parse_ClassList_SplitCorrectly()
    {
        var root = HtmlParser.Parse("<div class=\"foo bar baz\"></div>");
        var div  = root.Children[0];
        Assert.Equal(3, div.ClassList.Count);
        Assert.Contains("foo", div.ClassList);
        Assert.Contains("bar", div.ClassList);
        Assert.Contains("baz", div.ClassList);
    }

    [Fact]
    public void GetById_ReturnsCorrectNode()
    {
        var root = HtmlParser.Parse("<p id=\"main\">hello</p>");
        var node = root.GetById("main");
        Assert.NotNull(node);
        Assert.Equal("p", node!.Tag);
    }

    [Fact]
    public void GetByClass_ReturnsMatchingNodes()
    {
        var root = HtmlParser.Parse("<p class=\"news\">a</p><p class=\"news\">b</p><p class=\"other\">c</p>");
        var nodes = root.GetByClass("news");
        Assert.Equal(2, nodes.Count);
    }

    [Fact]
    public void GetByTag_ReturnsMatchingNodes()
    {
        var root  = HtmlParser.Parse("<p>a</p><div>b</div><p>c</p>");
        var nodes = root.GetByTag("p");
        Assert.Equal(2, nodes.Count);
    }

    // ─── Void / self-closing ──────────────────────────────────────────────────

    [Fact]
    public void Parse_VoidElement_NoChildren()
    {
        var root = HtmlParser.Parse("<br>");
        Assert.Equal("br", root.Children[0].Tag);
        Assert.Empty(root.Children[0].Children);
    }

    [Fact]
    public void Parse_SelfClosingSlash_TreatedAsVoid()
    {
        var root = HtmlParser.Parse("<img src=\"x.png\" />");
        Assert.Single(root.Children);
        Assert.Empty(root.Children[0].Children);
    }

    // ─── Comments / DOCTYPE ───────────────────────────────────────────────────

    [Fact]
    public void Parse_Comment_NodeTypeComment()
    {
        var root = HtmlParser.Parse("<!-- a comment --><p>text</p>");
        Assert.Equal(NodeType.Comment, root.Children[0].Type);
        Assert.Equal(" a comment ", root.Children[0].GetText());
    }

    [Fact]
    public void Parse_Doctype_NodeTypeDoctype()
    {
        var root = HtmlParser.Parse("<!DOCTYPE html><html></html>");
        Assert.Equal(NodeType.Doctype, root.Children[0].Type);
    }

    // ─── Entity decoding ──────────────────────────────────────────────────────

    [Fact]
    public void EntityDecode_NamedEntities()
    {
        Assert.Equal("&", HtmlParser.HtmlEntityDecode("&amp;"));
        Assert.Equal("<", HtmlParser.HtmlEntityDecode("&lt;"));
        Assert.Equal(">", HtmlParser.HtmlEntityDecode("&gt;"));
        Assert.Equal("\"", HtmlParser.HtmlEntityDecode("&quot;"));
        Assert.Equal("'", HtmlParser.HtmlEntityDecode("&apos;"));
    }

    [Fact]
    public void EntityDecode_NumericDecimal()
    {
        Assert.Equal("A", HtmlParser.HtmlEntityDecode("&#65;"));
    }

    [Fact]
    public void EntityDecode_NumericHex()
    {
        Assert.Equal("A", HtmlParser.HtmlEntityDecode("&#x41;"));
    }

    [Fact]
    public void EntityDecode_TextWithEntities_InAttribute()
    {
        var root = HtmlParser.Parse("<a title=\"a &amp; b\">x</a>");
        Assert.Equal("a & b", root.Children[0].GetAttr("title"));
    }

    // ─── Implied-end rules ────────────────────────────────────────────────────

    [Fact]
    public void Parse_ImpliedEnd_ParagraphBeforeDiv()
    {
        // <p> must be closed when <div> is encountered
        var root = HtmlParser.Parse("<p>text<div>inner</div>");
        // The p and div should be siblings under root (p auto-closed)
        bool pIsDirectChild = root.Children.Any(c => c.Tag == "p");
        bool divIsDirectChild = root.Children.Any(c => c.Tag == "div");
        Assert.True(pIsDirectChild, "p should be a direct child of root");
        Assert.True(divIsDirectChild, "div should be a direct child of root (p auto-closed)");
    }

    [Fact]
    public void Parse_ImpliedEnd_ConsecutiveLiItems()
    {
        var root = HtmlParser.Parse("<ul><li>a<li>b<li>c</ul>");
        var ul   = root.Children[0];
        Assert.Equal("ul", ul.Tag);
        // All three li should be direct children of ul
        var lis = ul.Children.Where(c => c.Tag == "li").ToList();
        Assert.Equal(3, lis.Count);
    }

    // ─── TextContent / InnerHTML ──────────────────────────────────────────────

    [Fact]
    public void TextContent_DeepNesting()
    {
        var root = HtmlParser.Parse("<div><p><span>deep</span></p></div>");
        Assert.Equal("deep", root.Children[0].TextContent());
    }

    [Fact]
    public void InnerHTML_ReconstructsMarkup()
    {
        var root = HtmlParser.Parse("<div><span>hi</span></div>");
        var div  = root.Children[0];
        string html = div.InnerHTML();
        Assert.Contains("<span>", html);
        Assert.Contains("hi", html);
    }

    // ─── Parent / prev / next links ──────────────────────────────────────────

    [Fact]
    public void Parent_Link_Set()
    {
        var root = HtmlParser.Parse("<p>text</p>");
        var p    = root.Children[0];
        Assert.NotNull(p.Parent);
        Assert.Equal("root", p.Parent!.Tag);
    }

    [Fact]
    public void Prev_Next_Links()
    {
        var root = HtmlParser.Parse("<p>a</p><p>b</p><p>c</p>");
        var p0 = root.Children[0];
        var p1 = root.Children[1];
        var p2 = root.Children[2];
        Assert.Null(p0.Prev);
        Assert.Same(p1, p0.Next);
        Assert.Same(p0, p1.Prev);
        Assert.Same(p2, p1.Next);
        Assert.Same(p1, p2.Prev);
        Assert.Null(p2.Next);
    }

    // ─── querySelector ────────────────────────────────────────────────────────

    [Fact]
    public void QuerySelector_ByTag()
    {
        var root = HtmlParser.Parse("<div><span>hello</span></div>");
        var span = root.QuerySelector("span");
        Assert.NotNull(span);
        Assert.Equal("span", span!.Tag);
    }

    [Fact]
    public void QuerySelector_ById()
    {
        var root = HtmlParser.Parse("<p id=\"target\">found</p><p>other</p>");
        var n = root.QuerySelector("#target");
        Assert.NotNull(n);
        Assert.Equal("p", n!.Tag);
    }

    [Fact]
    public void QuerySelector_ByClass()
    {
        var root = HtmlParser.Parse("<p class=\"news\">a</p><p class=\"other\">b</p>");
        var n = root.QuerySelector(".news");
        Assert.NotNull(n);
        Assert.Equal("news", n!.ClassList[0]);
    }

    [Fact]
    public void QuerySelector_ChildCombinator()
    {
        var root = HtmlParser.Parse("<div><p><span>deep</span></p></div>");
        // div > p should match but div > span should not
        Assert.NotNull(root.QuerySelector("div > p"));
        Assert.Null(root.QuerySelector("div > span"));
    }

    [Fact]
    public void QuerySelector_DescendantCombinator()
    {
        var root = HtmlParser.Parse("<div><p><span>deep</span></p></div>");
        Assert.NotNull(root.QuerySelector("div span"));
    }

    [Fact]
    public void QuerySelector_AttributeEquals()
    {
        var root = HtmlParser.Parse("<a href=\"page.html\">link</a>");
        Assert.NotNull(root.QuerySelector("[href=\"page.html\"]"));
        Assert.Null(root.QuerySelector("[href=\"other.html\"]"));
    }

    [Fact]
    public void QuerySelector_AttributeContains()
    {
        var root = HtmlParser.Parse("<div class=\"foo bar\">x</div>");
        Assert.NotNull(root.QuerySelector("[class~=\"foo\"]"));
    }

    [Fact]
    public void QuerySelectorAll_ReturnsAll()
    {
        var root = HtmlParser.Parse("<p>a</p><p>b</p><div><p>c</p></div>");
        var ps = root.QuerySelectorAll("p");
        Assert.Equal(3, ps.Count);
    }

    [Fact]
    public void QuerySelector_PseudoFirstChild()
    {
        var root = HtmlParser.Parse("<ul><li>a</li><li>b</li></ul>");
        var li = root.QuerySelector("li:first-child");
        Assert.NotNull(li);
        Assert.Equal("a", li!.TextContent());
    }

    [Fact]
    public void QuerySelector_PseudoLastChild()
    {
        var root = HtmlParser.Parse("<ul><li>a</li><li>b</li></ul>");
        var li = root.QuerySelector("li:last-child");
        Assert.NotNull(li);
        Assert.Equal("b", li!.TextContent());
    }

    [Fact]
    public void QuerySelector_CommaSeparatedSelectors()
    {
        var root = HtmlParser.Parse("<h1>a</h1><h2>b</h2><p>c</p>");
        var nodes = root.QuerySelectorAll("h1, h2");
        Assert.Equal(2, nodes.Count);
    }

    // ─── Mutation API ─────────────────────────────────────────────────────────

    [Fact]
    public void Append_AddsChildAtEnd()
    {
        var root = HtmlParser.Parse("<div></div>");
        var div  = root.Children[0];
        var span = new HtmlNode { Type = NodeType.Element, Tag = "span" };
        div.Append(span);
        Assert.Single(div.Children);
        Assert.Same(span, div.Children[0]);
        Assert.Same(div, span.Parent);
    }

    [Fact]
    public void Remove_ChildDetached()
    {
        var root = HtmlParser.Parse("<div><p>x</p></div>");
        var div  = root.Children[0];
        var p    = div.Children[0];
        div.Remove(p);
        Assert.Empty(div.Children);
        Assert.Null(p.Parent);
    }

    // ─── Clone ───────────────────────────────────────────────────────────────

    [Fact]
    public void Clone_DeepCopyIsIndependent()
    {
        var root  = HtmlParser.Parse("<div><span class=\"a\">text</span></div>");
        var div   = root.Children[0];
        var clone = div.Clone(deep: true);
        clone.ChildList[0].SetAttr("class", "b");
        Assert.Equal("a", div.Children[0].GetAttr("class"));
    }

    // ─── IndexAmongElements / IndexAmongType ──────────────────────────────────

    [Fact]
    public void IndexAmongElements_OneBasedCount()
    {
        var root = HtmlParser.Parse("<p>a</p><p>b</p><p>c</p>");
        Assert.Equal(1, root.Children[0].IndexAmongElements());
        Assert.Equal(2, root.Children[1].IndexAmongElements());
        Assert.Equal(3, root.Children[2].IndexAmongElements());
    }

    [Fact]
    public void IndexAmongType_CountsBySameTag()
    {
        var root = HtmlParser.Parse("<p>a</p><div>x</div><p>b</p>");
        var ps = root.GetByTag("p");
        Assert.Equal(1, ps[0].IndexAmongType());
        Assert.Equal(2, ps[1].IndexAmongType());
    }

    // ─── expression node {{...}} ──────────────────────────────────────────────

    [Fact]
    public void Parse_ExpressionNode_IsMarked()
    {
        var root = HtmlParser.Parse("<p>Hello {{ name }}!</p>");
        var p    = root.Children[0];
        // should have a text node, an expression node, and another text node
        var exprNode = p.Children.FirstOrDefault(c => c.IsExpression);
        Assert.NotNull(exprNode);
        Assert.Equal("name", exprNode!.GetText());
    }

    // ─── script / style hoisting ─────────────────────────────────────────────

    [Fact]
    public void Parse_ScriptInsideDiv_HoistedToRoot()
    {
        var root = HtmlParser.Parse("<div><script>var x=1;</script><p>text</p></div>");
        // script should be hoisted to root front, p should stay inside div
        bool scriptAtRoot = root.Children.Any(c => c.Tag == "script");
        Assert.True(scriptAtRoot, "script should be hoisted to root");
    }

    [Fact]
    public void Parse_StyleTag_RawBodyPreserved()
    {
        var root = HtmlParser.Parse("<style>body { color: red; }</style>");
        var style = root.QuerySelector("style");
        Assert.NotNull(style);
        Assert.Contains("color: red", style!.GetText());
    }
}
