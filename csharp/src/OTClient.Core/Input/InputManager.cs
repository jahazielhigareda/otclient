using Raylib_cs;

namespace OTClient.Framework.Input;

/// <summary>
/// A keyboard hotkey binding: a key plus optional modifier mask that triggers
/// a named action.
/// </summary>
public sealed class HotkeyBinding
{
    public HotkeyBinding(string action, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        Action    = action;
        Key       = key;
        Modifiers = modifiers;
    }

    /// <summary>Name of the action this binding triggers (e.g. <c>"attack"</c>).</summary>
    public string Action { get; }

    /// <summary>The key that must be pressed.</summary>
    public Key Key { get; }

    /// <summary>Modifier keys that must be held simultaneously.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>Returns <c>true</c> when <paramref name="ev"/> satisfies this binding.</summary>
    public bool Matches(KeyEvent ev)
        => ev.Key == Key
           && ev.Action == KeyAction.Pressed
           && ev.Modifiers == Modifiers;
}

/// <summary>
/// Polls Raylib each frame for keyboard, mouse and text-input state and dispatches
/// strongly-typed events (<see cref="KeyEvent"/>, <see cref="MouseEvent"/>,
/// <see cref="TextInputEvent"/>) to registered handlers.
/// <para>
/// Call <see cref="Poll"/> once per frame from the main loop, before any game-logic
/// update, so that event handlers see consistent state for the current frame.
/// </para>
/// Maps to <c>EventDispatcher</c> / <c>Mouse</c> in
/// <c>src/framework/input/</c> and the platform-event plumbing in
/// <c>src/framework/platform/platformevent.h</c>.
/// </summary>
public sealed class InputManager
{
    // ─── Keys tracked for "held" detection ───────────────────────────────────
    private readonly HashSet<Key> _heldKeys = [];

    // ─── Event handlers ───────────────────────────────────────────────────────
    private readonly List<Action<KeyEvent>>       _keyHandlers       = [];
    private readonly List<Action<TextInputEvent>> _textHandlers      = [];
    private readonly List<Action<MouseEvent>>     _mouseHandlers     = [];
    private readonly List<Action<string>>         _hotkeyHandlers    = [];

    // ─── Hotkey bindings ──────────────────────────────────────────────────────
    private readonly List<HotkeyBinding> _bindings = [];

    // ─── Mouse state ──────────────────────────────────────────────────────────
    private Vector2 _prevMousePos;

    // ─── Raylib mouse buttons to poll ─────────────────────────────────────────
    private static readonly (Raylib_cs.MouseButton Raylib, MouseButton Otc)[] s_mouseButtons =
    [
        (Raylib_cs.MouseButton.Left,    MouseButton.Left),
        (Raylib_cs.MouseButton.Right,   MouseButton.Right),
        (Raylib_cs.MouseButton.Middle,  MouseButton.Middle),
        (Raylib_cs.MouseButton.Side,    MouseButton.Side),
        (Raylib_cs.MouseButton.Extra,   MouseButton.Extra),
        (Raylib_cs.MouseButton.Forward, MouseButton.Forward),
        (Raylib_cs.MouseButton.Back,    MouseButton.Back),
    ];

    // ─── Keyboard keys to poll (Raylib-enumerable range) ─────────────────────
    // Raylib does not expose a "get all pressed keys" API, so we iterate over
    // the key values defined in our Key enum.
    private static readonly KeyboardKey[] s_allRaylibKeys =
        Array.ConvertAll(
            Enum.GetValues<Key>()
                .Where(k => k != Key.Unknown)
                .Select(k => (int)k)
                .Distinct()
                .ToArray(),
            code => (KeyboardKey)code);

    // ─── Event subscription ───────────────────────────────────────────────────

    /// <summary>Registers a handler invoked for every key press, repeat, or release.</summary>
    public void OnKey(Action<KeyEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _keyHandlers.Add(handler);
    }

    /// <summary>Removes a previously registered key handler.</summary>
    public void RemoveOnKey(Action<KeyEvent> handler) => _keyHandlers.Remove(handler);

    /// <summary>Registers a handler invoked for every Unicode character typed.</summary>
    public void OnTextInput(Action<TextInputEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _textHandlers.Add(handler);
    }

    /// <summary>Removes a previously registered text-input handler.</summary>
    public void RemoveOnTextInput(Action<TextInputEvent> handler) => _textHandlers.Remove(handler);

    /// <summary>Registers a handler invoked for every mouse event (move, button, scroll).</summary>
    public void OnMouse(Action<MouseEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _mouseHandlers.Add(handler);
    }

    /// <summary>Removes a previously registered mouse handler.</summary>
    public void RemoveOnMouse(Action<MouseEvent> handler) => _mouseHandlers.Remove(handler);

    /// <summary>
    /// Registers a handler invoked when a <see cref="HotkeyBinding"/> is triggered.
    /// The handler receives the binding's action name.
    /// </summary>
    public void OnHotkey(Action<string> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _hotkeyHandlers.Add(handler);
    }

    /// <summary>Removes a previously registered hotkey handler.</summary>
    public void RemoveOnHotkey(Action<string> handler) => _hotkeyHandlers.Remove(handler);

    // ─── Hotkey bindings ──────────────────────────────────────────────────────

    /// <summary>Adds a hotkey binding.</summary>
    public void BindHotkey(HotkeyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _bindings.Add(binding);
    }

    /// <summary>Adds a hotkey binding by parts.</summary>
    public void BindHotkey(string action, Key key, KeyModifiers modifiers = KeyModifiers.None)
        => BindHotkey(new HotkeyBinding(action, key, modifiers));

    /// <summary>Removes all bindings for the given action name.</summary>
    public void UnbindHotkey(string action)
        => _bindings.RemoveAll(b => b.Action == action);

    /// <summary>All currently registered bindings (read-only view).</summary>
    public IReadOnlyList<HotkeyBinding> Bindings => _bindings;

    // ─── Polling ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Polls Raylib for all input state changes that occurred since the last call
    /// and dispatches the corresponding events.  Call once per frame from the main loop.
    /// </summary>
    public void Poll()
    {
        KeyModifiers mods = KeyMap.CurrentModifiers();

        PollKeyboard(mods);
        PollTextInput(mods);
        PollMouse(mods);
    }

    // ─── Clipboard ───────────────────────────────────────────────────────────

    /// <summary>Returns the current OS clipboard text (may be empty).</summary>
    public string GetClipboardText()
    {
        return Raylib.GetClipboardText_() ?? string.Empty;
    }

    /// <summary>Writes <paramref name="text"/> to the OS clipboard.</summary>
    public void SetClipboardText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Raylib.SetClipboardText(text);
    }

    // ─── Cursor ───────────────────────────────────────────────────────────────

    /// <summary>Changes the OS cursor to the specified Raylib cursor shape.</summary>
    public void SetCursor(MouseCursor cursor) => Raylib.SetMouseCursor(cursor);

    /// <summary>Hides the OS cursor.</summary>
    public void HideCursor() => Raylib.HideCursor();

    /// <summary>Shows the OS cursor.</summary>
    public void ShowCursor() => Raylib.ShowCursor();

    /// <summary><c>true</c> when the OS cursor is currently hidden.</summary>
    public bool IsCursorHidden => Raylib.IsCursorHidden();

    // ─── Mouse helpers (synchronous query) ───────────────────────────────────

    /// <summary>Returns the current mouse position in window coordinates.</summary>
    public Vector2 MousePosition => Raylib.GetMousePosition();

    /// <summary>Returns <c>true</c> if <paramref name="button"/> is currently held.</summary>
    public bool IsMouseDown(MouseButton button)
        => Raylib.IsMouseButtonDown((Raylib_cs.MouseButton)(int)button);

    // ─── Key helpers (synchronous query) ─────────────────────────────────────

    /// <summary>Returns <c>true</c> if <paramref name="key"/> is currently held.</summary>
    public bool IsKeyDown(Key key)
        => Raylib.IsKeyDown(KeyMap.ToRaylib(key));

    // ─── Internal test helpers ────────────────────────────────────────────────

    /// <summary>
    /// Dispatches a synthetic <see cref="KeyEvent"/> directly to handlers.
    /// For use by unit tests only — does not require a Raylib window.
    /// </summary>
    internal void DispatchKeyEventForTest(KeyEvent ev) => DispatchKeyEvent(ev);

    /// <summary>
    /// Dispatches a synthetic <see cref="TextInputEvent"/> directly to handlers.
    /// For use by unit tests only — does not require a Raylib window.
    /// </summary>
    internal void DispatchTextEventForTest(TextInputEvent ev)
    {
        foreach (var h in _textHandlers) h(ev);
    }

    /// <summary>
    /// Dispatches a synthetic <see cref="MouseEvent"/> directly to handlers.
    /// For use by unit tests only — does not require a Raylib window.
    /// </summary>
    internal void DispatchMouseEventForTest(MouseEvent ev)
    {
        foreach (var h in _mouseHandlers) h(ev);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private void PollKeyboard(KeyModifiers mods)
    {
        foreach (KeyboardKey rk in s_allRaylibKeys)
        {
            Key key = KeyMap.FromRaylib(rk);
            if (key == Key.Unknown) continue;

            if (Raylib.IsKeyPressed(rk))
            {
                _heldKeys.Add(key);
                var ev = new KeyEvent(key, KeyAction.Pressed, mods);
                DispatchKeyEvent(ev);
            }
            else if (Raylib.IsKeyPressedRepeat(rk))
            {
                var ev = new KeyEvent(key, KeyAction.Repeated, mods);
                DispatchKeyEvent(ev);
            }
            else if (Raylib.IsKeyReleased(rk))
            {
                _heldKeys.Remove(key);
                var ev = new KeyEvent(key, KeyAction.Released, mods);
                DispatchKeyEvent(ev);
            }
        }
    }

    private void DispatchKeyEvent(KeyEvent ev)
    {
        foreach (var h in _keyHandlers) h(ev);

        // Check hotkey bindings only on Pressed events
        if (ev.Action == KeyAction.Pressed)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Matches(ev))
                {
                    string action = binding.Action;
                    foreach (var h in _hotkeyHandlers) h(action);
                }
            }
        }
    }

    private void PollTextInput(KeyModifiers mods)
    {
        int cp;
        while ((cp = Raylib.GetCharPressed()) != 0)
        {
            var ev = new TextInputEvent(cp, mods);
            foreach (var h in _textHandlers) h(ev);
        }
    }

    private void PollMouse(KeyModifiers mods)
    {
        Vector2 pos   = Raylib.GetMousePosition();
        Vector2 delta = Raylib.GetMouseDelta();

        // Movement
        if (delta != Vector2.Zero)
        {
            var ev = new MouseEvent(MouseAction.Moved, pos, delta, null, 0f, mods);
            foreach (var h in _mouseHandlers) h(ev);
        }

        // Buttons
        foreach (var (rk, btn) in s_mouseButtons)
        {
            if (Raylib.IsMouseButtonPressed(rk))
            {
                var ev = new MouseEvent(MouseAction.ButtonPressed, pos, Vector2.Zero, btn, 0f, mods);
                foreach (var h in _mouseHandlers) h(ev);
            }
            else if (Raylib.IsMouseButtonReleased(rk))
            {
                var ev = new MouseEvent(MouseAction.ButtonReleased, pos, Vector2.Zero, btn, 0f, mods);
                foreach (var h in _mouseHandlers) h(ev);
            }
        }

        // Scroll wheel
        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f)
        {
            var ev = new MouseEvent(MouseAction.WheelMoved, pos, Vector2.Zero, null, wheel, mods);
            foreach (var h in _mouseHandlers) h(ev);
        }

        _prevMousePos = pos;

        // Note: _prevMousePos is retained for future use (e.g. accumulated delta tracking).
    }
}
