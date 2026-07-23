using System.Reflection;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace VoicePipe.Audio;

/// <summary>自动捕获全部真实播放端点的系统总声音，只作为 AEC 参考。</summary>
internal sealed class SystemRenderReferenceManager : IAecReferenceProvider, IDisposable
{
    private static readonly FieldInfo? MmDeviceCollectionField =
        typeof(MMDeviceCollection).GetField("mmDeviceCollection", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly string[] ExcludedMarkers =
    {
        "CABLE Input", "CABLE-A Input", "CABLE-B Input", "VB-Audio",
        "VoiceMeeter Input", "VoiceMeeter Aux Input", "VoiceMeeter VAIO",
        "Virtual Desktop Audio", "Virtual Audio Cable", "网易虚拟音频设备",
        // HyperX NGENUITY 虚拟音频路由设备（不是实体声学输出）
        "NGENUITY",
        // DeskIn 远程桌面虚拟音频设备
        "DeskIn"
    };

    private static readonly string[] ExcludedMetadataMarkers =
    {
        "NEVirtualDevice", "nevaudio", "VDVAD", "VBCable", "VB-Audio", "VoiceMeeter",
        // HyperX NGENUITY 虚拟驱动
        "NGENUITY",
        // DeskIn 虚拟驱动
        "DeskIn"
    };

    private readonly object _sync = new();
    private readonly Dictionary<string, (RenderReferenceChannel Channel, RenderReferenceCapturer Capturer)> _captures = new();
    // App 路径回退参考：当设备级 loopback 拿不到数据时（如某些 USB 音箱驱动不支持 loopback），
    // 进程级 loopback 捕获到的 App 音频作为 AEC 参考。这保证即使设备 loopback 失效，
    // 正在播放的音频仍能被 AEC 用作参考信号。
    private RenderReferenceChannel? _appFallbackChannel;
    // 无数据通道检测：记录每个通道上次有数据的时间，持续无数据的通道从快照中剔除，
    // 避免空通道拖慢 SpeexDSP 多参考收敛。
    private readonly Dictionary<string, long> _lastDataTick = new();
    // 已剔除的设备 ID 黑名单：防止 RefreshEndpoints 反复重新加入被剔除的设备，
    // 导致 SpeexDSP 反复重置、预适应状态丢失。
    private readonly HashSet<string> _deadEndpointBlacklist = new();
    private const long DeadChannelTimeoutMs = 10_000; // 10 秒无数据即剔除
    private long _lastChannelPruneTick;
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;
    private volatile RenderReferenceChannel[] _channelSnapshot = Array.Empty<RenderReferenceChannel>();
    private bool _enabled;
    private bool _disposed;
    private long _lastStatsLogTick;

    public int ChannelCount => _channelSnapshot.Length;

    public static bool IsExcludedEndpointName(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName)) return true;
        foreach (string marker in ExcludedMarkers)
            if (friendlyName.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static bool IsExcludedEndpointMetadata(string metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return false;
        foreach (string marker in ExcludedMetadataMarkers)
            if (metadata.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static bool IsExcludedEndpoint(string friendlyName, string metadata)
        => IsExcludedEndpointName(friendlyName) || IsExcludedEndpointMetadata(metadata);

    public void SetEnabled(bool enabled)
    {
        CancellationTokenSource? startCts = null;
        lock (_sync)
        {
            if (_disposed || _enabled == enabled) return;
            _enabled = enabled;
            if (enabled)
            {
                startCts = new CancellationTokenSource();
                _cts = startCts;
                // 创建 App 回退参考通道（始终存在，只有被喂入数据时才有内容）
                _appFallbackChannel ??= new RenderReferenceChannel("app-fallback", "App路径回退参考");
            }
            else
            {
                _appFallbackChannel = null;
            }
        }

        if (!enabled)
        {
            Stop();
            return;
        }

        // 端点枚举、WASAPI 创建和 StartRecording 都是慢 COM 操作，绝不在 UI/音频线程同步执行。
        var token = startCts!.Token;
        _refreshTask = Task.Run(async () =>
        {
            RefreshEndpoints();
            while (!token.IsCancellationRequested)
            {
                try { await Task.Delay(2000, token); }
                catch (OperationCanceledException) { break; }
                if (!token.IsCancellationRequested) RefreshEndpoints();
                if (!token.IsCancellationRequested) PruneDeadChannels();
                if (!token.IsCancellationRequested) LogReferenceStatsIfDue();
            }
        }, token);
    }

    private void RefreshEndpoints()
    {
        var found = new Dictionary<string, (MMDevice Device, string Name)>();
        MMDeviceCollection? collection = null;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            collection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            for (int i = 0; i < collection.Count; i++)
            {
                var device = collection[i];
                try
                {
                    string name = device.FriendlyName;
                    string metadata = EndpointMetadataReader.GetRenderEndpointMetadata(device.ID);
                    if (IsExcludedEndpoint(name, metadata))
                    {
                        device.Dispose();
                        continue;
                    }
                    // 跳过已剔除黑名单中的设备，防止反复加入->剔除->重置 SpeexDSP
                    if (_deadEndpointBlacklist.Contains(device.ID))
                    {
                        device.Dispose();
                        continue;
                    }
                    found[device.ID] = (device, name);
                }
                catch
                {
                    try { device.Dispose(); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "SystemRenderReferenceManager: 枚举播放端点失败");
        }
        finally
        {
            if (collection != null) ReleaseCollection(collection);
        }

        List<RenderReferenceCapturer> toDispose = new();
        lock (_sync)
        {
            if (!_enabled || _disposed)
            {
                foreach (var item in found.Values) item.Device.Dispose();
                return;
            }

            foreach (string existing in _captures.Keys.Where(id => !found.ContainsKey(id)).ToList())
            {
                toDispose.Add(_captures[existing].Capturer);
                Serilog.Log.Information("AEC参考设备移除: {Name}", _captures[existing].Channel.DeviceName);
                _captures.Remove(existing);
            }

            foreach (var pair in found.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (_captures.ContainsKey(pair.Key))
                {
                    pair.Value.Device.Dispose();
                    continue;
                }

                var channel = new RenderReferenceChannel(pair.Key, pair.Value.Name);
                var capturer = new RenderReferenceCapturer(pair.Value.Device, channel);
                try
                {
                    capturer.Start();
                    _captures[pair.Key] = (channel, capturer);
                    Serilog.Log.Information("AEC参考设备加入: {Name}", pair.Value.Name);
                }
                catch (Exception ex)
                {
                    capturer.Dispose();
                    Serilog.Log.Warning(ex, "SystemRenderReferenceManager: 参考设备启动失败 {Name}", pair.Value.Name);
                }
            }
            // App 回退通道始终加入快照：它只有在被 FeedAppReference 喂入数据时才有内容。
            // 如果设备级 loopback 正常工作，设备通道已有相同音频，AEC 多一路相同参考不会造成问题
            // （SpeexDSP MCF 可以处理多参考通道）；如果设备级 loopback 失效，这路是唯一的参考。
            // 新通道初始标记为"刚加入"，给 10 秒宽限期等数据到达
            long now = Environment.TickCount64;
            foreach (var pair in found)
            {
                if (!_lastDataTick.ContainsKey(pair.Key))
                    _lastDataTick[pair.Key] = now;
            }
            RebuildChannelSnapshot();
        }

        foreach (var capturer in toDispose) _ = Task.Run(capturer.Dispose);
    }

    private void LogReferenceStatsIfDue()
    {
        long now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastStatsLogTick) < 60_000) return;
        Interlocked.Exchange(ref _lastStatsLogTick, now);

        foreach (var channel in _channelSnapshot)
        {
            RenderReferenceStats stats = channel.GetStats();
            Serilog.Log.Information(
                "AEC参考统计: Device={Device} Buffered={Buffered} Drop={Dropped} Repeat={Repeated} UnderrunFrames={Underruns} OverrunSamples={Overruns}",
                stats.DeviceName,
                stats.BufferedSamples,
                stats.DriftDroppedSamples,
                stats.DriftRepeatedSamples,
                stats.UnderrunFrames,
                stats.OverrunSamples);
        }
    }

    /// <summary>
    /// 将进程级 loopback 捕获的 App 音频喂入 AEC 参考回退通道。
    /// 当设备级 loopback 失效（如 USB 音箱驱动不支持）时，这是唯一的参考来源。
    /// 输入为交错立体声 float（与 FeedApp 相同格式），内部下混为单声道。
    /// </summary>
    private float[] _appRefMono = new float[4096];
    public void FeedAppReference(float[] samples, int count)
    {
        if (count <= 0 || _appFallbackChannel == null) return;

        // 下混立体声为单声道
        int frames = count / 2;
        if (_appRefMono.Length < frames)
            Array.Resize(ref _appRefMono, frames);
        for (int i = 0; i < frames; i++)
            _appRefMono[i] = (samples[i * 2] + samples[i * 2 + 1]) * 0.5f;

        _appFallbackChannel.Write(_appRefMono, frames);
        // 标记 App 回退通道有数据
        _lastDataTick[_appFallbackChannel.DeviceId] = Environment.TickCount64;
    }

    public bool TryReadFrame(float[] interleaved, int frameSize)
    {
        var channels = _channelSnapshot;
        if (channels.Length == 0 || interleaved.Length < frameSize * channels.Length) return false;

        Array.Clear(interleaved, 0, frameSize * channels.Length);
        bool any = false;
        long now = Environment.TickCount64;
        for (int channel = 0; channel < channels.Length; channel++)
        {
            bool hadAudio = channels[channel].ReadFrame(interleaved, 0, frameSize, channel, channels.Length);
            if (hadAudio)
            {
                any = true;
                // 记录此通道最近有数据的时间
                _lastDataTick[channels[channel].DeviceId] = now;
            }
        }
        return any;
    }

    /// <summary>
    /// 剔除持续无数据的参考通道（如 USB 音箱不支持 loopback 的设备级捕获）。
    /// 这些空通道让 SpeexDSP 多参考滤波器浪费算力、拖慢收敛。
    /// 剔除后重新配置 SpeexDSP 的通道数，只保留有实际音频的通道。
    /// </summary>
    private void PruneDeadChannels()
    {
        long now = Environment.TickCount64;
        // 只每 10 秒检查一次
        if (now - Interlocked.Read(ref _lastChannelPruneTick) < DeadChannelTimeoutMs) return;
        Interlocked.Exchange(ref _lastChannelPruneTick, now);

        List<string>? toRemove = null;
        foreach (var kv in _lastDataTick)
        {
            if (now - kv.Value > DeadChannelTimeoutMs)
            {
                (toRemove ??= new List<string>()).Add(kv.Key);
            }
        }
        if (toRemove == null) return;

        lock (_sync)
        {
            if (!_enabled || _disposed) return;

            bool changed = false;
            foreach (var id in toRemove)
            {
                if (_captures.TryGetValue(id, out var entry))
                {
                    Serilog.Log.Information("AEC参考设备剔除（持续无数据）: {Name}", entry.Channel.DeviceName);
                    _ = Task.Run(() => { try { entry.Capturer.Dispose(); } catch { } });
                    _captures.Remove(id);
                    _lastDataTick.Remove(id);
                    _deadEndpointBlacklist.Add(id); // 加入黑名单，防止 RefreshEndpoints 反复重新加入
                    changed = true;
                }
            }

            if (changed)
            {
                RebuildChannelSnapshot();
            }
        }
    }

    private void RebuildChannelSnapshot()
    {
        var channels = _captures.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => p.Value.Channel)
            .ToList();
        if (_appFallbackChannel != null)
            channels.Add(_appFallbackChannel);
        _channelSnapshot = channels.ToArray();
    }

    private void Stop()
    {
        CancellationTokenSource? cts;
        RenderReferenceCapturer[] captures;
        lock (_sync)
        {
            cts = _cts;
            _cts = null;
            captures = _captures.Values.Select(x => x.Capturer).ToArray();
            _captures.Clear();
            _lastDataTick.Clear();
            _deadEndpointBlacklist.Clear();
            _appFallbackChannel = null;
            _channelSnapshot = Array.Empty<RenderReferenceChannel>();
        }
        try { cts?.Cancel(); } catch { }
        try { cts?.Dispose(); } catch { }
        foreach (var capture in captures) _ = Task.Run(capture.Dispose);
    }

    private static void ReleaseCollection(MMDeviceCollection collection)
    {
        if (MmDeviceCollectionField == null) return;
        try
        {
            if (MmDeviceCollectionField.GetValue(collection) is object com)
                Marshal.ReleaseComObject(com);
        }
        catch { }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _enabled = false;
        }
        Stop();
    }
}
