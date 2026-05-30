using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VoicePipe.Core;

namespace VoicePipe.UI;

/// <summary>
/// 录制全局热键组合的按钮控件。点击进入录制模式，按下非修饰键即捕获一个
/// <see cref="HotkeyBinding"/>（修饰键 + 主键，存储为 Win32 MOD_*/VK 原始码）。
/// 录制中按 Delete/Backspace 清除绑定。HasConflict 为 true 时显示红色冲突视觉。
///
/// 显示文本：录制中 → StrHotkeyRecording；未设置 → StrHotkeyNone；
/// 已设置 → 人类可读组合（如 "Ctrl+Shift+M"）。
/// </summary>
public class HotkeyCaptureControl : Button
{
    // Win32 MOD_* 修饰标志
    private const uint MOD_ALT = 1u;
    private const uint MOD_CONTROL = 2u;
    private const uint MOD_SHIFT = 4u;
    private const uint MOD_WIN = 8u;

    private bool _recording;

    public static readonly DependencyProperty BindingProperty =
        DependencyProperty.Register(
            nameof(Binding),
            typeof(HotkeyBinding),
            typeof(HotkeyCaptureControl),
            new FrameworkPropertyMetadata(
                HotkeyBinding.None,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBindingChanged));

    /// <summary>当前绑定的热键（双向绑定）。</summary>
    public HotkeyBinding Binding
    {
        get => (HotkeyBinding)GetValue(BindingProperty);
        set => SetValue(BindingProperty, value);
    }

    public static readonly DependencyProperty HasConflictProperty =
        DependencyProperty.Register(
            nameof(HasConflict),
            typeof(bool),
            typeof(HotkeyCaptureControl),
            new FrameworkPropertyMetadata(false, OnHasConflictChanged));

    /// <summary>是否与其他应用/动作冲突（由设置面板绑定设置）。</summary>
    public bool HasConflict
    {
        get => (bool)GetValue(HasConflictProperty);
        set => SetValue(HasConflictProperty, value);
    }

    public HotkeyCaptureControl()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        Loaded += (_, _) => UpdateText();
    }

    private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HotkeyCaptureControl)d).UpdateText();

    private static void OnHasConflictChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HotkeyCaptureControl)d).UpdateConflictVisual();

    protected override void OnClick()
    {
        base.OnClick();
        BeginRecording();
    }

    private void BeginRecording()
    {
        if (_recording) return;
        _recording = true;
        Keyboard.Focus(this);
        UpdateText();
    }

    private void EndRecording()
    {
        if (!_recording) return;
        _recording = false;
        UpdateText();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_recording)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 纯修饰键：继续等待主键
        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        // Delete / Backspace：清除绑定
        if (key == Key.Delete || key == Key.Back)
        {
            Binding = HotkeyBinding.None;
            EndRecording();
            e.Handled = true;
            return;
        }

        // Escape：取消录制，保留原绑定
        if (key == Key.Escape)
        {
            EndRecording();
            e.Handled = true;
            return;
        }

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            e.Handled = true;
            return;
        }

        uint mods = 0u;
        var m = Keyboard.Modifiers;
        if ((m & ModifierKeys.Alt) != 0) mods |= MOD_ALT;
        if ((m & ModifierKeys.Control) != 0) mods |= MOD_CONTROL;
        if ((m & ModifierKeys.Shift) != 0) mods |= MOD_SHIFT;
        if ((m & ModifierKeys.Windows) != 0) mods |= MOD_WIN;

        Binding = new HotkeyBinding(mods, vk);
        EndRecording();
        e.Handled = true;
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        EndRecording();
    }

    /// <summary>外部清除绑定（如配套的"清除"按钮调用）。</summary>
    public void ClearBinding()
    {
        Binding = HotkeyBinding.None;
        EndRecording();
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System;

    private void UpdateText()
    {
        if (_recording)
        {
            Content = TryStr("StrHotkeyRecording", "Press keys...");
            return;
        }

        Content = Binding.IsSet
            ? FormatBinding(Binding)
            : TryStr("StrHotkeyNone", "Not set");
    }

    private void UpdateConflictVisual()
    {
        // 冲突时用红色（ClipBrush）；正常时恢复成主题标准画刷，而非缓存的旧值，
        // 这样主题切换 / 首次渲染前置位都能正确显示，不会残留旧主题颜色。(B5)
        if (HasConflict)
        {
            var red = (Application.Current?.TryFindResource("ClipBrush") as Brush)
                      ?? new SolidColorBrush(Color.FromRgb(0xE0, 0x00, 0x00));
            BorderBrush = red;
            Foreground = red;
            ToolTip = TryStr("StrHotkeyConflict", "Conflict — already in use");
        }
        else
        {
            BorderBrush = Application.Current?.TryFindResource("BorderBrush") as Brush ?? BorderBrush;
            Foreground = Application.Current?.TryFindResource("TextPrimaryBrush") as Brush ?? Foreground;
            ToolTip = null;
        }
    }

    /// <summary>把 Win32 MOD_*/VK 码格式化为人类可读组合，如 "Ctrl+Shift+M"。</summary>
    private static string FormatBinding(HotkeyBinding b)
    {
        var sb = new StringBuilder();
        // 稳定顺序：Ctrl + Alt + Shift + Win + 主键
        if ((b.Modifiers & MOD_CONTROL) != 0) sb.Append("Ctrl+");
        if ((b.Modifiers & MOD_ALT) != 0) sb.Append("Alt+");
        if ((b.Modifiers & MOD_SHIFT) != 0) sb.Append("Shift+");
        if ((b.Modifiers & MOD_WIN) != 0) sb.Append("Win+");

        Key key = KeyInterop.KeyFromVirtualKey((int)b.Key);
        sb.Append(key.ToString());
        return sb.ToString();
    }

    private static string TryStr(string key, string fallback)
        => Application.Current?.TryFindResource(key) as string ?? fallback;
}
