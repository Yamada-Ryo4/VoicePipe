namespace VoicePipe.Audio;

/// <summary>
/// 麦克风 Mic_Path 唯一的 48kHz/480-sample 分帧器。
/// 完整帧按 AEC（可选）→ RNNoise（可选）处理，RNNoise 干声与湿声统一延迟一帧。
/// </summary>
internal sealed class MicrophoneFrameProcessor : IDisposable
{
    public const int FrameSize = 480;

    private readonly IAudioFrameDenoiser _denoiser;
    private readonly IAudioFrameEchoCanceller _echoCanceller;
    private readonly float[] _inputAccum = new float[FrameSize];
    private readonly float[] _postAec = new float[FrameSize];
    private readonly float[] _denoised = new float[FrameSize];
    private readonly float[] _previousDry = new float[FrameSize];
    private float[] _reference = new float[FrameSize];
    private float[] _pending = new float[FrameSize * 4];
    private int _inputCount;
    private int _pendingCount;
    private bool _hasPreviousDry;
    private bool _disposed;

    public bool DenoiseEnabled { get; set; }
    public float DenoiseWetMix { get; set; } = 0.85f;
    public bool DenoiseAvailable => _denoiser.Available;
    public bool EchoCancellationEnabled { get; set; }
    public bool EchoCancellationAvailable => _echoCanceller.Available;
    public IAecReferenceProvider? ReferenceProvider { get; set; }

    public MicrophoneFrameProcessor() : this(new RnnoiseFrameProcessor(), CreateEchoCanceller()) { }

    internal MicrophoneFrameProcessor(IAudioFrameDenoiser denoiser)
        : this(denoiser, CreateEchoCanceller()) { }

    /// <summary>
    /// 优先使用 WebRTC AEC3（收敛快、效果更好），不可用时回退到 SpeexDSP。
    /// </summary>
    private static IAudioFrameEchoCanceller CreateEchoCanceller()
    {
        try
        {
            var webrtc = new WebRtcAecProcessor();
            if (webrtc.Available)
            {
                Serilog.Log.Information("MicrophoneFrameProcessor: 使用 WebRTC AEC3");
                return webrtc;
            }
            webrtc.Dispose();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "MicrophoneFrameProcessor: WebRTC AEC3 不可用，回退到 SpeexDSP");
        }
        Serilog.Log.Information("MicrophoneFrameProcessor: 使用 SpeexDSP");
        return new SpeexAecProcessor();
    }

    internal MicrophoneFrameProcessor(IAudioFrameDenoiser denoiser, IAudioFrameEchoCanceller echoCanceller)
    {
        _denoiser = denoiser;
        _echoCanceller = echoCanceller;
    }

    public void ProcessStereo48k(float[] buffer, int len)
    {
        if (_disposed || len <= 0 || !HasActiveProcessing()) return;

        int frames = len / 2;
        int inputIndex = 0;
        while (inputIndex < frames)
        {
            int take = Math.Min(FrameSize - _inputCount, frames - inputIndex);
            for (int i = 0; i < take; i++)
            {
                int source = (inputIndex + i) * 2;
                _inputAccum[_inputCount + i] = (buffer[source] + buffer[source + 1]) * 0.5f;
            }
            _inputCount += take;
            inputIndex += take;

            if (_inputCount == FrameSize)
            {
                ProcessCompleteFrame();
                _inputCount = 0;
            }
        }

        int available = Math.Min(frames, _pendingCount);
        for (int i = 0; i < available; i++)
        {
            float sample = _pending[i];
            buffer[i * 2] = sample;
            buffer[i * 2 + 1] = sample;
        }
        for (int i = available; i < frames; i++)
        {
            buffer[i * 2] = 0f;
            buffer[i * 2 + 1] = 0f;
        }

        int remain = _pendingCount - available;
        if (remain > 0) Array.Copy(_pending, available, _pending, 0, remain);
        _pendingCount = remain;
    }

    private bool HasActiveProcessing()
        => DenoiseEnabled && _denoiser.Available || EchoCancellationEnabled && _echoCanceller.Available;

    private void ProcessCompleteFrame()
    {
        bool aecApplied = TryApplyAec();
        if (!aecApplied) Array.Copy(_inputAccum, _postAec, FrameSize);

        if (DenoiseEnabled && _denoiser.Available)
        {
            _denoiser.ProcessFrame(_postAec, _denoised);
            EnsurePendingCapacity(_pendingCount + FrameSize);

            float wet = Math.Clamp(DenoiseWetMix, 0f, 1f);
            float dry = 1f - wet;
            for (int i = 0; i < FrameSize; i++)
            {
                float alignedDry = _hasPreviousDry ? _previousDry[i] : 0f;
                _pending[_pendingCount + i] = _denoised[i] * wet + alignedDry * dry;
            }
            _pendingCount += FrameSize;
            Array.Copy(_postAec, _previousDry, FrameSize);
            _hasPreviousDry = true;
        }
        else
        {
            EnsurePendingCapacity(_pendingCount + FrameSize);
            Array.Copy(_postAec, 0, _pending, _pendingCount, FrameSize);
            _pendingCount += FrameSize;
        }
    }

    private bool TryApplyAec()
    {
        var provider = ReferenceProvider;
        // AEC 滤波器始终后台运行（即使 EchoCancellationEnabled=false），保持持续适应状态。
        // 这样用户开/关 AEC 时是即时切换，不需要等待收敛。
        // EchoCancellationEnabled=false 时：仍跑 AEC 但丢弃输出（直通麦克风）
        // EchoCancellationEnabled=true 时：跑 AEC 并使用输出
        if (!_echoCanceller.Available || provider == null || provider.ChannelCount <= 0)
            return false;

        int channels = provider.ChannelCount;
        int needed = FrameSize * channels;
        if (_reference.Length < needed) _reference = new float[needed];
        if (!provider.TryReadFrame(_reference, FrameSize)) return false;

        if (_echoCanceller.ReferenceChannels != channels)
            _echoCanceller.Configure(channels);
        if (!_echoCanceller.Available || _echoCanceller.ReferenceChannels != channels) return false;

        // 始终跑 AEC 适应（后台保持收敛），但只有开关开着时才使用输出
        if (EchoCancellationEnabled)
        {
            _echoCanceller.ProcessFrame(_inputAccum, _reference, channels, _postAec);
            return true;
        }
        else
        {
            // 后台空跑：喂同样的输入让滤波器持续适应，但丢弃输出
            _echoCanceller.ProcessFrame(_inputAccum, _reference, channels, _postAec);
            return false; // 返回 false -> 上层直通麦克风（_inputAccum -> _postAec）
        }
    }

    private void EnsurePendingCapacity(int needed)
    {
        if (_pending.Length >= needed) return;
        int size = _pending.Length;
        while (size < needed) size *= 2;
        Array.Resize(ref _pending, size);
    }

    public void Reset()
    {
        _inputCount = 0;
        _pendingCount = 0;
        _hasPreviousDry = false;
        Array.Clear(_inputAccum);
        Array.Clear(_postAec);
        Array.Clear(_denoised);
        Array.Clear(_previousDry);
        _denoiser.Reset();
        _echoCanceller.Reset();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _echoCanceller.Dispose();
        _denoiser.Dispose();
    }
}
