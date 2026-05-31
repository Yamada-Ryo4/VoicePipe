using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace VoicePipe.Audio;

/// <summary>
/// 使用 WASAPI Per-Process Loopback 实现真正的进程级音频隔离捕获。
///
/// 实现原理：
/// 1. 通过 Windows 10 Build 19041+ 的 AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK
///    激活 IAudioClient，获取特定 PID 的音频流。
/// 2. 完全不捕获其他进程（包括游戏本身）的音频 → 零回声。
/// 3. 原始扬声器输出不受影响。
///
/// 容错机制：
/// CaptureLoop 被包裹在重试循环中（CaptureLoopWithRetry），
/// 当 WASAPI 抛出瞬态 COMException（如 E_UNEXPECTED）时自动重试，
/// 指数退避（1s→2s→4s→5s 上限），最多重试 10 次。
/// 成功捕获数据后重置重试计数器。
/// </summary>
public class LoopbackCapturer : IDisposable
{
    private Thread?          _captureThread;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    // 重试配置
    private const int MaxRetries = 10;
    private const int MaxBackoffSeconds = 5;

    // 重试计数器，CaptureLoop 成功后由外层重置
    private volatile int _consecutiveFailures;

    // ★ 暂停标志：非活跃 PID 的 capturer 设为 true，
    // 捕获循环仍排空 WASAPI 缓冲（防止缓冲满报错），但跳过内存拷贝和事件触发，
    // 避免停止后 / 多源切换后旧 capturer 在后台空转浪费 CPU + GC。
    private volatile bool _paused;
    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    public event EventHandler<(float[] Samples, int Count)>? SamplesAvailable;

    // ★ 复用的样本缓冲区，避免每个 WASAPI packet 都 new float[]（持续 GC 压力）。
    // 安全性：SamplesAvailable 的消费链是同步的（FeedApp → RingBuffer.Write 立即 Array.Copy 拷走），
    // 回调返回前数据已被复制走，复用同一缓冲不会有数据竞争。
    private float[] _sampleBuffer = new float[8192];

    /// <summary>
    /// 捕获彻底失败事件（超过最大重试次数后触发）。
    /// string 参数为错误描述信息。
    /// </summary>
    public event EventHandler<string>? CaptureFailed;

    public WaveFormat OutputFormat { get; } =
        WaveFormat.CreateIeeeFloatWaveFormat(AudioFormat.SampleRate, AudioFormat.Channels);

    public Task StartAsync(int targetPid)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _consecutiveFailures = 0;
        var token = _cts.Token;

        _captureThread = new Thread(() => CaptureLoopWithRetry(targetPid, token))
        {
            IsBackground = true,
            Name = $"LoopbackCapture-PID{targetPid}"
        };
        _captureThread.Start();

        Serilog.Log.Information("LoopbackCapturer: Per-Process 捕获启动 PID={Pid}", targetPid);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 带自动重试的捕获入口。
    /// 当 CaptureLoop 因 COMException 等瞬态错误退出时，
    /// 自动重试（指数退避），直到成功或达到最大重试次数。
    /// </summary>
    private void CaptureLoopWithRetry(int pid, CancellationToken token)
    {
        while (!token.IsCancellationRequested && _consecutiveFailures < MaxRetries)
        {
            try
            {
                CaptureLoop(pid, token);
                // CaptureLoop 正常退出 = 被 CancellationToken 取消
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                int delaySeconds = Math.Min((int)Math.Pow(2, _consecutiveFailures - 1), MaxBackoffSeconds);
                Serilog.Log.Warning(ex,
                    "LoopbackCapturer: 捕获失败（第 {N}/{Max} 次），{D}s 后重试 PID={Pid}",
                    _consecutiveFailures, MaxRetries, delaySeconds, pid);

                // 等待退避时间（如果被取消则立即退出）
                if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(delaySeconds)))
                    return;
            }
        }

        if (_consecutiveFailures >= MaxRetries && !token.IsCancellationRequested)
        {
            var msg = $"应用音频捕获失败（PID={pid}），已重试 {MaxRetries} 次";
            Serilog.Log.Error("LoopbackCapturer: {Msg}", msg);
            CaptureFailed?.Invoke(this, msg);
        }
    }

    /// <summary>
    /// 核心捕获循环：通过 COM 接口激活 WASAPI Per-Process Loopback。
    /// 异常直接向上抛出，由 CaptureLoopWithRetry 处理重试逻辑。
    /// </summary>
    private unsafe void CaptureLoop(int pid, CancellationToken token)
    {
        IAudioClient? audioClient = null;
        IAudioCaptureClient? captureClient = null;
        IntPtr eventHandle = IntPtr.Zero;
        bool started = false;
        try
        {
            // 激活 Per-Process Loopback IAudioClient
            audioClient = ActivateProcessLoopbackClient(pid);

            // 初始化为共享模式事件驱动
            var wfx = new WAVEFORMATEX
            {
                wFormatTag     = 3,      // WAVE_FORMAT_IEEE_FLOAT
                nChannels      = 2,
                nSamplesPerSec = AudioFormat.SampleRate,
                wBitsPerSample = 32,
                nBlockAlign    = 8,      // 2ch * 4bytes
                nAvgBytesPerSec= AudioFormat.SampleRate * 8,
                cbSize         = 0
            };

            const int AUDCLNT_STREAMFLAGS_LOOPBACK         = 0x00020000;
            const int AUDCLNT_STREAMFLAGS_EVENTCALLBACK    = 0x00040000;

            int hresult = audioClient.Initialize(
                AUDCLNT_SHAREMODE.AUDCLNT_SHAREMODE_SHARED,
                AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_EVENTCALLBACK,
                200_0000L,  // 200ms buffer (in 100ns units)
                0,
                ref wfx,
                Guid.Empty);

            if (hresult < 0) Marshal.ThrowExceptionForHR(hresult);

            // 获取 IAudioCaptureClient — 必须在 finally 中释放！
            var captureGuid = typeof(IAudioCaptureClient).GUID;
            audioClient.GetService(ref captureGuid, out object captureObj);
            captureClient = (IAudioCaptureClient)captureObj;

            // 创建事件句柄
            eventHandle = CreateEventW(IntPtr.Zero, false, false, null);
            audioClient.SetEventHandle(eventHandle);
            audioClient.Start();
            started = true;

            Serilog.Log.Information("LoopbackCapturer: WASAPI 流已启动");

            // 成功启动 → 重置连续失败计数
            _consecutiveFailures = 0;

            while (!token.IsCancellationRequested)
            {
                // 等待音频数据就绪（最多 100ms）
                WaitForSingleObject(eventHandle, 100);
                if (token.IsCancellationRequested) break;

                // 读取所有可用帧
                uint packetSize;
                captureClient.GetNextPacketSize(out packetSize);

                while (packetSize > 0 && !token.IsCancellationRequested)
                {
                    captureClient.GetBuffer(
                        out IntPtr dataPtr,
                        out uint numFrames,
                        out uint flags,
                        out ulong devicePos,
                        out ulong qpcPos);

                    const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x2;
                    bool isSilent = (flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0;

                    // ★ 暂停状态：仍需 ReleaseBuffer 排空 WASAPI 缓冲（否则缓冲满会报错），
                    // 但跳过内存拷贝和事件触发，避免空转开销。
                    if (_paused)
                    {
                        captureClient.ReleaseBuffer(numFrames);
                        captureClient.GetNextPacketSize(out packetSize);
                        continue;
                    }

                    int sampleCount = (int)(numFrames * 2); // 2ch

                    // ★ 复用缓冲区，按需扩容，避免每包 new float[]
                    if (_sampleBuffer.Length < sampleCount)
                        _sampleBuffer = new float[sampleCount];
                    var samples = _sampleBuffer;

                    if (!isSilent && dataPtr != IntPtr.Zero)
                    {
                        // 直接从非托管内存复制 float32 PCM
                        fixed (float* dest = samples)
                        {
                            Buffer.MemoryCopy(
                                (void*)dataPtr,
                                dest,
                                sampleCount * 4,
                                sampleCount * 4);
                        }
                    }
                    else
                    {
                        // 静音包：清零有效区间（复用缓冲可能残留上一包数据）
                        Array.Clear(samples, 0, sampleCount);
                    }

                    captureClient.ReleaseBuffer(numFrames);
                    // ★ 携带有效样本数，消费方只读 Count 个，不读整个复用缓冲
                    SamplesAvailable?.Invoke(this, (samples, sampleCount));
                    captureClient.GetNextPacketSize(out packetSize);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw; }
        finally
        {
            // ★ 必须按正确顺序释放所有 COM 对象，否则 Windows 认为旧会话仍活着
            // 顺序：Stop → 释放 CaptureClient → 释放 AudioClient → 关闭事件
            if (started)
            {
                try { audioClient?.Stop(); } catch { }
            }

            if (captureClient != null)
            {
                try { Marshal.ReleaseComObject(captureClient); } catch { }
                captureClient = null;
            }

            if (audioClient != null)
            {
                try { Marshal.ReleaseComObject(audioClient); } catch { }
                audioClient = null;
            }

            if (eventHandle != IntPtr.Zero)
                CloseHandle(eventHandle);
        }
    }

    /// <summary>
    /// 通过 ActivateAudioInterfaceAsync 激活 Per-Process Loopback IAudioClient。
    /// Windows 10 Build 19041 (20H1) 引入，
    /// 使用 AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK。
    /// </summary>
    private static IAudioClient ActivateProcessLoopbackClient(int pid)
    {
        // 构建激活参数结构体
        var activateParams = new AUDIOCLIENT_ACTIVATION_PARAMS
        {
            ActivationType       = AUDIOCLIENT_ACTIVATION_TYPE.PROCESS_LOOPBACK,
            ProcessLoopbackParams = new AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS
            {
                TargetProcessId  = (uint)pid,
                ProcessLoopbackMode = PROCESS_LOOPBACK_MODE.INCLUDE_TARGET_PROCESS_TREE,
            }
        };

        // 通过 PROPVARIANT 传递激活参数
        var propVariant = new PROPVARIANT_BLOB
        {
            vt       = 65,  // VT_BLOB
            blobSize = (uint)Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>(),
            blobData = Marshal.AllocHGlobal(Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>())
        };

        Marshal.StructureToPtr(activateParams, propVariant.blobData, false);

        try
        {
            var audioClientGuid = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

            var completionHandler = new ActivateAudioInterfaceCompletionHandler();
            ActivateAudioInterfaceAsync(
                VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK,
                ref audioClientGuid,
                ref propVariant,
                completionHandler,
                out _);

            completionHandler.WaitForCompletion();

            completionHandler.GetActivateResult(out int activateHr, out object activatedInterface);
            if (activateHr < 0) Marshal.ThrowExceptionForHR(activateHr);

            return (IAudioClient)activatedInterface;
        }
        finally
        {
            Marshal.FreeHGlobal(propVariant.blobData);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _captureThread?.Join(2000);
        _captureThread = null;
        _cts?.Dispose();
        _cts = null;
    }

    public Task StopAsync()
    {
        Stop();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }

    // ────────────────────────────────────────────────────────
    // P/Invoke 声明
    // ────────────────────────────────────────────────────────

    private const string VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK =
        "VAD\\Process_Loopback";

    [DllImport("Mmdevapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        ref Guid riid,
        ref PROPVARIANT_BLOB activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);

    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateEventW(
        IntPtr lpEventAttributes, bool bManualReset,
        bool bInitialState, [MarshalAs(UnmanagedType.LPWStr)] string? lpName);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    // 注：早期实现曾考虑显式 CoInitializeEx/CoUninitialize，但 .NET 对后台线程的 MTA COM 调用
    // 已自动处理，这里不再需要显式调用。原 P/Invoke 声明已移除。

    // ────────────────────────────────────────────────────────
    // 原生结构体和接口
    // ────────────────────────────────────────────────────────

    private enum AUDIOCLIENT_ACTIVATION_TYPE : uint
    {
        DEFAULT          = 0,
        PROCESS_LOOPBACK = 1,
    }

    private enum PROCESS_LOOPBACK_MODE : uint
    {
        INCLUDE_TARGET_PROCESS_TREE = 0,
        EXCLUDE_TARGET_PROCESS_TREE = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS
    {
        public uint              TargetProcessId;
        public PROCESS_LOOPBACK_MODE ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIOCLIENT_ACTIVATION_PARAMS
    {
        public AUDIOCLIENT_ACTIVATION_TYPE ActivationType;
        public AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS ProcessLoopbackParams;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT_BLOB
    {
        public ushort  vt;
        public ushort  wReserved1;
        public ushort  wReserved2;
        public ushort  wReserved3;
        public uint    blobSize;
        public IntPtr  blobData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint   nSamplesPerSec;
        public uint   nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    private enum AUDCLNT_SHAREMODE
    {
        AUDCLNT_SHAREMODE_SHARED    = 0,
        AUDCLNT_SHAREMODE_EXCLUSIVE = 1,
    }

    /// <summary>
    /// IAudioClient COM 接口 — 只声明 IAudioClient 的 12 个方法。
    /// 不要用 IAudioClient3 的 GUID，很多音频驱动不支持 IAudioClient3。
    /// </summary>
    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig]
        int Initialize(AUDCLNT_SHAREMODE shareMode, uint streamFlags,
            long hnsBufferDuration, long hnsPeriodicity,
            ref WAVEFORMATEX pFormat, Guid audioSessionGuid);
        void GetBufferSize(out uint pNumBufferFrames);
        void GetStreamLatency(out long phnsLatency);
        void GetCurrentPadding(out uint pNumPaddingFrames);
        void IsFormatSupported(AUDCLNT_SHAREMODE shareMode, ref WAVEFORMATEX pFormat,
            IntPtr ppClosestMatch);
        void GetMixFormat(out IntPtr ppDeviceFormat);
        void GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
        void Start();
        void Stop();
        void Reset();
        void SetEventHandle(IntPtr eventHandle);
        [PreserveSig]
        int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig]
        int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead,
            out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
        [PreserveSig]
        int ReleaseBuffer(uint numFramesRead);
        [PreserveSig]
        int GetNextPacketSize(out uint pNumFramesInNextPacket);
    }

    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
    }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceAsyncOperation
    {
        void GetActivateResult([MarshalAs(UnmanagedType.Error)] out int activateResult,
            [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
    }

    /// <summary>ActivateAudioInterfaceAsync 完成回调处理器</summary>
    private class ActivateAudioInterfaceCompletionHandler
        : IActivateAudioInterfaceCompletionHandler
    {
        private IActivateAudioInterfaceAsyncOperation? _operation;
        private readonly ManualResetEventSlim _completedEvent = new(false);

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
        {
            _operation = activateOperation;
            _completedEvent.Set();
        }

        public void WaitForCompletion(int timeoutMs = 5000)
        {
            if (!_completedEvent.Wait(timeoutMs))
                throw new TimeoutException("ActivateAudioInterfaceAsync 超时");
        }

        public void GetActivateResult(out int hr, out object activatedInterface)
        {
            if (_operation == null)
                throw new InvalidOperationException("激活尚未完成");
            _operation.GetActivateResult(out hr, out activatedInterface);
        }
    }
}
