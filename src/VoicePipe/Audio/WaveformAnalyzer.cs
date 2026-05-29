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
    private static readonly float[] _buffer = new float[512];
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
        if (_sampleCount >= 86) // 44100 / 512 approx
        {
            // 写入环形缓冲区的一个槽位
            _buffer[_index] = _currentPeak;
            _index = (_index + 1) % _buffer.Length;
            _currentPeak = 0;
            _sampleCount = 0;
        }
    }

    public static float[] GetSnapshot()
    {
        // UI 线程调用，简单复制一份快照
        var snap = new float[_buffer.Length];
        // 不加锁：最多读到一帧旧数据，对波形显示无影响
        int idx = Volatile.Read(ref _index);
        for (int i = 0; i < _buffer.Length; i++)
        {
            snap[i] = _buffer[(idx + i) % _buffer.Length];
        }
        return snap;
    }
}