using System.Runtime.InteropServices;

namespace VoicePipe.Audio;

internal interface IAecReferenceProvider
{
    int ChannelCount { get; }
    bool TryReadFrame(float[] interleaved, int frameSize);
}

internal interface IAudioFrameEchoCanceller : IDisposable
{
    bool Available { get; }
    int ReferenceChannels { get; }
    void Configure(int referenceChannels);
    void ProcessFrame(float[] microphone, float[] referenceInterleaved, int referenceChannels, float[] output);
    void Reset();
}

/// <summary>SpeexDSP 同步多参考 AEC；每次处理 48kHz/480-sample 单声道麦克风帧。</summary>
internal sealed class SpeexAecProcessor : IAudioFrameEchoCanceller
{
    private const string Lib = "speexdsp";
    private const int FrameSize = 480;
    // 200 ms covers the measured 140–144 ms physical speaker-to-microphone path
    // without adding pipeline delay; this is the adaptive filter's echo-tail length.
    private const int FilterLength = 9600;
    private const int SpeexEchoSetSamplingRate = 24;

    [DllImport(Lib)] private static extern IntPtr speex_echo_state_init_mc(int frameSize, int filterLength, int micChannels, int speakerChannels);
    [DllImport(Lib)] private static extern void speex_echo_state_destroy(IntPtr state);
    [DllImport(Lib)] private static extern void speex_echo_state_reset(IntPtr state);
    [DllImport(Lib)] private static extern unsafe void speex_echo_cancellation(IntPtr state, short* rec, short* play, short* output);
    [DllImport(Lib)] private static extern unsafe int speex_echo_ctl(IntPtr state, int request, int* value);

    private readonly object _stateLock = new();
    private readonly short[] _mic = new short[FrameSize];
    private readonly short[] _output = new short[FrameSize];
    private short[] _reference = new short[FrameSize];
    private IntPtr _state;
    private bool _disposed;

    public bool Available { get; private set; }
    public int ReferenceChannels { get; private set; }

    public SpeexAecProcessor()
    {
        try
        {
            NativeLibrary.Load("speexdsp.dll");
            Available = true;
            Serilog.Log.Information("SpeexAecProcessor: SpeexDSP 原生库可用");
        }
        catch (Exception ex)
        {
            Available = false;
            Serilog.Log.Warning(ex, "SpeexAecProcessor: 原生库加载失败，AEC 将旁路");
        }
    }

    public void Configure(int referenceChannels)
    {
        referenceChannels = Math.Max(0, referenceChannels);
        lock (_stateLock)
        {
            if (_disposed || !Available || referenceChannels == ReferenceChannels && _state != IntPtr.Zero) return;
            DestroyStateLocked();
            ReferenceChannels = referenceChannels;
            if (referenceChannels == 0) return;

            try
            {
                _state = speex_echo_state_init_mc(FrameSize, FilterLength, 1, referenceChannels);
                if (_state == IntPtr.Zero) throw new InvalidOperationException("speex_echo_state_init_mc returned null");
                unsafe
                {
                    int rate = AudioFormat.SampleRate;
                    if (speex_echo_ctl(_state, SpeexEchoSetSamplingRate, &rate) != 0)
                        throw new InvalidOperationException("speex_echo_ctl sampling-rate failed");
                }
                int needed = FrameSize * referenceChannels;
                if (_reference.Length < needed) _reference = new short[needed];
                Serilog.Log.Information("SpeexAecProcessor: AEC 配置完成 ReferenceChannels={Channels}", referenceChannels);
            }
            catch (Exception ex)
            {
                DestroyStateLocked();
                Available = false;
                Serilog.Log.Error(ex, "SpeexAecProcessor: AEC 初始化失败，已旁路");
            }
        }
    }

    public unsafe void ProcessFrame(float[] microphone, float[] referenceInterleaved, int referenceChannels, float[] output)
    {
        if (microphone.Length < FrameSize || output.Length < FrameSize)
            throw new ArgumentException("AEC microphone buffers must contain at least 480 samples.");

        if (!Available || _disposed || referenceChannels <= 0 || referenceInterleaved.Length < FrameSize * referenceChannels)
        {
            Array.Copy(microphone, output, FrameSize);
            return;
        }

        lock (_stateLock)
        {
            if (_disposed || _state == IntPtr.Zero || referenceChannels != ReferenceChannels)
            {
                Array.Copy(microphone, output, FrameSize);
                return;
            }

            try
            {
                for (int i = 0; i < FrameSize; i++) _mic[i] = FloatToInt16(microphone[i]);
                int referenceSamples = FrameSize * referenceChannels;
                for (int i = 0; i < referenceSamples; i++) _reference[i] = FloatToInt16(referenceInterleaved[i]);

                fixed (short* micPtr = _mic)
                fixed (short* refPtr = _reference)
                fixed (short* outPtr = _output)
                    speex_echo_cancellation(_state, micPtr, refPtr, outPtr);

                for (int i = 0; i < FrameSize; i++) output[i] = _output[i] / 32768f;
            }
            catch (Exception ex)
            {
                Available = false;
                Array.Copy(microphone, output, FrameSize);
                Serilog.Log.Error(ex, "SpeexAecProcessor: 处理异常，AEC 已自动旁路");
            }
        }
    }

    private static short FloatToInt16(float sample)
        => (short)Math.Clamp((int)MathF.Round(sample * 32767f), short.MinValue, short.MaxValue);

    public void Reset()
    {
        lock (_stateLock)
        {
            if (_state != IntPtr.Zero)
            {
                try { speex_echo_state_reset(_state); } catch { }
            }
        }
    }

    private void DestroyStateLocked()
    {
        if (_state != IntPtr.Zero)
        {
            try { speex_echo_state_destroy(_state); } catch { }
            _state = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
            DestroyStateLocked();
            Available = false;
            ReferenceChannels = 0;
        }
    }
}
