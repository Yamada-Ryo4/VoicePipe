using System.IO;
using System.Text.Json;

namespace VoicePipe.Core;

public class AppSettings
{
    // ── existing ──
    public string LastAppProcessName { get; set; } = "";
    public int    LastAppPid         { get; set; } = 0;
    public string LastMicDeviceId    { get; set; } = "";
    public float  AppGain            { get; set; } = 0.70f;
    public float  MicGain            { get; set; } = 1.0f;
    public bool   MinimizeToTray     { get; set; } = true;
    public bool   AutoStartPipeline  { get; set; } = false;
    public string Language           { get; set; } = "zh-CN";
    public bool   IsDarkTheme        { get; set; } = true;

    // ── new ──
    public bool   AutoStartBoot      { get; set; } = false; // Req 5 (registry Run entry mirror)
    public bool   NoiseGateEnabled   { get; set; } = false; // Req 4.5/4.8 default OFF
    public float  NoiseGateThreshold { get; set; } = 0.02f; // Req 4.7 linear amplitude (~ -34 dBFS)
    public float  DenoiseStrength    { get; set; } = 0.85f; // 降噪干湿混合比 0~1（1=最彻底，0=纯原声）。默认 0.85 缓解人声发空

    // 本地监听（耳机回放）：主开关 + 两个子开关。默认全关。
    public bool   MonitorEnabled     { get; set; } = false; // 监听主开关
    public bool   MonitorApp         { get; set; } = false; // 子开关：单独监听 App
    public bool   MonitorMic         { get; set; } = false; // 子开关：单独监听麦克风（两子开关都关+主开关开=监听整个输出）
    public string MonitorDeviceId    { get; set; } = "";    // 监听输出目标设备 ID（空=系统默认播放设备）
    public float  MonitorGain        { get; set; } = 1.0f;  // 监听音量 0~2（独立于 VB-Cable 输出音量）

    // 开机自启时静默最小化到托盘（不弹主窗口）
    public bool   StartMinimized     { get; set; } = false;

    // 可视化模式：true=频谱图，false=波形图（默认波形）
    public bool   ShowSpectrum       { get; set; } = false;

    // 首次使用引导：首次启动显示一次上手说明，看过后置 true 不再显示
    public bool   FirstRunDone       { get; set; } = false;

    public HotkeyBinding MuteHotkey     { get; set; } = HotkeyBinding.None; // Req 2.3
    public HotkeyBinding PipelineHotkey { get; set; } = HotkeyBinding.None; // Req 2.3
    // MicMuted is intentionally NOT persisted (session-only). App starts unmuted.

    private static string GetFilePath()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoicePipe");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, "appsettings.json");
    }

    private static readonly string FilePath = GetFilePath();

    // ★ 共享单例：ViewModel 和 MainWindow 必须用同一个实例，
    // 否则各自 Load 出独立实例后分别 Save 会互相覆盖字段（如改语言丢增益，改增益丢语言）。
    private static AppSettings? _current;
    public static AppSettings Current => _current ??= Load();

    public static AppSettings Load() => Load(FilePath);

    public void Save() => Save(FilePath);

    /// <summary>
    /// Path-based load overload (testable). Returns defaults on any read/parse failure.
    /// </summary>
    internal static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
        }
        catch { }
        return new();
    }

    /// <summary>
    /// Path-based save overload (testable). On write failure the exception is logged via
    /// Serilog and the in-memory values are left intact — no data loss in the running session.
    /// </summary>
    internal void Save(string path)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to save settings to {Path}; keeping in-memory values", path);
        }
    }
}
