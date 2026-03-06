using System.Numerics;
using OTClient.Framework.Input;
using OTClient.Framework.UI;
using Raylib_cs;
using Xunit;

namespace OTClient.Tests.UI;

/// <summary>
/// Tests for <see cref="UIManager"/>: focus management, input routing, layout, render,
/// FindById, and the CreateWidget factory method.
/// Tasks 7.7, 7.23, 7.24.
/// </summary>
public sealed class UIManagerTests : IDisposable
{
    private readonly InputManager _input  = new();
    private readonly UIManager    _ui     = new();

    public UIManagerTests() => _ui.Attach(_input);
    public void Dispose() => _ui.Dispose();

    // ─── Root ────────────────────────────────────────────────────────────────

    [Fact]
    public void Root_HasId_Root()
    {
        Assert.Equal("root", _ui.Root.Id);
    }

    // ─── Focus management ─────────────────────────────────────────────────────

    [Fact]
    public void SetFocus_ChangesFocused()
    {
        var w = new UIWidget();
        _ui.Root.AddChild(w);
        _ui.SetFocus(w);
        Assert.Same(w, _ui.Focused);
    }

    [Fact]
    public void SetFocus_PreviousWidget_LosesFocus()
    {
        var a = new UIWidget();
        var b = new UIWidget();
        _ui.Root.AddChild(a);
        _ui.Root.AddChild(b);
        _ui.SetFocus(a);
        bool lostFocused = false;
        a.OnFocusLost += _ => lostFocused = true;
        _ui.SetFocus(b);
        Assert.True(lostFocused);
    }

    [Fact]
    public void SetFocus_NewWidget_GainsFocus()
    {
        var w = new UIWidget();
        _ui.Root.AddChild(w);
        bool gained = false;
        w.OnFocusGained += _ => gained = true;
        _ui.SetFocus(w);
        Assert.True(gained);
    }

    [Fact]
    public void SetFocus_SameWidget_NoEvents()
    {
        var w = new UIWidget();
        _ui.Root.AddChild(w);
        _ui.SetFocus(w);
        int events = 0;
        w.OnFocusGained += _ => events++;
        w.OnFocusLost   += _ => events++;
        _ui.SetFocus(w);       // same widget — should be a no-op
        Assert.Equal(0, events);
    }

    [Fact]
    public void SetFocus_Null_ClearsFocused()
    {
        var w = new UIWidget();
        _ui.SetFocus(w);
        _ui.SetFocus(null);
        Assert.Null(_ui.Focused);
    }

    // ─── Key routing ─────────────────────────────────────────────────────────

    [Fact]
    public void Key_Press_RoutedToFocusedWidget()
    {
        var w = new UIWidget();
        _ui.Root.AddChild(w);
        _ui.SetFocus(w);
        KeyEvent? received = null;
        w.OnKeyDown += (_, ev) => received = ev;

        var key = new KeyEvent(Key.A, KeyAction.Pressed, KeyModifiers.None);
        _input.DispatchKeyEventForTest(key);

        Assert.NotNull(received);
        Assert.Equal(Key.A, received.Value.Key);
    }

    [Fact]
    public void Key_Press_NotRoutedWhenNoFocus()
    {
        var w = new UIWidget();
        _ui.Root.AddChild(w);
        KeyEvent? received = null;
        w.OnKeyDown += (_, ev) => received = ev;

        _input.DispatchKeyEventForTest(new KeyEvent(Key.A, KeyAction.Pressed, KeyModifiers.None));

        Assert.Null(received);
    }

    // ─── Text input routing ───────────────────────────────────────────────────

    [Fact]
    public void TextInput_InsertedIntoFocusedTextEdit()
    {
        var te = new UITextEdit();
        _ui.Root.AddChild(te);
        _ui.SetFocus(te);

        _input.DispatchTextEventForTest(new TextInputEvent('H', KeyModifiers.None));
        _input.DispatchTextEventForTest(new TextInputEvent('i', KeyModifiers.None));

        Assert.Equal("Hi", te.Text);
    }

    // ─── Mouse routing ────────────────────────────────────────────────────────

    [Fact]
    public void MouseClick_SetsFocusToHitWidget()
    {
        var btn = new UIButton { Size = new Vector2(100, 40) };
        _ui.Root.AddChild(btn);
        _ui.Layout(new Rectangle(0, 0, 800, 600));

        var click = new MouseEvent(
            MouseAction.ButtonPressed,
            new Vector2(50, 20),
            Vector2.Zero,
            OTClient.Framework.Input.MouseButton.Left,
            0f,
            KeyModifiers.None);
        _input.DispatchMouseEventForTest(click);

        Assert.Same(btn, _ui.Focused);
    }

    // ─── Layout + Render ─────────────────────────────────────────────────────

    [Fact]
    public void Layout_UpdatesRootComputedRect()
    {
        _ui.Layout(new Rectangle(0, 0, 1280, 720));
        // Root has no size constraint → fills available
        Assert.Equal(1280f, _ui.Root.ComputedRect.Width);
    }

    [Fact]
    public void Render_ReturnsCommands()
    {
        var lbl = new UILabel
        {
            Text = "Test",
            Size = new Vector2(200, 40),
            BackgroundColor = Color.Red,
        };
        _ui.Root.AddChild(lbl);
        _ui.Layout(new Rectangle(0, 0, 800, 600));

        var cmds = _ui.Render();
        Assert.Contains(cmds, c => c is UIDrawCommand.FillRect);
        Assert.Contains(cmds, c => c is UIDrawCommand.DrawText);
    }

    // ─── FindById ─────────────────────────────────────────────────────────────

    [Fact]
    public void FindById_Root_ReturnsRoot()
    {
        var found = _ui.FindById("root");
        Assert.Same(_ui.Root, found);
    }

    [Fact]
    public void FindById_NestedWidget_Found()
    {
        var panel = new UIWidget { Id = "panel" };
        var btn   = new UIWidget { Id = "ok-btn" };
        panel.AddChild(btn);
        _ui.Root.AddChild(panel);

        Assert.Same(btn, _ui.FindById("ok-btn"));
    }

    [Fact]
    public void FindById_Missing_ReturnsNull()
    {
        Assert.Null(_ui.FindById("ghost"));
    }

    // ─── CreateWidget ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateWidget_ReturnsCorrectType()
    {
        var w = _ui.CreateWidget("UIButton");
        Assert.IsType<UIButton>(w);
    }

    [Fact]
    public void CreateWidget_WithParent_Attached()
    {
        var parent = new UIWidget();
        _ui.Root.AddChild(parent);
        var child = _ui.CreateWidget("UILabel", parent);
        Assert.Contains(child, parent.Children);
    }

    // ─── Dispose ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var mgr = new UIManager();
        mgr.Attach(new InputManager());
        var ex = Record.Exception(() => mgr.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var mgr = new UIManager();
        mgr.Dispose();
        var ex = Record.Exception(() => mgr.Dispose());
        Assert.Null(ex);
    }
}
