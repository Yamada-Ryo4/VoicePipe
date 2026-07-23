using NAudio.Wave;

namespace VoicePipe.Audio;

/// <summary>
/// 双路 PCM 混音引擎（Pull 模型）。
/// 实现 IWaveProvider，由 WaveOutEvent 按需拉取数据，保证输出速率与设备时钟严格同步。
///
/// 性能优化：
/// - 预分配固定缓冲区，Read() 内零堆分配
/// - 波形分析内联到 Read()，消除额外的 lock + 数组拷贝
/// - 重采样使用缓存的缓冲区
/// </summary>
public class AudioMixEngine : IWaveProvider, IDisposable
{
    // 100ms 缓冲：WASAPI 共享模式 packet 间隔稳定在 ~10ms，100ms = 10 个 packet 余量绰绰有余。
    // 比之前 200ms 更紧凑：堆积超过 100ms 时旧数据被覆盖（RingBuffer 推进读指针），
    // 强制把最大延迟钳在 ~100ms 以内，避免数据在 buffer 里无限堆积导致延迟越来越高。
    private readonly RingBuffer _appBuffer = new(AudioFormat.SampleRate * AudioFormat.Channels * 100 / 1000);
    private readonly RingBuffer _micBuffer = new(AudioFormat.SampleRate * AudioFormat.Channels * 100 / 1000);

    // ★ 本地监听（把混音送到耳机/默认播放设备）专用的独立缓冲。
    //   完全独立于 VB-Cable 输出路径：Read() 在生成 VB-Cable 数据的同一循环里顺手把监听信号写进这里，
    //   由独立的 MonitorProvider + WasapiOut（默认设备）拉取。绝不影响 VB-Cable 的 10ms 低延迟。
    private readonly RingBuffer _monitorBuffer = new(AudioFormat.SampleRate * AudioFormat.Channels * 100 / 1000);

    // 麦克风降噪：RNNoise（RNN 神经网络）实时人声降噪，仅作用于 Mic_Path。
    // 在 FeedMic 中 Resample 到统一采样率（48000）立体声之后、写入 _micBuffer 之前原地处理。
    // 关闭时为纯直通；原生库不可用时自动降级为直通。App_Path 永不经过。(Req 4.4/4.9)
    private readonly MicrophoneFrameProcessor _micProcessor = new();

    // 麦克风静音（会话状态，不持久化）。仅在 Read() 的 mic 项上生效，App_Path 不受影响。(Req 2.6)
    // volatile：UI/热键线程写入，音频播放线程读取，与现有增益缓存一致的无锁可见性。
    private volatile bool _micMuted;

    /// <summary>麦克风静音开关。Mic mute (session state, not persisted; mic path only). (Req 2.6)</summary>
    public bool MicMuted
    {
        get => _micMuted;
        set => _micMuted = value;
    }

    // ── 本地监听（耳机回放）控制 ──
    // _monitorEnabled：监听主开关。关闭时 Read() 不向 _monitorBuffer 写入（监听静音）。
    // _monitorApp / _monitorMic：子开关。两者都关但主开关开 = 监听整个 VoicePipe 输出（App+Mic）。
    // 全部 volatile：UI 线程写、音频线程读。
    private volatile bool _monitorEnabled;
    private volatile bool _monitorApp;
    private volatile bool _monitorMic;

    /// <summary>本地监听主开关：把混音送到耳机/默认设备。仅影响独立监听缓冲，不碰 VB-Cable 路径。</summary>
    public bool MonitorEnabled
    {
        get => _monitorEnabled;
        set
        {
            _monitorEnabled = value;
            if (!value) _monitorBuffer.Clear(); // 关闭时清空，避免残留
        }
    }

    /// <summary>子开关：单独监听 App 音频。</summary>
    public bool MonitorApp
    {
        get => _monitorApp;
        set => _monitorApp = value;
    }

    /// <summary>子开关：单独监听麦克风。</summary>
    public bool MonitorMic
    {
        get => _monitorMic;
        set => _monitorMic = value;
    }

    // 监听音量（独立于发往 VB-Cable 的音量）：只作用于监听信号，0~2，感知响度曲线。
    // 让用户耳机里听得大/小一点，而不影响实际发出去的音量。
    private float _monitorGain = 1.0f;
    private volatile float _monitorGainAmp = 1.0f;

    /// <summary>监听音量（0~2，独立于 VB-Cable 输出音量）。仅影响耳机监听响度。</summary>
    public float MonitorGain
    {
        get => _monitorGain;
        set
        {
            _monitorGain = value;
            _monitorGainAmp = MathF.Pow(value, GainExponent);
        }
    }

    /// <summary>供监听输出端（MonitorProvider）拉取监听 PCM 的缓冲。</summary>
    internal RingBuffer MonitorBuffer => _monitorBuffer;

    // ★ VB-Cable writer 是否在跑。决定监听数据从哪来：
    //   true  → VB-Cable 的 Read() 在泵动混音引擎，监听从 _monitorBuffer 拉（原逻辑）
    //   false → 完全停止混音但监听还开着：由监听端 GenerateMonitorStandalone() 自驱动混音引擎
    //           （消费 app/mic 缓冲 + 跑波形分析 + 直接产出监听 PCM），不经过 _monitorBuffer。
    //   volatile：UI/管线线程写，监听播放线程读。
    private volatile bool _vbCableActive;

    /// <summary>VB-Cable 输出链是否活跃。由 PipelineManager 在 Writer 启停时设置。</summary>
    public bool VbCableActive
    {
        get => _vbCableActive;
        set => _vbCableActive = value;
    }

    /// <summary>RNNoise 降噪启用。仅 Mic_Path。(Req 4.5/4.8)</summary>
    public bool NoiseGateEnabled
    {
        get => _micProcessor.DenoiseEnabled;
        set => _micProcessor.DenoiseEnabled = value;
    }

    /// <summary>原生 RNNoise 库是否可用（不可用时降噪开关无效，UI 可据此提示）。</summary>
    public bool DenoiseAvailable => _micProcessor.DenoiseAvailable;

    /// <summary>降噪强度（干湿混合比，0~1）。仅 Mic_Path。</summary>
    public float DenoiseStrength
    {
        get => _micProcessor.DenoiseWetMix;
        set => _micProcessor.DenoiseWetMix = value;
    }

    /// <summary>消除扬声器声学回声。仅 Mic_Path，处理顺序在 RNNoise 之前。</summary>
    public bool EchoCancellationEnabled
    {
        get => _micProcessor.EchoCancellationEnabled;
        set => _micProcessor.EchoCancellationEnabled = value;
    }

    public bool EchoCancellationAvailable => _micProcessor.EchoCancellationAvailable;

    internal void SetAecReferenceProvider(IAecReferenceProvider? provider)
        => _micProcessor.ReferenceProvider = provider;

    /// <summary>保留字段以兼容旧设置/UI 绑定；RNNoise 自动工作，不使用阈值。</summary>
    public float NoiseGateThreshold { get; set; }

    // AppGain / MicGain：感知响度（perceptual loudness）增益曲线。
    //
    // 人耳对响度的感知接近对数，纯线性幅度缩放手感很差（拉到 30% 听起来还有大半，
    // 接近 0 才突然变小，最后一下掉到静音 = 悬崖感）。所以这里采用指数感知曲线：
    //   实际幅度 = 滑块值 ^ GainExponent
    // 让“滑块位置 ≈ 听到的响度百分比”，往下拉有线性、按比例变小的手感。
    //
    // 锚点保证：
    //   0%   → 0      （静音）
    //   100% → ×1.0   （严格等于原音，不增不减）
    //   50%  → ×0.31  （听起来约一半响）
    //   150% → ×1.98  （听起来约 1.5 倍响）
    //
    // 性能：曲线只在 setter（UI 改动增益时，极低频）里算一次 MathF.Pow 并缓存到
    // _appGainAmp / _micGainAmp。音频播放线程的 Read() 只读缓存值，仍是单次乘法，零额外开销。
    // volatile 确保播放线程始终读到 UI 线程写入的最新缓存值，防止 JIT 缓存旧值。
    private const float GainExponent = 1.7f;

    private float _appGain = 0.70f;
    private float _micGain = 1.0f;
    private volatile float _appGainAmp = MathF.Pow(0.70f, GainExponent);
    private volatile float _micGainAmp = 1.0f; // 1.0^1.7 = 1.0

    public float AppGain
    {
        get => _appGain;
        set
        {
            _appGain = value;
            _appGainAmp = MathF.Pow(value, GainExponent);
        }
    }
    public float MicGain
    {
        get => _micGain;
        set
        {
            _micGain = value;
            _micGainAmp = MathF.Pow(value, GainExponent);
        }
    }

    // IWaveProvider 输出格式：48000Hz / 2ch / Float32（全管线统一采样率）
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(AudioFormat.SampleRate, AudioFormat.Channels);

    // ★ 预分配缓冲区，避免 Read() 每次调用时堆分配
    private const int MaxSamplesPerRead = 4096;
    private readonly float[] _appTemp = new float[MaxSamplesPerRead];
    private readonly float[] _micTemp = new float[MaxSamplesPerRead];
    private readonly float[] _monitorTemp = new float[MaxSamplesPerRead]; // 监听信号暂存（写入 _monitorBuffer）

    // ★ 重采样缓存缓冲区（FeedMic 在 WASAPI 回调线程单线程调用，无需加锁）
    private float[]? _resampleStereoCache;
    private float[]? _resampleRateCache;
    private float[]? _monoStereoCache;

    // --- 输入端：由 WASAPI 回调线程调用，只往 RingBuffer 里写数据 ---

    public void FeedApp(float[] samples) => _appBuffer.Write(samples);
    public void FeedApp(float[] samples, int count) => _appBuffer.Write(samples, 0, count);

    public void FeedMic(float[] samples, WaveFormat srcFormat) => FeedMic(samples, samples.Length, srcFormat);

    public void FeedMic(float[] samples, int count, WaveFormat srcFormat)
    {
        // ★ Resample 返回缓存数组 + 有效长度，Write 用显式长度避免 ToArray()
        var (buf, len) = Resample(samples, count, srcFormat);
        // 降噪：仅 Mic_Path，重采样后、写入 _micBuffer 前原地处理（关闭/不可用时为直通）。(Req 4.4)
        _micProcessor.ProcessStereo48k(buf, len);
        _micBuffer.Write(buf, 0, len);
    }

    // --- 输出端：由 WaveOutEvent 的播放线程调用，按需拉取混音数据 ---

    /// <summary>
    /// WaveOutEvent 每次需要数据时调用。count = 需要的字节数。
    /// 我们从两个 RingBuffer 读取可用数据，混合后填充 buffer。
    /// 数据不足时补零（静音），保证永远返回 count 字节，不会卡顿。
    /// </summary>
    public int Read(byte[] buffer, int offset, int count)
    {
        int samplesNeeded = count / 4; // float32 = 4 bytes per sample

        // ★ 使用预分配缓冲区，先清零；异常大的请求用临时分配
        var appBuf = samplesNeeded <= MaxSamplesPerRead ? _appTemp : new float[samplesNeeded];
        var micBuf = samplesNeeded <= MaxSamplesPerRead ? _micTemp : new float[samplesNeeded];
        Array.Clear(appBuf, 0, samplesNeeded);
        Array.Clear(micBuf, 0, samplesNeeded);

        // App：读可用数据到预分配缓冲区
        _appBuffer.Read(appBuf, 0, samplesNeeded);

        // Mic：读可用数据到预分配缓冲区
        _micBuffer.Read(micBuf, 0, samplesNeeded);

        // 混音 + 软限幅 + 内联波形分析 → 写入输出 buffer
        // 感知响度曲线：用 setter 里预算好的缓存幅度值，循环内仍是单次乘法
        float appGain = _appGainAmp;
        float micGain = _micGainAmp;

        // ── 本地监听信号计算参数（在 VB-Cable 主循环里顺手生成，不额外遍历）──
        // 监听主开关开时：两个子开关都关 = 监听整个输出（App+Mic）；否则按子开关选择。
        bool monitorOn = _monitorEnabled;
        bool monApp, monMic;
        if (!monitorOn) { monApp = false; monMic = false; }
        else if (!_monitorApp && !_monitorMic) { monApp = true; monMic = true; } // 都关 = 监听整个 VoicePipe 输出
        else { monApp = _monitorApp; monMic = _monitorMic; }
        var monBuf = _monitorTemp;
        bool writeMonitor = monitorOn && samplesNeeded <= MaxSamplesPerRead;
        float monGain = _monitorGainAmp; // 监听独立音量

        // ★ fixed 提到循环外，只 pin 一次（之前每个样本 pin 一次，代价很高）
        unsafe
        {
            fixed (byte* ptr = &buffer[offset])
            {
                float* fptr = (float*)ptr;
                for (int i = 0; i < samplesNeeded; i++)
                {
                    float appComp = appBuf[i] * appGain;
                    float micComp = _micMuted ? 0f : micBuf[i] * micGain;

                    // VB-Cable 输出：App + Mic（与原逻辑完全一致，未改动）
                    float mixed = SoftLimit(appComp + micComp);
                    fptr[i] = mixed;

                    // ★ 内联波形分析 — 不再调用 WaveformAnalyzer.Push()，消除锁 + 数组分配
                    WaveformAnalyzer.InlineSample(mixed);
                    SpectrumAnalyzer.InlineSample(mixed); // ★ 频谱：写入环形缓冲（零分配 O(1)）

                    // 本地监听信号：按子开关选择 App/Mic 分量（独立于 VB-Cable 输出），再乘监听独立音量
                    if (writeMonitor)
                        monBuf[i] = SoftLimit(((monApp ? appComp : 0f) + (monMic ? micComp : 0f)) * monGain);
                }
            }
        }

        // 把监听信号写入独立缓冲，供 MonitorProvider 拉取（不影响上面的 VB-Cable 输出）
        if (writeMonitor)
            _monitorBuffer.Write(monBuf, 0, samplesNeeded);

        return count; // 永远返回请求的字节数，保证输出流连续
    }

    /// <summary>
    /// ★ 独立监听泵（方案 A）：当 VB-Cable writer 不在跑、但监听还开着时，
    /// 由 MonitorOutput 的 WasapiOut 直接调用本方法驱动混音引擎，把混音结果写进 outBuf（监听设备）。
    ///
    /// 与 Read() 的关系：Read() 是给 VB-Cable 的（产 CABLE 输出 + 顺手填监听缓冲）；
    /// 本方法是给监听的（VB-Cable 停了之后接管泵动）。两者不会同时跑：
    ///   - VbCableActive=true  时监听走 _monitorBuffer（Read 填的），本方法不被调用
    ///   - VbCableActive=false 时 Read 没人调，改由本方法消费 app/mic 缓冲 + 跑波形分析 + 产监听 PCM
    /// 这样停止混音后，监听/波形/麦克风直通仍然活着（前提是 MicCapturer/loopback 还在喂数据）。
    ///
    /// outBuf：监听设备的 float32 交错缓冲；samplesNeeded：需要的样本数。
    /// </summary>
    public void GenerateMonitorStandalone(float[] outBuf, int samplesNeeded)
    {
        var appBuf = samplesNeeded <= MaxSamplesPerRead ? _appTemp : new float[samplesNeeded];
        var micBuf = samplesNeeded <= MaxSamplesPerRead ? _micTemp : new float[samplesNeeded];
        Array.Clear(appBuf, 0, samplesNeeded);
        Array.Clear(micBuf, 0, samplesNeeded);

        _appBuffer.Read(appBuf, 0, samplesNeeded);
        _micBuffer.Read(micBuf, 0, samplesNeeded);

        float appGain = _appGainAmp;
        float micGain = _micGainAmp;

        // 监听选择：与 Read() 内的逻辑一致（主开关开 + 两子开关都关 = 整个输出）
        bool monitorOn = _monitorEnabled;
        bool monApp, monMic;
        if (!monitorOn) { monApp = false; monMic = false; }
        else if (!_monitorApp && !_monitorMic) { monApp = true; monMic = true; }
        else { monApp = _monitorApp; monMic = _monitorMic; }
        float monGain = _monitorGainAmp;

        for (int i = 0; i < samplesNeeded; i++)
        {
            float appComp = appBuf[i] * appGain;
            float micComp = _micMuted ? 0f : micBuf[i] * micGain;

            // 波形/频谱：用整个混音(App+Mic)喂，停止混音后波形仍随麦克风/直通动
            float mixed = SoftLimit(appComp + micComp);
            WaveformAnalyzer.InlineSample(mixed);
            SpectrumAnalyzer.InlineSample(mixed);

            // 监听输出：按子开关选择，乘监听音量
            outBuf[i] = monitorOn
                ? SoftLimit(((monApp ? appComp : 0f) + (monMic ? micComp : 0f)) * monGain)
                : 0f;
        }
    }

    // --- 信号处理 ---

    // 软限幅：低于阈值完全透传（保留冲击感），超过阈值用 tanh 曲线压缩。
    // 阈值 0.85（-1.4 dBFS），knee=0.15 控制曲线柔软度。
    // 数学保证：输出严格 ≤ ±1.0，即使输入达到 ±3.0 或更大。
    //   output = sign * (thr + (1-thr) * tanh(excess / knee))
    //   极限：excess→∞ 时 tanh→1，output → sign * (thr + 1-thr) = sign * 1.0
    private const float LimiterThreshold = 0.85f;
    private const float LimiterKnee = 0.15f;

    private static float SoftLimit(float x)
    {
        float abs = MathF.Abs(x);
        if (abs <= LimiterThreshold) return x;
        float sign = x > 0 ? 1f : -1f;
        float excess = abs - LimiterThreshold;
        return sign * (LimiterThreshold + (1f - LimiterThreshold) * MathF.Tanh(excess / LimiterKnee));
    }

    /// <summary>
    /// 重采样并返回 (缓存数组, 有效样本数)，调用方用显式长度写 RingBuffer，避免 ToArray()。
    /// </summary>
    private (float[] buf, int len) Resample(float[] input, int inputLen, WaveFormat srcFmt)
    {
        int channels = srcFmt.Channels;
        float[] workBuf = input;
        int workLen = inputLen;

        // 多声道 → 立体声下混
        if (channels > 2)
        {
            int frames = inputLen / channels;
            int stereoLen = frames * 2;
            if (_resampleStereoCache == null || _resampleStereoCache.Length < stereoLen)
                _resampleStereoCache = new float[stereoLen];

            for (int f = 0; f < frames; f++)
            {
                float sum = 0;
                for (int c = 0; c < channels; c++)
                    sum += input[f * channels + c];
                float avg = sum / channels;
                _resampleStereoCache[f * 2]     = avg;
                _resampleStereoCache[f * 2 + 1] = avg;
            }
            workBuf = _resampleStereoCache; // ★ 直接用缓存，不再 ToArray()
            workLen = stereoLen;
            channels = 2;
        }
        // 单声道 → 立体声
        else if (channels == 1)
        {
            int stereoLen = inputLen * 2;
            // ★ 缓存单声道→立体声缓冲区
            if (_monoStereoCache == null || _monoStereoCache.Length < stereoLen)
                _monoStereoCache = new float[stereoLen];
            for (int i = 0; i < inputLen; i++)
            {
                _monoStereoCache[i * 2]     = input[i];
                _monoStereoCache[i * 2 + 1] = input[i];
            }
            workBuf = _monoStereoCache;
            workLen = stereoLen;
            channels = 2;
        }

        // 采样率转换（如 44100Hz → 48000Hz）：仅当设备采样率与管线不一致时才转
        if (srcFmt.SampleRate != AudioFormat.SampleRate)
            return ResampleRate(workBuf, workLen, srcFmt.SampleRate, AudioFormat.SampleRate, channels);

        return (workBuf, workLen);
    }

    /// <summary>
    /// 三次 Hermite 插值重采样，返回 (缓存数组, 有效样本数)，零分配。
    /// </summary>
    private (float[] buf, int len) ResampleRate(float[] input, int inputLen, int srcRate, int dstRate, int channels)
    {
        double ratio = (double)srcRate / dstRate;
        int srcFrames = inputLen / channels;
        int dstFrames = (int)Math.Ceiling(srcFrames / ratio);
        int outputLen = dstFrames * channels;

        // ★ 缓存输出缓冲区，直接返回，不再拷贝
        if (_resampleRateCache == null || _resampleRateCache.Length < outputLen)
            _resampleRateCache = new float[outputLen];

        for (int f = 0; f < dstFrames; f++)
        {
            double srcF = f * ratio;
            int i1 = (int)srcF;
            float t = (float)(srcF - i1);

            for (int c = 0; c < channels; c++)
            {
                float s0 = GetSample(input, i1 - 1, c, channels, srcFrames);
                float s1 = GetSample(input, i1,     c, channels, srcFrames);
                float s2 = GetSample(input, i1 + 1, c, channels, srcFrames);
                float s3 = GetSample(input, i1 + 2, c, channels, srcFrames);
                _resampleRateCache[f * channels + c] = CatmullRom(s0, s1, s2, s3, t);
            }
        }

        return (_resampleRateCache, outputLen); // ★ 直接返回缓存，零分配
    }

    private static float GetSample(float[] buf, int frame, int ch, int channels, int totalFrames)
    {
        int clamped = Math.Clamp(frame, 0, totalFrames - 1);
        return buf[clamped * channels + ch];
    }

    private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    public void Reset()
    {
        _appBuffer.Clear();
        _micBuffer.Clear();
        _monitorBuffer.Clear(); // ★ 清监听缓冲，切换/重启时避免残留
        _micProcessor.Reset(); // 清 AEC/RNNoise 跨帧状态，避免切麦残留
    }

    /// <summary>仅重置麦克风处理状态；切麦时即使复用 Writer 也必须调用。</summary>
    public void ResetMicProcessing()
    {
        _micBuffer.Clear();
        _micProcessor.Reset();
    }

    /// <summary>释放降噪器持有的 RNNoise 原生状态（rnnoise_destroy）。应用退出时由管线调用。(B1)</summary>
    public void Dispose()
    {
        _micProcessor.Dispose();
    }
}