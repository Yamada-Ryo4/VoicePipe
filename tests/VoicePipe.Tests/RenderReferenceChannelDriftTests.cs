using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

public class RenderReferenceChannelDriftTests
{
    private const int FrameSize = MicrophoneFrameProcessor.FrameSize; // 480

    /// <summary>
    /// When the FIFO is overfilled beyond the drift hysteresis threshold, the old
    /// implementation dropped one sample at the read head, producing a hard
    /// discontinuity. The fix spreads that correction across the entire frame via
    /// linear interpolation, so output values should be non-integer.
    /// </summary>
    [Fact]
    public void OverfilledChannel_InterpolatesInsteadOfHardDrop()
    {
        var channel = new RenderReferenceChannel("test", "test");

        // TargetBufferedSamples = 2400 (50ms), DriftHysteresis = 960 (20ms).
        // targetBeforeRead = 2400 + 480 = 2880. Drop threshold = 2880 + 960 = 3840.
        // Need buffer > 3840 after startup delay consumed.
        int fillCount = 5000;
        var ramp = new float[fillCount];
        for (int i = 0; i < fillCount; i++) ramp[i] = i;
        channel.Write(ramp, fillCount);

        // Exhaust startup delay (TargetBufferedSamples / FrameSize = 2400/480 = 5 frames).
        var scratch = new float[FrameSize];
        for (int i = 0; i < 5; i++)
            channel.ReadFrame(scratch, 0, FrameSize, 0, 1);

        // Buffer still has 5000 samples. 5000 > 3840 -> drift drop.
        var output = new float[FrameSize];
        bool hadAudio = channel.ReadFrame(output, 0, FrameSize, 0, 1);

        Assert.True(hadAudio);

        // With interpolation: step = 481/480, so most outputs are non-integer.
        int integerCount = 0;
        for (int i = 0; i < FrameSize; i++)
        {
            if (output[i] == Math.Floor(output[i]))
                integerCount++;
        }

        Assert.True(integerCount <= 2,
            $"Expected smooth interpolation (at most 2 integer outputs), but {integerCount} of {FrameSize} values are integers - hard drop detected.");
    }

    /// <summary>
    /// When the FIFO is underfilled, the fix interpolates 480 output samples from
    /// 479 source samples smoothly, with no zero-diff consecutive pairs.
    /// </summary>
    [Fact]
    public void UnderfilledChannel_InterpolatesInsteadOfHardRepeat()
    {
        var channel = new RenderReferenceChannel("test", "test");

        // Fill just enough to pass startup, then read to drain below repeat threshold.
        // TargetBufferedSamples = 2400, startup = 5 frames.
        int fillCount = 2600;
        var ramp = new float[fillCount];
        for (int i = 0; i < fillCount; i++) ramp[i] = i;
        channel.Write(ramp, fillCount);

        // Exhaust startup delay (5 frames).
        var scratch = new float[FrameSize];
        for (int i = 0; i < 5; i++)
            channel.ReadFrame(scratch, 0, FrameSize, 0, 1);

        // Buffer: 2600 samples. targetBeforeRead = 2880.
        // 2600 < 2880 - 960 = 1920? No (2600 > 1920). Need to drain more.
        // Read 2 more frames: 2600 - 960 = 1640. 1640 < 1920 -> repeat triggered.
        channel.ReadFrame(scratch, 0, FrameSize, 0, 1);
        channel.ReadFrame(scratch, 0, FrameSize, 0, 1);

        var output = new float[FrameSize];
        bool hadAudio = channel.ReadFrame(output, 0, FrameSize, 0, 1);

        Assert.True(hadAudio);

        int zeroDiffCount = 0;
        for (int i = 1; i < FrameSize; i++)
        {
            if (Math.Abs(output[i] - output[i - 1]) < 1e-6)
                zeroDiffCount++;
        }

        Assert.True(zeroDiffCount <= 1,
            $"Expected smooth interpolation (at most 1 zero-diff pair), but {zeroDiffCount} zero-diff pairs found - hard repeat detected.");
    }

    /// <summary>
    /// When the buffer is at the target level (no drift), the output must be
    /// an exact copy of the input - no interpolation artifacts.
    /// </summary>
    [Fact]
    public void BalancedChannel_ProducesExactOutput()
    {
        var channel = new RenderReferenceChannel("test", "test");

        // TargetBufferedSamples = 2400, startup = 5 frames.
        // After startup, need buffer in [1920, 3840] for balanced (no drift).
        // Fill 3000, read 5 startup frames (buffer stays 3000).
        // targetBeforeRead = 2880. 3000 > 2880+960=3840? No. 3000 < 2880-960=1920? No.
        // -> balanced.
        int fillCount = 3000;
        var ramp = new float[fillCount];
        for (int i = 0; i < fillCount; i++) ramp[i] = i;
        channel.Write(ramp, fillCount);

        var scratch = new float[FrameSize];
        for (int i = 0; i < 5; i++)
            channel.ReadFrame(scratch, 0, FrameSize, 0, 1);

        var output = new float[FrameSize];
        channel.ReadFrame(output, 0, FrameSize, 0, 1);

        // Output should be exact ramp values.
        for (int i = 0; i < FrameSize; i++)
        {
            Assert.Equal((float)i, output[i], precision: 0);
        }
    }
}
