using NAudio.Wave;

namespace VoicePipe.Audio;

/// <summary>
/// 双路 PCM 混音引擎。两路音频通过各自 RingBuffer 缓冲，统一格式后叠加，Clamp 防爆音。
/// </summary>
public class AudioMixEngine
{
    // 两路各 ~500ms 的缓冲（44100Hz × 2ch × 0.5s = 44100 float samples）
    private readonly RingBuffer _appBuffer = new(44100 * 2 * 500 / 1000);
    private readonly RingBuffer _micBuffer = new(44100 * 2 * 500 / 1000);

    public float AppGain { get; set; } = 0.75f;
    public float MicGain { get; set; } = 1.0f;

    public event Action<byte[]>? OnMixed;

    private bool _firstTick = true;
    private int  _tickCount  = 0;

    public void FeedApp(float[] samples) => _appBuffer.Write(samples);

    public void FeedMic(float[] samples, WaveFormat srcFormat)
    {
        var resampled = Resample(samples, srcFormat);
        _micBuffer.Write(resampled);
    }

    /// <summary>混音 Tick，每次 App 帧到达时调用。</summary>
    public void Tick()
    {
        // 关键修复：允许在任意一路有数据时推进，用零填充不足的一路
        int appLen = _appBuffer.Available;
        if (appLen < 64) return;

        if (_firstTick)
        {
            Serilog.Log.Information("AudioMixEngine: 首次 Tick，appBuf={App} micBuf={Mic}",
                appLen, _micBuffer.Available);
            _firstTick = false;
        }

        _tickCount++;
        if (_tickCount % 300 == 0)
            Serilog.Log.Debug("AudioMixEngine: 混音运行中 Tick#{N} appAvail={App} micAvail={Mic}",
                _tickCount, appLen, _micBuffer.Available);

        int len = appLen;
        var app = _appBuffer.Read(len);

        float[] mic;
        int micAvail = _micBuffer.Available;
        if (micAvail >= len)
        {
            mic = _micBuffer.Read(len);
        }
        else
        {
            // mic 数据不足时，能读多少读多少，其余补零
            mic = new float[len];
            if (micAvail > 0)
            {
                var available = _micBuffer.Read(micAvail);
                Array.Copy(available, mic, available.Length);
            }
        }

        var mixed = new float[len];
        for (int i = 0; i < len; i++)
        {
            mixed[i] = Math.Clamp(
                app[i] * AppGain + mic[i] * MicGain,
                -1f, 1f);
        }

        WaveformAnalyzer.Push(mixed);
        OnMixed?.Invoke(FloatArrayToBytes(mixed));
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

        // 采样率：如 16000 → 44100，使用简单线性插值
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

    private static float[] ResampleRate(float[] input, int srcRate, int dstRate, int channels)
    {
        double ratio = (double)srcRate / dstRate;
        int srcFrames = input.Length / channels;
        int dstFrames = (int)(srcFrames / ratio);
        var output = new float[dstFrames * channels];

        for (int f = 0; f < dstFrames; f++)
        {
            double srcF = f * ratio;
            int i0 = (int)srcF;
            int i1 = Math.Min(i0 + 1, srcFrames - 1);
            float t = (float)(srcF - i0);

            for (int c = 0; c < channels; c++)
            {
                float s0 = i0 * channels + c < input.Length ? input[i0 * channels + c] : 0f;
                float s1 = i1 * channels + c < input.Length ? input[i1 * channels + c] : 0f;
                output[f * channels + c] = s0 + t * (s1 - s0);
            }
        }
        return output;
    }

    private static byte[] FloatArrayToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * 4];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public void Reset()
    {
        _appBuffer.Clear();
        _micBuffer.Clear();
        _firstTick = true;
        _tickCount  = 0;
    }
}