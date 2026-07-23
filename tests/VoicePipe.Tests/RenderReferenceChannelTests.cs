using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

public class RenderReferenceChannelTests
{
    [Fact]
    public void ReadFrame_PrefillsAcousticDelayBeforeReferenceSamples()
    {
        var channel = new RenderReferenceChannel("id", "speakers");
        var source = Enumerable.Repeat(0.5f, MicrophoneFrameProcessor.FrameSize).ToArray();
        // Write enough to cover startup delay (TargetBufferedSamples = 2400 = 5 frames)
        for (int i = 0; i < 6; i++)
            channel.Write(source, source.Length);
        var output = new float[MicrophoneFrameProcessor.FrameSize];

        // First 5 frames consume startup delay, return false
        bool f1 = channel.ReadFrame(output, 0, MicrophoneFrameProcessor.FrameSize, 0, 1);
        bool f2 = channel.ReadFrame(output, 0, MicrophoneFrameProcessor.FrameSize, 0, 1);
        bool f3 = channel.ReadFrame(output, 0, MicrophoneFrameProcessor.FrameSize, 0, 1);
        bool f4 = channel.ReadFrame(output, 0, MicrophoneFrameProcessor.FrameSize, 0, 1);
        bool f5 = channel.ReadFrame(output, 0, MicrophoneFrameProcessor.FrameSize, 0, 1);
        // 6th frame: startup exhausted, real audio flows
        bool sixth = channel.ReadFrame(output, 0, MicrophoneFrameProcessor.FrameSize, 0, 1);

        Assert.False(f1);
        Assert.False(f2);
        Assert.False(f3);
        Assert.False(f4);
        Assert.False(f5);
        Assert.True(sixth);
        Assert.All(output, sample => Assert.Equal(0.5f, sample, 5));
    }

    [Fact]
    public void StatsSnapshot_ReportsBufferedAndDriftCounters()
    {
        var channel = new RenderReferenceChannel("id", "speakers");
        channel.Write(new float[123], 123);

        RenderReferenceStats stats = channel.GetStats();

        Assert.Equal("id", stats.DeviceId);
        Assert.Equal("speakers", stats.DeviceName);
        Assert.Equal(123, stats.BufferedSamples);
        Assert.Equal(0, stats.OverrunSamples);
        Assert.Equal(0, stats.UnderrunFrames);
        Assert.Equal(0, stats.DriftDroppedSamples);
        Assert.Equal(0, stats.DriftRepeatedSamples);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(-500)]
    public void ThirtyMinutesOfIndependentClockDrift_KeepsReferenceWaterLevelBounded(int partsPerMillion)
    {
        const int framesPerSecond = 100;
        const int durationSeconds = 30 * 60;
        var channel = new RenderReferenceChannel("id", "speakers");
        var source = new float[MicrophoneFrameProcessor.FrameSize + 1];
        Array.Fill(source, 0.25f);
        var output = new float[MicrophoneFrameProcessor.FrameSize];
        double writeBudget = 0;
        long written = 0;

        for (int frame = 0; frame < framesPerSecond * durationSeconds; frame++)
        {
            writeBudget += MicrophoneFrameProcessor.FrameSize * (1d + partsPerMillion / 1_000_000d);
            long shouldHaveWritten = (long)Math.Floor(writeBudget);
            int count = (int)(shouldHaveWritten - written);
            channel.Write(source, count);
            written += count;
            channel.ReadFrame(output, 0, MicrophoneFrameProcessor.FrameSize, 0, 1);
        }

        Assert.InRange(channel.BufferedSamples,
            RenderReferenceChannel.TargetBufferedSamples - RenderReferenceChannel.DriftHysteresisSamples - 2,
            RenderReferenceChannel.TargetBufferedSamples + RenderReferenceChannel.DriftHysteresisSamples + 2);
        Assert.Equal(0, channel.OverrunSamples);
        Assert.Equal(0, channel.UnderrunFrames);
        if (partsPerMillion > 0)
            Assert.True(channel.DriftDroppedSamples > 0);
        else
            Assert.True(channel.DriftRepeatedSamples > 0);
    }
}
