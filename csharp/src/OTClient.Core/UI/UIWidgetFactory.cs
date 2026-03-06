namespace OTClient.Framework.UI;

/// <summary>
/// Factory that creates <see cref="UIWidget"/> instances from a string type name.
/// Can be extended by registering custom widget factories via
/// <see cref="Register"/>.
/// <para>
/// Used by the Lua UI system: <c>g_ui:createWidget("UIButton", parent)</c>.
/// </para>
/// Task 7.9.
/// </summary>
public static class UIWidgetFactory
{
    private static readonly Dictionary<string, Func<UIWidget>> s_creators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UIWidget"]      = () => new UIWidget(),
        ["UILabel"]       = () => new UILabel(),
        ["UIButton"]      = () => new UIButton(),
        ["UITextEdit"]    = () => new UITextEdit(),
        ["UIScrollBar"]   = () => new UIScrollBar(),
        ["UIScrollArea"]  = () => new UIScrollArea(),
        ["UIProgressBar"] = () => new UIProgressBar(),
        ["UICheckBox"]    = () => new UICheckBox(),
        ["UIRadioButton"] = () => new UIRadioButton(),
        ["UITabBar"]      = () => new UITabBar(),
        ["UIWindow"]      = () => new UIWindow(),
        ["UIMap"]         = () => new UIMap(),
        ["UIItem"]        = () => new UIItem(),
        ["UICreature"]    = () => new UICreature(),
        ["UIMinimap"]     = () => new UIMinimap(),
        ["UISprite"]      = () => new UISprite(),
    };

    /// <summary>
    /// Creates a widget of the requested type and optionally adds it to
    /// <paramref name="parent"/>.
    /// </summary>
    /// <param name="typeName">Widget type name, case-insensitive (e.g. <c>"UIButton"</c>).</param>
    /// <param name="parent">Optional parent to attach the widget to.</param>
    /// <returns>A new, unconfigured widget instance.</returns>
    /// <exception cref="ArgumentException">When <paramref name="typeName"/> is unknown.</exception>
    public static UIWidget Create(string typeName, UIWidget? parent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        if (!s_creators.TryGetValue(typeName, out var factory))
            throw new ArgumentException($"Unknown widget type: '{typeName}'.", nameof(typeName));

        var widget = factory();
        parent?.AddChild(widget);
        return widget;
    }

    /// <summary>Creates a strongly-typed widget of <typeparamref name="T"/>.</summary>
    public static T Create<T>(UIWidget? parent = null) where T : UIWidget, new()
    {
        var widget = new T();
        parent?.AddChild(widget);
        return widget;
    }

    /// <summary>
    /// Registers a custom widget type.  Overwrites an existing registration
    /// when <paramref name="typeName"/> is already known.
    /// </summary>
    public static void Register(string typeName, Func<UIWidget> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(factory);
        s_creators[typeName] = factory;
    }

    /// <summary>Returns <c>true</c> when <paramref name="typeName"/> is a registered widget type.</summary>
    public static bool IsKnown(string typeName)
        => !string.IsNullOrWhiteSpace(typeName) && s_creators.ContainsKey(typeName);

    /// <summary>Returns all currently registered type names.</summary>
    public static IReadOnlyCollection<string> RegisteredTypes
        => s_creators.Keys.ToArray();
}
