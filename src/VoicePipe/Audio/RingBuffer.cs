using System.Collections.Concurrent;

namespace VoicePipe.Audio;

/// <summary>
/// 线程安全环形缓冲区，供 AudioMixEngine 的两路音频流使用。
/// </summary>
public class RingBuffer
{
    private readonly float[] _buffer;
    private int _writePos;
    private int _readPos;
    private int _available;
    private readonly object _lock = new();

    public int Available => Volatile.Read(ref _available);

    public RingBuffer(int capacity)
    {
        _buffer = new float[capacity];
    }

    public void Write(float[] data)
    {
        lock (_lock)
        {
            foreach (var sample in data)
            {
                _buffer[_writePos] = sample;
                _writePos = (_writePos + 1) % _buffer.Length;
                if (_available < _buffer.Length)
                    _available++;
                else
                    _readPos = (_readPos + 1) % _buffer.Length; // 覆盖旧数据
            }
        }
    }

    public float[] Read(int count)
    {
        lock (_lock)
        {
            count = Math.Min(count, _available);
            var result = new float[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = _buffer[_readPos];
                _readPos = (_readPos + 1) % _buffer.Length;
            }
            _available -= count;
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _writePos = _readPos = _available = 0;
        }
    }
}
