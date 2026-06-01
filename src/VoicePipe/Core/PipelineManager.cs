using NAudio.CoreAudioApi;
using VoicePipe.Core;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VoicePipe.Audio;

public class PipelineManager : IDisposable
{
    private readonly AudioMixEngine _mixer = new();
    private MonitorOutput? _monitor;
    // ★ 缓存所有用过的 LoopbackCapturer，按 PID 索引
    // Windows Per-Process Loopback API 不允许对已关闭的 PID 重新激活（E_UNEXPECTED），
    // 所以必须保持所有用过的 capturer 在后台运行，切换时直接复用。
    private readonly Dictionary<int, LoopbackCapturer> _loopbackCache = new();
    private volatile int _currentPid;
    private MicCapturer? _micCapture;
    private VirtualMicWriter? _writer;

    // ★ 串行化 StartAsync/StopAsync，防止运行中快速切换音源/麦克风时
    // 两次 Start 重叠创建出两个 Writer / 竞态。
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 应用音频捕获彻底失败时触发（LoopbackCapturer 重试耗尽）。
    /// </summary>
    public event EventHandler<string>? CaptureFailed;

    public float AppGain
    {
        get => _mixer.AppGain;
        set => _mixer.AppGain = value;
    }

    public float MicGain
    {
        get => _mixer.MicGain;
        set => _mixer.MicGain = value;
    }

    public bool MicMuted
    {
        get => _mixer.MicMuted;
        set => _mixer.MicMuted = value;
    }

    public bool NoiseGateEnabled
    {
        get => _mixer.NoiseGateEnabled;
        set => _mixer.NoiseGateEnabled = value;
    }

    public float NoiseGateThreshold
    {
        get => _mixer.NoiseGateThreshold;
        set => _mixer.NoiseGateThreshold = value;
    }

    /// <summary>降噪强度（干湿混合比，0~1）。1=最彻底去噪，0=纯原声。仅 Mic_Path。</summary>
    public float DenoiseStrength
    {
        get => _mixer.DenoiseStrength;
        set => _mixer.DenoiseStrength = value;
    }

    // ── 本地监听（耳机回放）pass-through。独立于 VB-Cable 路径，绝不影响其 10ms 延迟。──

    /// <summary>本地监听主开关：把混音回放到默认播放设备（耳机）。</summary>
    public bool MonitorEnabled
    {
        get => _mixer.MonitorEnabled;
        set
        {
            _mixer.MonitorEnabled = value;
            // 监听输出链按开关启停。开：用幂等 EnsureStarted（避免与 ViewModel.EnsureMonitorRunning 重复 Start）；
            // 关：停监听输出链。仅在已有 _monitor 实例时即时生效。
            if (value) _monitor?.EnsureStarted();
            else _monitor?.Stop();
        }
    }

    /// <summary>子开关：单独监听 App 音频。两个子开关都关 + 主开关开 = 监听整个输出。</summary>
    public bool MonitorApp
    {
        get => _mixer.MonitorApp;
        set => _mixer.MonitorApp = value;
    }

    /// <summary>子开关：单独监听麦克风。</summary>
    public bool MonitorMic
    {
        get => _mixer.MonitorMic;
        set => _mixer.MonitorMic = value;
    }

    /// <summary>监听音量（0~2，独立于 VB-Cable 输出音量）。仅影响耳机监听响度。</summary>
    public float MonitorGain
    {
        get => _mixer.MonitorGain;
        set => _mixer.MonitorGain = value;
    }

    // 监听目标设备 ID（空=系统默认）。在 _monitor 未创建时先记住，创建时套用。
    private string _monitorDeviceId = "";

    /// <summary>监听输出目标设备 ID（空=系统默认）。运行中改会即时切设备。</summary>
    public string MonitorDeviceId
    {
        get => _monitorDeviceId;
        set
        {
            _monitorDeviceId = value ?? "";
            if (_monitor != null) _monitor.TargetDeviceId = _monitorDeviceId;
        }
    }

    public async Task StartAsync(int targetPid, string micId)
    {
        // ★ 串行化，防止运行中快速切换源/麦克风导致两次 Start 重叠
        await _gate.WaitAsync();
        try
        {
        // ★★ 复用判断（核心优化）：
        //   仅在设备/资源真的需要换的时候才拆解重建。三类资源各自独立判断：
        //     1) Writer（VB-Cable）：只要 IsAlive 就一直复用 — 我们永远输出到 CABLE Input 这一个设备
        //     2) MicCapturer：当前设备 == 新设备 → 复用；否则才重建
        //     3) MonitorOutput：在下面 EnsureStarted 里幂等处理
        //
        //   解决"直通→开始混音(同麦)" 的卡顿问题：以前无脑 Dispose 旧 mic + new writer，
        //   HyperX 等驱动 WASAPI 资源拆解可耗 3-7s。现在直接走快路径，几毫秒完成。
        bool reuseWriter = _writer != null && _writer.IsAlive;
        bool reuseMic = _micCapture != null && _micCapture.IsAlive
                        && string.Equals(_micCapture.CurrentDeviceId, micId, StringComparison.Ordinal);

        // 后台线程清理 + 必要的拆解（任何 COM 操作都不在 UI 线程做）
        await Task.Run(() =>
        {
            // 清理已退出进程的缓存会话
            PurgeDeadSessions();

            // 只在不能复用时才停（WasapiOut.Stop / WasapiCapture.Dispose 都是慢同步 COM 调用）
            if (!reuseWriter)
            {
                _writer?.Stop();
                _writer = null;
            }
            if (!reuseMic)
            {
                _micCapture?.Dispose();
                _micCapture = null;
            }
        });

        // 切换当前 PID（影响哪个 capturer 的数据会被喂入 mixer）
        _currentPid = targetPid;

        // ★ 暂停所有非活跃 PID 的 capturer，避免后台空转；只激活当前 PID
        foreach (var kv in _loopbackCache)
            kv.Value.Paused = kv.Key != targetPid;

        // 查找或创建该 PID 的 LoopbackCapturer（已在后台线程）
        if (!_loopbackCache.TryGetValue(targetPid, out var capturer))
        {
            capturer = new LoopbackCapturer();
            capturer.Paused = false;

            // 闭包捕获 pid，只有当前活跃 PID 的数据才喂入 mixer
            int pid = targetPid;
            capturer.SamplesAvailable += (_, args) =>
            {
                if (_currentPid == pid)
                    _mixer.FeedApp(args.Samples, args.Count);
            };

            capturer.CaptureFailed += (_, msg) => CaptureFailed?.Invoke(this, msg);

            _loopbackCache[targetPid] = capturer;
            await capturer.StartAsync(targetPid);
        }
        // else: 已有缓存的 capturer 在后台运行，直接复用

        // ★ 复用 Writer 时不要 Reset mixer：会清空 RingBuffer 导致正在输出的连续流出现微杂音/断续。
        //   只有真的拆建过 writer 时才需要 Reset 以避免遗留数据。
        if (!reuseWriter) _mixer.Reset();

        // ★ Writer 初始化（FindCableInputDevice 枚举 + WasapiOut 构造）移到后台线程
        if (!reuseWriter)
        {
            var writer = new VirtualMicWriter();
            await Task.Run(() => writer.Initialize(_mixer));
            _writer = writer;
        }
        // else: 复用现有 writer，CABLE Input 输出从未断过，零延迟切换
        _mixer.VbCableActive = true; // ★ VB-Cable 在跑，监听走 _monitorBuffer（Read 填的）

        // 麦克风：复用就跳过整次 Start（几毫秒级；否则才重建）
        if (!reuseMic)
        {
            _micCapture = new MicCapturer();
            _micCapture.SamplesAvailable += (_, args) => _mixer.FeedMic(args.Samples, args.Count, args.Format);
            _micCapture.Start(micId);
        }

        // ★ 本地监听：若主开关开着，启动独立监听输出链（不影响 VB-Cable）
        //   用 EnsureStarted 幂等启动，已在跑就直接复用，避免每次 StartAsync 都 Stop+New 一次（~50ms）
        _monitor ??= new MonitorOutput(_mixer);
        _monitor.TargetDeviceId = _monitorDeviceId; // 套用持久化的监听设备（空=系统默认）
        if (_mixer.MonitorEnabled) _monitor.EnsureStarted();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
        // ★ 停 VB-Cable 输出（writer）。停了之后 _mixer.Read() 不再被泵动，
        //   VbCableActive=false → 监听改由 GenerateMonitorStandalone 自驱动。
        _writer?.Stop();
        _writer = null;
        _mixer.VbCableActive = false;

        // ★ 监听是否还开着，决定停止语义：
        //   监听开 → 保留数据源（MicCapturer + loopback）继续喂混音引擎，
        //            监听端 WasapiOut 接管泵动 → 停混音后仍能单独听麦克风/App、波形继续动
        //   监听关 → 完全拆除所有数据源 + 清波形（彻底空闲）
        bool keepForMonitor = _mixer.MonitorEnabled;

        if (keepForMonitor)
        {
            // App 不再混入（用户停的是"发往 VB-Cable 的混音"），但 mic 继续喂以便监听麦克风
            _currentPid = 0;
            foreach (var capturer in _loopbackCache.Values)
                capturer.Paused = true;
            // ★ 监听已开 → 确保监听输出链在跑（它现在自驱动混音引擎）
            _monitor?.EnsureStarted();
            // MicCapturer 保持不动，继续 FeedMic → GenerateMonitorStandalone 消费
        }
        else
        {
            // 完全停止：停监听输出、暂停所有 loopback、释放 mic、清波形显示
            _monitor?.Stop();
            foreach (var capturer in _loopbackCache.Values)
                capturer.Paused = true;
            _micCapture?.Dispose();
            _micCapture = null;
            _currentPid = 0;
            // ★ 清波形/频谱，避免冻结在最后一帧（B：完全停止后显示平线）
            WaveformAnalyzer.Clear();
            SpectrumAnalyzer.Clear();
        }
        }
        finally
        {
            _gate.Release();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 仅停止 App 音频混入，保留麦克风直通到虚拟麦克风。
    /// Writer 和 MicCapturer 继续运行，只是不再混入应用声音。
    /// </summary>
    public void StopAppOnly()
    {
        _currentPid = 0;
        // ★ 暂停所有 loopback（App 不再混入），但 Writer/Mic 继续直通
        foreach (var capturer in _loopbackCache.Values)
            capturer.Paused = true;
    }

    /// <summary>
    /// ★ 方案 A 支撑：在"完全停止混音"之后，用户单独开启监听时调用。
    /// 此时 VB-Cable writer 已停（VbCableActive=false），监听由 GenerateMonitorStandalone 自驱动，
    /// 但需要数据源喂混音引擎。本方法确保：
    ///   - MicCapturer 在跑（喂麦克风，让监听能听到麦克风、波形能动）
    ///   - 监听输出链 EnsureStarted
    /// 不重启 VB-Cable（保持完全停止状态，不往 CABLE 发声）。
    /// micId 为空则不拉 mic（用户没选麦克风时只是空监听）。
    /// </summary>
    public void EnsureMonitorRunning(string micId)
    {
        // 监听没开就不做（setter 已处理停止）
        if (!_mixer.MonitorEnabled) return;

        // VB-Cable 在跑时监听走原路径（_monitorBuffer），无需自驱动，直接确保监听链在跑即可
        if (_mixer.VbCableActive)
        {
            _monitor ??= new MonitorOutput(_mixer);
            _monitor.TargetDeviceId = _monitorDeviceId;
            _monitor.EnsureStarted();
            return;
        }

        // VB-Cable 停了：确保 mic 在采（喂 GenerateMonitorStandalone）
        bool micAlive = _micCapture != null && _micCapture.IsAlive
                        && string.Equals(_micCapture.CurrentDeviceId, micId, StringComparison.Ordinal);
        if (!micAlive && !string.IsNullOrEmpty(micId))
        {
            _micCapture?.Dispose();
            _micCapture = new MicCapturer();
            _micCapture.SamplesAvailable += (_, args) => _mixer.FeedMic(args.Samples, args.Count, args.Format);
            _micCapture.Start(micId);
        }

        _monitor ??= new MonitorOutput(_mixer);
        _monitor.TargetDeviceId = _monitorDeviceId;
        _monitor.EnsureStarted();
    }

    /// <summary>
    /// ★ 方案 A 支撑：完全停止状态下用户关闭监听时调用。
    /// 释放为监听单独拉起的 MicCapturer，回到彻底空闲，并清波形。
    /// 仅在 VB-Cable 未运行时有意义（运行中关监听只停监听输出链，由 MonitorEnabled setter 处理）。
    /// </summary>
    public void StopMonitorStandalone()
    {
        if (_mixer.VbCableActive) return; // VB-Cable 在跑，mic 归主路径管，别动
        _monitor?.Stop();
        _micCapture?.Dispose();
        _micCapture = null;
        WaveformAnalyzer.Clear();
        SpectrumAnalyzer.Clear();
    }

    /// <summary>
    /// 扫描缓存，清理已退出进程的 LoopbackCapturer 会话。
    /// 防止用户切换多个音频源后，旧进程退出但 capturer 仍在后台空转导致内存泄漏。
    /// </summary>
    private void PurgeDeadSessions()
    {
        var deadPids = new List<int>();
        foreach (var pid in _loopbackCache.Keys)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                // 进程仍然存活，保留
            }
            catch
            {
                // GetProcessById 抛异常 = 进程已退出
                deadPids.Add(pid);
            }
        }

        foreach (var pid in deadPids)
        {
            Serilog.Log.Information("PipelineManager: 清理已退出进程 PID={Pid} 的 Loopback 会话", pid);
            try { _loopbackCache[pid].Dispose(); } catch { }
            _loopbackCache.Remove(pid);
        }
    }

    /// <summary>
    /// 完全清理（应用退出时调用）。
    /// </summary>
    public void Dispose()
    {
        _writer?.Stop();
        _writer = null;

        // ★ 释放本地监听输出链
        try { _monitor?.Dispose(); } catch { }
        _monitor = null;

        foreach (var capturer in _loopbackCache.Values)
        {
            try { capturer.Dispose(); } catch { }
        }
        _loopbackCache.Clear();

        _micCapture?.Dispose();
        _micCapture = null;

        // ★ 释放混音器持有的 RNNoise 原生状态，避免非托管内存泄漏 (B1)
        try { _mixer.Dispose(); } catch { }

        _gate.Dispose();
    }
}