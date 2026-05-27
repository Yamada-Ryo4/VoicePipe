using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VoicePipe.UI;

/// <summary>
/// 波形可视化控件：接收 float[] 波形数据，绘制折线图。
/// </summary>
public class WaveformControl : Control
{
    public static readonly DependencyProperty WaveformDataProperty =
        DependencyProperty.Register(nameof(WaveformData), typeof(float[]),
            typeof(WaveformControl),
            new FrameworkPropertyMetadata(Array.Empty<float>(),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public float[] WaveformData
    {
        get => (float[])GetValue(WaveformDataProperty);
        set => SetValue(WaveformDataProperty, value);
    }

    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(nameof(LineBrush), typeof(Brush),
            typeof(WaveformControl),
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

        // 背景
        dc.DrawRectangle(Background ?? Brushes.Transparent, null,
            new Rect(0, 0, ActualWidth, ActualHeight));

        var data = WaveformData;
        if (data == null || data.Length < 2) return;

        double w = ActualWidth;
        double h = ActualHeight;
        double midY = h / 2;
        double scaleX = w / data.Length;

        // 使用 LineBrush 或默认颜色
        var gradPen = new Pen(LineBrush ?? Brushes.White, 1.5);
        // 如果 Brush 是被冻结的，这里就不需要重复冻结了，或者可以尝试 Clone 后冻结
        // gradPen.Freeze();

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, midY - data[0] * midY * 0.9), false, false);
            for (int i = 1; i < data.Length; i++)
            {
                double x = i * scaleX;
                double y = midY - data[i] * midY * 0.9;
                ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geometry.Freeze();

        // 中心线 (稍微浅一些的颜色)
        var centerLinePen = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), 0.5);
        if (centerLinePen.CanFreeze) centerLinePen.Freeze();
        dc.DrawLine(centerLinePen, new Point(0, midY), new Point(w, midY));

        dc.DrawGeometry(null, gradPen, geometry);
    }
}
