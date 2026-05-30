using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VoicePipe.UI;

/// <summary>
/// 频谱条形可视化控件：接收 float[]（每条 0~1）绘制柱状频谱。
/// 画刷缓存 + 圆角矩形，随主题色（LineBrush）变化。
/// </summary>
public class SpectrumControl : Control
{
    private Brush? _cachedBrushRef;
    private Brush? _cachedFill;

    public static readonly DependencyProperty SpectrumDataProperty =
        DependencyProperty.Register(nameof(SpectrumData), typeof(float[]),
            typeof(SpectrumControl),
            new FrameworkPropertyMetadata(Array.Empty<float>(),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public float[] SpectrumData
    {
        get => (float[])GetValue(SpectrumDataProperty);
        set => SetValue(SpectrumDataProperty, value);
    }

    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(nameof(LineBrush), typeof(Brush),
            typeof(SpectrumControl),
            new FrameworkPropertyMetadata(Brushes.White,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Background ?? Brushes.Transparent, null, new Rect(0, 0, w, h));

        var data = SpectrumData;
        if (data == null || data.Length == 0) return;

        var brush = LineBrush ?? Brushes.White;
        if (_cachedFill == null || !ReferenceEquals(_cachedBrushRef, brush))
        {
            _cachedFill = brush;
            _cachedBrushRef = brush;
        }

        int n = data.Length;
        double gap = 2.0;
        double barW = (w - gap * (n - 1)) / n;
        if (barW < 1) barW = 1;

        for (int i = 0; i < n; i++)
        {
            double v = data[i];
            if (v < 0) v = 0; else if (v > 1) v = 1;
            double barH = v * h;
            double x = i * (barW + gap);
            double y = h - barH;
            // 圆角柱
            double r = System.Math.Min(barW / 2, 2);
            dc.DrawRoundedRectangle(_cachedFill, null, new Rect(x, y, barW, barH), r, r);
        }
    }
}
