using System;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoicePipe.Audio;
using VoicePipe.Core;

namespace VoicePipe.ViewModels;

public partial class AudioProcessItem : ObservableObject
{
    [ObservableProperty] private int _pid;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string? _iconPath;
    [ObservableProperty] private float _peakLevel;

    /// <summary>ComboBox 显示文本</summary>
    public string DisplayName => $"{Name}  (PID {Pid})";
}

public partial class MicDeviceItem : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private float _peakLevel;
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

    [ObservableProperty] private float _appGain = 0.75f;
    [ObservableProperty] private float _micGain = 1.0f;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isCableAvailable;
    [ObservableProperty] private bool _micPassthrough;
    private bool _isMicPassthroughActive; // 当前是否处于麦克风直通状态

    [ObservableProperty] private float[] _waveformData = Array.Empty<float>();

    public MainViewModel()
    {
        _settings = AppSettings.Load();
        AppGain = _settings.AppGain;
        MicGain = _settings.MicGain;

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

        _waveformTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _waveformTimer.Tick += WaveformTimer_Tick;
        _waveformTimer.Start();

        PeakMonitor.Start();

        // 初始加载也走异步，避免构造函数卡 UI
        _ = RefreshAllAsync();
    }

    private void WaveformTimer_Tick(object? sender, EventArgs e)
    {
        // 30fps update peaks
        foreach (var p in Processes)
        {
            if (PeakMonitor.ProcessPeaks.TryGetValue(p.Pid, out var peak))
                p.PeakLevel = peak;
        }

        foreach (var m in MicDevices)
        {
            if (PeakMonitor.MicPeaks.TryGetValue(m.Id, out var peak))
                m.PeakLevel = peak;
        }

        WaveformData = WaveformAnalyzer.GetSnapshot();
    }

    /// <summary>
    /// 所有重量级 COM 枚举操作在后台线程执行，只把结果带回 UI 线程更新集合。
    /// 彻底消除每 2 秒的 UI 冻结。
    /// </summary>
    private async Task RefreshAllAsync()
    {
        // ★ 在后台线程执行所有 COM 操作
        var (processList, micList, cableAvail) = await Task.Run(() =>
        {
            var procs = ProcessEnumerator.GetActiveAudioProcesses();
            var mics = MicCapturer.GetAvailableMics();
            var cable = VirtualMicWriter.IsCableInputAvailable();
            return (procs, mics, cable);
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

        try
        {
            StatusText = "Starting...";
            _pipeline.AppGain = AppGain;
            _pipeline.MicGain = MicGain;
            await _pipeline.StartAsync(SelectedProcess.Pid, SelectedMic.Id);
            IsRunning = true;
            StatusText = $"Running: {SelectedProcess.Name} + Mic";

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
            // 仅停止 App 音频，保留麦克风直通
            _pipeline.StopAppOnly();
            _isMicPassthroughActive = true;
            IsRunning = false;
            StatusText = "🎤 麦克风直通中";
        }
        else
        {
            await _pipeline.StopAsync();
            _isMicPassthroughActive = false;
            IsRunning = false;
            StatusText = "Stopped";
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
        }
    }

    partial void OnAppGainChanged(float value)
    {
        _pipeline.AppGain = value;
        _settings.AppGain = value;
    }

    partial void OnMicGainChanged(float value)
    {
        _pipeline.MicGain = value;
        _settings.MicGain = value;
    }

    partial void OnSelectedProcessChanged(AudioProcessItem? value)
    {
        if (IsRunning && value != null && SelectedMic != null)
            _ = StartPipelineCommand.ExecuteAsync(null);
    }

    partial void OnSelectedMicChanged(MicDeviceItem? value)
    {
        if (IsRunning && value != null && SelectedProcess != null)
            _ = StartPipelineCommand.ExecuteAsync(null);
    }

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