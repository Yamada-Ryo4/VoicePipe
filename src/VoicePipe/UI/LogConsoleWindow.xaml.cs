using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Serilog.Events;
using VoicePipe.Sinks;

namespace VoicePipe.UI;

public partial class LogConsoleWindow : Window
{
    private int _lineCount = 0;
    private const int MaxLines = 2000;

    // 日志级别 → 颜色
    private static readonly Dictionary<LogEventLevel, Brush> LevelColors = new()
    {
        [LogEventLevel.Verbose]     = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
        [LogEventLevel.Debug]       = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
        [LogEventLevel.Information] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
        [LogEventLevel.Warning]     = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
        [LogEventLevel.Error]       = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)),
        [LogEventLevel.Fatal]       = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)),
    };

    public LogConsoleWindow()
    {
        InitializeComponent();

        // 回填历史记录
        foreach (var (text, level) in InMemoryLogSink.GetHistory())
            AppendLine(text, level);

        // 订阅新日志
        InMemoryLogSink.OnLogEmitted += OnLogEmitted;
    }

    private void OnLogEmitted(string text, LogEventLevel level)
    {
        // 从任意线程调用，必须派发到 UI 线程
        Dispatcher.InvokeAsync(() => AppendLine(text, level));
    }

    private void AppendLine(string text, LogEventLevel level)
    {
        var color = LevelColors.TryGetValue(level, out var b) ? b : Brushes.LightGray;

        var para = new Paragraph(new Run(text))
        {
            Foreground      = color,
            Margin          = new Thickness(0),
            LineHeight      = double.NaN,
            FontWeight      = level >= LogEventLevel.Error ? FontWeights.SemiBold : FontWeights.Normal,
        };

        LogBox.Document.Blocks.Add(para);
        _lineCount++;

        // 超出上限时从头删除
        if (_lineCount > MaxLines)
        {
            LogBox.Document.Blocks.Remove(LogBox.Document.Blocks.FirstBlock);
            _lineCount--;
        }

        // 自动滚动
        if (CbAutoScroll.IsChecked == true)
            Scroller.ScrollToEnd();

        TbStatus.Text = $"{_lineCount} 行";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Document.Blocks.Clear();
        _lineCount = 0;
        TbStatus.Text = "0 行";
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        var all = string.Join("\n",
            LogBox.Document.Blocks.OfType<Paragraph>()
                  .Select(p => new TextRange(p.ContentStart, p.ContentEnd).Text));
        if (!string.IsNullOrEmpty(all))
            Clipboard.SetText(all);
    }

    private void CbTopmost_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = CbTopmost.IsChecked == true;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // 取消订阅，防止向已关闭的窗口派发
        InMemoryLogSink.OnLogEmitted -= OnLogEmitted;
    }
}
