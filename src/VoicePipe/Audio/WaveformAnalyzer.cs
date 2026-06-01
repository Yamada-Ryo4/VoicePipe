using System;

namespace VoicePipe.Audio;

/// <summary>
/// 波形可视化数据收集器。
/// 
/// 性能优化：
/// - InlineSample() 由 AudioMixEngine.Read() 在播放线程逐样本调用，
///   不再通过 Push(float[]) 批量传递（消除额外的数组分配和锁竞争）
/// - GetSnapshot() 用简单的 lock 保护，只在 UI 帧率（30fps）调用
/// </summary>
public static class WaveformAnalyzer
{
    private const int Size = 512;
    private const int Mask = Size - 1; // 511，用位运算替代取模
    // 每积累约 (采样率/512) 个样本写入一个波形槽位，使 512 槽位 ≈ 1 秒滚动窗口。
    // 48000/512 ≈ 94。随 AudioFormat.SampleRate 自动推导，避免改采样率后波形滚动变速。
    private const int SamplesPerSlot = AudioFormat.SampleRate / Size;
    private static readonly float[] _buffer = new float[Size];
    private static int _index = 0;
    private static float _currentPeak = 0;
    private static int _sampleCount = 0;

    /// <summary>
    /// 由 AudioMixEngine.Read() 的混音循环内联调用，每个样本调用一次。
    /// 无需额外的数组分配或锁（单线程调用）。
    /// </summary>
    public static void InlineSample(float sample)
    {
        float abs = MathF.Abs(sample);
        if (abs > _currentPeak) _currentPeak = abs;
        _sampleCount++;
        if (_sampleCount >= SamplesPerSlot)
        {
            // 写入环形缓冲区的一个槽位
            _buffer[_index] = _currentPeak;
            _index = (_index + 1) & Mask; // ★ 位运算替代 % Size
            _currentPeak = 0;
            _sampleCount = 0;
        }
    }

    /// <summary>
    /// 拷贝当前波形快照到外部缓冲（长度需 >= 512），返回有效长度。
    /// ★ 复用外部缓冲，避免每帧 new float[512]。
    /// 不加锁：最多读到一帧旧数据，对波形显示无影响。
    /// </summary>
    public static int GetSnapshot(float[] dest)
    {
        int idx = Volatile.Read(ref _index);
        for (int i = 0; i < Size; i++)
            dest[i] = _buffer[(idx + i) & Mask]; // ★ 位运算替代 % Length
        return Size;
    }

    /// <summary>兼容旧调用：分配新数组返回快照。</summary>
    public static float[] GetSnapshot()
    {
        var snap = new float[Size];
        GetSnapshot(snap);
        return snap;
    }

    /// <summary>
    /// 读取最近写入槽位的峰值（0~1），供 UI 显示实时电平 / dB。
    /// 无锁读取最新槽位，最多差一帧，对显示无影响。
    /// </summary>
    public static float GetLatestPeak()
    {
        int idx = Volatile.Read(ref _index);
        // _index 指向下一个要写的槽位，最近写入的是 idx-1
        return _buffer[(idx - 1) & Mask];
    }

    /// <summary>
    /// 清空波形缓冲（归零）。完全停止管线时调用，使波形显示平线而非冻结在最后一帧。
    /// </summary>
    public static void Clear()
    {
        Array.Clear(_buffer, 0, Size);
        _currentPeak = 0;
        _sampleCount = 0;
        // _index 不重置：保持滚动位置连续，清零后 GetSnapshot 读到的就是平线
    }
}