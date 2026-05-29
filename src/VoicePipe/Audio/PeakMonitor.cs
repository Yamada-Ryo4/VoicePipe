using NAudio.CoreAudioApi;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace VoicePipe.Audio;

/// <summary>
/// 后台线程轮询系统音频峰值，完全不阻塞 UI。
/// 对麦克风设备会打开静默监听会话，使 AudioMeterInformation 返回真实峰值。
/// 
/// 性能优化：
/// - COM 对象（MMDeviceEnumerator / MMDevice）被缓存复用，不再每帧创建销毁
/// - 轮询频率 15fps（67ms），人眼对音量条感知约 10-15fps 足够
/// </summary>
public static class PeakMonitor
{
    public static ConcurrentDictionary<int, float> ProcessPeaks { get; } = new();
    public static ConcurrentDictionary<string, float> MicPeaks { get; } = new();
    private static volatile bool _isRunning;

    // 保持对每个麦克风的静默捕获，让 AudioMeterInformation 能返回真实值
    private static readonly ConcurrentDictionary<string, WasapiCapture> _micListeners = new();

    // ★ 缓存的 COM 对象，避免每帧创建/销毁
    private static MMDeviceEnumerator? _cachedEnumerator;
    private static MMDevice? _cachedRenderDevice;
    private static readonly Dictionary<string, MMDevice> _cachedMicDevices = new();

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

        // 清理缓存的 COM 对象
        DisposeCachedDevices();
    }

    private static async Task MonitorLoop()
    {
        while (_isRunning)
        {
            try
            {
                // ★ 复用缓存的枚举器
                _cachedEnumerator ??= new MMDeviceEnumerator();

                // ── App process peaks ──
                try
                {
                    // ★ 复用缓存的渲染设备
                    _cachedRenderDevice ??= _cachedEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var sessions = _cachedRenderDevice.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        using var session = sessions[i];
                        var pid = (int)session.GetProcessID;
                        if (pid != 0)
                            ProcessPeaks[pid] = session.AudioMeterInformation.MasterPeakValue;
                    }
                }
                catch
                {
                    // 设备可能被拔出/切换，清空缓存下次重建
                    try { _cachedRenderDevice?.Dispose(); } catch { }
                    _cachedRenderDevice = null;
                }

                // ── Mic input peaks ──
                try
                {
                    var micCol = _cachedEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                    // 清理已拔出的设备缓存
                    var activeIds = new HashSet<string>();
                    for (int i = 0; i < micCol.Count; i++)
                    {
                        var micId = micCol[i].ID;
                        activeIds.Add(micId);

                        // ★ 复用缓存的麦克风设备对象
                        if (!_cachedMicDevices.TryGetValue(micId, out var cachedMic))
                        {
                            cachedMic = micCol[i];
                            _cachedMicDevices[micId] = cachedMic;
                        }
                        else
                        {
                            micCol[i].Dispose(); // 本次枚举出的新对象不需要，释放
                        }

                        EnsureMicListener(micId);
                        MicPeaks[micId] = cachedMic.AudioMeterInformation.MasterPeakValue;
                    }

                    // 清理已断开的设备
                    var toRemove = new List<string>();
                    foreach (var kv in _cachedMicDevices)
                    {
                        if (!activeIds.Contains(kv.Key))
                            toRemove.Add(kv.Key);
                    }
                    foreach (var id in toRemove)
                    {
                        try { _cachedMicDevices[id].Dispose(); } catch { }
                        _cachedMicDevices.Remove(id);
                        MicPeaks.TryRemove(id, out _);
                    }
                }
                catch
                {
                    // 枚举失败，清空缓存下次重建
                    DisposeCachedMics();
                }
            }
            catch
            {
                // 最外层异常（枚举器本身坏了），重建一切
                DisposeCachedDevices();
            }

            await Task.Delay(67); // ★ 15fps，够了
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
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "PeakMonitor: 启动麦克风监听失败 {Id}", deviceId);
        }
    }

    private static void DisposeCachedDevices()
    {
        try { _cachedRenderDevice?.Dispose(); } catch { }
        _cachedRenderDevice = null;
        DisposeCachedMics();
        try { _cachedEnumerator?.Dispose(); } catch { }
        _cachedEnumerator = null;
    }

    private static void DisposeCachedMics()
    {
        foreach (var kv in _cachedMicDevices)
        {
            try { kv.Value.Dispose(); } catch { }
        }
        _cachedMicDevices.Clear();
    }
}