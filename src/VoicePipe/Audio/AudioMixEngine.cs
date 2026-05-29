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
public class AudioMixEngine : IWaveProvider
{
    // 200ms 缓冲：足够应对 WASAPI 回调间隔波动
    private readonly RingBuffer _appBuffer = new(44100 * 2 * 200 / 1000);
    private readonly RingBuffer _micBuffer = new(44100 * 2 * 200 / 1000);

    // AppGain / MicGain：各自信源的直接幅度系数，相互独立。
    // volatile 确保播放线程始终读到 UI 线程写入的最新值，防止 JIT 缓存旧值。
    private volatile float _appGain = 0.75f;
    private volatile float _micGain = 0.75f;
    private volatile float _appGainSquared = 0.5625f; // 0.75^2
    private volatile float _micGainSquared = 0.5625f; // 0.75^2

    public float AppGain 
    { 
        get => _appGain; 
        set 
        { 
            _appGain = value; 
            _appGainSquared = value * value; 
        } 
    }
    public float MicGain 
    { 
        get => _micGain; 
        set 
        { 
            _micGain = value; 
            _micGainSquared = value * value; 
        } 
    }

    // 软限幅阈值放宽到 -0.5 dBFS (0.944)，只做极限防爆音，不压制正常动态
    private const float LimiterThreshold = 0.944f;

    // IWaveProvider 输出格式：44100Hz / 2ch / Float32
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    // ★ 预分配缓冲区，避免 Read() 每次调用时堆分配
    private const int MaxSamplesPerRead = 4096;
    private readonly float[] _appTemp = new float[MaxSamplesPerRead];
    private readonly float[] _micTemp = new float[MaxSamplesPerRead];

    // ★ 重采样缓存缓冲区（FeedMic 在 WASAPI 回调线程单线程调用，无需加锁）
    private float[]? _resampleStereoCache;
    private float[]? _resampleRateCache;
    private float[]? _monoStereoCache;

    // --- 输入端：由 WASAPI 回调线程调用，只往 RingBuffer 里写数据 ---

    public void FeedApp(float[] samples) => _appBuffer.Write(samples);

    public void FeedMic(float[] samples, WaveFormat srcFormat)
    {
        // ★ Resample 返回缓存数组 + 有效长度，Write 用显式长度避免 ToArray()
        var (buf, len) = Resample(samples, srcFormat);
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
        float appGainSq = _appGainSquared;
        float micGainSq = _micGainSquared;

        for (int i = 0; i < samplesNeeded; i++)
        {
            float mixed = SoftLimit(appBuf[i] * appGainSq + micBuf[i] * micGainSq);
            int pos = offset + i * 4;
            // 直接写 float 的 4 字节到 byte[]
            unsafe
            {
                fixed (byte* ptr = &buffer[pos])
                    *(float*)ptr = mixed;
            }

            // ★ 内联波形分析 — 不再调用 WaveformAnalyzer.Push()，消除锁 + 数组分配
            WaveformAnalyzer.InlineSample(mixed);
        }

        return count; // 永远返回请求的字节数，保证输出流连续
    }

    // --- 信号处理 ---

    private static float SoftLimit(float x)
    {
        float abs = MathF.Abs(x);
        if (abs <= LimiterThreshold) return x;
        float sign = x > 0 ? 1f : -1f;
        float excess = abs - LimiterThreshold;
        return sign * (LimiterThreshold + MathF.Tanh(excess) * (1f - LimiterThreshold));
    }

    /// <summary>
    /// 重采样并返回 (缓存数组, 有效样本数)，调用方用显式长度写 RingBuffer，避免 ToArray()。
    /// </summary>
    private (float[] buf, int len) Resample(float[] input, WaveFormat srcFmt)
    {
        int channels = srcFmt.Channels;
        float[] workBuf = input;
        int workLen = input.Length;

        // 多声道 → 立体声下混
        if (channels > 2)
        {
            int frames = input.Length / channels;
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
            int stereoLen = input.Length * 2;
            // ★ 缓存单声道→立体声缓冲区
            if (_monoStereoCache == null || _monoStereoCache.Length < stereoLen)
                _monoStereoCache = new float[stereoLen];
            for (int i = 0; i < input.Length; i++)
            {
                _monoStereoCache[i * 2]     = input[i];
                _monoStereoCache[i * 2 + 1] = input[i];
            }
            workBuf = _monoStereoCache;
            workLen = stereoLen;
            channels = 2;
        }

        // 采样率转换（如 48000Hz → 44100Hz）
        if (srcFmt.SampleRate != 44100)
            return ResampleRate(workBuf, workLen, srcFmt.SampleRate, 44100, channels);

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
    }
}