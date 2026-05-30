namespace VoicePipe.Audio;

/// <summary>
/// 全管线统一的内部音频格式常量（单一事实来源）。
///
/// VoicePipe 内部所有环节（App Loopback 捕获、麦克风重采样、混音、VB-Cable 输出、降噪）
/// 都以这个采样率为基准。选择 48000Hz 的理由：
///   1. RNNoise 神经网络固定工作在 48000Hz / 480 样本每帧，内部不再需要 44.1k↔48k 来回重采样
///      （消除音质损失与额外延迟），480 还能整除 10ms 帧、与管线天然对齐。
///   2. 现代麦克风、Windows 系统混音、VB-Cable 默认几乎都是 48000Hz，
///      所以 App/麦克风/输出三条路在常见设备上重采样开销也一并消失（仅非 48k 设备才转一次）。
///
/// ⚠ 改这个值会影响整条管线，务必全链路一致。各处不要再写裸数字采样率，一律引用本常量。
/// </summary>
public static class AudioFormat
{
    /// <summary>内部统一采样率（Hz）。</summary>
    public const int SampleRate = 48000;

    /// <summary>内部统一声道数（立体声）。</summary>
    public const int Channels = 2;
}
