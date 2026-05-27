using NAudio.Wave;

namespace VoicePipe.Audio;

/// <summary>
/// 双路 PCM 混音引擎（Pull 模型）。
/// 实现 IWaveProvider，由 WaveOutEvent 按需拉取数据，保证输出速率与设备时钟严格同步。
/// 彻底消除 Push 模型的时钟漂移 / 缓冲区溢出 / 数据丢弃问题。
///
/// AppGain / MicGain 是各自信源的直接幅度增益（独立控制，互不影响）。
/// 重采样使用 Catmull-Rom 三次插值，保证音乐质量。
/// </summary>
public class AudioMixEngine : IWaveProvider
{
    // 200ms 缓冲：足够应对 WASAPI 回调间隔波动
    private readonly RingBuffer _appBuffer = new(44100 * 2 * 200 / 1000);
    private readonly RingBuffer _micBuffer = new(44100 * 2 * 200 / 1000);

    // AppGain / MicGain：各自信源的直接幅度系数，相互独立。
    public float AppGain { get; set; } = 0.75f;
    public float MicGain { get; set; } = 0.75f;

    // 软限幅阈值（-3 dBFS = 0.708）
    private const float LimiterThreshold = 0.708f;

    // IWaveProvider 输出格式：44100Hz / 2ch / Float32
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    private int _readCount = 0;

    // --- 输入端：由 WASAPI 回调线程调用，只往 RingBuffer 里写数据 ---

    public void FeedApp(float[] samples) => _appBuffer.Write(samples);

    public void FeedMic(float[] samples, WaveFormat srcFormat)
    {
        var resampled = Resample(samples, srcFormat);
        _micBuffer.Write(resampled);
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

        // App：读可用数据，不足补零
        float[] app = new float[samplesNeeded];
        int appAvail = _appBuffer.Available;
        if (appAvail > 0)
        {
            int toRead = Math.Min(appAvail, samplesNeeded);
            var appData = _appBuffer.Read(toRead);
            Array.Copy(appData, app, toRead);
        }

        // Mic：读可用数据，不足补零
        float[] mic = new float[samplesNeeded];
        int micAvail = _micBuffer.Available;
        if (micAvail > 0)
        {
            int toRead = Math.Min(micAvail, samplesNeeded);
            var micData = _micBuffer.Read(toRead);
            Array.Copy(micData, mic, toRead);
        }

        // 混音 + 软限幅 → 写入输出 buffer
        for (int i = 0; i < samplesNeeded; i++)
        {
            float mixed = SoftLimit(app[i] * AppGain + mic[i] * MicGain);
            int pos = offset + i * 4;
            // 直接写 float 的 4 字节到 byte[]
            unsafe
            {
                fixed (byte* ptr = &buffer[pos])
                    *(float*)ptr = mixed;
            }
        }

        // 波形分析（用于 UI 显示）
        var mixedFloats = new float[samplesNeeded];
        Buffer.BlockCopy(buffer, offset, mixedFloats, 0, count);
        WaveformAnalyzer.Push(mixedFloats);

        _readCount++;
        if (_readCount % 500 == 0)
            Serilog.Log.Debug("AudioMixEngine: Read#{N} samples={S} appAvail={App} micAvail={Mic}",
                _readCount, samplesNeeded, appAvail, micAvail);

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

    private static float[] Resample(float[] input, WaveFormat srcFmt)
    {
        int channels = srcFmt.Channels;

        // 多声道 → 立体声下混
        if (channels > 2)
        {
            int frames = input.Length / channels;
            var stereo = new float[frames * 2];
            for (int f = 0; f < frames; f++)
            {
                float sum = 0;
                for (int c = 0; c < channels; c++)
                    sum += input[f * channels + c];
                float avg = sum / channels;
                stereo[f * 2]     = avg;
                stereo[f * 2 + 1] = avg;
            }
            input = stereo;
            channels = 2;
        }
        // 单声道 → 立体声
        else if (channels == 1)
        {
            input = MonoToStereo(input);
            channels = 2;
        }

        // 采样率转换（如 48000Hz → 44100Hz）
        if (srcFmt.SampleRate != 44100)
            input = ResampleRate(input, srcFmt.SampleRate, 44100, channels);

        return input;
    }

    private static float[] MonoToStereo(float[] mono)
    {
        var stereo = new float[mono.Length * 2];
        for (int i = 0; i < mono.Length; i++)
        {
            stereo[i * 2]     = mono[i];
            stereo[i * 2 + 1] = mono[i];
        }
        return stereo;
    }

    /// <summary>
    /// 三次 Hermite 插值重采样。
    /// </summary>
    private static float[] ResampleRate(float[] input, int srcRate, int dstRate, int channels)
    {
        double ratio = (double)srcRate / dstRate;
        int srcFrames = input.Length / channels;
        int dstFrames = (int)Math.Ceiling(srcFrames / ratio);
        var output = new float[dstFrames * channels];

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
                output[f * channels + c] = CatmullRom(s0, s1, s2, s3, t);
            }
        }
        return output;
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
        _readCount = 0;
    }
}