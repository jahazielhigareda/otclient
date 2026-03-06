using System.Numerics;
using OTClient.Framework.UI;
using Raylib_cs;
using Xunit;

namespace OTClient.Tests.UI;

/// <summary>
/// Tests for concrete widget types: UILabel, UIButton, UITextEdit,
/// UIScrollBar, UIProgressBar, UICheckBox, UIWindow, UITabBar,
/// and the UIWidgetFactory.
/// Tasks 7.9–7.17, 7.24.
/// </summary>
public sealed class UIWidgetTypeTests
{
    private static Rectangle Screen => new(0, 0, 800, 600);

    // ─── UILabel ─────────────────────────────────────────────────────────────

    [Fact]
    public void UILabel_TypeName_IsUILabel()
    {
        Assert.Equal("UILabel", new UILabel().TypeName);
    }

    [Fact]
    public void UILabel_Draw_EmitsTextCommand()
    {
        var l = new UILabel { Text = "Hello", Size = new Vector2(200, 30) };
        l.Layout(Screen);
        var cmds = new List<UIDrawCommand>();
        l.Draw(cmds);
        Assert.Contains(cmds, c => c is UIDrawCommand.DrawText dt && dt.Text == "Hello");
    }

    [Fact]
    public void UILabel_EmptyText_NoTextCommand()
    {
        var l = new UILabel { Text = "", Size = new Vector2(200, 30) };
        l.Layout(Screen);
        var cmds = new List<UIDrawCommand>();
        l.Draw(cmds);
        Assert.DoesNotContain(cmds, c => c is UIDrawCommand.DrawText);
    }

    // ─── UIButton ─────────────────────────────────────────────────────────────

    [Fact]
    public void UIButton_TypeName_IsUIButton()
    {
        Assert.Equal("UIButton", new UIButton().TypeName);
    }

    [Fact]
    public void UIButton_Click_RaisesOnClick()
    {
        var btn = new UIButton();
        bool fired = false;
        btn.OnClick += _ => fired = true;
        btn.RaiseClick();
        Assert.True(fired);
    }

    [Fact]
    public void UIButton_Disabled_DoesNotRaiseClick()
    {
        var btn = new UIButton { Enabled = false };
        bool fired = false;
        btn.OnClick += _ => fired = true;
        btn.RaiseClick();
        Assert.False(fired);
    }

    // ─── UITextEdit ──────────────────────────────────────────────────────────

    [Fact]
    public void UITextEdit_TypeName_IsUITextEdit()
    {
        Assert.Equal("UITextEdit", new UITextEdit().TypeName);
    }

    [Fact]
    public void UITextEdit_InsertAt_AppendsText()
    {
        var te = new UITextEdit();
        te.InsertAt(0, "Hello");
        Assert.Equal("Hello", te.Text);
    }

    [Fact]
    public void UITextEdit_InsertAt_AtCursor()
    {
        var te = new UITextEdit { Text = "Helo" };
        te.MoveCursor(3);
        te.InsertAt(3, "l");
        Assert.Equal("Hello", te.Text);
    }

    [Fact]
    public void UITextEdit_DeleteAt_RemovesCharacter()
    {
        var te = new UITextEdit { Text = "Hello" };
        te.DeleteAt(1, 1);
        Assert.Equal("Hllo", te.Text);
    }

    [Fact]
    public void UITextEdit_MoveCursor_ClampedToRange()
    {
        var te = new UITextEdit { Text = "Hi" };
        te.MoveCursor(100);
        Assert.Equal(2, te.CursorPosition);
    }

    [Fact]
    public void UITextEdit_MoveCursorNegative_ClampedToZero()
    {
        var te = new UITextEdit { Text = "Hi" };
        te.MoveCursor(-100);
        Assert.Equal(0, te.CursorPosition);
    }

    [Fact]
    public void UITextEdit_SelectAll_SelectsEverything()
    {
        var te = new UITextEdit { Text = "Hello" };
        te.SelectAll();
        Assert.True(te.HasSelection);
        Assert.Equal("Hello", te.SelectedText);
    }

    [Fact]
    public void UITextEdit_CutSelection_ClearsText()
    {
        var te = new UITextEdit { Text = "Hello" };
        te.SelectAll();
        var cut = te.CutSelection();
        Assert.Equal("Hello", cut);
        Assert.Empty(te.Text);
    }

    [Fact]
    public void UITextEdit_OnTextChanged_RaisedOnInsert()
    {
        var te = new UITextEdit();
        bool raised = false;
        te.OnTextChanged += _ => raised = true;
        te.InsertAt(0, "x");
        Assert.True(raised);
    }

    [Fact]
    public void UITextEdit_Draw_EmitsTextWhenNotEmpty()
    {
        var te = new UITextEdit { Text = "Abc", Size = new Vector2(200, 30) };
        te.Layout(Screen);
        var cmds = new List<UIDrawCommand>();
        te.Draw(cmds);
        Assert.Contains(cmds, c => c is UIDrawCommand.DrawText dt && dt.Text == "Abc");
    }

    // ─── UIScrollBar ─────────────────────────────────────────────────────────

    [Fact]
    public void UIScrollBar_TypeName_IsUIScrollBar()
    {
        Assert.Equal("UIScrollBar", new UIScrollBar().TypeName);
    }

    [Fact]
    public void UIScrollBar_ScrollBy_ClampsToRange()
    {
        var sb = new UIScrollBar { Min = 0, Max = 100, Value = 90 };
        sb.ScrollBy(20);
        Assert.Equal(100f, sb.Value);
    }

    [Fact]
    public void UIScrollBar_ScrollBy_NegativeClampsToMin()
    {
        var sb = new UIScrollBar { Min = 0, Max = 100, Value = 5 };
        sb.ScrollBy(-20);
        Assert.Equal(0f, sb.Value);
    }

    [Fact]
    public void UIScrollBar_OnValueChanged_RaisedOnScroll()
    {
        var sb = new UIScrollBar { Value = 0, Max = 100 };
        bool raised = false;
        sb.OnValueChanged += _ => raised = true;
        sb.ScrollBy(10);
        Assert.True(raised);
    }

    [Fact]
    public void UIScrollBar_OnValueChanged_NotRaisedWhenAtMax()
    {
        var sb = new UIScrollBar { Value = 100, Max = 100 };
        bool raised = false;
        sb.OnValueChanged += _ => raised = true;
        sb.ScrollBy(10);
        Assert.False(raised);
    }

    // ─── UIProgressBar ───────────────────────────────────────────────────────

    [Fact]
    public void UIProgressBar_TypeName_IsUIProgressBar()
    {
        Assert.Equal("UIProgressBar", new UIProgressBar().TypeName);
    }

    [Fact]
    public void UIProgressBar_Value_ClampedTo01()
    {
        var pb = new UIProgressBar { Value = 2f };
        Assert.Equal(1f, pb.Value);
        pb.Value = -1f;
        Assert.Equal(0f, pb.Value);
    }

    [Fact]
    public void UIProgressBar_Draw_EmitsFillRect_WhenValuePositive()
    {
        var pb = new UIProgressBar { Value = 0.5f, Size = new Vector2(200, 20) };
        pb.Layout(Screen);
        var cmds = new List<UIDrawCommand>();
        pb.Draw(cmds);
        Assert.Contains(cmds, c => c is UIDrawCommand.FillRect);
    }

    // ─── UICheckBox ──────────────────────────────────────────────────────────

    [Fact]
    public void UICheckBox_TypeName_IsUICheckBox()
    {
        Assert.Equal("UICheckBox", new UICheckBox().TypeName);
    }

    [Fact]
    public void UICheckBox_Click_TogglesIsChecked()
    {
        var cb = new UICheckBox();
        Assert.False(cb.IsChecked);
        cb.RaiseClick();
        Assert.True(cb.IsChecked);
        cb.RaiseClick();
        Assert.False(cb.IsChecked);
    }

    [Fact]
    public void UICheckBox_OnCheckedChanged_Raised()
    {
        var cb = new UICheckBox();
        bool raised = false;
        cb.OnCheckedChanged += _ => raised = true;
        cb.RaiseClick();
        Assert.True(raised);
    }

    // ─── UIRadioButton ───────────────────────────────────────────────────────

    [Fact]
    public void UIRadioButton_SelectOne_UnchecksOtherInGroup()
    {
        var parent = new UIWidget();
        var r1 = new UIRadioButton { Group = "grp" };
        var r2 = new UIRadioButton { Group = "grp" };
        parent.AddChild(r1);
        parent.AddChild(r2);

        r1.RaiseClick(); // checks r1
        r2.RaiseClick(); // should uncheck r1

        Assert.False(r1.IsChecked);
        Assert.True(r2.IsChecked);
    }

    // ─── UITabBar ────────────────────────────────────────────────────────────

    [Fact]
    public void UITabBar_TypeName_IsUITabBar()
    {
        Assert.Equal("UITabBar", new UITabBar().TypeName);
    }

    [Fact]
    public void UITabBar_AddTab_IncrementsTabCount()
    {
        var tb = new UITabBar();
        tb.AddTab("Tab1", new UIWidget());
        tb.AddTab("Tab2", new UIWidget());
        Assert.Equal(2, tb.TabCount);
    }

    [Fact]
    public void UITabBar_FirstAddedTab_IsActive()
    {
        var tb = new UITabBar();
        var w = new UIWidget();
        tb.AddTab("T1", w);
        Assert.Same(w, tb.ActiveContent);
    }

    [Fact]
    public void UITabBar_SelectTab_ChangesActive()
    {
        var tb = new UITabBar();
        tb.AddTab("T1", new UIWidget());
        var w2 = new UIWidget();
        tb.AddTab("T2", w2);
        tb.SelectTab(1);
        Assert.Same(w2, tb.ActiveContent);
    }

    [Fact]
    public void UITabBar_OnTabChanged_Raised()
    {
        var tb = new UITabBar();
        tb.AddTab("T1", new UIWidget());
        tb.AddTab("T2", new UIWidget());
        int changedIndex = -1;
        tb.OnTabChanged += (_, i) => changedIndex = i;
        tb.SelectTab(1);
        Assert.Equal(1, changedIndex);
    }

    // ─── UIWindow ────────────────────────────────────────────────────────────

    [Fact]
    public void UIWindow_TypeName_IsUIWindow()
    {
        Assert.Equal("UIWindow", new UIWindow().TypeName);
    }

    [Fact]
    public void UIWindow_Close_RaisesOnClose()
    {
        var w = new UIWindow();
        bool closed = false;
        w.OnClose += _ => closed = true;
        w.Close();
        Assert.True(closed);
    }

    [Fact]
    public void UIWindow_Draw_EmitsTitleBarCommands()
    {
        var w = new UIWindow { Title = "My Window", Size = new Vector2(300, 200) };
        w.Layout(Screen);
        var cmds = new List<UIDrawCommand>();
        w.Draw(cmds);
        Assert.Contains(cmds, c => c is UIDrawCommand.DrawText dt && dt.Text == "My Window");
    }
}
