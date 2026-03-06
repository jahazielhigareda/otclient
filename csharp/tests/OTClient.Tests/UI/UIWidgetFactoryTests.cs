using OTClient.Framework.UI;
using Xunit;

namespace OTClient.Tests.UI;

/// <summary>
/// Tests for <see cref="UIWidgetFactory"/> — widget creation by type name.
/// Task 7.9, 7.24.
/// </summary>
public sealed class UIWidgetFactoryTests
{
    // ─── IsKnown ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("UIWidget")]
    [InlineData("UILabel")]
    [InlineData("UIButton")]
    [InlineData("UITextEdit")]
    [InlineData("UIScrollBar")]
    [InlineData("UIScrollArea")]
    [InlineData("UIProgressBar")]
    [InlineData("UICheckBox")]
    [InlineData("UIRadioButton")]
    [InlineData("UITabBar")]
    [InlineData("UIWindow")]
    [InlineData("UIMap")]
    [InlineData("UIItem")]
    [InlineData("UICreature")]
    [InlineData("UIMinimap")]
    [InlineData("UISprite")]
    public void IsKnown_BuiltInTypes_ReturnsTrue(string typeName)
    {
        Assert.True(UIWidgetFactory.IsKnown(typeName));
    }

    [Fact]
    public void IsKnown_UnknownType_ReturnsFalse()
    {
        Assert.False(UIWidgetFactory.IsKnown("UIGhost"));
    }

    [Fact]
    public void IsKnown_CaseInsensitive()
    {
        Assert.True(UIWidgetFactory.IsKnown("uilabel"));
        Assert.True(UIWidgetFactory.IsKnown("UILABEL"));
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("UIWidget",      typeof(UIWidget))]
    [InlineData("UILabel",       typeof(UILabel))]
    [InlineData("UIButton",      typeof(UIButton))]
    [InlineData("UITextEdit",    typeof(UITextEdit))]
    [InlineData("UIScrollBar",   typeof(UIScrollBar))]
    [InlineData("UIScrollArea",  typeof(UIScrollArea))]
    [InlineData("UIProgressBar", typeof(UIProgressBar))]
    [InlineData("UICheckBox",    typeof(UICheckBox))]
    [InlineData("UIRadioButton", typeof(UIRadioButton))]
    [InlineData("UITabBar",      typeof(UITabBar))]
    [InlineData("UIWindow",      typeof(UIWindow))]
    [InlineData("UIMap",         typeof(UIMap))]
    [InlineData("UIItem",        typeof(UIItem))]
    [InlineData("UICreature",    typeof(UICreature))]
    [InlineData("UIMinimap",     typeof(UIMinimap))]
    [InlineData("UISprite",      typeof(UISprite))]
    public void Create_ByName_ReturnsCorrectType(string name, Type expectedType)
    {
        var w = UIWidgetFactory.Create(name);
        Assert.IsType(expectedType, w);
    }

    [Fact]
    public void Create_UnknownType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => UIWidgetFactory.Create("UIGhost"));
    }

    [Fact]
    public void Create_WithParent_AddsToParentChildren()
    {
        var parent = new UIWidget();
        var child  = UIWidgetFactory.Create("UIButton", parent);
        Assert.Contains(child, parent.Children);
    }

    [Fact]
    public void Create_WithoutParent_HasNullParent()
    {
        var w = UIWidgetFactory.Create("UILabel");
        Assert.Null(w.Parent);
    }

    [Fact]
    public void Create_Generic_ReturnsTypedInstance()
    {
        var btn = UIWidgetFactory.Create<UIButton>();
        Assert.IsType<UIButton>(btn);
    }

    // ─── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public void Register_CustomType_IsCreatable()
    {
        UIWidgetFactory.Register("UICustomWidget", () => new UILabel { Text = "custom" });
        var w = UIWidgetFactory.Create("UICustomWidget");
        Assert.IsType<UILabel>(w);
        Assert.Equal("custom", ((UILabel)w).Text);
    }

    [Fact]
    public void Register_EmptyTypeName_Throws()
    {
        Assert.Throws<ArgumentException>(() => UIWidgetFactory.Register("", () => new UIWidget()));
    }

    [Fact]
    public void Register_NullFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UIWidgetFactory.Register("X", null!));
    }

    // ─── RegisteredTypes ─────────────────────────────────────────────────────

    [Fact]
    public void RegisteredTypes_ContainsAllBuiltIns()
    {
        var types = UIWidgetFactory.RegisteredTypes;
        Assert.Contains("UIWidget", types, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("UIButton", types, StringComparer.OrdinalIgnoreCase);
    }
}
