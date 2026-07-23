namespace VoicePipe.Audio;

/// <summary>跨捕获 packet 保持连续相位的单声道线性重采样器。</summary>
internal sealed class StreamingLinearResampler
{
    private readonly double _sourceFramesPerOutputFrame;
    private double _nextSourcePosition;
    private long _sourceFramesSeen;
    private float _previousSample;
    private bool _hasPreviousSample;
    private float[] _outputBuffer = new float[4096];

    public float[] OutputBuffer => _outputBuffer;

    public StreamingLinearResampler(int sourceRate, int targetRate)
    {
        if (sourceRate <= 0) throw new ArgumentOutOfRangeException(nameof(sourceRate));
        if (targetRate <= 0) throw new ArgumentOutOfRangeException(nameof(targetRate));
        _sourceFramesPerOutputFrame = (double)sourceRate / targetRate;
    }

    public int Process(float[] input, int count)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (count < 0 || count > input.Length) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return 0;

        long packetStart = _sourceFramesSeen;
        long packetEnd = packetStart + count - 1L;
        int outputCount = 0;

        while (_nextSourcePosition <= packetEnd)
        {
            long leftIndex = (long)Math.Floor(_nextSourcePosition);
            long rightIndex = leftIndex + 1;
            float left;
            float right;

            if (leftIndex < packetStart)
            {
                if (!_hasPreviousSample || leftIndex != packetStart - 1L) break;
                left = _previousSample;
                right = input[0];
            }
            else
            {
                int leftOffset = (int)(leftIndex - packetStart);
                left = input[leftOffset];
                if (rightIndex <= packetEnd)
                {
                    right = input[leftOffset + 1];
                }
                else
                {
                    if (_nextSourcePosition > leftIndex) break;
                    right = left;
                }
            }

            EnsureOutputCapacity(outputCount + 1);
            float fraction = (float)(_nextSourcePosition - leftIndex);
            _outputBuffer[outputCount++] = left + (right - left) * fraction;
            _nextSourcePosition += _sourceFramesPerOutputFrame;
        }

        _previousSample = input[count - 1];
        _hasPreviousSample = true;
        _sourceFramesSeen += count;
        return outputCount;
    }

    public void Reset()
    {
        _nextSourcePosition = 0;
        _sourceFramesSeen = 0;
        _previousSample = 0f;
        _hasPreviousSample = false;
    }

    private void EnsureOutputCapacity(int needed)
    {
        if (_outputBuffer.Length >= needed) return;
        int size = _outputBuffer.Length;
        while (size < needed) size *= 2;
        Array.Resize(ref _outputBuffer, size);
    }
}
