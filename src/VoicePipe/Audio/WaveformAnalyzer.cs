using System;

namespace VoicePipe.Audio;

public static class WaveformAnalyzer
{
    private static readonly object _lock = new();
    private static readonly float[] _buffer = new float[512];
    private static int _index = 0;
    private static float _currentPeak = 0;
    private static int _sampleCount = 0;

    public static void Push(float[] mixed)
    {
        lock (_lock)
        {
            foreach (var s in mixed)
            {
                float abs = Math.Abs(s);
                if (abs > _currentPeak) _currentPeak = abs;
                _sampleCount++;
                if (_sampleCount >= 86) // 44100 / 512 approx
                {
                    _buffer[_index] = _currentPeak;
                    _index = (_index + 1) % _buffer.Length;
                    _currentPeak = 0;
                    _sampleCount = 0;
                }
            }
        }
    }

    public static float[] GetSnapshot()
    {
        lock (_lock)
        {
            var snap = new float[_buffer.Length];
            for (int i = 0; i < _buffer.Length; i++)
            {
                snap[i] = _buffer[(_index + i) % _buffer.Length];
            }
            return snap;
        }
    }
}