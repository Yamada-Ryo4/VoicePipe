using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

public class WebRtcAecConvergenceTests
{
    private const int FrameSize = MicrophoneFrameProcessor.FrameSize; // 480
    private const int SampleRate = 48000;

    /// <summary>
    /// 合成回声测试：参考信号延迟 N 样本后以 0.5 增益混入麦克风，
    /// 验证 WebRTC AEC3 能消除回声（ERLE >= 10 dB）。
    /// </summary>
    [Theory]
    [InlineData(100)]   // ~2ms 延迟
    [InlineData(480)]   // 10ms 延迟
    [InlineData(4800)]  // 100ms 延迟（接近真实声学路径）
    public void SyntheticEcho_WebRtcAec3_SuppressesEcho(int delaySamples)
    {
        using var aec = new WebRtcAecProcessor();
        Assert.True(aec.Available, "WebRTC AEC3 should be available");
        aec.Configure(1);

        var random = new Random(0xAEC);
        var reference = new float[FrameSize];
        var microphone = new float[FrameSize];
        var output = new float[FrameSize];

        // 回声延迟缓冲
        var echoBuffer = new float[delaySamples];
        int echoWritePos = 0;

        double micEnergy = 0;
        double outEnergy = 0;
        int totalFrames = 600; // 6 秒

        for (int frame = 0; frame < totalFrames; frame++)
        {
            // 生成参考信号（带限噪声模拟人声）
            for (int i = 0; i < FrameSize; i++)
            {
                float val = ((float)random.NextDouble() * 2f - 1f) * 0.3f;
                reference[i] = val;

                // 参考信号延迟后作为回声混入麦克风
                float echo = echoBuffer[echoWritePos] * 0.5f;
                echoBuffer[echoWritePos] = val;
                echoWritePos = (echoWritePos + 1) % delaySamples;

                // 麦克风 = 回声 + 少量近端噪声
                microphone[i] = echo + ((float)random.NextDouble() * 2f - 1f) * 0.001f;
            }

            aec.ProcessFrame(microphone, reference, 1, output);

            // 跳过前 3 秒让 AEC 收敛
            if (frame < 300) continue;

            for (int i = 0; i < FrameSize; i++)
            {
                micEnergy += microphone[i] * microphone[i];
                outEnergy += output[i] * output[i];
            }
        }

        double erleDb = 10d * Math.Log10(micEnergy / Math.Max(outEnergy, 1e-20));
        Assert.True(erleDb >= 10d,
            $"Expected at least 10 dB ERLE with {delaySamples}-sample delay, got {erleDb:F2} dB");
    }

    /// <summary>
    /// 验证 WebRTC AEC3 收敛速度：1 秒内应达到 >= 6 dB ERLE。
    /// </summary>
    [Fact]
    public void WebRtcAec3_ConvergesWithinOneSecond()
    {
        using var aec = new WebRtcAecProcessor();
        Assert.True(aec.Available);
        aec.Configure(1);

        var random = new Random(0xAEC);
        var reference = new float[FrameSize];
        var microphone = new float[FrameSize];
        var output = new float[FrameSize];

        int delaySamples = 480; // 10ms
        var echoBuffer = new float[delaySamples];
        int echoWritePos = 0;

        double micEnergy = 0;
        double outEnergy = 0;
        // 只跑 1 秒（100 帧），看收敛速度
        for (int frame = 0; frame < 100; frame++)
        {
            for (int i = 0; i < FrameSize; i++)
            {
                float val = ((float)random.NextDouble() * 2f - 1f) * 0.3f;
                reference[i] = val;

                float echo = echoBuffer[echoWritePos] * 0.5f;
                echoBuffer[echoWritePos] = val;
                echoWritePos = (echoWritePos + 1) % delaySamples;

                microphone[i] = echo + ((float)random.NextDouble() * 2f - 1f) * 0.001f;
            }

            aec.ProcessFrame(microphone, reference, 1, output);

            // 统计最后 0.5 秒（50 帧）的 ERLE
            if (frame < 50) continue;

            for (int i = 0; i < FrameSize; i++)
            {
                micEnergy += microphone[i] * microphone[i];
                outEnergy += output[i] * output[i];
            }
        }

        double erleDb = 10d * Math.Log10(micEnergy / Math.Max(outEnergy, 1e-20));
        Assert.True(erleDb >= 6d,
            $"Expected at least 6 dB ERLE within 1 second, got {erleDb:F2} dB");
    }

    /// <summary>
    /// 验证 WebRTC AEC3 不会消除近端语音（双讲保护）：
    /// 当麦克风只有近端语音、没有回声时，输出应保留近端语音。
    /// </summary>
    [Fact]
    public void WebRtcAec3_PreservesNearEndSpeech()
    {
        using var aec = new WebRtcAecProcessor();
        Assert.True(aec.Available);
        aec.Configure(1);

        var random = new Random(0xAEC);
        var reference = new float[FrameSize];
        var microphone = new float[FrameSize];
        var output = new float[FrameSize];

        // 先让 AEC 适应回声路径 3 秒
        int delaySamples = 480;
        var echoBuffer = new float[delaySamples];
        int echoWritePos = 0;

        for (int frame = 0; frame < 300; frame++)
        {
            for (int i = 0; i < FrameSize; i++)
            {
                float val = ((float)random.NextDouble() * 2f - 1f) * 0.3f;
                reference[i] = val;
                float echo = echoBuffer[echoWritePos] * 0.5f;
                echoBuffer[echoWritePos] = val;
                echoWritePos = (echoWritePos + 1) % delaySamples;
                microphone[i] = echo;
            }
            aec.ProcessFrame(microphone, reference, 1, output);
        }

        // 然后切换到纯近端语音（参考静音，麦克风有语音）
        double micEnergy = 0;
        double outEnergy = 0;
        for (int frame = 0; frame < 100; frame++)
        {
            for (int i = 0; i < FrameSize; i++)
            {
                reference[i] = 0f; // 参考静音（没有远端声音）
                microphone[i] = ((float)random.NextDouble() * 2f - 1f) * 0.3f; // 近端语音
            }
            aec.ProcessFrame(microphone, reference, 1, output);

            for (int i = 0; i < FrameSize; i++)
            {
                micEnergy += microphone[i] * microphone[i];
                outEnergy += output[i] * output[i];
            }
        }

        // 输出应保留至少 50% 的近端语音能量（不被过度抑制）
        double retentionDb = 10d * Math.Log10(outEnergy / Math.Max(micEnergy, 1e-20));
        Assert.True(retentionDb >= -6d,
            $"Near-end speech should be preserved (>= -6 dB), got {retentionDb:F2} dB");
    }
}
