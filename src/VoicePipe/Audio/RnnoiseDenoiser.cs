using System;
using System.Runtime.InteropServices;

namespace VoicePipe.Audio;

/// <summary>
/// 基于 Xiph RNNoise（RNN 神经网络）的实时人声降噪。
/// 能在说话的同时去除背景噪声（风声/键盘/风扇），而不是噪声门那样“要么全放要么全掐”。
///
/// 约束：RNNoise 固定工作在 48000Hz / 单声道 / 480 样本每帧。
/// VoicePipe 内部管线已统一为 48000Hz（见 <see cref="AudioFormat"/>），因此本类
/// 直接接收 48000Hz 立体声交错缓冲，内部只做：
///   立体声→单声道 → 按 480 帧送入 RNNoise（干湿混合）→ 单声道→立体声。
/// 不再有任何 44.1k↔48k 重采样（彻底消除往返重采样的音质损失与额外延迟）。
///
/// 干湿混合（dry/wet mix）：RNNoise 为了干净会把人声气声/高频/尾音也一并压掉，听感发“空、闷”。
/// 通过保留一小部分原始人声（干声）混回降噪结果（湿声）来缓解。关键是干声与湿声必须经过
/// 完全相同的 FIFO 延迟，否则相位错位会产生梳状滤波（金属声）——本类用一条平行干声 FIFO 保证对齐。
///
/// 仅作用于 Mic_Path。关闭（Enabled=false）时为纯直通，不触碰数据。
/// 原生库 rnnoise.dll（x64）随程序部署，P/Invoke 加载。加载/创建失败时自动降级为直通，绝不崩溃。
/// </summary>
public sealed class RnnoiseDenoiser : IDisposable
{
    private const string Lib = "rnnoise";
    private const int FrameSize = 480;          // RNNoise 固定帧长 @48k
    private const float Scale = short.MaxValue; // RNNoise 期望 16-bit 量级的 float
    private const float ScaleInv = 1f / short.MaxValue;

    [DllImport(Lib)] private static extern IntPtr rnnoise_create(IntPtr model);
    [DllImport(Lib)] private static extern void rnnoise_destroy(IntPtr state);
    [DllImport(Lib)] private static extern unsafe float rnnoise_process_frame(IntPtr state, float* outPtr, float* inPtr);

    private IntPtr _state;
    private volatile bool _enabled;
    private bool _available;     // 原生库可用
    private bool _disposed;

    // 48k 单声道帧处理 FIFO：
    // _inAccum 累积未满 480 的输入；_wetPending 存放已降噪、待输出的样本；
    // _dryPending 与 _wetPending 严格同步，存放对应帧“未经降噪的原始样本”，用于干湿混合。
    // 二者走同一套索引，延迟完全一致，混合时不会因相位错位产生梳状滤波（金属声）。
    // 输出长度恒等于输入长度，且永不越界（启动期不足处用 0 填充，即算法固有的一帧 ~10ms 延迟）。
    private readonly float[] _inAccum = new float[FrameSize];
    private int _inAccumCount;
    private float[] _wetPending = new float[FrameSize * 4];
    private float[] _dryPending = new float[FrameSize * 4];
    private int _pendingCount;

    // 干湿混合比例：1.0 = 完全降噪（最干净但可能发空），0.0 = 完全原声（不降噪）。
    // 默认 0.85：保留 15% 原声补回人声的气声/高频/质感，明显缓解“空”感，同时仍压掉大部分底噪。
    private volatile float _wetMix = 0.85f;

    /// <summary>
    /// 降噪强度（干湿混合比）。1.0=全降噪（可能发空），0.0=不降噪（纯原声）。默认 0.85。
    /// 调小让人声更自然饱满（保留更多原声），调大更彻底去噪。
    /// </summary>
    public float WetMix
    {
        get => _wetMix;
        set => _wetMix = Math.Clamp(value, 0f, 1f);
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>原生库是否成功加载（供 UI 判断“AI 降噪是否可用”）。</summary>
    public bool Available => _available;

    public RnnoiseDenoiser()
    {
        try
        {
            _state = rnnoise_create(IntPtr.Zero);
            _available = _state != IntPtr.Zero;
            if (!_available)
                Serilog.Log.Warning("RnnoiseDenoiser: rnnoise_create 返回空，降噪不可用");
            else
                Serilog.Log.Information("RnnoiseDenoiser: RNNoise 初始化成功（原生 48kHz，无重采样）");
        }
        catch (Exception ex)
        {
            _available = false;
            Serilog.Log.Warning(ex, "RnnoiseDenoiser: 原生库加载失败，降噪降级为直通");
        }
    }

    /// <summary>
    /// 处理内部格式（48000Hz 立体声交错）的麦克风缓冲，原地写回。
    /// 关闭或不可用时直接返回（纯直通）。仅作用于传入的 mic 缓冲，App_Path 不受影响。
    /// </summary>
    public void ProcessStereo48k(float[] buffer, int len)
    {
        if (!_enabled || !_available || _disposed || len <= 0) return;

        int frames = len / 2; // 立体声帧数
        if (frames == 0) return;

        try
        {
            ProcessStereo48kCore(buffer, len, frames);
        }
        catch (Exception ex)
        {
            // 音频线程上的原生互操作异常绝不允许冒泡（会中断麦克风捕获）。
            // 出错即降级为直通（buffer 保持原样），并禁用降噪避免持续抛错。
            _enabled = false;
            Serilog.Log.Error(ex, "RnnoiseDenoiser: 处理异常，降噪已自动关闭（降级为直通）");
        }
    }

    private void ProcessStereo48kCore(float[] buffer, int len, int frames)
    {
        // 1) 立体声 → 单声道（求平均）
        var mono = RentMono(frames);
        for (int i = 0; i < frames; i++)
            mono[i] = (buffer[i * 2] + buffer[i * 2 + 1]) * 0.5f;

        // 2) 按 480 帧送 RNNoise（干湿混合），原地把 mono 替换为输出样本
        DenoiseInPlace(mono, frames);

        // 3) 单声道 → 立体声，写回 buffer
        for (int i = 0; i < frames; i++)
        {
            float s = mono[i];
            buffer[i * 2] = s;
            buffer[i * 2 + 1] = s;
        }
    }

    // ── RNNoise 帧处理（FIFO 模型）：原地把 mono48k 的 [0,len) 替换为“干湿混合后”的样本。
    // 输出 = 之前已处理但未取走的样本 + 本次新处理的样本，长度恒等于 len。
    // 干声平行 FIFO 保证混合时干湿严格同延迟，避免梳状滤波。──
    private unsafe void DenoiseInPlace(float[] data, int len)
    {
        // 1) 把本次输入累积，凑满 480 就处理一帧：
        //    先把原始帧存入干声 FIFO，再降噪并把结果存入湿声 FIFO（两者同索引）。
        int i = 0;
        while (i < len)
        {
            int take = Math.Min(FrameSize - _inAccumCount, len - i);
            Array.Copy(data, i, _inAccum, _inAccumCount, take);
            _inAccumCount += take;
            i += take;

            if (_inAccumCount == FrameSize)
            {
                EnsureCapacity(_pendingCount + FrameSize);

                // 干声：保存未经降噪的原始帧（与湿声同索引，供干湿混合）
                Array.Copy(_inAccum, 0, _dryPending, _pendingCount, FrameSize);

                // 湿声：RNNoise 原地处理（期望 16-bit 量级）
                for (int k = 0; k < FrameSize; k++) _inAccum[k] *= Scale;
                fixed (float* p = _inAccum)
                    rnnoise_process_frame(_state, p, p);
                for (int k = 0; k < FrameSize; k++) _inAccum[k] *= ScaleInv;

                Array.Copy(_inAccum, 0, _wetPending, _pendingCount, FrameSize);
                _pendingCount += FrameSize;
                _inAccumCount = 0;
            }
        }

        // 2) 从 FIFO 取 len 个做干湿混合写回 data；不足部分补 0（启动期的一帧延迟）
        float wet = _wetMix;
        float dry = 1f - wet;
        int avail = Math.Min(len, _pendingCount);
        for (int k = 0; k < avail; k++)
            data[k] = _wetPending[k] * wet + _dryPending[k] * dry;
        for (int k = avail; k < len; k++) data[k] = 0f;

        // 3) 移除已取走的样本，保留剩余（干湿 FIFO 同步移位）
        int remain = _pendingCount - avail;
        if (remain > 0)
        {
            Array.Copy(_wetPending, avail, _wetPending, 0, remain);
            Array.Copy(_dryPending, avail, _dryPending, 0, remain);
        }
        _pendingCount = remain;
    }

    private void EnsureCapacity(int needed)
    {
        if (_wetPending.Length < needed)
        {
            int n = _wetPending.Length;
            while (n < needed) n *= 2;
            Array.Resize(ref _wetPending, n);
            Array.Resize(ref _dryPending, n);
        }
    }

    // ── 单声道工作缓冲（按需扩容，零长期分配） ──
    private float[]? _monoBuf;

    private float[] RentMono(int n)
    {
        if (_monoBuf == null || _monoBuf.Length < n) _monoBuf = new float[n];
        return _monoBuf;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_state != IntPtr.Zero)
        {
            try { rnnoise_destroy(_state); } catch { }
            _state = IntPtr.Zero;
        }
    }

    /// <summary>
    /// 重置内部跨调用状态（输入累积、干/湿输出 FIFO）。
    /// 切换麦克风/重启管线时调用，避免上一路残留数据与新流拼接产生杂音/相位跳变。(B2)
    /// </summary>
    public void Reset()
    {
        _inAccumCount = 0;
        _pendingCount = 0;
    }
}
