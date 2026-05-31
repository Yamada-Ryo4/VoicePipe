namespace VoicePipe.Audio;

/// <summary>
/// 高性能线程安全环形缓冲区。
/// 
/// 性能优化：
/// - 容量强制 2 的幂，用位运算 (&amp; _mask) 替代取模 (% Length)
/// - Write/Read 用 Array.Copy 批量拷贝替代逐样本循环
/// - Read 接受外部缓冲区，避免每次分配新数组
/// </summary>
public class RingBuffer
{
    private readonly float[] _buffer;
    private readonly int _mask; // capacity - 1，用于位运算取模
    private int _writePos;
    private int _readPos;
    private int _available;
    private readonly object _lock = new();

    public int Available => Volatile.Read(ref _available);

    public RingBuffer(int minCapacity)
    {
        // 向上取到 2 的幂
        int capacity = 1;
        while (capacity < minCapacity) capacity <<= 1;
        _buffer = new float[capacity];
        _mask = capacity - 1;
    }

    public void Write(float[] data)
    {
        Write(data, 0, data.Length);
    }

    public void Write(float[] data, int offset, int count)
    {
        lock (_lock)
        {
            int capacity = _buffer.Length;

            // 如果数据超出容量，只保留最后 capacity 个样本
            if (count > capacity)
            {
                offset += count - capacity;
                count = capacity;
            }

            // 批量拷贝：可能需要分两段（环绕点）
            int firstChunk = Math.Min(count, capacity - _writePos);
            Array.Copy(data, offset, _buffer, _writePos, firstChunk);
            if (firstChunk < count)
                Array.Copy(data, offset + firstChunk, _buffer, 0, count - firstChunk);

            // 更新写位置
            _writePos = (_writePos + count) & _mask;

            // 更新可用量（覆盖旧数据时推进读位置）
            _available += count;
            if (_available > capacity)
            {
                _readPos = _writePos; // 读位置被推到写位置
                _available = capacity;
            }
        }
    }

    /// <summary>
    /// 读取数据到外部缓冲区，避免每次分配新数组。
    /// 返回实际读取的样本数。
    /// </summary>
    public int Read(float[] dest, int offset, int count)
    {
        lock (_lock)
        {
            count = Math.Min(count, _available);
            if (count == 0) return 0;

            int capacity = _buffer.Length;
            int firstChunk = Math.Min(count, capacity - _readPos);
            Array.Copy(_buffer, _readPos, dest, offset, firstChunk);
            if (firstChunk < count)
                Array.Copy(_buffer, 0, dest, offset + firstChunk, count - firstChunk);

            _readPos = (_readPos + count) & _mask;
            _available -= count;
            return count;
        }
    }

    /// <summary>
    /// 兼容旧 API：分配新数组返回（仅用于不频繁的调用路径）。
    /// ⚠ 注意：返回数组长度可能小于参数 count（实际可读量不足时只返回可读部分）。
    /// 高频路径请用 <see cref="Read(float[], int, int)"/> 重载，零分配且明确返回读取数。
    /// </summary>
    public float[] Read(int count)
    {
        lock (_lock)
        {
            count = Math.Min(count, _available);
            var result = new float[count];
            if (count > 0)
            {
                int capacity = _buffer.Length;
                int firstChunk = Math.Min(count, capacity - _readPos);
                Array.Copy(_buffer, _readPos, result, 0, firstChunk);
                if (firstChunk < count)
                    Array.Copy(_buffer, 0, result, firstChunk, count - firstChunk);

                _readPos = (_readPos + count) & _mask;
                _available -= count;
            }
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
