using System.Numerics;
using OTClient.Framework.Input;

namespace OTClient.Tests.Input;

/// <summary>
/// Unit tests for <see cref="InputManager"/>'s event subscription, hotkey
/// dispatch, and handler management.  These tests drive <see cref="InputManager"/>
/// without opening a Raylib window by using the internal test-helper dispatch methods.
/// </summary>
public sealed class InputManagerTests
{
    // ─── KeyEvent subscription ───────────────────────────────────────────────

    [Fact]
    public void OnKey_HandlerReceivesInjectedKeyEvent()
    {
        var mgr = new InputManager();
        KeyEvent? received = null;
        mgr.OnKey(ev => received = ev);

        var expected = new KeyEvent(Key.A, KeyAction.Pressed, KeyModifiers.None);
        mgr.DispatchKeyEventForTest(expected);

        Assert.NotNull(received);
        Assert.Equal(expected, received);
    }

    [Fact]
    public void RemoveOnKey_HandlerNoLongerReceivesEvents()
    {
        var mgr = new InputManager();
        int count = 0;
        Action<KeyEvent> handler = _ => count++;
        mgr.OnKey(handler);
        mgr.RemoveOnKey(handler);

        mgr.DispatchKeyEventForTest(new KeyEvent(Key.B, KeyAction.Pressed, KeyModifiers.None));

        Assert.Equal(0, count);
    }

    // ─── TextInputEvent subscription ─────────────────────────────────────────

    [Fact]
    public void OnTextInput_HandlerReceivesInjectedTextEvent()
    {
        var mgr = new InputManager();
        TextInputEvent? received = null;
        mgr.OnTextInput(ev => received = ev);

        var expected = new TextInputEvent('H', KeyModifiers.Shift);
        mgr.DispatchTextEventForTest(expected);

        Assert.NotNull(received);
        Assert.Equal(expected, received);
    }

    [Fact]
    public void RemoveOnTextInput_HandlerNoLongerReceivesEvents()
    {
        var mgr = new InputManager();
        int count = 0;
        Action<TextInputEvent> handler = _ => count++;
        mgr.OnTextInput(handler);
        mgr.RemoveOnTextInput(handler);

        mgr.DispatchTextEventForTest(new TextInputEvent('X', KeyModifiers.None));

        Assert.Equal(0, count);
    }

    // ─── MouseEvent subscription ──────────────────────────────────────────────

    [Fact]
    public void OnMouse_HandlerReceivesInjectedMouseEvent()
    {
        var mgr = new InputManager();
        MouseEvent? received = null;
        mgr.OnMouse(ev => received = ev);

        var expected = new MouseEvent(MouseAction.ButtonPressed, new Vector2(10, 20),
                                      Vector2.Zero, MouseButton.Left, 0f, KeyModifiers.None);
        mgr.DispatchMouseEventForTest(expected);

        Assert.NotNull(received);
        Assert.Equal(expected, received);
    }

    [Fact]
    public void RemoveOnMouse_HandlerNoLongerReceivesEvents()
    {
        var mgr = new InputManager();
        int count = 0;
        Action<MouseEvent> handler = _ => count++;
        mgr.OnMouse(handler);
        mgr.RemoveOnMouse(handler);

        mgr.DispatchMouseEventForTest(new MouseEvent(MouseAction.Moved, Vector2.Zero,
                                                     Vector2.Zero, MouseButton.Left, 0f, KeyModifiers.None));

        Assert.Equal(0, count);
    }

    // ─── Hotkey binding & dispatch ────────────────────────────────────────────

    [Fact]
    public void BindHotkey_FiresOnMatchingKeyEvent()
    {
        var mgr = new InputManager();
        string? triggered = null;
        mgr.BindHotkey("attack", Key.F1);
        mgr.OnHotkey(action => triggered = action);

        mgr.DispatchKeyEventForTest(new KeyEvent(Key.F1, KeyAction.Pressed, KeyModifiers.None));

        Assert.Equal("attack", triggered);
    }

    [Fact]
    public void BindHotkey_DoesNotFireOnRepeat()
    {
        var mgr = new InputManager();
        int count = 0;
        mgr.BindHotkey("attack", Key.F1);
        mgr.OnHotkey(_ => count++);

        mgr.DispatchKeyEventForTest(new KeyEvent(Key.F1, KeyAction.Repeated, KeyModifiers.None));

        Assert.Equal(0, count);
    }

    [Fact]
    public void BindHotkey_DoesNotFireOnRelease()
    {
        var mgr = new InputManager();
        int count = 0;
        mgr.BindHotkey("attack", Key.F1);
        mgr.OnHotkey(_ => count++);

        mgr.DispatchKeyEventForTest(new KeyEvent(Key.F1, KeyAction.Released, KeyModifiers.None));

        Assert.Equal(0, count);
    }

    [Fact]
    public void BindHotkey_WithModifier_OnlyFiresWithCorrectModifier()
    {
        var mgr = new InputManager();
        int count = 0;
        mgr.BindHotkey("save", Key.S, KeyModifiers.Control);
        mgr.OnHotkey(_ => count++);

        // Wrong modifier — should not fire
        mgr.DispatchKeyEventForTest(new KeyEvent(Key.S, KeyAction.Pressed, KeyModifiers.None));
        Assert.Equal(0, count);

        // Correct modifier — should fire
        mgr.DispatchKeyEventForTest(new KeyEvent(Key.S, KeyAction.Pressed, KeyModifiers.Control));
        Assert.Equal(1, count);
    }

    [Fact]
    public void UnbindHotkey_RemovesBinding()
    {
        var mgr = new InputManager();
        int count = 0;
        mgr.BindHotkey("attack", Key.F1);
        mgr.OnHotkey(_ => count++);
        mgr.UnbindHotkey("attack");

        mgr.DispatchKeyEventForTest(new KeyEvent(Key.F1, KeyAction.Pressed, KeyModifiers.None));

        Assert.Equal(0, count);
    }

    [Fact]
    public void Bindings_ReflectsAddedBindings()
    {
        var mgr = new InputManager();
        mgr.BindHotkey("jump",   Key.Space);
        mgr.BindHotkey("attack", Key.F1);

        Assert.Equal(2, mgr.Bindings.Count);
        Assert.Contains(mgr.Bindings, b => b.Action == "jump");
        Assert.Contains(mgr.Bindings, b => b.Action == "attack");
    }

    // ─── HotkeyBinding.Matches ────────────────────────────────────────────────

    [Fact]
    public void HotkeyBinding_Matches_ReturnsTrueForExactMatch()
    {
        var binding = new HotkeyBinding("save", Key.S, KeyModifiers.Control);
        var ev      = new KeyEvent(Key.S, KeyAction.Pressed, KeyModifiers.Control);
        Assert.True(binding.Matches(ev));
    }

    [Fact]
    public void HotkeyBinding_Matches_ReturnsFalseForWrongKey()
    {
        var binding = new HotkeyBinding("save", Key.S, KeyModifiers.Control);
        var ev      = new KeyEvent(Key.A, KeyAction.Pressed, KeyModifiers.Control);
        Assert.False(binding.Matches(ev));
    }

    [Fact]
    public void HotkeyBinding_Matches_ReturnsFalseForWrongAction()
    {
        var binding = new HotkeyBinding("save", Key.S, KeyModifiers.Control);
        var ev      = new KeyEvent(Key.S, KeyAction.Released, KeyModifiers.Control);
        Assert.False(binding.Matches(ev));
    }
}
