using System.Runtime.InteropServices;

namespace VoicePipe.Audio;

/// <summary>WebRTC AEC3 回声消除；48kHz/480-sample 单声道，收敛速度快于 SpeexDSP。</summary>
internal sealed class WebRtcAecProcessor : IAudioFrameEchoCanceller
{
    private const string Lib = "webrtc-apm";
    private const int FrameSize = 480;

    // === P/Invoke: webrtc-apm.dll C API ===

    [DllImport(Lib)] private static extern IntPtr webrtc_apm_create();
    [DllImport(Lib)] private static extern void webrtc_apm_destroy(IntPtr apm);
    [DllImport(Lib)] private static extern int webrtc_apm_get_frame_size(int sampleRateHz);

    [DllImport(Lib)] private static extern IntPtr webrtc_apm_config_create();
    [DllImport(Lib)] private static extern void webrtc_apm_config_destroy(IntPtr config);
    [DllImport(Lib)] private static extern void webrtc_apm_config_set_echo_canceller(IntPtr config, int enabled, int mobileMode);
    [DllImport(Lib)] private static extern void webrtc_apm_config_set_high_pass_filter(IntPtr config, int enabled);
    [DllImport(Lib)] private static extern void webrtc_apm_config_set_noise_suppression(IntPtr config, int enabled, int level);
    [DllImport(Lib)] private static extern void webrtc_apm_config_set_pipeline(IntPtr config, int maxInternalRate, int multiChannelRender, int multiChannelCapture, int downmixMethod);
    [DllImport(Lib)] private static extern int webrtc_apm_apply_config(IntPtr apm, IntPtr config);
    [DllImport(Lib)] private static extern int webrtc_apm_initialize(IntPtr apm);

    [DllImport(Lib)] private static extern IntPtr webrtc_apm_stream_config_create(int sampleRateHz, UIntPtr numChannels);
    [DllImport(Lib)] private static extern void webrtc_apm_stream_config_destroy(IntPtr config);

    // process_reverse_stream: 喂参考音频（远端/扬声器），让 AEC3 学习回声路径
    // WebRTC APM 使用 planar float：传入 IntPtr[]（每通道一个指针），每个指针指向 float[samples]
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int webrtc_apm_process_reverse_stream(IntPtr apm, IntPtr[] src, IntPtr inputConfig, IntPtr outputConfig, IntPtr[] dest);

    // process_stream: 处理麦克风音频（近端），输出回声消除后的结果
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int webrtc_apm_process_stream(IntPtr apm, IntPtr[] src, IntPtr inputConfig, IntPtr outputConfig, IntPtr[] dest);

    [DllImport(Lib)] private static extern void webrtc_apm_set_stream_delay_ms(IntPtr apm, int delay);

    private readonly object _stateLock = new();
    private IntPtr _apm;
    private IntPtr _captureConfig; // 麦克风输入/输出配置（48kHz/1ch）
    private IntPtr _reverseConfig; // 参考输入配置（48kHz/1ch）
    // Planar float buffers: float[1][480] for mono
    private readonly float[] _micFloat = new float[FrameSize];
    private readonly float[] _refFloat = new float[FrameSize];
    private readonly float[] _outFloat = new float[FrameSize];
    private bool _disposed;

    public bool Available { get; private set; }
    public int ReferenceChannels { get; private set; }

    public WebRtcAecProcessor()
    {
        try
        {
            NativeLibrary.Load("webrtc-apm.dll");
            _apm = webrtc_apm_create();
            if (_apm == IntPtr.Zero) throw new InvalidOperationException("webrtc_apm_create returned null");

            // 配置：启用 AEC3（desktop 模式，非 mobile）、高通滤波器
            IntPtr config = webrtc_apm_config_create();
            try
            {
                webrtc_apm_config_set_echo_canceller(config, 1, 0); // enabled=1, mobileMode=0 (AEC3)
                webrtc_apm_config_set_high_pass_filter(config, 1);
                webrtc_apm_config_set_noise_suppression(config, 0, 0); // 关闭 NS（RNNoise 已处理）
                webrtc_apm_config_set_pipeline(config, AudioFormat.SampleRate, 0, 0, 0);
                if (webrtc_apm_apply_config(_apm, config) != 0)
                    throw new InvalidOperationException("webrtc_apm_apply_config failed");
            }
            finally { webrtc_apm_config_destroy(config); }

            if (webrtc_apm_initialize(_apm) != 0)
                throw new InvalidOperationException("webrtc_apm_initialize failed");

            // 创建流配置：48kHz / 1ch
            _captureConfig = webrtc_apm_stream_config_create(AudioFormat.SampleRate, (UIntPtr)1);
            _reverseConfig = webrtc_apm_stream_config_create(AudioFormat.SampleRate, (UIntPtr)1);

            if (_captureConfig == IntPtr.Zero || _reverseConfig == IntPtr.Zero)
                throw new InvalidOperationException($"stream_config 创建失败: capture={_captureConfig}, reverse={_reverseConfig}");

            Serilog.Log.Information("WebRtcAecProcessor: handle apm=0x{Apm:X} capture=0x{Cap:X} reverse=0x{Rev:X}",
                _apm.ToInt64(), _captureConfig.ToInt64(), _reverseConfig.ToInt64());

            // 测试处理一帧，确认不会崩溃
            var testMic = new float[FrameSize];
            var testRef = new float[FrameSize];
            var testOut = new float[FrameSize];
            ProcessFrame(testMic, testRef, 1, testOut);
            Serilog.Log.Information("WebRtcAecProcessor: 首帧测试通过");

            Available = true;
            Serilog.Log.Information("WebRtcAecProcessor: WebRTC AEC3 初始化成功");
        }
        catch (Exception ex)
        {
            Available = false;
            Serilog.Log.Warning(ex, "WebRtcAecProcessor: 初始化失败，AEC 将旁路");
            Cleanup();
        }
    }

    public void Configure(int referenceChannels)
    {
        // WebRTC AEC3 使用 process_reverse_stream 喂参考，不需要像 SpeexDSP 那样预配置通道数。
        // 参考通道在调用时动态混合为单声道后喂入。
        ReferenceChannels = Math.Max(0, referenceChannels);
    }

    public unsafe void ProcessFrame(float[] microphone, float[] referenceInterleaved, int referenceChannels, float[] output)
    {
        if (microphone.Length < FrameSize || output.Length < FrameSize)
            throw new ArgumentException("AEC buffers must contain at least 480 samples.");

        if (!Available || _disposed || referenceChannels <= 0 || referenceInterleaved.Length < FrameSize * referenceChannels)
        {
            Array.Copy(microphone, output, FrameSize);
            return;
        }

        lock (_stateLock)
        {
            if (_disposed || _apm == IntPtr.Zero)
            {
                Array.Copy(microphone, output, FrameSize);
                return;
            }

            try
            {
                // 准备麦克风 float 数据
                Array.Copy(microphone, _micFloat, FrameSize);

                // 多参考通道下混为单声道 float
                for (int i = 0; i < FrameSize; i++)
                {
                    float sum = 0;
                    for (int c = 0; c < referenceChannels; c++)
                        sum += referenceInterleaved[i * referenceChannels + c];
                    _refFloat[i] = sum / referenceChannels;
                }

                // 1. 喂参考音频（远端）让 AEC3 分析回声路径
                // WebRTC APM 使用 planar 格式：IntPtr[]（每通道一个指针）
                fixed (float* refPtr = _refFloat)
                {
                    IntPtr[] refPlanar = { (IntPtr)refPtr };
                    webrtc_apm_process_reverse_stream(_apm, refPlanar, _reverseConfig, _reverseConfig, refPlanar);
                }

                // 2. 设置流延迟
                webrtc_apm_set_stream_delay_ms(_apm, 0);

                // 3. 处理麦克风音频（近端），输出回声消除后结果
                fixed (float* micPtr = _micFloat)
                fixed (float* outPtr = _outFloat)
                {
                    IntPtr[] micPlanar = { (IntPtr)micPtr };
                    IntPtr[] outPlanar = { (IntPtr)outPtr };
                    webrtc_apm_process_stream(_apm, micPlanar, _captureConfig, _captureConfig, outPlanar);
                }

                // 输出
                Array.Copy(_outFloat, output, FrameSize);
            }
            catch (Exception ex)
            {
                Available = false;
                Array.Copy(microphone, output, FrameSize);
                Serilog.Log.Error(ex, "WebRtcAecProcessor: 处理异常，AEC 已自动旁路");
            }
        }
    }

    public void Reset()
    {
        // WebRTC APM 没有 reset API，重新创建是最干净的方式
        lock (_stateLock)
        {
            if (_apm != IntPtr.Zero)
            {
                try { webrtc_apm_destroy(_apm); } catch { }
                _apm = webrtc_apm_create();
                if (_apm != IntPtr.Zero)
                {
                    IntPtr config = webrtc_apm_config_create();
                    try
                    {
                        webrtc_apm_config_set_echo_canceller(config, 1, 0);
                        webrtc_apm_config_set_high_pass_filter(config, 1);
                        webrtc_apm_config_set_noise_suppression(config, 0, 0);
                        webrtc_apm_config_set_pipeline(config, AudioFormat.SampleRate, 0, 0, 0);
                        webrtc_apm_apply_config(_apm, config);
                    }
                    finally { webrtc_apm_config_destroy(config); }
                    webrtc_apm_initialize(_apm);
                }
            }
        }
    }

    private void Cleanup()
    {
        if (_captureConfig != IntPtr.Zero) { try { webrtc_apm_stream_config_destroy(_captureConfig); } catch { } _captureConfig = IntPtr.Zero; }
        if (_reverseConfig != IntPtr.Zero) { try { webrtc_apm_stream_config_destroy(_reverseConfig); } catch { } _reverseConfig = IntPtr.Zero; }
        if (_apm != IntPtr.Zero) { try { webrtc_apm_destroy(_apm); } catch { } _apm = IntPtr.Zero; }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
            Available = false;
            Cleanup();
        }
    }
}
