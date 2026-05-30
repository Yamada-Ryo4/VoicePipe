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
    // ★ 缓存的画笔/画刷，避免每帧 OnRender 都 new（每秒 20~30 次重建 = GC 压力）
    private static readonly Pen _centerLinePen;
    private Pen? _cachedLinePen;
    private Brush? _cachedLineBrushRef;

    static WaveformControl()
    {
        _centerLinePen = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), 0.5);
        _centerLinePen.Freeze();
    }

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

        // ★ 波形画笔：仅当 LineBrush 引用变化时才重建并冻结，否则复用缓存
        var lineBrush = LineBrush ?? Brushes.White;
        if (_cachedLinePen == null || !ReferenceEquals(_cachedLineBrushRef, lineBrush))
        {
            _cachedLinePen = new Pen(lineBrush, 1.5);
            if (_cachedLinePen.CanFreeze) _cachedLinePen.Freeze();
            _cachedLineBrushRef = lineBrush;
        }
        var gradPen = _cachedLinePen;

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

        // ★ 中心线用缓存的冻结画笔
        dc.DrawLine(_centerLinePen, new Point(0, midY), new Point(w, midY));

        dc.DrawGeometry(null, gradPen, geometry);
    }
}
