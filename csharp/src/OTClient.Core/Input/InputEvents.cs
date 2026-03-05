namespace OTClient.Framework.Input;

/// <summary>
/// Describes the action that triggered a <see cref="KeyEvent"/>.
/// </summary>
public enum KeyAction
{
    /// <summary>The key transitioned from released → pressed this frame.</summary>
    Pressed,

    /// <summary>The key was held long enough to generate a repeat event.</summary>
    Repeated,

    /// <summary>The key transitioned from pressed → released this frame.</summary>
    Released,
}

/// <summary>
/// Strongly-typed keyboard event dispatched by <see cref="InputManager"/>.
/// Maps to the key-event data in <c>src/framework/platform/platformevent.h</c>.
/// </summary>
public readonly record struct KeyEvent(
    Key Key,
    KeyAction Action,
    KeyModifiers Modifiers);

/// <summary>
/// Strongly-typed text-input event carrying a single Unicode code point.
/// Dispatched by <see cref="InputManager"/> using <c>Raylib.GetCharPressed()</c>.
/// </summary>
public readonly record struct TextInputEvent(
    int Codepoint,
    KeyModifiers Modifiers)
{
    /// <summary>Converts the codepoint to a UTF-16 string (length 1 for BMP characters).</summary>
    public string Text => char.ConvertFromUtf32(Codepoint);
}

/// <summary>Mouse button identifiers, mirroring <c>Raylib_cs.MouseButton</c>.</summary>
public enum MouseButton
{
    Left   = 0,
    Right  = 1,
    Middle = 2,
    Side   = 3,
    Extra  = 4,
    Forward = 5,
    Back   = 6,
}

/// <summary>
/// Describes the action that triggered a <see cref="MouseEvent"/>.
/// </summary>
public enum MouseAction
{
    /// <summary>The cursor moved.</summary>
    Moved,

    /// <summary>A mouse button was pressed.</summary>
    ButtonPressed,

    /// <summary>A mouse button was released.</summary>
    ButtonReleased,

    /// <summary>The scroll wheel was moved.</summary>
    WheelMoved,
}

/// <summary>
/// Strongly-typed mouse event dispatched by <see cref="InputManager"/>.
/// Maps to the mouse-event data in <c>src/framework/platform/platformevent.h</c>.
/// </summary>
public readonly record struct MouseEvent(
    MouseAction Action,
    Vector2 Position,
    Vector2 Delta,
    MouseButton Button,
    float WheelDelta,
    KeyModifiers Modifiers);
