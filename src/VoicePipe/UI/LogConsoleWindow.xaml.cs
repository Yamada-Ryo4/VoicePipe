using System.Text;
using System.Windows;
using Serilog.Events;
using VoicePipe.Sinks;

namespace VoicePipe.UI;

/// <summary>
/// 实时日志控制台。
///
/// 性能：用纯文本 <see cref="System.Windows.Controls.TextBox"/>（单一文本缓冲）替代原 RichTextBox
/// （每行一个 Paragraph，数千行时内存/渲染开销大）。日志级别靠行首 [INF]/[WRN]/[ERR] 前缀区分，
/// 不再为每行构建富文本段落，几千行也流畅。
///
/// 行数上限 <see cref="MaxLines"/>：超出时整体重建一次文本（低频，批量裁剪比逐行删段落便宜）。
/// </summary>
public partial class LogConsoleWindow : Window
{
    private int _lineCount = 0;
    private const int MaxLines = 2000;

    public LogConsoleWindow()
    {
        InitializeComponent();

        // 回填历史记录：一次性拼好整段文本，避免逐行触发 TextBox 重排
        var history = InMemoryLogSink.GetHistory();
        if (history.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var (text, _) in history) sb.AppendLine(text);
            LogBox.Text = sb.ToString();
            _lineCount = history.Count;
            TbStatus.Text = $"{_lineCount} 行";
            LogBox.CaretIndex = LogBox.Text.Length;
            LogBox.ScrollToEnd();
        }

        // 订阅新日志
        InMemoryLogSink.OnLogEmitted += OnLogEmitted;
    }

    private void OnLogEmitted(string text, LogEventLevel level)
    {
        // 从任意线程调用，必须派发到 UI 线程。
        // try-catch：窗口关闭瞬间可能 Dispatcher 已 shut down，吞掉避免上传到音频线程
        try { Dispatcher.InvokeAsync(() => AppendLine(text)); } catch { }
    }

    private void AppendLine(string text)
    {
        // 追加一行（AppendText 比重设 Text 高效，不会整体重排）
        LogBox.AppendText(text + "\r\n");
        _lineCount++;

        // 超出上限：批量裁掉最旧的 1/4，避免频繁单行裁剪
        if (_lineCount > MaxLines)
        {
            var lines = LogBox.Text.Split('\n');
            int keep = MaxLines * 3 / 4;
            int skip = lines.Length - keep;
            if (skip > 0)
            {
                LogBox.Text = string.Join("\n", lines[skip..]);
                _lineCount = LogBox.Text.Length == 0 ? 0
                           : LogBox.Text.Split('\n').Length - (LogBox.Text.EndsWith("\n") ? 1 : 0);
            }
        }

        // 自动滚动
        if (CbAutoScroll.IsChecked == true)
        {
            LogBox.CaretIndex = LogBox.Text.Length;
            LogBox.ScrollToEnd();
        }

        TbStatus.Text = $"{_lineCount} 行";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Clear();
        _lineCount = 0;
        TbStatus.Text = "0 行";
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        // ★ 直接从日志 Sink 的历史取纯文本（快、准）。换行用 \r\n（Windows 剪贴板/记事本规范）。
        var history = InMemoryLogSink.GetHistory();
        var all = string.Join("\r\n", history.Select(h => h.Text));
        if (string.IsNullOrEmpty(all))
        {
            TbStatus.Text = "没有可复制的日志";
            return;
        }

        // ★ 剪贴板可能被其它进程短暂占用而抛 COMException：重试几次 + 兜底，绝不崩。
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(all, true); // copy=true：本进程退出后内容仍保留在剪贴板
                TbStatus.Text = $"已复制 {history.Count} 行到剪贴板";
                return;
            }
            catch
            {
                System.Threading.Thread.Sleep(40);
            }
        }
        TbStatus.Text = "复制失败：剪贴板被占用，请重试";
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
