using NAudio.CoreAudioApi;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace VoicePipe.Audio;

/// <summary>
/// 后台线程以 30fps 轮询系统音频峰值，完全不阻塞 UI。
/// 对麦克风设备会打开静默监听会话，使 AudioMeterInformation 返回真实峰值。
/// </summary>
public static class PeakMonitor
{
    public static ConcurrentDictionary<int, float> ProcessPeaks { get; } = new();
    public static ConcurrentDictionary<string, float> MicPeaks { get; } = new();
    private static volatile bool _isRunning;

    // 保持对每个麦克风的静默捕获，让 AudioMeterInformation 能返回真实值
    private static readonly ConcurrentDictionary<string, WasapiCapture> _micListeners = new();

    public static void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        Task.Run(MonitorLoop);
    }

    public static void Stop()
    {
        _isRunning = false;
        // 清理所有静默监听
        foreach (var kvp in _micListeners)
        {
            try { kvp.Value.StopRecording(); kvp.Value.Dispose(); } catch { }
        }
        _micListeners.Clear();
    }

    private static async Task MonitorLoop()
    {
        while (_isRunning)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();

                // ── App process peaks ──
                try
                {
                    using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var sessions = device.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        using var session = sessions[i];
                        var pid = (int)session.GetProcessID;
                        if (pid != 0)
                            ProcessPeaks[pid] = session.AudioMeterInformation.MasterPeakValue;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "PeakMonitor: render session enum failed");
                }

                // ── Mic input peaks ──
                try
                {
                    var micCol = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                    for (int i = 0; i < micCol.Count; i++)
                    {
                        var mic = micCol[i];
                        var micId = mic.ID;

                        // 确保对该麦克风有一个静默监听会话
                        // 否则 AudioMeterInformation.MasterPeakValue 在没有应用录音时始终返回 0
                        EnsureMicListener(micId);

                        MicPeaks[micId] = mic.AudioMeterInformation.MasterPeakValue;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "PeakMonitor: capture enum failed");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "PeakMonitor: outer loop error");
            }

            await Task.Delay(33);
        }
    }

    /// <summary>
    /// 确保对指定麦克风设备有一个静默捕获会话在运行。
    /// 这让 Windows 启用该设备的硬件 peak meter，使 AudioMeterInformation 能返回真实值。
    /// 捕获到的数据直接丢弃（DataAvailable 不做任何处理）。
    /// </summary>
    private static void EnsureMicListener(string deviceId)
    {
        if (_micListeners.ContainsKey(deviceId)) return;

        try
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(deviceId);
            var capture = new WasapiCapture(device)
            {
                ShareMode = AudioClientShareMode.Shared
            };

            // 空回调：只为激活设备的 peak meter，数据直接丢弃
            capture.DataAvailable += (_, _) => { };
            capture.RecordingStopped += (_, e) =>
            {
                // 设备拔出等情况，从缓存移除
                _micListeners.TryRemove(deviceId, out var _removed);
                try { capture.Dispose(); } catch { }
                try { device.Dispose(); } catch { }
                try { enumerator.Dispose(); } catch { }
            };
            capture.StartRecording();

            _micListeners[deviceId] = capture;
            Serilog.Log.Debug("PeakMonitor: 已为 {Id} 启动静默监听", deviceId);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "PeakMonitor: 启动麦克风监听失败 {Id}", deviceId);
        }
    }
}