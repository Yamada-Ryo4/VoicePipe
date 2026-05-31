using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace VoicePipe.Audio;

/// <summary>
/// 将混音引擎（IWaveProvider）直接连接到 VB-Cable CABLE Input 虚拟设备。
/// Pull 模型：WasapiOut 按需从 AudioMixEngine.Read() 拉取数据。
/// 使用 WASAPI 替代 WaveOut，避免 Windows 音频路由混淆导致声音输出到扬声器。
/// </summary>
public class VirtualMicWriter : IDisposable
{
    private WasapiOut? _wasapiOut;
    private MMDevice? _device;
    private bool _disposed;

    public static readonly WaveFormat OutputFormat =
        WaveFormat.CreateIeeeFloatWaveFormat(AudioFormat.SampleRate, AudioFormat.Channels);

    // ★ 缓存上次成功命中的 CABLE Input 设备 ID。
    // 首次启动需要枚举全部 Render 端点（每端点都查 PropertyStore，多设备机器累计 300-500ms），
    // 命中后存住，下次开混音直接用 enumerator.GetDevice(id) 跳过整个枚举循环（~50ms）。
    // 用户卸载/重装 VB-Cable 会让 ID 失效 → 命中失败时回退全量枚举并刷新缓存。
    private static string? _cachedCableInputId;
    private static readonly object _cacheLock = new();

    /// <summary>
    /// 初始化并启动输出。直接将 AudioMixEngine 作为数据源。
    /// </summary>
    public void Initialize(AudioMixEngine mixEngine)
    {
        Stop();
        try
        {
            _device = FindCableInputDevice(logFound: true);
            if (_device == null)
            {
                Serilog.Log.Warning("VirtualMicWriter: 未找到 CABLE Input，将无法输出虚拟麦克风信号。");
                return;
            }

            // 使用 WasapiOut 并指定具体的 MMDevice，防止输出被路由到默认扬声器
            _wasapiOut = new WasapiOut(
                _device,
                AudioClientShareMode.Shared,
                true,
                10); // 10ms 超低延迟
                
            _wasapiOut.Init(mixEngine);
            _wasapiOut.Play();
            Serilog.Log.Information("VirtualMicWriter: WASAPI 初始化完成 设备={Name} 格式={Rate}Hz/{Ch}ch/Float32",
                _device.FriendlyName, OutputFormat.SampleRate, OutputFormat.Channels);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "VirtualMicWriter: 初始化失败");
            Stop();
            throw;
        }
    }

    private static MMDevice? FindCableInputDevice(bool logFound = false)
    {
        // ★ 快路径：用缓存的设备 ID 直接 GetDevice。
        // 命中：跳过整个枚举（~50ms vs ~500ms）。失败（设备已卸载/驱动重装）：清缓存回退慢路径。
        string? cachedId;
        lock (_cacheLock) cachedId = _cachedCableInputId;
        if (!string.IsNullOrEmpty(cachedId))
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                try
                {
                    var dev = enumerator.GetDevice(cachedId);
                    if (dev != null && dev.State == DeviceState.Active &&
                        dev.FriendlyName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
                    {
                        if (logFound)
                            Serilog.Log.Information("VirtualMicWriter: 找到 CABLE Input（缓存命中） → {Name}", dev.FriendlyName);
                        // enumerator 不能 Dispose（dev 持有它的内部 COM 引用），交给调用方间接管理；
                        // 改为 GC：MMDeviceEnumerator 的 finalizer 会清理。注：这里 Dispose enumerator
                        // 才安全，因为 NAudio 的 MMDevice 各自持有独立 COM 引用，与 enumerator 解耦。
                        try { enumerator.Dispose(); } catch { }
                        return dev;
                    }
                    try { dev?.Dispose(); } catch { }
                }
                finally { try { enumerator.Dispose(); } catch { } }
            }
            catch
            {
                // 缓存失效（设备 ID 不存在），清掉，落入慢路径
            }
            lock (_cacheLock) _cachedCableInputId = null;
        }

        // 慢路径：全量枚举 Render 端点
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            MMDevice? match = null;
            foreach (var device in devices)
            {
                // ★ 命中前不要提前 return：否则循环里此前遍历过的非命中设备不会被 Dispose（COM 泄漏，
                //   且 IsCableInputAvailable 每 2 秒走一次会持续累积）。命中后保存、其余一律 Dispose。
                if (match == null &&
                    device.FriendlyName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
                {
                    match = device; // 保留命中的，调用方负责 Dispose
                }
                else
                {
                    device.Dispose();
                }
            }

            if (match != null)
            {
                lock (_cacheLock) _cachedCableInputId = match.ID; // ★ 写缓存
                if (logFound)
                    Serilog.Log.Information("VirtualMicWriter: 找到 CABLE Input → {Name}", match.FriendlyName);
            }

            return match;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "VirtualMicWriter: 查找 CABLE Input 设备异常");
        }

        return null;
    }

    public static bool IsCableInputAvailable()
    {
        using var device = FindCableInputDevice();
        return device != null;
    }

    /// <summary>
    /// 当前 Writer 是否仍在运行（有活动的 WasapiOut 实例）。
    /// PipelineManager 据此判断启动新管线时是否能复用现有 Writer，避免无谓拆建。
    /// </summary>
    public bool IsAlive => _wasapiOut != null && !_disposed;

    public void Stop()
    {
        try
        {
            _wasapiOut?.Stop();
            _wasapiOut?.Dispose();
        }
        catch { }
        _wasapiOut = null;
        
        _device?.Dispose();
        _device = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
