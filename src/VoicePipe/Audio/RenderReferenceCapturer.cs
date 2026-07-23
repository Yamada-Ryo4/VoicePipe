using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoicePipe.Audio;

/// <summary>单个真实播放端点的 WASAPI endpoint-loopback 捕获。</summary>
internal sealed class RenderReferenceCapturer : IDisposable
{
    private readonly MMDevice _device;
    private readonly RenderReferenceChannel _channel;
    private WasapiLoopbackCapture? _capture;
    private float[] _floatBuffer = new float[8192];
    private float[] _monoBuffer = new float[4096];
    private StreamingLinearResampler? _resampler;
    private int _resamplerSourceRate;
    private bool _disposed;

    public RenderReferenceCapturer(MMDevice device, RenderReferenceChannel channel)
    {
        _device = device;
        _channel = channel;
    }

    public void Start()
    {
        if (_disposed || _capture != null) return;
        _capture = new WasapiLoopbackCapture(_device);
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || sender is not WasapiLoopbackCapture capture) return;
        int samples = ConvertToFloat(e.Buffer, e.BytesRecorded, capture.WaveFormat);
        int frames = DownmixToMono(_floatBuffer, samples, capture.WaveFormat.Channels);
        if (frames <= 0) return;

        if (capture.WaveFormat.SampleRate == AudioFormat.SampleRate)
            _channel.Write(_monoBuffer, frames);
        else
        {
            int sourceRate = capture.WaveFormat.SampleRate;
            if (_resampler == null || _resamplerSourceRate != sourceRate)
            {
                _resampler = new StreamingLinearResampler(sourceRate, AudioFormat.SampleRate);
                _resamplerSourceRate = sourceRate;
            }
            int outputFrames = _resampler.Process(_monoBuffer, frames);
            if (outputFrames > 0) _channel.Write(_resampler.OutputBuffer, outputFrames);
        }
    }

    private int ConvertToFloat(byte[] input, int bytes, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            int count = bytes / 4;
            EnsureFloat(count);
            Buffer.BlockCopy(input, 0, _floatBuffer, 0, bytes);
            return count;
        }

        int bytesPerSample = format.BitsPerSample / 8;
        if (bytesPerSample <= 0) return 0;
        int sampleCount = bytes / bytesPerSample;
        EnsureFloat(sampleCount);
        switch (format.BitsPerSample)
        {
            case 16:
                for (int i = 0; i < sampleCount; i++) _floatBuffer[i] = BitConverter.ToInt16(input, i * 2) / 32768f;
                break;
            case 24:
                for (int i = 0; i < sampleCount; i++)
                {
                    int o = i * 3;
                    int sample = (input[o] << 8) | (input[o + 1] << 16) | (input[o + 2] << 24);
                    _floatBuffer[i] = (sample >> 8) / 8388608f;
                }
                break;
            case 32:
                for (int i = 0; i < sampleCount; i++) _floatBuffer[i] = BitConverter.ToInt32(input, i * 4) / 2147483648f;
                break;
            default:
                return 0;
        }
        return sampleCount;
    }

    private int DownmixToMono(float[] input, int sampleCount, int channels)
    {
        if (channels <= 0) return 0;
        int frames = sampleCount / channels;
        if (_monoBuffer.Length < frames) _monoBuffer = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++) sum += input[f * channels + c];
            _monoBuffer[f] = sum / channels;
        }
        return frames;
    }

    private void EnsureFloat(int count)
    {
        if (_floatBuffer.Length < count) _floatBuffer = new float[count];
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null && !_disposed)
            Serilog.Log.Warning(e.Exception, "RenderReferenceCapturer: 参考捕获停止 Device={Device}", _channel.DeviceName);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_capture != null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            try { _capture.StopRecording(); } catch { }
            try { _capture.Dispose(); } catch { }
            _capture = null;
        }
        try { _device.Dispose(); } catch { }
    }
}
