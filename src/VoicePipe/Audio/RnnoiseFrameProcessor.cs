using System.Runtime.InteropServices;

namespace VoicePipe.Audio;

internal interface IAudioFrameDenoiser : IDisposable
{
    bool Available { get; }
    void ProcessFrame(float[] input, float[] output);
    void Reset();
}

/// <summary>RNNoise 原生 48kHz/480-sample 单声道帧处理器；不拥有上层流式 FIFO。</summary>
internal sealed class RnnoiseFrameProcessor : IAudioFrameDenoiser
{
    private const string Lib = "rnnoise";
    internal const int FrameSize = 480;
    private const float Scale = short.MaxValue;
    private const float ScaleInv = 1f / short.MaxValue;

    [DllImport(Lib)] private static extern IntPtr rnnoise_create(IntPtr model);
    [DllImport(Lib)] private static extern void rnnoise_destroy(IntPtr state);
    [DllImport(Lib)] private static extern unsafe float rnnoise_process_frame(IntPtr state, float* output, float* input);

    private readonly object _stateLock = new();
    private readonly float[] _work = new float[FrameSize];
    private IntPtr _state;
    private bool _disposed;

    public bool Available { get; private set; }

    public RnnoiseFrameProcessor()
    {
        try
        {
            _state = rnnoise_create(IntPtr.Zero);
            Available = _state != IntPtr.Zero;
            if (Available)
                Serilog.Log.Information("RnnoiseFrameProcessor: RNNoise 初始化成功（48kHz/480帧）");
            else
                Serilog.Log.Warning("RnnoiseFrameProcessor: rnnoise_create 返回空，降噪不可用");
        }
        catch (Exception ex)
        {
            Available = false;
            Serilog.Log.Warning(ex, "RnnoiseFrameProcessor: 原生库加载失败，降噪降级为直通");
        }
    }

    public unsafe void ProcessFrame(float[] input, float[] output)
    {
        if (input.Length < FrameSize || output.Length < FrameSize)
            throw new ArgumentException("RNNoise frame buffers must contain at least 480 samples.");

        if (!Available || _disposed)
        {
            Array.Copy(input, output, FrameSize);
            return;
        }

        try
        {
            lock (_stateLock)
            {
                if (_disposed || _state == IntPtr.Zero)
                {
                    Array.Copy(input, output, FrameSize);
                    return;
                }

                for (int i = 0; i < FrameSize; i++) _work[i] = input[i] * Scale;
                fixed (float* p = _work)
                    rnnoise_process_frame(_state, p, p);
                for (int i = 0; i < FrameSize; i++) output[i] = _work[i] * ScaleInv;
            }
        }
        catch (Exception ex)
        {
            Available = false;
            Array.Copy(input, output, FrameSize);
            Serilog.Log.Error(ex, "RnnoiseFrameProcessor: 处理异常，降噪已自动旁路");
        }
    }

    public void Reset()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            if (_state != IntPtr.Zero)
            {
                try { rnnoise_destroy(_state); } catch { }
                _state = IntPtr.Zero;
            }
            try
            {
                _state = rnnoise_create(IntPtr.Zero);
                Available = _state != IntPtr.Zero;
            }
            catch
            {
                Available = false;
            }
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
            if (_state != IntPtr.Zero)
            {
                try { rnnoise_destroy(_state); } catch { }
                _state = IntPtr.Zero;
            }
            Available = false;
        }
    }
}
