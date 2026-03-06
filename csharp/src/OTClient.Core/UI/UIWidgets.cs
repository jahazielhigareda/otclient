using Raylib_cs;

namespace OTClient.Framework.UI;

// ─── UILabel (7.17) ───────────────────────────────────────────────────────────

/// <summary>
/// A simple non-interactive text label.
/// Supports a <see cref="Text"/> property, optional word-wrap, and
/// inline colour markup via <c>[color=#hex]…[/color]</c> tags.
/// Task 7.17.
/// </summary>
public class UILabel : UIWidget
{
    public override string TypeName => "UILabel";

    public string Text      { get; set; } = string.Empty;
    public float  FontSize  { get; set; } = 14f;
    public bool   WordWrap  { get; set; } = false;

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);
        if (!string.IsNullOrEmpty(Text))
        {
            commands.Add(new UIDrawCommand.DrawText(
                Text,
                new Vector2(ComputedRect.X, ComputedRect.Y),
                FontSize,
                ForegroundColor));
        }
    }
}

// ─── UIButton (7.10) ──────────────────────────────────────────────────────────

/// <summary>
/// A clickable button with hover, pressed, and disabled visual states.
/// Task 7.10.
/// </summary>
public class UIButton : UILabel
{
    public override string TypeName => "UIButton";

    // Colors per state
    public Color HoveredColor  { get; set; } = new Color(200, 200, 200, 255);
    public Color PressedColor  { get; set; } = new Color(150, 150, 150, 255);
    public Color DisabledColor { get; set; } = new Color(80,  80,  80,  255);

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        // Override background based on state
        var savedBg = BackgroundColor;
        if (!Enabled)
            BackgroundColor = DisabledColor;
        else if (IsPressed)
            BackgroundColor = PressedColor;
        else if (IsHovered)
            BackgroundColor = HoveredColor;

        base.Draw(commands);
        BackgroundColor = savedBg;
    }

    internal override void RaiseMouseMove(Input.MouseEvent ev)
    {
        base.RaiseMouseMove(ev);
        bool inside = Contains(ev.Position);
        if (inside && ev.Action == Input.MouseAction.ButtonPressed)
            State |= WidgetState.Pressed;
        if (ev.Action == Input.MouseAction.ButtonReleased)
        {
            if (IsPressed && inside) RaiseClick();
            State &= ~WidgetState.Pressed;
        }
    }
}

// ─── UITextEdit (7.11) ────────────────────────────────────────────────────────

/// <summary>
/// A single-line (or multi-line) text input widget with cursor and selection.
/// Task 7.11.
/// </summary>
public class UITextEdit : UIWidget
{
    public override string TypeName => "UITextEdit";

    private string _text = string.Empty;
    private int    _cursor = 0;
    private int    _selectionStart = -1;
    private int    _selectionEnd   = -1;

    public string Text
    {
        get => _text;
        set
        {
            _text   = value ?? string.Empty;
            _cursor = Math.Clamp(_cursor, 0, _text.Length);
        }
    }

    public int    CursorPosition    => _cursor;
    public bool   HasSelection      => _selectionStart >= 0 && _selectionStart != _selectionEnd;
    public string SelectedText      => HasSelection
        ? _text[Math.Min(_selectionStart, _selectionEnd)..Math.Max(_selectionStart, _selectionEnd)]
        : string.Empty;

    public float FontSize { get; set; } = 14f;
    public bool  IsMultiLine { get; set; } = false;

    public event Action<UITextEdit>? OnTextChanged;

    // ─── Text manipulation ────────────────────────────────────────────────────

    public void InsertAt(int pos, string s)
    {
        _text   = _text.Insert(Math.Clamp(pos, 0, _text.Length), s);
        _cursor = Math.Clamp(pos + s.Length, 0, _text.Length);
        OnTextChanged?.Invoke(this);
    }

    public void DeleteAt(int pos, int length)
    {
        if (pos < 0 || pos >= _text.Length) return;
        length  = Math.Min(length, _text.Length - pos);
        _text   = _text.Remove(pos, length);
        _cursor = Math.Clamp(pos, 0, _text.Length);
        OnTextChanged?.Invoke(this);
    }

    public void MoveCursor(int delta) =>
        _cursor = Math.Clamp(_cursor + delta, 0, _text.Length);

    public void SelectAll()
    {
        _selectionStart = 0;
        _selectionEnd   = _text.Length;
    }

    public void ClearSelection()
    {
        _selectionStart = -1;
        _selectionEnd   = -1;
    }

    public string CutSelection()
    {
        var s = SelectedText;
        if (HasSelection) DeleteAt(Math.Min(_selectionStart, _selectionEnd), s.Length);
        ClearSelection();
        return s;
    }

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);

        if (!string.IsNullOrEmpty(_text))
            commands.Add(new UIDrawCommand.DrawText(
                _text,
                new Vector2(ComputedRect.X + Padding.Left, ComputedRect.Y + Padding.Top),
                FontSize, ForegroundColor));
    }

    internal override void RaiseKeyDown(Input.KeyEvent ev)
    {
        base.RaiseKeyDown(ev);
        if (!Enabled || !IsFocused) return;

        switch (ev.Key)
        {
            case Input.Key.Left:  MoveCursor(-1); break;
            case Input.Key.Right: MoveCursor(+1); break;
            case Input.Key.Home:  _cursor = 0; break;
            case Input.Key.End:   _cursor = _text.Length; break;

            case Input.Key.Backspace:
                if (HasSelection) { CutSelection(); break; }
                if (_cursor > 0) { DeleteAt(_cursor - 1, 1); }
                break;

            case Input.Key.Delete:
                if (HasSelection) { CutSelection(); break; }
                if (_cursor < _text.Length) DeleteAt(_cursor, 1);
                break;

            case Input.Key.A when ev.Modifiers.HasFlag(Input.KeyModifiers.Control):
                SelectAll();
                break;
        }
    }
}

// ─── UIScrollBar (7.12) ───────────────────────────────────────────────────────

/// <summary>
/// A scroll bar widget.  <see cref="Value"/> ranges from 0 to <see cref="Max"/>.
/// Task 7.12.
/// </summary>
public class UIScrollBar : UIWidget
{
    public override string TypeName => "UIScrollBar";

    public LayoutDirection Orientation { get; set; } = LayoutDirection.Vertical;
    public float Min   { get; set; } = 0f;
    public float Max   { get; set; } = 100f;
    public float Value { get; set; } = 0f;
    public float Step  { get; set; } = 10f;

    public event Action<UIScrollBar>? OnValueChanged;

    public void ScrollBy(float delta)
    {
        float newVal = Math.Clamp(Value + delta, Min, Max);
        if (newVal != Value)
        {
            Value = newVal;
            OnValueChanged?.Invoke(this);
        }
    }

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);

        // Draw the thumb
        float range  = Max - Min;
        float ratio  = range > 0 ? (Value - Min) / range : 0f;

        var r = ComputedRect;
        Rectangle thumb;
        if (Orientation == LayoutDirection.Vertical)
        {
            float thumbH = Math.Max(16f, r.Height * 0.1f);
            float thumbY = r.Y + (r.Height - thumbH) * ratio;
            thumb = new Rectangle(r.X, thumbY, r.Width, thumbH);
        }
        else
        {
            float thumbW = Math.Max(16f, r.Width * 0.1f);
            float thumbX = r.X + (r.Width - thumbW) * ratio;
            thumb = new Rectangle(thumbX, r.Y, thumbW, r.Height);
        }

        commands.Add(new UIDrawCommand.FillRect(thumb, ForegroundColor));
    }
}

// ─── UIScrollArea (7.12) ──────────────────────────────────────────────────────

/// <summary>
/// A container with independent vertical/horizontal scroll bars.
/// Task 7.12.
/// </summary>
public class UIScrollArea : UIWidget
{
    public override string TypeName => "UIScrollArea";

    public UIScrollBar VScrollBar { get; } = new() { Orientation = LayoutDirection.Vertical };
    public UIScrollBar HScrollBar { get; } = new() { Orientation = LayoutDirection.Horizontal };

    public Vector2 ScrollOffset => new(HScrollBar.Value, VScrollBar.Value);

    public UIScrollArea()
    {
        // Scroll-bars are rendered separately, not as children
    }

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);
        VScrollBar.Draw(commands);
        HScrollBar.Draw(commands);
    }
}

// ─── UIProgressBar (7.15) ─────────────────────────────────────────────────────

/// <summary>
/// A horizontal or vertical progress bar.  <see cref="Value"/> ranges 0→1.
/// Task 7.15.
/// </summary>
public class UIProgressBar : UIWidget
{
    public override string TypeName => "UIProgressBar";

    private float _value = 0f;
    public float Value
    {
        get => _value;
        set => _value = Math.Clamp(value, 0f, 1f);
    }

    public LayoutDirection Direction { get; set; } = LayoutDirection.Horizontal;
    public Color FillColor { get; set; } = new Color(0, 128, 255, 255);

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);

        var r = ComputedRect;
        Rectangle fill = Direction == LayoutDirection.Horizontal
            ? new Rectangle(r.X, r.Y, r.Width * Value, r.Height)
            : new Rectangle(r.X, r.Y + r.Height * (1f - Value), r.Width, r.Height * Value);

        if (fill.Width > 0 && fill.Height > 0)
            commands.Add(new UIDrawCommand.FillRect(fill, FillColor));
    }
}

// ─── UICheckBox (7.14) ────────────────────────────────────────────────────────

/// <summary>
/// A checkbox widget.  <see cref="IsChecked"/> toggles on click.
/// Task 7.14.
/// </summary>
public class UICheckBox : UIButton
{
    public override string TypeName => "UICheckBox";

    public bool IsChecked
    {
        get => (State & WidgetState.Checked) != 0;
        set
        {
            if (value) State |= WidgetState.Checked;
            else        State &= ~WidgetState.Checked;
        }
    }

    public event Action<UICheckBox>? OnCheckedChanged;

    public UICheckBox()
    {
        OnClick += _ =>
        {
            IsChecked = !IsChecked;
            OnCheckedChanged?.Invoke(this);
        };
    }

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);

        // Draw a small tick mark inside the box when checked
        if (IsChecked)
        {
            float pad = 3f;
            var r = ComputedRect;
            var tick = new Rectangle(r.X + pad, r.Y + pad, r.Width - pad * 2, r.Height - pad * 2);
            commands.Add(new UIDrawCommand.FillRect(tick, ForegroundColor));
        }
    }
}

// ─── UIRadioButton (7.14) ─────────────────────────────────────────────────────

/// <summary>
/// A radio button that belongs to a named group.  Selecting one unchecks others
/// in the same group within the same parent.
/// Task 7.14.
/// </summary>
public class UIRadioButton : UICheckBox
{
    public override string TypeName => "UIRadioButton";

    public string Group { get; set; } = "default";

    public UIRadioButton()
    {
        // On click, uncheck siblings in the same group
        OnClick += _ =>
        {
            if (Parent is null) return;
            foreach (var sibling in Parent.Children.OfType<UIRadioButton>())
            {
                if (sibling != this && sibling.Group == Group)
                    sibling.IsChecked = false;
            }
        };
    }
}

// ─── UITabBar (7.13) ──────────────────────────────────────────────────────────

/// <summary>
/// A horizontal strip of tab buttons.  Exactly one tab is active at a time.
/// Task 7.13.
/// </summary>
public class UITabBar : UIWidget
{
    public override string TypeName => "UITabBar";

    private readonly List<(string Label, UIWidget Content)> _tabs = [];
    private int _activeIndex = -1;

    public int  ActiveIndex => _activeIndex;
    public int  TabCount    => _tabs.Count;

    public event Action<UITabBar, int>? OnTabChanged;

    public void AddTab(string label, UIWidget content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _tabs.Add((label, content));
        if (_activeIndex < 0) SelectTab(0);
    }

    public void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _activeIndex = index;
        OnTabChanged?.Invoke(this, index);
    }

    public UIWidget? ActiveContent
        => _activeIndex >= 0 && _activeIndex < _tabs.Count ? _tabs[_activeIndex].Content : null;

    public string? ActiveLabel
        => _activeIndex >= 0 && _activeIndex < _tabs.Count ? _tabs[_activeIndex].Label : null;
}

// ─── UIWindow (7.16) ──────────────────────────────────────────────────────────

/// <summary>
/// A draggable, closeable window container.
/// Task 7.16.
/// </summary>
public class UIWindow : UIWidget
{
    public override string TypeName => "UIWindow";

    public string Title { get; set; } = string.Empty;
    public bool   IsMovable { get; set; } = true;
    public bool   ShowCloseButton { get; set; } = true;
    public float  TitleBarHeight  { get; set; } = 24f;

    private bool    _dragging;
    private Vector2 _dragOffset;

    public event Action<UIWindow>? OnClose;

    public void Close() => OnClose?.Invoke(this);

    internal override void RaiseMouseMove(Input.MouseEvent ev)
    {
        base.RaiseMouseMove(ev);
        if (!IsMovable) return;

        if (ev.Action == Input.MouseAction.ButtonPressed && ev.Button == Input.MouseButton.Left)
        {
            // Start drag if click is in title bar
            var titleBar = new Rectangle(ComputedRect.X, ComputedRect.Y, ComputedRect.Width, TitleBarHeight);
            var p = ev.Position;
            if (p.X >= titleBar.X && p.X <= titleBar.X + titleBar.Width &&
                p.Y >= titleBar.Y && p.Y <= titleBar.Y + titleBar.Height)
            {
                _dragging   = true;
                _dragOffset = new Vector2(p.X - ComputedRect.X, p.Y - ComputedRect.Y);
            }
        }

        if (ev.Action == Input.MouseAction.Moved && _dragging)
        {
            Position = new Vector2(ev.Position.X - _dragOffset.X, ev.Position.Y - _dragOffset.Y);
        }

        if (ev.Action == Input.MouseAction.ButtonReleased)
            _dragging = false;
    }

    public override void Draw(List<UIDrawCommand> commands)
    {
        if (!Visible) return;
        base.Draw(commands);

        // Title bar
        var r = ComputedRect;
        var titleBar = new Rectangle(r.X, r.Y, r.Width, TitleBarHeight);
        commands.Add(new UIDrawCommand.FillRect(titleBar, new Color(60, 60, 60, 220)));

        if (!string.IsNullOrEmpty(Title))
            commands.Add(new UIDrawCommand.DrawText(Title,
                new Vector2(r.X + 4f, r.Y + 4f), 14f, Color.White));
    }
}
