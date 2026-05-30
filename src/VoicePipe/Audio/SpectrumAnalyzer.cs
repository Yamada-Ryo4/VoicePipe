using System;

namespace VoicePipe.Audio;

/// <summary>
/// 频谱可视化数据收集器。
///
/// 设计目标：在不影响音频线程的前提下提供频谱。
/// - 音频线程（AudioMixEngine.Read）通过 <see cref="InlineSample"/> 只做一次「写入环形缓冲 + 自增索引」，
///   零分配、无锁、O(1)，对实时路径几乎无开销。
/// - UI 线程（低频，20fps）调用 <see cref="GetSpectrum"/>：从环形缓冲拷一份最近的 FftSize 个样本，
///   加 Hann 窗 → 实数 FFT（Cooley-Tukey 迭代基 2）→ 取幅度 → 对数分箱压缩成 Bars 条，
///   全部在 UI 线程的临时/复用缓冲里完成，不碰音频线程数据结构。
///
/// 频谱条数 <see cref="Bars"/> = 48，覆盖 ~20Hz–20kHz 的对数频率轴，适合人眼观感。
/// </summary>
public static class SpectrumAnalyzer
{
    private const int FftSize = 1024;            // 必须是 2 的幂
    private const int FftMask = FftSize - 1;
    public const int Bars = 48;                  // 输出频谱条数

    // 音频线程写入的原始样本环形缓冲（单声道混音样本）
    private static readonly float[] _ring = new float[FftSize];
    private static int _writePos;

    // UI 线程复用缓冲（仅 UI 线程访问，无需同步）
    private static readonly float[] _re = new float[FftSize];
    private static readonly float[] _im = new float[FftSize];
    private static readonly float[] _window = BuildHann(FftSize);

    /// <summary>音频线程逐样本调用：写入环形缓冲。零分配、O(1)。</summary>
    public static void InlineSample(float sample)
    {
        int p = _writePos;
        _ring[p] = sample;
        _writePos = (p + 1) & FftMask;
    }

    /// <summary>
    /// UI 线程调用：计算当前频谱，写入 dest（长度需 >= Bars），每条为 0~1 的归一化幅度。
    /// </summary>
    public static void GetSpectrum(float[] dest)
    {
        // 1) 从环形缓冲拷最近 FftSize 个样本（按时间顺序），同时加 Hann 窗
        int start = Volatile.Read(ref _writePos);
        for (int i = 0; i < FftSize; i++)
        {
            float s = _ring[(start + i) & FftMask];
            _re[i] = s * _window[i];
            _im[i] = 0f;
        }

        // 2) 原地 FFT
        Fft(_re, _im);

        // 3) 取前半谱的幅度，按对数频率轴分箱压缩为 Bars 条
        int half = FftSize / 2;
        for (int b = 0; b < Bars; b++)
        {
            // 对数映射：第 b 条覆盖 [lo, hi) 个 bin
            int lo = (int)(half * Math.Pow((double)b / Bars, 2.0));
            int hi = (int)(half * Math.Pow((double)(b + 1) / Bars, 2.0));
            if (hi <= lo) hi = lo + 1;
            if (hi > half) hi = half;

            float max = 0f;
            for (int k = lo; k < hi; k++)
            {
                float mag = MathF.Sqrt(_re[k] * _re[k] + _im[k] * _im[k]);
                if (mag > max) max = mag;
            }

            // 归一化 + 轻度对数压缩，让低电平也可见
            float norm = max / (FftSize * 0.5f);
            float db = 20f * MathF.Log10(norm + 1e-6f);   // ~ -120..0
            float v = (db + 60f) / 60f;                    // -60dB→0, 0dB→1
            dest[b] = Math.Clamp(v, 0f, 1f);
        }
    }

    private static float[] BuildHann(int n)
    {
        var w = new float[n];
        for (int i = 0; i < n; i++)
            w[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (n - 1)));
        return w;
    }

    /// <summary>迭代基-2 Cooley-Tukey FFT，原地变换。re/im 长度须为 2 的幂。</summary>
    private static void Fft(float[] re, float[] im)
    {
        int n = re.Length;

        // 位反转置换
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        // 蝶形运算
        for (int len = 2; len <= n; len <<= 1)
        {
            float ang = -2f * MathF.PI / len;
            float wlenRe = MathF.Cos(ang);
            float wlenIm = MathF.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                float wRe = 1f, wIm = 0f;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k;
                    int bIdx = i + k + len / 2;
                    float uRe = re[a], uIm = im[a];
                    float vRe = re[bIdx] * wRe - im[bIdx] * wIm;
                    float vIm = re[bIdx] * wIm + im[bIdx] * wRe;
                    re[a] = uRe + vRe; im[a] = uIm + vIm;
                    re[bIdx] = uRe - vRe; im[bIdx] = uIm - vIm;
                    float nwRe = wRe * wlenRe - wIm * wlenIm;
                    wIm = wRe * wlenIm + wIm * wlenRe;
                    wRe = nwRe;
                }
            }
        }
    }
}
