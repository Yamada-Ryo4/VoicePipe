using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace VoicePipe.Audio;

public record MicInfo(string Id, string Name);

public class MicCapturer : IDisposable
{
    private WasapiCapture? _capture;
    private MMDevice? _device;
    private bool _disposed;
    private readonly object _sync = new(); // ★ 保护 _capture/_device，防止后台初始化与 Stop 并发竞态 (B3)

    // ★ 当前正在捕获的设备 ID（启动后由后台线程赋值，主线程读）。
    // PipelineManager 据此判断启动新管线时是否还在采同一个麦克风，能否跳过整次拆建。
    private volatile string? _currentDeviceId;
    public string? CurrentDeviceId => _currentDeviceId;
    public bool IsAlive => _capture != null && !_disposed;

    public event EventHandler<(float[] Samples, int Count, WaveFormat Format)>? SamplesAvailable;
    public WaveFormat? OutputFormat => _capture?.WaveFormat;

    // ★ 复用的转换缓冲区，避免每次 DataAvailable 回调都 new float[]。
    // 安全性：消费链同步（FeedMic → Resample → RingBuffer.Write 立即拷走），回调返回前数据已被复制。
    private float[] _convertBuffer = new float[8192];

    /// <summary>在后台线程上初始化并启动 WASAPI 捕获，避免阻塞 UI 线程。</summary>
    public void Start(string deviceId)
    {
        Stop();

        // 在 MTA 线程中做 WASAPI 初始化
        Task.Run(() =>
        {
            try
            {
                lock (_sync)
                {
                    if (_disposed) return; // 已释放则不再初始化

                    using var enumerator = new MMDeviceEnumerator();
                    _device = enumerator.GetDevice(deviceId);

                    _capture = new WasapiCapture(_device)
                    {
                        ShareMode = AudioClientShareMode.Shared,
                    };

                    _capture.DataAvailable += OnDataAvailable;
                    _capture.RecordingStopped += (_, e) =>
                    {
                        if (e.Exception != null)
                            Serilog.Log.Error(e.Exception, "MicCapturer: 录音停止异常");
                        else
                            Serilog.Log.Information("MicCapturer: 停止");
                    };
                    _capture.StartRecording();
                    _currentDeviceId = deviceId; // ★ 记录设备 ID 供复用判断
                    Serilog.Log.Information("MicCapturer: 开始捕获 {Id} 格式={Rate}Hz/{Ch}ch/{Bits}bit {Enc}",
                        deviceId,
                        _capture.WaveFormat.SampleRate,
                        _capture.WaveFormat.Channels,
                        _capture.WaveFormat.BitsPerSample,
                        _capture.WaveFormat.Encoding);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "MicCapturer: 初始化失败");
                Stop();
            }
        });
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // ★ 用 sender（触发事件的 WasapiCapture 实例）而非字段 _capture：
        //   避免与并发 Stop()（置 _capture=null）产生空引用竞态。
        if (e.BytesRecorded == 0 || sender is not WasapiCapture capture) return;
        var fmt = capture.WaveFormat;
        int count = ConvertToFloat32(e.Buffer, e.BytesRecorded, fmt);
        if (count > 0)
            SamplesAvailable?.Invoke(this, (_convertBuffer, count, fmt));
    }

    /// <summary>
    /// 将 PCM 字节转换为 float32，写入复用缓冲 _convertBuffer，返回有效样本数。
    /// </summary>
    private int ConvertToFloat32(byte[] buffer, int bytesRecorded, WaveFormat fmt)
    {
        if (fmt.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            int sampleCount = bytesRecorded / 4;
            if (_convertBuffer.Length < sampleCount) _convertBuffer = new float[sampleCount];
            Buffer.BlockCopy(buffer, 0, _convertBuffer, 0, bytesRecorded);
            return sampleCount;
        }
        else
        {
            int bytesPerSample = fmt.BitsPerSample / 8;
            int sampleCount = bytesRecorded / bytesPerSample;
            if (_convertBuffer.Length < sampleCount) _convertBuffer = new float[sampleCount];
            var floats = _convertBuffer;

            switch (fmt.BitsPerSample)
            {
                case 16:
                    for (int i = 0; i < sampleCount; i++)
                        floats[i] = BitConverter.ToInt16(buffer, i * 2) / 32768f;
                    break;
                case 24:
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int offset = i * 3;
                        // 24-bit signed: shift into high 3 bytes of int32, then shift back for sign extension
                        int sample = (buffer[offset] << 8) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 24);
                        floats[i] = (sample >> 8) / 8388608f; // 2^23
                    }
                    break;
                case 32:
                    for (int i = 0; i < sampleCount; i++)
                        floats[i] = BitConverter.ToInt32(buffer, i * 4) / 2147483648f; // 2^31
                    break;
                default:
                    Serilog.Log.Warning("MicCapturer: 不支持的位深度 {Bits}", fmt.BitsPerSample);
                    return 0;
            }
            return sampleCount;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_capture != null)
            {
                try
                {
                    _capture.StopRecording();
                    _capture.DataAvailable -= OnDataAvailable;
                    _capture.Dispose();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "MicCapturer: 停止时异常");
                }
                _capture = null;
            }
            _device?.Dispose();
            _device = null;
            _currentDeviceId = null;
        }
    }

    // VB-Audio 虚拟线的录音端（CABLE Output 等）必须从麦克风列表排除：
    // 它是 VoicePipe 输出目标 CABLE Input 的另一端，选它当麦克风会形成
    // 混音→CABLE Input→CABLE Output→采集→再混音 的反馈环路，声音指数级冲到满刻度（嗡嗡/嘟嘟）。
    private static readonly string[] LoopbackMarkers =
    {
        "CABLE Output",   // VB-Cable 主线录音端
        "CABLE-A Output", // VB-Cable A+B
        "CABLE-B Output",
        "VB-Audio",       // VB-Audio 系列虚拟设备统称
        "VoiceMeeter Out",// VoiceMeeter 虚拟输出端
        "VoiceMeeter Aux Out",
    };

    private static bool IsLoopbackCaptureDevice(string friendlyName)
    {
        foreach (var m in LoopbackMarkers)
            if (friendlyName.Contains(m, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// 按设备 ID 判断是否为虚拟回环录音端（CABLE Output 等）。
    /// 供管线启动前兜底校验：即使设置里残留了回环设备 ID，也拒绝启动以防反馈环路。
    /// 解析失败（设备不存在）时返回 false（让正常流程处理"找不到设备"）。
    /// </summary>
    public static bool IsLoopbackDeviceId(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return false;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var dev = enumerator.GetDevice(deviceId);
            return dev != null && IsLoopbackCaptureDevice(dev.FriendlyName);
        }
        catch
        {
            return false;
        }
    }

    public static List<MicInfo> GetAvailableMics()
    {
        var result = new List<MicInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var col = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            for (int i = 0; i < col.Count; i++)
            {
                using var dev = col[i];
                // ★ 过滤虚拟回环录音端，防止反馈环路（嗡嗡/嘟嘟声）
                if (IsLoopbackCaptureDevice(dev.FriendlyName))
                {
                    // ★ 每个设备只记一次日志（GetAvailableMics 每 2 秒调一次，否则刷屏）
                    if (_loggedExcludedDevices.Add(dev.ID))
                        Serilog.Log.Information("MicCapturer: 已排除虚拟回环设备 {Name}（防反馈环路）", dev.FriendlyName);
                    continue;
                }
                result.Add(new MicInfo(dev.ID, dev.FriendlyName));
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "MicCapturer: 枚举麦克风失败");
        }
        return result;
    }

    // 已记录过"已排除"日志的回环设备 ID 集合，避免每 2 秒枚举重复刷屏
    private static readonly HashSet<string> _loggedExcludedDevices = new();

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        Stop();
    }
}