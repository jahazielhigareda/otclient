using OTClient.Framework.Input;

namespace OTClient.Framework.UI;

/// <summary>
/// Central UI manager that owns the root widget tree, the focused widget, and
/// routes input events from <see cref="InputManager"/> to the correct widget.
/// <para>
/// Wire it up by calling <see cref="Attach"/> after both the
/// <see cref="InputManager"/> and the root widget are created.
/// </para>
/// Maps to <c>g_ui</c> / <c>UIManager</c> in <c>src/framework/ui/uimanager.*</c>.
/// Tasks 7.7, 7.23.
/// </summary>
public sealed class UIManager : IDisposable
{
    private InputManager? _input;
    private bool _disposed;

    // ─── Widget tree ──────────────────────────────────────────────────────────

    /// <summary>Top-level root widget that covers the entire screen.</summary>
    public UIWidget Root { get; } = new() { Id = "root" };

    // ─── Focus ────────────────────────────────────────────────────────────────

    private UIWidget? _focused;

    /// <summary>The currently keyboard-focused widget, or <c>null</c>.</summary>
    public UIWidget? Focused => _focused;

    /// <summary>
    /// Moves keyboard focus to <paramref name="widget"/>.
    /// Raises <c>OnFocusLost</c> on the previous widget and
    /// <c>OnFocusGained</c> on the new one.
    /// </summary>
    public void SetFocus(UIWidget? widget)
    {
        if (_focused == widget) return;
        _focused?.RaiseFocusLost();
        _focused = widget;
        _focused?.RaiseFocusGained();
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects this manager to an <see cref="InputManager"/> so that input
    /// events are automatically routed to the focused widget.
    /// </summary>
    public void Attach(InputManager input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Detach();
        _input = input;
        _input.OnKey(HandleKey);
        _input.OnMouse(HandleMouse);
        _input.OnTextInput(HandleText);
    }

    /// <summary>Disconnects from the current <see cref="InputManager"/>.</summary>
    public void Detach()
    {
        if (_input is null) return;
        _input.RemoveOnKey(HandleKey);
        _input.RemoveOnMouse(HandleMouse);
        _input.RemoveOnTextInput(HandleText);
        _input = null;
    }

    // ─── Layout + Draw ────────────────────────────────────────────────────────

    /// <summary>Performs a layout pass on the root widget.</summary>
    public void Layout(Raylib_cs.Rectangle viewport) => Root.Layout(viewport);

    /// <summary>Collects draw commands for the entire widget tree.</summary>
    public List<UIDrawCommand> Render()
    {
        var commands = new List<UIDrawCommand>();
        Root.Draw(commands);
        return commands;
    }

    // ─── Widget factory ───────────────────────────────────────────────────────

    /// <summary>Creates and optionally attaches a widget to a named parent.</summary>
    public UIWidget CreateWidget(string typeName, UIWidget? parent = null)
        => UIWidgetFactory.Create(typeName, parent);

    /// <summary>Returns the widget with the given id by searching the root tree.</summary>
    public UIWidget? FindById(string id) => FindById(Root, id);

    private static UIWidget? FindById(UIWidget node, string id)
    {
        if (node.Id == id) return node;
        foreach (var c in node.Children)
        {
            var found = FindById(c, id);
            if (found is not null) return found;
        }
        return null;
    }

    // ─── Input routing (task 7.23) ────────────────────────────────────────────

    private void HandleKey(KeyEvent ev)
    {
        if (ev.Action == KeyAction.Pressed || ev.Action == KeyAction.Repeated)
            _focused?.RaiseKeyDown(ev);
    }

    private void HandleText(TextInputEvent ev)
    {
        if (_focused is UITextEdit te)
            te.InsertAt(te.CursorPosition, ev.Text);
    }

    private void HandleMouse(MouseEvent ev)
    {
        // Route hover/click to all widgets via hit-test
        Root.RaiseMouseMove(ev);

        // Click focuses the hit widget
        if (ev.Action == MouseAction.ButtonPressed)
        {
            var hit = Root.HitTest(ev.Position);
            SetFocus(hit);
        }
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
    }
}
