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
    [ObservableProperty] private AudioProcessItem? _activeProcess;
    [ObservableProperty] private string _activeSourceName = "";

    [ObservableProperty] private ObservableCollection<MicDeviceItem> _micDevices = new();
    [ObservableProperty] private MicDeviceItem? _selectedMic;
    [ObservableProperty] private MicDeviceItem? _activeMic;
    [ObservableProperty] private string _activeMicName = "";

    [ObservableProperty] private float _appGain = 0.75f;
    [ObservableProperty] private float _micGain = 1.0f;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isCableAvailable;

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

        _activeSourceName = Application.Current.TryFindResource("StrNoSource") as string ?? "None Selected";
        _activeMicName = Application.Current.TryFindResource("StrNoSource") as string ?? "None Selected";

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += (_, _) => { RefreshProcesses(); RefreshMicDevices(); };
        _refreshTimer.Start();

        _waveformTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _waveformTimer.Tick += WaveformTimer_Tick;
        _waveformTimer.Start();

        PeakMonitor.Start();

        RefreshProcesses();
        RefreshMicDevices();
        IsCableAvailable = VirtualMicWriter.IsCableInputAvailable();
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

    [RelayCommand]
    private void RefreshProcesses()
    {
        var list = ProcessEnumerator.GetActiveAudioProcesses();
        
        var toRemove = Processes.Where(p => !list.Any(l => l.Pid == p.Pid)).ToList();
        foreach (var r in toRemove) Processes.Remove(r);

        foreach (var l in list)
        {
            if (!Processes.Any(p => p.Pid == l.Pid))
                Processes.Add(new AudioProcessItem { Pid = l.Pid, Name = l.Name, IconPath = l.IconPath });
        }

        if (SelectedProcess == null)
        {
            SelectedProcess = Processes.FirstOrDefault(p => p.Name.Equals(_settings.LastAppProcessName, StringComparison.OrdinalIgnoreCase)) ?? Processes.FirstOrDefault();
        }

        if (ActiveProcess == null && SelectedProcess != null && !string.IsNullOrEmpty(_settings.LastAppProcessName))
            ConfirmSource();
    }

    [RelayCommand]
    private void ConfirmSource()
    {
        if (SelectedProcess == null) return;
        ActiveProcess = SelectedProcess;
        ActiveSourceName = $"{SelectedProcess.Name}  (PID {SelectedProcess.Pid})";
    }

    [RelayCommand]
    private void RefreshMicDevices()
    {
        var list = MicCapturer.GetAvailableMics();
        
        var toRemove = MicDevices.Where(m => !list.Any(l => l.Id == m.Id)).ToList();
        foreach (var r in toRemove) MicDevices.Remove(r);

        foreach (var l in list)
        {
            if (!MicDevices.Any(m => m.Id == l.Id))
                MicDevices.Add(new MicDeviceItem { Id = l.Id, Name = l.Name });
        }

        if (SelectedMic == null)
        {
            SelectedMic = MicDevices.FirstOrDefault(m => m.Id == _settings.LastMicDeviceId) ?? MicDevices.FirstOrDefault();
        }

        if (ActiveMic == null && SelectedMic != null && !string.IsNullOrEmpty(_settings.LastMicDeviceId))
            ConfirmMic();
    }

    [RelayCommand]
    private void ConfirmMic()
    {
        if (SelectedMic == null) return;
        ActiveMic = SelectedMic;
        ActiveMicName = SelectedMic.Name;
    }

    [RelayCommand]
    private async Task StartPipeline()
    {
        if (ActiveProcess == null)
        {
            StatusText = Application.Current.TryFindResource("StrNoSource") as string ?? "Please select an app source";
            return;
        }
        if (ActiveMic == null)
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
            await _pipeline.StartAsync(ActiveProcess.Pid, ActiveMic.Id);
            IsRunning = true;
            StatusText = $"Running: {ActiveProcess.Name} + Mic";

            _settings.LastAppProcessName = ActiveProcess.Name;
            _settings.LastAppPid = ActiveProcess.Pid;
            _settings.LastMicDeviceId = ActiveMic.Id;
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
        await _pipeline.StopAsync();
        IsRunning = false;
        StatusText = "Stopped";
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