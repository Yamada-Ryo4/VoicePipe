using System;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoicePipe.Audio;
using VoicePipe.Core;
using VoicePipe.Services;

namespace VoicePipe.ViewModels;

public partial class AudioProcessItem : ObservableObject
{
    [ObservableProperty] private int _pid;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string? _iconPath;
    [ObservableProperty] private float _peakLevel;

    /// <summary>峰值达到/超过 0.99（接近满刻度）即视为削波</summary>
    public bool IsClipping => PeakLevel >= 0.99f;

    partial void OnPeakLevelChanged(float value) => OnPropertyChanged(nameof(IsClipping));

    /// <summary>ComboBox 显示文本</summary>
    public string DisplayName => $"{Name}  (PID {Pid})";
}

public partial class MicDeviceItem : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private float _peakLevel;

    /// <summary>峰值达到/超过 0.99（接近满刻度）即视为削波</summary>
    public bool IsClipping => PeakLevel >= 0.99f;

    partial void OnPeakLevelChanged(float value) => OnPropertyChanged(nameof(IsClipping));
}

/// <summary>监听输出可选设备项（Id 空字符串代表“系统默认”）。</summary>
public partial class MonitorDeviceItem : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _name = "";
}

public partial class MainViewModel : ObservableObject
{
    private readonly PipelineManager _pipeline = new();
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _waveformTimer;

    [ObservableProperty] private ObservableCollection<AudioProcessItem> _processes = new();
    [ObservableProperty] private AudioProcessItem? _selectedProcess;

    [ObservableProperty] private ObservableCollection<MicDeviceItem> _micDevices = new();
    [ObservableProperty] private MicDeviceItem? _selectedMic;

    [ObservableProperty] private float _appGain = 0.70f;
    [ObservableProperty] private float _micGain = 1.0f;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isCableAvailable;
    [ObservableProperty] private bool _micPassthrough;
    [ObservableProperty] private bool _isMicMuted;
    private bool _isMicPassthroughActive; // 当前是否处于麦克风直通状态

    /// <summary>共享的管线管理器，供设置面板实时应用降噪等设置。</summary>
    public PipelineManager Pipeline => _pipeline;

    [ObservableProperty] private float[] _waveformData = Array.Empty<float>();

    // ── 设置面板（主页面内嵌切换视图，非独立窗口）──
    [ObservableProperty] private bool _showSettings;

    // 托盘 / 噪声门 / 自启 设置（基于共享 AppSettings.Current，即时应用 + 持久化）
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _noiseGateEnabled;
    [ObservableProperty] private float _noiseGateThreshold;
    [ObservableProperty] private float _denoiseStrength;

    // 本地监听（耳机回放）：主开关 + 两个子开关
    [ObservableProperty] private bool _monitorEnabled;
    [ObservableProperty] private bool _monitorApp;
    [ObservableProperty] private bool _monitorMic;
    // 监听输出设备列表 + 选中项（首项为"系统默认"，Id=""）
    [ObservableProperty] private ObservableCollection<MonitorDeviceItem> _monitorDevices = new();
    [ObservableProperty] private MonitorDeviceItem? _selectedMonitorDevice;
    [ObservableProperty] private bool _autoStartBoot;
    [ObservableProperty] private bool _autoStartPipelineSetting;

    // 热键绑定 + 冲突标志
    [ObservableProperty] private HotkeyBinding _muteHotkey;
    [ObservableProperty] private HotkeyBinding _pipelineHotkey;
    [ObservableProperty] private bool _muteHotkeyConflict;
    [ObservableProperty] private bool _pipelineHotkeyConflict;

    // 由 MainWindow 注入的服务（热键注册 / 开机自启）
    private Services.HotkeyManager? _hotkeyManager;
    private Services.AutoStartService? _autoStartService;
    private bool _settingsLoading;

    // ★ 波形双缓冲：交替使用两个数组，既给绑定提供引用变化以触发重绘，
    // 又避免每帧 new float[512]（只在首次分配两个，之后复用）。
    private readonly float[][] _waveformBuffers = { new float[512], new float[512] };
    private int _waveformBufferIndex;

    public MainViewModel()
    {
        _settings = AppSettings.Current;
        _settingsLoading = true;        // ★ 加载初值期间禁止存盘，避免启动时无谓写盘
        AppGain = _settings.AppGain;
        MicGain = _settings.MicGain;
        _settingsLoading = false;

        // 应用持久化的噪声门设置（默认关闭，关闭时为纯直通不影响音质）
        _pipeline.NoiseGateEnabled = _settings.NoiseGateEnabled;
        _pipeline.NoiseGateThreshold = _settings.NoiseGateThreshold;
        _pipeline.DenoiseStrength = _settings.DenoiseStrength;

        // 应用持久化的本地监听设置（默认全关；监听输出链在管线启动时按主开关启动）
        _pipeline.MonitorApp = _settings.MonitorApp;
        _pipeline.MonitorMic = _settings.MonitorMic;
        _pipeline.MonitorDeviceId = _settings.MonitorDeviceId;
        _pipeline.MonitorEnabled = _settings.MonitorEnabled;

        // 监听应用音频捕获彻底失败（LoopbackCapturer 重试耗尽）
        _pipeline.CaptureFailed += (_, msg) =>
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                StatusText = $"⚠ {msg}";
                IsRunning = false;
            });
        };

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += async (_, _) => await RefreshAllAsync();
        _refreshTimer.Start();

        _waveformTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; // 20fps
        _waveformTimer.Tick += WaveformTimer_Tick;
        _waveformTimer.Start();

        PeakMonitor.Start();

        // 初始加载也走异步，避免构造函数卡 UI
        _ = RefreshAllAsync();
    }

    // 仅在启动后的首次刷新尝试一次自动启动管线
    private bool _autoStartAttempted;

    private void WaveformTimer_Tick(object? sender, EventArgs e)
    {
        // ★ 列表已按进程名去重（一条=一个 app，用根进程 PID）。但出声的常是子进程，
        //   故按"进程名"查 PeakMonitor 的按名聚合峰值表（同名所有出声 PID 的最大值）来显示音量。
        foreach (var p in Processes)
        {
            if (PeakMonitor.ProcessPeaksByName.TryGetValue(p.Name, out var peak))
                p.PeakLevel = peak;
            else
                p.PeakLevel = 0f;
        }

        foreach (var m in MicDevices)
        {
            if (PeakMonitor.MicPeaks.TryGetValue(m.Id, out var peak))
                m.PeakLevel = peak;
        }

        // ★ 双缓冲：填充另一个数组并交换引用，触发重绘但不分配
        var buf = _waveformBuffers[_waveformBufferIndex];
        WaveformAnalyzer.GetSnapshot(buf);
        WaveformData = buf;
        _waveformBufferIndex ^= 1;

        // ★ B4：降噪器若因处理异常自动降级关闭，把 UI 开关同步回 false，避免显示不一致
        if (NoiseGateEnabled && !_pipeline.NoiseGateEnabled)
            NoiseGateEnabled = false;
    }

    /// <summary>
    /// 窗口聚焦/失焦时调节可视化刷新率：失焦（如挂后台打游戏）时降帧省 CPU，
    /// 但波形仍在动（不冻结）。聚焦时恢复满帧。
    /// </summary>
    public void SetWindowFocused(bool focused)
    {
        if (focused)
        {
            _waveformTimer.Interval = TimeSpan.FromMilliseconds(50);  // 20fps
            _refreshTimer.Interval  = TimeSpan.FromSeconds(2);
            PeakMonitor.PollIntervalMs = 67;                           // 15fps
        }
        else
        {
            _waveformTimer.Interval = TimeSpan.FromMilliseconds(66);  // 15fps，失焦时也保持流畅
            _refreshTimer.Interval  = TimeSpan.FromSeconds(5);        // 后台时进程列表刷新放慢
            PeakMonitor.PollIntervalMs = 200;                          // 5fps
        }
    }

    /// <summary>
    /// 所有重量级 COM 枚举操作在后台线程执行，只把结果带回 UI 线程更新集合。
    /// 彻底消除每 2 秒的 UI 冻结。
    /// </summary>
    private async Task RefreshAllAsync()
    {
        // ★ 在后台线程执行所有 COM 操作
        var (processList, micList, cableAvail, renderList) = await Task.Run(() =>
        {
            var procs = ProcessEnumerator.GetActiveAudioProcesses();
            var mics = MicCapturer.GetAvailableMics();
            var cable = VirtualMicWriter.IsCableInputAvailable();
            var renders = MonitorOutput.GetAvailableRenderDevices();
            return (procs, mics, cable, renders);
        });

        // ★ 回到 UI 线程更新 ObservableCollection
        IsCableAvailable = cableAvail;

        // 进程列表差异更新
        var procToRemove = Processes.Where(p => !processList.Any(l => l.Pid == p.Pid)).ToList();
        foreach (var r in procToRemove) Processes.Remove(r);
        foreach (var l in processList)
        {
            if (!Processes.Any(p => p.Pid == l.Pid))
                Processes.Add(new AudioProcessItem { Pid = l.Pid, Name = l.Name, IconPath = l.IconPath });
        }
        if (SelectedProcess == null)
            SelectedProcess = Processes.FirstOrDefault(p => p.Name.Equals(_settings.LastAppProcessName, StringComparison.OrdinalIgnoreCase)) ?? Processes.FirstOrDefault();

        // 麦克风列表差异更新
        var micToRemove = MicDevices.Where(m => !micList.Any(l => l.Id == m.Id)).ToList();
        foreach (var r in micToRemove) MicDevices.Remove(r);
        foreach (var l in micList)
        {
            if (!MicDevices.Any(m => m.Id == l.Id))
                MicDevices.Add(new MicDeviceItem { Id = l.Id, Name = l.Name });
        }
        if (SelectedMic == null)
            SelectedMic = MicDevices.FirstOrDefault(m => m.Id == _settings.LastMicDeviceId) ?? MicDevices.FirstOrDefault();

        // 监听输出设备列表：首项固定为"系统默认"（Id=""），其后为各播放设备（差异更新）
        if (MonitorDevices.Count == 0 || MonitorDevices[0].Id != "")
        {
            MonitorDevices.Insert(0, new MonitorDeviceItem
            {
                Id = "",
                Name = Application.Current.TryFindResource("StrMonitorDefaultDevice") as string ?? "System default"
            });
        }
        var renderToRemove = MonitorDevices.Where(d => d.Id != "" && !renderList.Any(l => l.Id == d.Id)).ToList();
        foreach (var r in renderToRemove) MonitorDevices.Remove(r);
        foreach (var l in renderList)
        {
            if (!MonitorDevices.Any(d => d.Id == l.Id))
                MonitorDevices.Add(new MonitorDeviceItem { Id = l.Id, Name = l.Name });
        }
        if (SelectedMonitorDevice == null)
            SelectedMonitorDevice = MonitorDevices.FirstOrDefault(d => d.Id == _settings.MonitorDeviceId)
                                    ?? MonitorDevices.FirstOrDefault();

        TryAutoStartPipeline();
    }

    /// <summary>
    /// 启动时根据 AutoStartPipeline 自动接上次的源+麦克风并开始混音。只尝试一次。(Req 5.4-5.8)
    /// 关闭时（默认）不做任何事，也不显示任何缺失提示。
    /// </summary>
    private void TryAutoStartPipeline()
    {
        if (_autoStartAttempted) return;
        if (!_settings.AutoStartPipeline) return; // Req 5.8：关闭时静默
        _autoStartAttempted = true;

        // 按上次记录匹配源/麦克风
        var proc = Processes.FirstOrDefault(p =>
            p.Name.Equals(_settings.LastAppProcessName, StringComparison.OrdinalIgnoreCase));
        var mic = MicDevices.FirstOrDefault(m => m.Id == _settings.LastMicDeviceId);

        if (proc == null)
        {
            // Req 5.6：上次的音频源未在产生音频（不在列表中）
            StatusText = Application.Current.TryFindResource("StrAutoStartNoSource") as string
                         ?? "Last audio source not found — pipeline not started";
            return;
        }
        if (mic == null)
        {
            // Req 5.7：上次的麦克风不可用
            StatusText = Application.Current.TryFindResource("StrAutoStartNoMic") as string
                         ?? "Last microphone not found — pipeline not started";
            return;
        }

        // Req 5.5：两者都可用 → 自动启动
        SelectedProcess = proc;
        SelectedMic = mic;
        _ = StartPipelineCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task StartPipeline()
    {
        if (SelectedProcess == null)
        {
            StatusText = Application.Current.TryFindResource("StrNoSource") as string ?? "Please select an app source";
            return;
        }
        if (SelectedMic == null)
        {
            StatusText = Application.Current.TryFindResource("StrNoSource") as string ?? "Please select a microphone";
            return;
        }
        if (!IsCableAvailable)
        {
            StatusText = "VB-Cable not detected";
            return;
        }
        // ★ 兜底防反馈环路：拒绝把 VB-Cable 回环录音端（CABLE Output 等）当麦克风启动。
        // 正常情况下这些设备已被麦克风列表过滤，此处防设置残留/异常恢复的回环 ID。
        if (Audio.MicCapturer.IsLoopbackDeviceId(SelectedMic.Id))
        {
            StatusText = "⚠ 不能选择 CABLE Output 作为麦克风（会产生回音）";
            Serilog.Log.Warning("StartPipeline: 拒绝回环设备作为麦克风 {Id}", SelectedMic.Id);
            return;
        }
        // ★ 拒绝把 VoicePipe 自身（含多开实例）当音频源：会形成 混音→CABLE→采集→再混音 的回环。
        //   列表里保留 VoicePipe 便于对比音量，但选它启动时在此拦截 + 提示。
        if (SelectedProcess.Name.Equals("VoicePipe", StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "⚠ 不能选择 VoicePipe 自己作为音频源（会产生回音）";
            Serilog.Log.Warning("StartPipeline: 拒绝 VoicePipe 自身作为音频源 PID={Pid}", SelectedProcess.Pid);
            return;
        }

        try
        {
            StatusText = "Starting...";
            _pipeline.AppGain = AppGain;
            _pipeline.MicGain = MicGain;
            await _pipeline.StartAsync(SelectedProcess.Pid, SelectedMic.Id);
            IsRunning = true;
            StatusText = $"Running: {SelectedProcess.Name} + Mic";
            Serilog.Log.Information("管线启动: 源={Source}(PID {Pid}) 麦克风={Mic} App增益={AppGain:P0} 麦增益={MicGain:P0} 降噪={Denoise}",
                SelectedProcess.Name, SelectedProcess.Pid, SelectedMic.Name, AppGain, MicGain,
                _pipeline.NoiseGateEnabled ? "开" : "关");

            // ★ 运行中的麦克风已被 MicCapturer 占用，meter 已唤醒，无需再开静默监听
            PeakMonitor.SetRunningMic(SelectedMic.Id);

            _settings.LastAppProcessName = SelectedProcess.Name;
            _settings.LastAppPid = SelectedProcess.Pid;
            _settings.LastMicDeviceId = SelectedMic.Id;
            _settings.AppGain = AppGain;
            _settings.MicGain = MicGain;
            _settings.Save();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Pipeline start failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopPipeline()
    {
        if (MicPassthrough)
        {
            // 仅停止 App 音频，保留麦克风直通（麦克风仍被 MicCapturer 占用）
            _pipeline.StopAppOnly();
            _isMicPassthroughActive = true;
            IsRunning = false;
            StatusText = "🎤 麦克风直通中";
            Serilog.Log.Information("管线停止 App 音频，保留麦克风直通");
        }
        else
        {
            await _pipeline.StopAsync();
            _isMicPassthroughActive = false;
            IsRunning = false;
            StatusText = "Stopped";
            // ★ 麦克风已释放，恢复对选中麦克风的静默监听
            PeakMonitor.SetRunningMic(null);
            Serilog.Log.Information("管线已完全停止");
        }
    }

    partial void OnMicPassthroughChanged(bool value)
    {
        // 取消勾选时，如果正在直通，则完全停止
        if (!value && _isMicPassthroughActive)
        {
            _ = _pipeline.StopAsync();
            _isMicPassthroughActive = false;
            StatusText = "Stopped";
            PeakMonitor.SetRunningMic(null);
        }
    }

    /// <summary>
    /// 切换混音管线启停（供全局热键和 UI 共用，操作同一 IsRunning 状态）。(Req 2.7, 2.8)
    /// 停止时走 StopPipeline，启动时走 StartPipeline。
    /// </summary>
    public void TogglePipeline()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (IsRunning)
                _ = StopPipelineCommand.ExecuteAsync(null);
            else
                _ = StartPipelineCommand.ExecuteAsync(null);
        });
    }

    /// <summary>
    /// 切换麦克风静音（供全局热键和 UI 共用）。仅作用于 Mic_Path，App 音频不受影响。(Req 2.6)
    /// </summary>
    public void ToggleMicMute()
    {
        Application.Current.Dispatcher.Invoke(() => IsMicMuted = !IsMicMuted);
    }

    partial void OnIsMicMutedChanged(bool value)
    {
        _pipeline.MicMuted = value;
        Serilog.Log.Information("麦克风静音: {State}", value ? "开" : "关");
        // 状态栏反馈：静音时提示，取消静音时恢复运行/就绪状态
        if (value)
            StatusText = Application.Current.TryFindResource("StrMicMuted") as string ?? "Mic muted";
        else
            StatusText = IsRunning ? $"Running: {SelectedProcess?.Name} + Mic" : "Ready";
    }

    // ════════════ 内嵌设置面板 ════════════

    /// <summary>由 MainWindow 注入热键/自启服务并加载设置初值。</summary>
    public void InitSettings(HotkeyManager hotkeys, AutoStartService autoStart)
    {
        _hotkeyManager = hotkeys;
        _autoStartService = autoStart;

        _settingsLoading = true;
        MinimizeToTray = _settings.MinimizeToTray;
        NoiseGateEnabled = _settings.NoiseGateEnabled;
        NoiseGateThreshold = _settings.NoiseGateThreshold;
        DenoiseStrength = _settings.DenoiseStrength;
        MonitorEnabled = _settings.MonitorEnabled;
        MonitorApp = _settings.MonitorApp;
        MonitorMic = _settings.MonitorMic;
        try { AutoStartBoot = _autoStartService.IsEnabled(); } catch { AutoStartBoot = _settings.AutoStartBoot; }
        _settings.AutoStartBoot = AutoStartBoot;
        AutoStartPipelineSetting = _settings.AutoStartPipeline;
        MuteHotkey = _settings.MuteHotkey;
        PipelineHotkey = _settings.PipelineHotkey;
        _settingsLoading = false;
    }

    private void PersistSettings()
    {
        if (_settingsLoading) return;
        _settings.Save();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settings.MinimizeToTray = value;
        PersistSettings();
    }

    partial void OnNoiseGateEnabledChanged(bool value)
    {
        _pipeline.NoiseGateEnabled = value; // 即时应用（仅 Mic_Path）
        _settings.NoiseGateEnabled = value;
        if (!_settingsLoading) Serilog.Log.Information("降噪: {State}", value ? "开" : "关");
        PersistSettings();
    }

    partial void OnNoiseGateThresholdChanged(float value)
    {
        _pipeline.NoiseGateThreshold = value;
        _settings.NoiseGateThreshold = value;
        PersistSettings();
    }

    partial void OnDenoiseStrengthChanged(float value)
    {
        _pipeline.DenoiseStrength = value; // 即时应用（仅 Mic_Path）
        _settings.DenoiseStrength = value;
        // 滑块拖动属高频，不打日志（避免刷屏）
        PersistSettings();
    }

    partial void OnMonitorEnabledChanged(bool value)
    {
        _pipeline.MonitorEnabled = value; // 即时启停监听输出链
        _settings.MonitorEnabled = value;
        if (!_settingsLoading) Serilog.Log.Information("本地监听: {State}", value ? "开" : "关");
        PersistSettings();
    }

    partial void OnMonitorAppChanged(bool value)
    {
        _pipeline.MonitorApp = value;
        _settings.MonitorApp = value;
        if (!_settingsLoading) Serilog.Log.Information("监听 App 音频: {State}", value ? "开" : "关");
        PersistSettings();
    }

    partial void OnMonitorMicChanged(bool value)
    {
        _pipeline.MonitorMic = value;
        _settings.MonitorMic = value;
        if (!_settingsLoading) Serilog.Log.Information("监听麦克风: {State}", value ? "开" : "关");
        PersistSettings();
    }

    partial void OnSelectedMonitorDeviceChanged(MonitorDeviceItem? value)
    {
        if (value == null) return;
        _pipeline.MonitorDeviceId = value.Id; // 空=系统默认；运行中改会即时切设备
        _settings.MonitorDeviceId = value.Id;
        if (!_settingsLoading)
            Serilog.Log.Information("监听输出设备: {Name}", string.IsNullOrEmpty(value.Id) ? "系统默认" : value.Name);
        PersistSettings();
    }

    partial void OnAutoStartBootChanged(bool value)
    {
        try { _autoStartService?.SetEnabled(value); } catch { }
        _settings.AutoStartBoot = value;
        if (!_settingsLoading) Serilog.Log.Information("开机自启: {State}", value ? "开" : "关");
        PersistSettings();
    }

    partial void OnAutoStartPipelineSettingChanged(bool value)
    {
        _settings.AutoStartPipeline = value;
        if (!_settingsLoading) Serilog.Log.Information("自动启动管线: {State}", value ? "开" : "关");
        PersistSettings();
    }

    partial void OnMuteHotkeyChanged(HotkeyBinding value)
    {
        if (_settingsLoading) return;
        _settings.MuteHotkey = value;
        if (_hotkeyManager != null)
            MuteHotkeyConflict = !_hotkeyManager.Register(HotkeyManager.Action.ToggleMute, value);
        Serilog.Log.Information("静音热键已设置: Mod={Mod} Key={Key} 冲突={Conflict}",
            value.Modifiers, value.Key, MuteHotkeyConflict);
        PersistSettings();
    }

    partial void OnPipelineHotkeyChanged(HotkeyBinding value)
    {
        if (_settingsLoading) return;
        _settings.PipelineHotkey = value;
        if (_hotkeyManager != null)
            PipelineHotkeyConflict = !_hotkeyManager.Register(HotkeyManager.Action.TogglePipeline, value);
        Serilog.Log.Information("启停管线热键已设置: Mod={Mod} Key={Key} 冲突={Conflict}",
            value.Modifiers, value.Key, PipelineHotkeyConflict);
        PersistSettings();
    }

    /// <summary>把两个热键都重置为未设置（None）。各自的 OnXxxHotkeyChanged 会注销热键、清冲突标志并持久化。</summary>
    public void ResetHotkeys()
    {
        MuteHotkey = HotkeyBinding.None;
        PipelineHotkey = HotkeyBinding.None;
    }

    partial void OnAppGainChanged(float value)
    {
        _pipeline.AppGain = value;
        _settings.AppGain = value;
        PersistSettings(); // ★ 拖滑块即时存盘（PersistSettings 带 _settingsLoading 守卫）
    }

    partial void OnMicGainChanged(float value)
    {
        _pipeline.MicGain = value;
        _settings.MicGain = value;
        PersistSettings(); // ★ 拖滑块即时存盘
    }

    partial void OnSelectedProcessChanged(AudioProcessItem? value)
    {
        if (value != null)
            Serilog.Log.Information("已选择音频源: {Name}(PID {Pid})", value.Name, value.Pid);
        if (IsRunning && value != null && SelectedMic != null)
            _ = StartPipelineCommand.ExecuteAsync(null);
    }

    partial void OnSelectedMicChanged(MicDeviceItem? value)
    {
        // ★ 平时只监听选中的麦克风，告知 PeakMonitor 切换目标
        PeakMonitor.SetTargetMic(value?.Id);

        if (value != null)
            Serilog.Log.Information("已选择麦克风: {Name}", value.Name);
        if (IsRunning && value != null && SelectedProcess != null)
            _ = StartPipelineCommand.ExecuteAsync(null);
    }

    /// <summary>麦克风下拉菜单打开：临时监听全部麦克风，便于用音量条辨认设备。</summary>
    public void OnMicDropDownOpened() => PeakMonitor.MonitorAllMics = true;

    /// <summary>麦克风下拉菜单关闭：收回到只监听选中的设备。</summary>
    public void OnMicDropDownClosed() => PeakMonitor.MonitorAllMics = false;

    /// <summary>
    /// 完全清理管线（包括保持运行的 LoopbackCapturer），应用退出时调用。
    /// </summary>
    public void Cleanup()
    {
        _pipeline.Dispose();
        _refreshTimer.Stop();
        _waveformTimer.Stop();
        PeakMonitor.Stop();
    }
}