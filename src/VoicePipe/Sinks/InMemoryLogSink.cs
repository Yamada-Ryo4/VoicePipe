using Serilog.Core;
using Serilog.Events;
using System.Text;

namespace VoicePipe.Sinks;

/// <summary>实时内存日志 Sink，向 UI 控制台窗口广播日志条目。</summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    public static readonly InMemoryLogSink Instance = new();

    // 历史记录（最多2000条），供新打开的控制台窗口回填
    private static readonly List<(string Text, LogEventLevel Level)> _history = new();
    private static readonly object _histLock = new();
    private const int MaxHistory = 2000;

    // UI 控制台窗口订阅此事件（从任意线程触发）
    public static event Action<string, LogEventLevel>? OnLogEmitted;

    public static IList<(string Text, LogEventLevel Level)> GetHistory()
    {
        lock (_histLock) return _history.ToList();
    }

    public void Emit(LogEvent logEvent)
    {
        var ts    = logEvent.Timestamp.ToString("HH:mm:ss.fff");
        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose     => "VRB",
            LogEventLevel.Debug       => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning     => "WRN",
            LogEventLevel.Error       => "ERR",
            LogEventLevel.Fatal       => "FTL",
            _                         => "???"
        };

        var sb = new StringBuilder();
        sb.Append($"[{ts}][{level}] {logEvent.RenderMessage()}");
        if (logEvent.Exception != null)
            sb.Append($"\n         ↳ {logEvent.Exception.GetType().Name}: {logEvent.Exception.Message}");

        var text = sb.ToString();

        lock (_histLock)
        {
            if (_history.Count >= MaxHistory) _history.RemoveAt(0);
            _history.Add((text, logEvent.Level));
        }

        OnLogEmitted?.Invoke(text, logEvent.Level);
    }
}
