using NAudio.CoreAudioApi;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VoicePipe.Audio;

/// <summary>
/// 后台线程轮询系统音频峰值，完全不阻塞 UI。
///
/// 性能优化：
/// - COM 对象（MMDeviceEnumerator / MMDevice）被缓存复用，不再每帧创建销毁
/// - 轮询频率 15fps（67ms），人眼对音量条感知约 10-15fps 足够
/// - ★ 麦克风静默监听只对「需要的」设备开启，不再对所有麦克风都开一条 WasapiCapture 空转线程：
///     * 平时只监听当前选中的麦克风（TargetMicId）
///     * 麦克风下拉菜单打开时（MonitorAllMics=true）临时监听全部，便于用户用音量条辨认设备
///     * 管线运行时（RunningMicId）该麦克风已被 MicCapturer 占用，其 peak meter 本就被唤醒，无需再开监听
///
/// 关于成本：
/// - App/进程峰值是从 AudioSessionManager 一次性枚举读取，读 1 个和读全部成本相同 → 始终全监听
/// - 麦克风峰值需要为每个设备开真实 WASAPI 捕获流唤醒硬件电平表 → 这是真正的开销，按需开启
/// </summary>
public static class PeakMonitor
{
    public static ConcurrentDictionary<int, float> ProcessPeaks { get; } = new();
    public static ConcurrentDictionary<string, float> MicPeaks { get; } = new();
    // ★ 按进程名聚合的峰值（同名 app 所有出声 PID 的最大值）。
    //   列表按名去重后用根进程 PID 显示，但出声的常是子进程，故 UI 按名字查这个表才显示得出音量。
    public static ConcurrentDictionary<string, float> ProcessPeaksByName { get; } = new(StringComparer.OrdinalIgnoreCase);
    private static volatile bool _isRunning;

    // ── 麦克风监听策略控制 ──
    // 当前选中的麦克风（平时只监听它）
    private static volatile string? _targetMicId;
    // 下拉菜单打开时为 true：临时监听全部麦克风以便辨认
    private static volatile bool _monitorAllMics;
    // 管线运行中的麦克风：它已被 MicCapturer 占用，meter 已唤醒，无需再开静默监听
    private static volatile string? _runningMicId;

    public static void SetTargetMic(string? deviceId) => _targetMicId = deviceId;
    public static bool MonitorAllMics { get => _monitorAllMics; set => _monitorAllMics = value; }
    public static void SetRunningMic(string? deviceId) => _runningMicId = deviceId;

    // 轮询间隔（毫秒）。窗口失焦时调大以省 CPU。默认 67ms（约 15fps）。
    private static volatile int _pollIntervalMs = 67;
    public static int PollIntervalMs
    {
        get => _pollIntervalMs;
        set => _pollIntervalMs = value < 33 ? 33 : value; // 下限 33ms，防止设成 0 空转
    }

    // 静默捕获会话：只为唤醒设备硬件 peak meter，数据直接丢弃
    private static readonly ConcurrentDictionary<string, WasapiCapture> _micListeners = new();

    // ★ 缓存的 COM 对象，避免每帧创建/销毁
    private static MMDeviceEnumerator? _cachedEnumerator;
    private static MMDevice? _cachedRenderDevice;
    private static readonly Dictionary<string, MMDevice> _cachedMicDevices = new();

    // ★ 反射访问 NAudio SessionCollection.audioSessionEnumerator（私有 COM 字段）。
    //   .NET 缓存 FieldInfo，避免每次都做类型查找。
    //   背景：NAudio 的 AudioSessionManager.RefreshSessions() 每次都调用
    //   IAudioSessionManager2.GetSessionEnumerator() 创建一个新的 COM 枚举器 RCW，
    //   直接覆盖 this.sessions 字段，从不对旧枚举器调用 Marshal.ReleaseComObject。
    //   SessionCollection 也不实现 IDisposable。结果是旧 RCW 只能等 GC 终结，
    //   实际上 COM 引用计数一直挂着，Windows Audio 服务 (audiodg) 内存随时间无限增长
    //   （用户实测 7GB）。这里在每次 RefreshSessions 前手动释放上一轮的枚举器，
    //   把每小时泄漏的约 54000 个 COM 引用清零。
    private static readonly FieldInfo? _sessionEnumeratorField =
        typeof(NAudio.CoreAudioApi.SessionCollection)
            .GetField("audioSessionEnumerator", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? _sessionsField =
        typeof(NAudio.CoreAudioApi.AudioSessionManager)
            .GetField("sessions", BindingFlags.Instance | BindingFlags.NonPublic);

    // 默认渲染设备变更检测节流：用 Environment.TickCount64 记录上次检查时间，
    // 约每 1s 比对一次默认设备 ID，避免每帧重取设备对象（保留性能优化意图）。
    private static long _lastDeviceCheckTick;
    private const int DeviceCheckIntervalMs = 1000;

    // ★ PID→进程名 映射缓存 + 节流。映射几乎不变（进程名从启动到退出不变），
    //   没必要每轮（15fps）都给全系统进程拍 Toolhelp 快照（几百进程 marshal + 字典分配 = 真实热点）。
    //   节流到约每 1s 重取一次。注意：峰值数值（MasterPeakValue）仍每轮实时读取，动态音量条不受影响，
    //   只是新启动 app 的名字最多晚 1s 进表（列表本身在 ViewModel 里就是 2s 刷一次，更慢）。
    private static Dictionary<int, string>? _cachedPidName;
    private static long _lastPidNameTick;
    private const int PidNameRefreshIntervalMs = 1000;

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

        // ★ 重置 PID→名字 映射缓存，下次 Start 立即重新快照（不复用上次会话的陈旧映射）
        _cachedPidName = null;
        _lastPidNameTick = 0;
    }

    private static async Task MonitorLoop()
    {
        while (_isRunning)
        {
            try
            {
                // ★ 复用缓存的枚举器
                _cachedEnumerator ??= new MMDeviceEnumerator();

                // ── App process peaks（一次性枚举，成本低，始终全监听）──
                try
                {
                    // (a) 冷启动/异常重建：首次获取默认渲染设备
                    _cachedRenderDevice ??= _cachedEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                    // (b) 节流感知默认设备变更（约每 1s 一次）：复用缓存的枚举器重取默认端点，
                    //     比对设备 ID——相同则丢弃临时对象；不同则 Dispose 旧设备并切换到新设备。
                    //     (a) 之后 _cachedRenderDevice 必非 null，故此处直接做节流检查。
                    var nowTick = Environment.TickCount64;
                    if (nowTick - _lastDeviceCheckTick >= DeviceCheckIntervalMs)
                    {
                        _lastDeviceCheckTick = nowTick;
                        var fresh = _cachedEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                        if (fresh.ID != _cachedRenderDevice.ID)
                        {
                            try { _cachedRenderDevice.Dispose(); } catch { }
                            _cachedRenderDevice = fresh; // 切换到新默认设备
                        }
                        else
                        {
                            try { fresh.Dispose(); } catch { } // 未变更，丢弃临时对象
                        }
                    }

                    // (c) ★ 关键修复：每轮刷新会话枚举，使首轮后新建的会话可见。
                    //     NAudio 的 Sessions 只在 RefreshSessions() 时重取快照，缓存设备不刷新就读不到新会话。
                    //
                    //     ★★★ COM 内存泄漏修复 ★★★
                    //     RefreshSessions() 内部每次都 GetSessionEnumerator() 创建新的 COM 枚举器，
                    //     直接覆盖旧 sessions 字段，从不 ReleaseComObject。15fps × 3600s = 54000 RCW/小时，
                    //     全部挂在 Windows Audio (audiodg) 进程上，用户实测内存涨到 7GB。
                    //     这里在 RefreshSessions 之前把旧 sessions.audioSessionEnumerator 释放掉。
                    ReleaseStaleSessionEnumerator(_cachedRenderDevice.AudioSessionManager);
                    _cachedRenderDevice.AudioSessionManager.RefreshSessions();
                    var sessions = _cachedRenderDevice.AudioSessionManager.Sessions;
                    // 本轮已写过的 PID：用于在同一轮内对同进程的多个会话取最大值，
                    // 而不会被后写的静默会话覆盖成 0（Edge/Apple Music 等多进程/多流 app 关键）。
                    var seenThisRound = new HashSet<int>();
                    // 本轮按名字聚合峰值（重建，保证随时间衰减）
                    var nameRound = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                    // ★ PID→进程名 映射：节流到约 1s 取一次（快照贵，映射几乎不变）。
                    //   峰值数值仍每轮实时读，动态音量条不受影响。
                    var nowTickName = Environment.TickCount64;
                    if (_cachedPidName == null || nowTickName - _lastPidNameTick >= PidNameRefreshIntervalMs)
                    {
                        _cachedPidName = ProcessEnumerator.GetPidNameMap();
                        _lastPidNameTick = nowTickName;
                    }
                    var pidName = _cachedPidName;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        using var session = sessions[i];
                        var pid = (int)session.GetProcessID;
                        if (pid == 0) continue;

                        // ★ MasterPeakValue 对某些 app（自身提升了流音量）可能 >1.0，
                        //   直接显示会出现 200%/400% 异常值。钳到 [0,1]。
                        float peak = session.AudioMeterInformation.MasterPeakValue;
                        if (peak < 0f) peak = 0f; else if (peak > 1f) peak = 1f;

                        if (seenThisRound.Add(pid))
                        {
                            // 本轮首个会话：直接刷新（保证随时间衰减，不会卡在旧的高值）
                            ProcessPeaks[pid] = peak;
                        }
                        else if (ProcessPeaks.TryGetValue(pid, out var cur) && peak > cur)
                        {
                            // 同进程的后续会话：取较大者（活跃流胜出，避免被静默流覆盖）
                            ProcessPeaks[pid] = peak;
                        }

                        // 按名字聚合（取该名字所有出声 PID 的最大值）
                        if (pidName.TryGetValue(pid, out var nm) && !string.IsNullOrEmpty(nm))
                        {
                            if (!nameRound.TryGetValue(nm, out var nc) || peak > nc)
                                nameRound[nm] = peak;
                        }
                    }

                    // 用本轮结果整体刷新按名字峰值表（移除已消失的名字，避免卡住旧值）
                    foreach (var kv in nameRound) ProcessPeaksByName[kv.Key] = kv.Value;
                    foreach (var key in ProcessPeaksByName.Keys.ToList())
                        if (!nameRound.ContainsKey(key)) ProcessPeaksByName.TryRemove(key, out _);
                }
                catch
                {
                    try { _cachedRenderDevice?.Dispose(); } catch { }
                    _cachedRenderDevice = null;
                }

                // ── Mic input peaks ──
                try
                {
                    var micCol = _cachedEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
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

                        // 读取该设备的峰值（读 meter 本身很便宜；无监听且非运行设备会返回 ~0）
                        MicPeaks[micId] = cachedMic.AudioMeterInformation.MasterPeakValue;
                    }

                    // ★ 按策略调谐静默监听集合（只开需要的，关掉不需要的）
                    ReconcileMicListeners(activeIds);

                    // 清理已断开的设备缓存
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
                    DisposeCachedMics();
                }
            }
            catch
            {
                DisposeCachedDevices();
            }

            await Task.Delay(_pollIntervalMs); // ★ 失焦时由 ViewModel 调大省 CPU
        }
    }

    /// <summary>
    /// 根据当前策略计算「需要静默监听」的麦克风集合，启动缺失的、停止多余的。
    /// 需要监听 = （监听全部 ? 所有 active 设备 : 仅选中设备），且排除正在被管线占用的设备。
    /// </summary>
    private static void ReconcileMicListeners(HashSet<string> activeIds)
    {
        var running = _runningMicId;

        // 计算目标监听集合
        var desired = new HashSet<string>();
        if (_monitorAllMics)
        {
            foreach (var id in activeIds) desired.Add(id);
        }
        else
        {
            var target = _targetMicId;
            if (target != null && activeIds.Contains(target)) desired.Add(target);
        }
        // 管线运行的麦克风已被 MicCapturer 唤醒，不需要再开静默监听
        if (running != null) desired.Remove(running);

        // 停止不再需要的监听
        var toStop = new List<string>();
        foreach (var id in _micListeners.Keys)
        {
            if (!desired.Contains(id)) toStop.Add(id);
        }
        foreach (var id in toStop)
        {
            if (_micListeners.TryRemove(id, out var cap))
            {
                try { cap.StopRecording(); cap.Dispose(); } catch { }
            }
        }

        // 启动新增需要的监听
        foreach (var id in desired)
        {
            if (!_micListeners.ContainsKey(id))
                EnsureMicListener(id);
        }
    }

    /// <summary>
    /// 对指定麦克风开一个静默捕获会话，唤醒其硬件 peak meter，使 AudioMeterInformation 返回真实值。
    /// 捕获数据直接丢弃。
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

            capture.DataAvailable += (_, _) => { }; // 数据丢弃
            capture.RecordingStopped += (_, e) =>
            {
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

    /// <summary>
    /// 在 RefreshSessions() 之前释放上一轮缓存的 <see cref="NAudio.CoreAudioApi.SessionCollection"/>
    /// 内部持有的 <c>IAudioSessionEnumerator</c> COM 对象，避免 RCW 泄漏到 audiodg 进程。
    ///
    /// 为什么需要：NAudio 的 AudioSessionManager.RefreshSessions() 每次都创建新的枚举器 RCW，
    /// 直接覆盖 sessions 字段，旧 RCW 只能等 GC 终结，COM 引用计数一直挂着，Windows Audio
    /// 服务内存随时间无限增长。本方法通过反射读取并主动 ReleaseComObject，把泄漏清零。
    /// </summary>
    private static void ReleaseStaleSessionEnumerator(NAudio.CoreAudioApi.AudioSessionManager mgr)
    {
        // 反射查找失败时静默跳过 —— 不影响功能，只是无法回收（极少见，NAudio 字段名变了才会发生）
        if (_sessionsField is null || _sessionEnumeratorField is null) return;
        try
        {
            if (_sessionsField.GetValue(mgr) is NAudio.CoreAudioApi.SessionCollection oldSessions)
            {
                if (_sessionEnumeratorField.GetValue(oldSessions) is object enumObj)
                {
                    // 释放底层 IAudioSessionEnumerator COM 引用。
                    // 释放后 RCW 失效，后续即使意外访问也只是返回 0/空，不会崩（且我们在 RefreshSessions 后才用新的 sessions）。
                    Marshal.ReleaseComObject(enumObj);
                }
            }
        }
        catch
        {
            // 反射/COM 释放失败不影响主轮询流程；最坏情况是这一轮不回收，下轮再试。
        }
    }

    private static void DisposeCachedDevices()
    {
        // ★ 退出/异常重建时释放最后一轮残留的 session enumerator COM 引用，
        //   避免 Stop 后 audiodg 仍持有引用直到 GC 终结（可能数分钟）。
        if (_cachedRenderDevice is not null)
        {
            try { ReleaseStaleSessionEnumerator(_cachedRenderDevice.AudioSessionManager); } catch { }
        }
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
