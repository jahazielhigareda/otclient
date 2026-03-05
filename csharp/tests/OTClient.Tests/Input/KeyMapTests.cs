using OTClient.Framework.Input;

namespace OTClient.Tests.Input;

/// <summary>
/// Unit tests for <see cref="Key"/> / <see cref="KeyMap"/> — no Raylib window required.
/// </summary>
public sealed class KeyMapTests
{
    // ─── FromRaylib round-trip ────────────────────────────────────────────────

    [Theory]
    [InlineData(Raylib_cs.KeyboardKey.A,       Key.A)]
    [InlineData(Raylib_cs.KeyboardKey.Z,       Key.Z)]
    [InlineData(Raylib_cs.KeyboardKey.Space,   Key.Space)]
    [InlineData(Raylib_cs.KeyboardKey.Escape,  Key.Escape)]
    [InlineData(Raylib_cs.KeyboardKey.Enter,   Key.Enter)]
    [InlineData(Raylib_cs.KeyboardKey.Tab,     Key.Tab)]
    [InlineData(Raylib_cs.KeyboardKey.F1,      Key.F1)]
    [InlineData(Raylib_cs.KeyboardKey.F12,     Key.F12)]
    [InlineData(Raylib_cs.KeyboardKey.Up,      Key.Up)]
    [InlineData(Raylib_cs.KeyboardKey.Down,    Key.Down)]
    [InlineData(Raylib_cs.KeyboardKey.Left,    Key.Left)]
    [InlineData(Raylib_cs.KeyboardKey.Right,   Key.Right)]
    [InlineData(Raylib_cs.KeyboardKey.Kp0,     Key.Kp0)]
    [InlineData(Raylib_cs.KeyboardKey.LeftShift,   Key.LeftShift)]
    [InlineData(Raylib_cs.KeyboardKey.RightControl, Key.RightControl)]
    public void FromRaylib_KnownKeys_ReturnCorrectOtcKey(Raylib_cs.KeyboardKey rk, Key expected)
    {
        Assert.Equal(expected, KeyMap.FromRaylib(rk));
    }

    [Fact]
    public void FromRaylib_UnknownKey_ReturnsUnknown()
    {
        // Use a value that is not defined in the Key enum.
        var result = KeyMap.FromRaylib((Raylib_cs.KeyboardKey)9999);
        Assert.Equal(Key.Unknown, result);
    }

    // ─── ToRaylib round-trip ──────────────────────────────────────────────────

    [Theory]
    [InlineData(Key.A,       Raylib_cs.KeyboardKey.A)]
    [InlineData(Key.Escape,  Raylib_cs.KeyboardKey.Escape)]
    [InlineData(Key.Enter,   Raylib_cs.KeyboardKey.Enter)]
    [InlineData(Key.Space,   Raylib_cs.KeyboardKey.Space)]
    public void ToRaylib_KnownKey_ReturnCorrectRaylibKey(Key key, Raylib_cs.KeyboardKey expected)
    {
        Assert.Equal(expected, KeyMap.ToRaylib(key));
    }

    [Fact]
    public void ToRaylib_UnknownKey_ReturnsNull()
    {
        Assert.Equal(Raylib_cs.KeyboardKey.Null, KeyMap.ToRaylib(Key.Unknown));
    }

    // ─── Symmetry ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Space)]
    [InlineData(Key.Escape)]
    [InlineData(Key.F5)]
    [InlineData(Key.LeftControl)]
    public void RoundTrip_FromRaylib_ToRaylib_IsSymmetric(Key key)
    {
        var rk     = KeyMap.ToRaylib(key);
        var result = KeyMap.FromRaylib(rk);
        Assert.Equal(key, result);
    }

    // ─── KeyModifiers flag combinations ──────────────────────────────────────

    [Fact]
    public void KeyModifiers_None_IsZero()
    {
        Assert.Equal(0, (int)KeyModifiers.None);
    }

    [Fact]
    public void KeyModifiers_CanCombineShiftAndControl()
    {
        var combined = KeyModifiers.Shift | KeyModifiers.Control;
        Assert.True(combined.HasFlag(KeyModifiers.Shift));
        Assert.True(combined.HasFlag(KeyModifiers.Control));
        Assert.False(combined.HasFlag(KeyModifiers.Alt));
        Assert.False(combined.HasFlag(KeyModifiers.Super));
    }

    [Fact]
    public void KeyModifiers_AllFlagsCanBeSet()
    {
        var all = KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Super;
        Assert.True(all.HasFlag(KeyModifiers.Shift));
        Assert.True(all.HasFlag(KeyModifiers.Control));
        Assert.True(all.HasFlag(KeyModifiers.Alt));
        Assert.True(all.HasFlag(KeyModifiers.Super));
    }

    // ─── Full key coverage spot-checks ───────────────────────────────────────

    [Theory]
    [InlineData(Key.D0, Raylib_cs.KeyboardKey.Zero)]
    [InlineData(Key.D9, Raylib_cs.KeyboardKey.Nine)]
    [InlineData(Key.Backspace, Raylib_cs.KeyboardKey.Backspace)]
    [InlineData(Key.Delete,    Raylib_cs.KeyboardKey.Delete)]
    [InlineData(Key.Insert,    Raylib_cs.KeyboardKey.Insert)]
    [InlineData(Key.Home,      Raylib_cs.KeyboardKey.Home)]
    [InlineData(Key.End,       Raylib_cs.KeyboardKey.End)]
    [InlineData(Key.PageUp,    Raylib_cs.KeyboardKey.PageUp)]
    [InlineData(Key.PageDown,  Raylib_cs.KeyboardKey.PageDown)]
    [InlineData(Key.CapsLock,  Raylib_cs.KeyboardKey.CapsLock)]
    [InlineData(Key.LeftAlt,   Raylib_cs.KeyboardKey.LeftAlt)]
    [InlineData(Key.RightAlt,  Raylib_cs.KeyboardKey.RightAlt)]
    [InlineData(Key.KpEnter,   Raylib_cs.KeyboardKey.KpEnter)]
    [InlineData(Key.KpDecimal, Raylib_cs.KeyboardKey.KpDecimal)]
    [InlineData(Key.Menu,      Raylib_cs.KeyboardKey.KeyboardMenu)]
    public void ToRaylib_AdditionalKeys_ReturnCorrectRaylibKey(Key key, Raylib_cs.KeyboardKey expected)
    {
        Assert.Equal(expected, KeyMap.ToRaylib(key));
    }
}
