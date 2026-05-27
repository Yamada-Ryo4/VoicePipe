using NAudio.CoreAudioApi;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace VoicePipe.Audio;

/// <summary>
/// 后台线程以 30fps 轮询系统音频峰值，完全不阻塞 UI。
/// </summary>
public static class PeakMonitor
{
    public static ConcurrentDictionary<int, float> ProcessPeaks { get; } = new();
    public static ConcurrentDictionary<string, float> MicPeaks { get; } = new();
    private static volatile bool _isRunning;

    public static void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        Task.Run(MonitorLoop);
    }

    public static void Stop() => _isRunning = false;

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
                        var session = sessions[i];
                        var pid = (int)session.GetProcessID;
                        if (pid != 0)
                            ProcessPeaks[pid] = session.AudioMeterInformation.MasterPeakValue;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "PeakMonitor: render session enum failed");
                }

                // ── Mic input peaks ──  use indexed access, never call Dispose inside loop
                try
                {
                    var micCol = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                    for (int i = 0; i < micCol.Count; i++)
                    {
                        var mic = micCol[i];
                        MicPeaks[mic.ID] = mic.AudioMeterInformation.MasterPeakValue;
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
}