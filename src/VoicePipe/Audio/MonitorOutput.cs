using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Generic;

namespace VoicePipe.Audio;

/// <summary>监听输出可选的播放设备信息（Id 用于持久化/选择，Name 用于显示）。</summary>
public record RenderDeviceInfo(string Id, string Name);


/// <summary>
/// 本地监听输出：把 AudioMixEngine 生成的监听信号回放到默认播放设备（耳机/扬声器），
/// 让用户实时听到自己发往 VB-Cable 的混音效果。
///
/// 关键隔离：本类是一条<b>完全独立</b>于 VB-Cable 输出的 WASAPI 输出链。
/// AudioMixEngine.Read()（VB-Cable 那路，由 VirtualMicWriter 拉取）在生成数据的同一循环里
/// 顺手把监听信号写入 engine 的独立 _monitorBuffer；本类的 WasapiOut 从该缓冲拉取。
/// 因此监听的开关、延迟、设备故障都<b>绝不影响 VB-Cable 的 10ms 低延迟主路径</b>。
///
/// 数据不足时补零（静音），保证输出流连续不卡顿；监听主开关关闭时 engine 不写入，自然静音。
/// </summary>
public sealed class MonitorOutput : IDisposable
{
    private readonly AudioMixEngine _engine;
    private WasapiOut? _out;
    private MMDevice? _device;
    private bool _disposed;
    private readonly object _sync = new(); // ★ 串行化 Start/Stop/设备切换，防止多线程重叠创建/销毁 WasapiOut

    // 监听目标设备 ID。空字符串 = 跟随系统默认播放设备。volatile：UI 线程改、Start 读。
    private volatile string _targetDeviceId = "";

    /// <summary>设置监听输出目标设备 ID（空=系统默认）。若正在运行则自动重启切到新设备。</summary>
    public string TargetDeviceId
    {
        get => _targetDeviceId;
        set
        {
            var v = value ?? "";
            lock (_sync)
            {
                if (_targetDeviceId == v) return;
                _targetDeviceId = v;
                // 运行中切设备：重启输出链到新设备（在锁内调 StartLocked 避免与其它 Start/Stop 重叠）
                if (_out != null) StartLocked();
            }
        }
    }

    public MonitorOutput(AudioMixEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// 启动监听输出。目标设备为空时用系统默认播放设备；指定 ID 时用该设备，
    /// 找不到/异常时回退系统默认。重复调用先停旧的。
    /// 失败（无设备/驱动异常）只记日志并降级，不抛到上层、不影响 VB-Cable。
    /// </summary>
    public void Start()
    {
        lock (_sync) StartLocked();
    }

    /// <summary>
    /// 幂等启动：已在运行（且当前设备匹配 _targetDeviceId）就什么都不做，不重启 WasapiOut。
    /// 用于 PipelineManager.StartAsync — 启动新管线时如果监听已经在跑，就别拆掉重建（多余的 50ms+ 抖动）。
    /// </summary>
    public void EnsureStarted()
    {
        lock (_sync)
        {
            if (_out != null && _device != null)
            {
                // 已在跑：仅当目标设备和当前设备不匹配时才重启。
                bool defaultRequested = string.IsNullOrEmpty(_targetDeviceId);
                try
                {
                    bool currentMatches = defaultRequested
                        ? true   // 跟默认 - 假设默认没变（设备切换由 TargetDeviceId setter 处理）
                        : _device.ID == _targetDeviceId;
                    if (currentMatches) return; // ★ 复用：直接走人，零开销
                }
                catch
                {
                    // ★ _device 的底层 COM 可能被 fire-and-forget StopLocked 释放了（竞态），
                    //   访问 _device.ID 抛 InvalidCastException (E_NOINTERFACE)。
                    //   不返回，走 StartLocked 重建。
                    Serilog.Log.Warning("MonitorOutput: EnsureStarted 检测到 _device COM 失效，重建");
                }
            }
            StartLocked();
        }
    }

    // 必须在持有 _sync 锁的前提下调用。
    private void StartLocked()
    {
        // ★ StopLocked 可能因 COM 异常失败（InvalidCastException 等），但不能让它阻止 Start。
        try { StopLocked(); } catch (Exception ex) { Serilog.Log.Warning(ex, "MonitorOutput: StartLocked 内 StopLocked 失败（忽略，继续 Start）"); }
        if (_disposed) return;
        try
        {
            using var enumerator = new MMDeviceEnumerator();

            // 指定设备 ID 则用之，否则（或解析失败）回退系统默认多媒体播放设备
            var id = _targetDeviceId;
            if (!string.IsNullOrEmpty(id))
            {
                try { _device = enumerator.GetDevice(id); }
                catch { _device = null; }
                // 设备不存在或非激活状态 → 回退默认
                if (_device == null || _device.State != DeviceState.Active)
                {
                    try { _device?.Dispose(); } catch { }
                    _device = null;
                    Serilog.Log.Warning("MonitorOutput: 指定监听设备不可用，回退系统默认");
                }
            }
            _device ??= enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            // 监听用较宽松的延迟（不需要像 VB-Cable 那样压到 10ms；监听只要听感同步即可），
            // 用 50ms 兼顾稳定与延迟，避免欠载爆音。
            _out = new WasapiOut(_device, AudioClientShareMode.Shared, true, 50);
            _out.Init(new MonitorProvider(_engine));
            _out.Play();
            Serilog.Log.Information("MonitorOutput: 本地监听已启动 设备={Name}", _device.FriendlyName);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "MonitorOutput: 启动失败（监听不可用，不影响 VB-Cable 输出）");
            StopLocked();
        }
    }

    /// <summary>枚举可用的播放（渲染）设备，供 UI 选择监听输出目标。</summary>
    public static List<RenderDeviceInfo> GetAvailableRenderDevices()
    {
        var result = new List<RenderDeviceInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var col = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            for (int i = 0; i < col.Count; i++)
            {
                using var dev = col[i];
                result.Add(new RenderDeviceInfo(dev.ID, dev.FriendlyName));
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "MonitorOutput: 枚举播放设备失败");
        }
        return result;
    }

    public void Stop()
    {
        lock (_sync) StopLocked();
    }

    // 必须在持有 _sync 锁的前提下调用。
    private void StopLocked()
    {
        // ★ Stop / Dispose 拆成两个 try：Stop 抛异常时 Dispose 仍会执行，
        //   避免 _out=null 清引用后 WasapiOut COM 资源泄漏（等 GC 终结不可靠）。
        try { _out?.Stop(); } catch { }
        try { _out?.Dispose(); } catch { }
        _out = null;

        try { _device?.Dispose(); } catch { }
        _device = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            StopLocked();
        }
    }

    /// <summary>
    /// 从 engine 的监听缓冲拉取 PCM 喂给 WasapiOut。数据不足补零，保证连续。
    /// </summary>
    private sealed class MonitorProvider : IWaveProvider
    {
        private readonly AudioMixEngine _engine;
        // 50ms latency × 48kHz × 2ch = 4800 样本，预分配 6000 留足余量避免首次 Read 扩容
        private float[] _temp = new float[6000];

        public MonitorProvider(AudioMixEngine engine) => _engine = engine;

        public WaveFormat WaveFormat { get; } =
            WaveFormat.CreateIeeeFloatWaveFormat(AudioFormat.SampleRate, AudioFormat.Channels);

        public int Read(byte[] buffer, int offset, int count)
        {
            int samplesNeeded = count / 4; // float32
            if (_temp.Length < samplesNeeded) _temp = new float[samplesNeeded];

            if (_engine.VbCableActive)
            {
                // VB-Cable 在跑：Read() 已填好 _monitorBuffer，这里拉取即可
                int got = _engine.MonitorBuffer.Read(_temp, 0, samplesNeeded);
                for (int i = got; i < samplesNeeded; i++) _temp[i] = 0f; // 不足补零
            }
            else
            {
                // VB-Cable 停了：监听自驱动混音引擎（消费 app/mic + 跑波形 + 产监听 PCM）
                _engine.GenerateMonitorStandalone(_temp, samplesNeeded);
            }

            unsafe
            {
                fixed (byte* ptr = &buffer[offset])
                {
                    float* fptr = (float*)ptr;
                    for (int i = 0; i < samplesNeeded; i++) fptr[i] = _temp[i];
                }
            }
            return count; // 永远返回请求字节数，保证连续不卡顿
        }
    }
}
