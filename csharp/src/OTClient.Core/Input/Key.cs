using Raylib_cs;

namespace OTClient.Framework.Input;

/// <summary>
/// OTClient-level keyboard key codes, mapped from Raylib's
/// <see cref="KeyboardKey"/> enum.
/// <para>
/// Values intentionally mirror the Raylib numeric codes so that the
/// conversion in <see cref="KeyMap.FromRaylib"/> is a single lookup
/// with no allocation.
/// </para>
/// Maps to the key-code enum in <c>src/framework/platform/platformevent.h</c>.
/// </summary>
public enum Key
{
    Unknown = 0,

    // ─── Printable ────────────────────────────────────────────────────────────
    Space        = 32,
    Apostrophe   = 39,
    Comma        = 44,
    Minus        = 45,
    Period       = 46,
    Slash        = 47,

    D0 = 48, D1 = 49, D2 = 50, D3 = 51, D4 = 52,
    D5 = 53, D6 = 54, D7 = 55, D8 = 56, D9 = 57,

    Semicolon    = 59,
    Equal        = 61,

    A = 65, B = 66, C = 67, D = 68, E = 69, F = 70,
    G = 71, H = 72, I = 73, J = 74, K = 75, L = 76,
    M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82,
    S = 83, T = 84, U = 85, V = 86, W = 87, X = 88,
    Y = 89, Z = 90,

    LeftBracket  = 91,
    Backslash    = 92,
    RightBracket = 93,
    GraveAccent  = 96,

    // ─── Function / control ───────────────────────────────────────────────────
    Escape       = 256,
    Enter        = 257,
    Tab          = 258,
    Backspace    = 259,
    Insert       = 260,
    Delete       = 261,
    Right        = 262,
    Left         = 263,
    Down         = 264,
    Up           = 265,
    PageUp       = 266,
    PageDown     = 267,
    Home         = 268,
    End          = 269,
    CapsLock     = 280,
    ScrollLock   = 281,
    NumLock      = 282,
    PrintScreen  = 283,
    Pause        = 284,

    F1  = 290, F2  = 291, F3  = 292, F4  = 293, F5  = 294,
    F6  = 295, F7  = 296, F8  = 297, F9  = 298, F10 = 299,
    F11 = 300, F12 = 301,

    // ─── Numpad ───────────────────────────────────────────────────────────────
    Kp0 = 320, Kp1 = 321, Kp2 = 322, Kp3 = 323, Kp4 = 324,
    Kp5 = 325, Kp6 = 326, Kp7 = 327, Kp8 = 328, Kp9 = 329,
    KpDecimal  = 330,
    KpDivide   = 331,
    KpMultiply = 332,
    KpSubtract = 333,
    KpAdd      = 334,
    KpEnter    = 335,
    KpEqual    = 336,

    // ─── Modifiers ────────────────────────────────────────────────────────────
    LeftShift    = 340,
    LeftControl  = 341,
    LeftAlt      = 342,
    LeftSuper    = 343,
    RightShift   = 344,
    RightControl = 345,
    RightAlt     = 346,
    RightSuper   = 347,
    Menu         = 348,
}

/// <summary>
/// Modifier-key flags that can be combined (e.g. Ctrl+Shift).
/// </summary>
[Flags]
public enum KeyModifiers
{
    None    = 0,
    Shift   = 1 << 0,
    Control = 1 << 1,
    Alt     = 1 << 2,
    Super   = 1 << 3,
}

/// <summary>
/// Utility that converts between Raylib's <see cref="KeyboardKey"/> and the
/// OTClient <see cref="Key"/> enum.  Because both enums share the same numeric
/// values the conversion is a simple cast guarded by a range check.
/// </summary>
public static class KeyMap
{
    /// <summary>
    /// Converts a Raylib <see cref="KeyboardKey"/> to an OTClient <see cref="Key"/>.
    /// Returns <see cref="Key.Unknown"/> for keys that have no OTClient mapping.
    /// </summary>
    public static Key FromRaylib(KeyboardKey rk)
    {
        int code = (int)rk;
        return Enum.IsDefined(typeof(Key), code) ? (Key)code : Key.Unknown;
    }

    /// <summary>
    /// Converts an OTClient <see cref="Key"/> to a Raylib <see cref="KeyboardKey"/>.
    /// Returns <see cref="KeyboardKey.Null"/> for unknown keys.
    /// </summary>
    public static KeyboardKey ToRaylib(Key key)
    {
        int code = (int)key;
        return Enum.IsDefined(typeof(KeyboardKey), code) ? (KeyboardKey)code : KeyboardKey.Null;
    }

    /// <summary>
    /// Samples the current modifier state from Raylib.
    /// </summary>
    public static KeyModifiers CurrentModifiers()
    {
        KeyModifiers m = KeyModifiers.None;
        if (Raylib.IsKeyDown(KeyboardKey.LeftShift)   || Raylib.IsKeyDown(KeyboardKey.RightShift))   m |= KeyModifiers.Shift;
        if (Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl)) m |= KeyModifiers.Control;
        if (Raylib.IsKeyDown(KeyboardKey.LeftAlt)     || Raylib.IsKeyDown(KeyboardKey.RightAlt))     m |= KeyModifiers.Alt;
        if (Raylib.IsKeyDown(KeyboardKey.LeftSuper)   || Raylib.IsKeyDown(KeyboardKey.RightSuper))   m |= KeyModifiers.Super;
        return m;
    }
}
