using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VoicePipe.UI;

/// <summary>
/// 自定义 Slider：点击轨道任意位置后滑块立即跳过去，
/// 并且无需松开鼠标即可直接开始拖动（click-to-jump + immediate drag）。
/// </summary>
public class SmoothSlider : Slider
{
    private bool _isDragging;

    static SmoothSlider()
    {
        // 让 SmoothSlider 继承 Slider 的隐式样式（包括自定义模板）
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SmoothSlider),
            new FrameworkPropertyMetadata(typeof(Slider)));
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // 如果点击的是 Thumb 本身，走默认拖拽逻辑
        if (e.OriginalSource is FrameworkElement fe && IsPartOfThumb(fe))
        {
            base.OnPreviewMouseLeftButtonDown(e);
            return;
        }

        // 点击轨道：跳到点击位置 + 立即开始拖拽
        JumpToMousePosition(e);
        CaptureMouse();
        _isDragging = true;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            JumpToMousePosition(e);
            e.Handled = true;
        }
        else
        {
            base.OnMouseMove(e);
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
        else
        {
            base.OnPreviewMouseLeftButtonUp(e);
        }
    }

    private void JumpToMousePosition(MouseEventArgs e)
    {
        Point pos = e.GetPosition(this);
        double ratio = pos.X / ActualWidth;
        ratio = Math.Max(0, Math.Min(1, ratio));
        Value = Minimum + (Maximum - Minimum) * ratio;
    }

    private static bool IsPartOfThumb(DependencyObject obj)
    {
        while (obj != null)
        {
            if (obj is System.Windows.Controls.Primitives.Thumb)
                return true;
            obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
        }
        return false;
    }
}
