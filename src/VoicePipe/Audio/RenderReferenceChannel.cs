namespace VoicePipe.Audio;

internal readonly record struct RenderReferenceStats(
    string DeviceId,
    string DeviceName,
    int BufferedSamples,
    long OverrunSamples,
    long UnderrunFrames,
    long DriftDroppedSamples,
    long DriftRepeatedSamples);

/// <summary>一个播放端点的有界 48kHz 单声道参考 FIFO。</summary>
/// <remarks>
/// 当捕获时钟与管线时钟存在漂移时，FIFO 水位会缓慢偏离目标。旧实现直接 drop/repeat
/// 单个 sample 修正水位，但每次 drop/repeat 都在参考信号中产生硬不连续，AEC 自适应
/// 滤波器无法稳定收敛。本实现改为帧内线性插值：当 drift 超过阈值时，将 1 sample 的
/// 修正平滑分布到整个 480-sample 帧（步长 = 479/480 或 481/480），输出仍是连续的。
/// </remarks>
internal sealed class RenderReferenceChannel
{
    private const int Capacity = AudioFormat.SampleRate / 2; // 500ms
    // 目标缓冲 50ms：WASAPI loopback 以 ~10ms 突发包到达，水位会在目标附近 ±10ms 抖动。
    // 50ms 目标给突发抖动足够余量，不会每帧都触发 drift 修正。
    internal const int TargetBufferedSamples = MicrophoneFrameProcessor.FrameSize * 5; // 50ms (2400 samples)
    // 滞后带 20ms：只有水位偏离目标超过 20ms 才触发修正，避免突发抖动引起的震荡。
    internal const int DriftHysteresisSamples = MicrophoneFrameProcessor.FrameSize * 2; // 20ms (960 samples)
    private readonly float[] _buffer = new float[Capacity];
    private readonly object _sync = new();
    private int _read;
    private int _write;
    private int _available;
    private int _startupDelaySamples = TargetBufferedSamples;
    private float _lastReadSample;
    private bool _hasLastReadSample;

    public string DeviceId { get; }
    public string DeviceName { get; }
    internal int BufferedSamples { get { lock (_sync) return _available; } }
    internal long OverrunSamples { get; private set; }
    internal long UnderrunFrames { get; private set; }
    internal long DriftDroppedSamples { get; private set; }
    internal long DriftRepeatedSamples { get; private set; }

    public RenderReferenceChannel(string deviceId, string deviceName)
    {
        DeviceId = deviceId;
        DeviceName = deviceName;
    }

    public void Write(float[] samples, int count)
    {
        if (count <= 0) return;
        lock (_sync)
        {
            int start = 0;
            if (count > Capacity)
            {
                start = count - Capacity;
                OverrunSamples += start;
                count = Capacity;
            }
            for (int i = 0; i < count; i++)
            {
                _buffer[_write] = samples[start + i];
                _write = (_write + 1) % Capacity;
                if (_available == Capacity)
                {
                    _read = (_read + 1) % Capacity;
                    OverrunSamples++;
                }
                else
                {
                    _available++;
                }
            }
        }
    }

    internal RenderReferenceStats GetStats()
    {
        lock (_sync)
        {
            return new RenderReferenceStats(
                DeviceId,
                DeviceName,
                _available,
                OverrunSamples,
                UnderrunFrames,
                DriftDroppedSamples,
                DriftRepeatedSamples);
        }
    }

    public bool ReadFrame(float[] destination, int destinationOffset, int frameSize, int channelIndex, int channelCount)
    {
        bool hadAudio = false;
        lock (_sync)
        {
            // 确定本帧的源样本数与目标样本数：
            //   正常：sourceCount == frameSize，1:1 直读
            //   过满（drift drop）：sourceCount == frameSize + 1，用线性插值从 481 个源样本
            //     生成 480 个输出样本，平滑地多消耗 1 sample，不产生硬不连续
            //   不足（drift repeat）：sourceCount == frameSize - 1，用线性插值从 479 个源样本
            //     生成 480 个输出样本，平滑地少消耗 1 sample
            int sourceCount = frameSize;
            if (_startupDelaySamples <= 0)
            {
                int targetBeforeRead = TargetBufferedSamples + frameSize;
                if (_available > targetBeforeRead + DriftHysteresisSamples)
                {
                    // 正常 drift drop：每帧多消耗 1 sample
                    sourceCount = frameSize + 1;
                    // 暴排：积压远超目标（超过 2 倍目标）时，按比例多消耗，快速排到目标水位。
                    // 否则靠每帧 +1 排 10000+ 样本需要 100+ 秒，期间参考延迟严重错位。
                    if (_available > TargetBufferedSamples * 2)
                    {
                        int excess = _available - TargetBufferedSamples;
                        // 每帧多排 excess/100 个样本，约 100 帧（1秒）排完
                        sourceCount = frameSize + Math.Max(1, excess / 100);
                    }
                }
                else if (_available < targetBeforeRead - DriftHysteresisSamples &&
                         _available >= frameSize - 1 && _hasLastReadSample)
                    sourceCount = frameSize - 1;
            }

            // 实际可用的源样本数（不能超过 FIFO 存量）
            int availableForRead = Math.Min(sourceCount, _available);
            int samplesAvailableAtStart = _available;

            if (availableForRead <= 0)
            {
                // 完全无数据：填零
                for (int i = 0; i < frameSize; i++)
                    destination[destinationOffset + i * channelCount + channelIndex] = 0f;
                if (_startupDelaySamples <= 0 && samplesAvailableAtStart < sourceCount)
                    UnderrunFrames++;
                return false;
            }

            if (_startupDelaySamples > 0)
            {
                // 启动延迟：消耗 startupDelaySamples 个零，之后才开始真正读
                // 与旧实现一致：startup 期间每个帧位置消费 1 个 startup slot
                // 注意：startup 阶段 sourceCount 始终 == frameSize，不产生 drift 计数
                int consumed = 0;
                for (int i = 0; i < frameSize; i++)
                {
                    float sample = 0f;
                    if (_startupDelaySamples > 0)
                    {
                        _startupDelaySamples--;
                    }
                    else if (consumed < availableForRead)
                    {
                        sample = ReadOneSampleLocked();
                        consumed++;
                        hadAudio = true;
                    }
                    else if (_hasLastReadSample)
                    {
                        sample = _lastReadSample;
                        hadAudio = true;
                    }
                    destination[destinationOffset + i * channelCount + channelIndex] = sample;
                }
                if (_startupDelaySamples <= 0 && samplesAvailableAtStart < sourceCount)
                    UnderrunFrames++;
                return hadAudio;
            }

            // 仅在 interpolation 路径（startup 已结束、有数据）才统计 drift 计数，
            // 避免把 startup 阶段的正常水位偏差误计入 drift。
            // 按实际偏差样本数计，与 OverrunSamples/UnderrunFrames 单位一致。
            int actualDiff = availableForRead - frameSize; // >0 drop, <0 repeat, 0 balanced
            if (actualDiff > 0)  DriftDroppedSamples  += actualDiff;
            else if (actualDiff < 0) DriftRepeatedSamples += -actualDiff;

            // 正常 + drift：用线性插值从 availableForRead 个源样本生成 frameSize 个输出。
            // 步长 = availableForRead / frameSize：
            //   正常 = 1.0（精确直读）
            //   drop = 481/480 ≈ 1.00208（平滑多消耗）
            //   repeat = 479/480 ≈ 0.99792（平滑少消耗）
            double step = (double)availableForRead / frameSize;
            double sourcePos = 0.0;
            // 预读源样本到局部数组，避免环形索引在插值循环里反复计算
            // （frameSize 最多 480，局部数组开销可忽略）
            Span<float> sourceBuf = stackalloc float[Math.Max(availableForRead, 1)];
            for (int i = 0; i < availableForRead; i++)
                sourceBuf[i] = ReadOneSampleLocked();
            hadAudio = true;

            for (int i = 0; i < frameSize; i++)
            {
                int leftIdx = (int)sourcePos;
                double frac = sourcePos - leftIdx;
                float sample;
                if (leftIdx + 1 < availableForRead)
                {
                    sample = (float)(sourceBuf[leftIdx] + (sourceBuf[leftIdx + 1] - sourceBuf[leftIdx]) * frac);
                }
                else
                {
                    // 最后一个样本：无右邻居，直接用左值
                    sample = sourceBuf[availableForRead - 1];
                }
                destination[destinationOffset + i * channelCount + channelIndex] = sample;
                sourcePos += step;
            }

            if (samplesAvailableAtStart < sourceCount)
                UnderrunFrames++;
        }
        return hadAudio;
    }

    private float ReadOneSampleLocked()
    {
        float sample = _buffer[_read];
        _read = (_read + 1) % Capacity;
        _available--;
        _lastReadSample = sample;
        _hasLastReadSample = true;
        return sample;
    }
}
