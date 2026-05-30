using System;

namespace VoicePipe.Audio;

/// <summary>
/// 麦克风噪声门 / Microphone noise gate (Mic_Path only).
///
/// 设计约束 (design constraints):
/// - 仅作用于麦克风路径，App_Path 不经过本组件 (Req 4.4)。
/// - 关闭时为纯直通恒等：output == input，逐元素相等 (Req 4.5/4.10)。
/// - 启用时按样本比较幅度与阈值进行门控，增益以固定步长平滑斜坡到 0 或 1，
///   每个样本最多移动一个步长，避免拉链噪声 (Req 4.6)。
/// - 原地处理、无堆分配、无锁、不抛异常；不引入任何缓冲/前瞻，
///   因此不改变 Cable_Output 的 10ms 延迟 (Req 4.9)。
///
/// 性能与线程 (performance & threading):
/// - <see cref="Process"/> 由麦克风捕获线程在 FeedMic 路径内调用，逐样本 O(1)。
/// - 参数 (<see cref="Enabled"/>/<see cref="Threshold"/>) 由 UI 线程写入，
///   用 volatile 字段做无锁可见性，与现有增益缓存一致。
/// - 平滑增益 <see cref="_gain"/> 仅在音频线程读写（单写者），无需同步。
/// </summary>
public sealed class NoiseGate
{
    /// <summary>采样率假设值（用于推导斜坡步长）。Sample rate assumed for ramp-step derivation.</summary>
    private const float SampleRate = AudioFormat.SampleRate;

    /// <summary>攻击时间常数 ~5ms。Attack time constant (gain rising toward 1).</summary>
    private const float AttackSeconds = 0.005f;

    /// <summary>释放时间常数 ~50ms。Release time constant (gain falling toward 0).</summary>
    private const float ReleaseSeconds = 0.050f;

    private volatile bool _enabled;     // Req 4.5：默认关闭 / default false
    private volatile float _threshold;  // 线性幅度 0..1 / linear amplitude (Req 4.2/4.3)

    private float _gain = 1f;           // 当前平滑增益（门状态）/ current smoothed gain

    private readonly float _attackStep;  // 每样本上升步长 / per-sample ramp-up step (Req 4.6)
    private readonly float _releaseStep; // 每样本下降步长 / per-sample ramp-down step (Req 4.6)

    public NoiseGate()
    {
        // step = 1.0 / (seconds * sampleRate)
        _attackStep = 1f / (AttackSeconds * SampleRate);
        _releaseStep = 1f / (ReleaseSeconds * SampleRate);
    }

    /// <summary>是否启用噪声门。Whether the gate is enabled (volatile-backed).</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>门限（线性幅度 0..1）。Threshold as linear amplitude (volatile-backed).</summary>
    public float Threshold
    {
        get => _threshold;
        set => _threshold = value;
    }

    /// <summary>
    /// 测试钩子：读取当前已应用的平滑增益，用于斜坡/稳态断言。
    /// Test hook: current applied smoothed gain (for ramp/steady-state assertions).
    /// </summary>
    public float CurrentGain => _gain;

    /// <summary>
    /// 原地门控 [0,len) 区间样本。无分配、无锁、不抛异常。
    /// 关闭时直接返回（纯直通恒等，Req 4.5/4.10）。
    /// In-place gate over [0,len). Allocation-free, lock-free, exception-free.
    /// Identity passthrough when disabled.
    /// </summary>
    public void Process(float[] buffer, int len)
    {
        if (!_enabled) return; // 纯直通恒等：output 与 input 逐元素相等 (Req 4.5/4.10)

        // 防御性边界处理，保证不抛异常（exception-free）。
        if (buffer == null || len <= 0) return;
        if (len > buffer.Length) len = buffer.Length;

        for (int i = 0; i < len; i++)
        {
            // 高于/等于阈值 → 开门(target=1)；低于阈值 → 关门(target=0) (Req 4.2/4.3)
            float target = MathF.Abs(buffer[i]) >= _threshold ? 1f : 0f;

            // 斜坡：每样本最多移动一个步长，绝不越过目标 (Req 4.6)
            if (_gain < target) _gain = MathF.Min(target, _gain + _attackStep);
            else if (_gain > target) _gain = MathF.Max(target, _gain - _releaseStep);

            buffer[i] *= _gain;
        }
    }
}
