using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

public class StreamingLinearResamplerTests
{
    [Fact]
    public void PacketizedInput_MatchesSingleContinuousInput()
    {
        const int sourceRate = 44100;
        const int targetRate = 48000;
        var source = Enumerable.Range(0, 10000)
            .Select(i => MathF.Sin(2f * MathF.PI * 997f * i / sourceRate))
            .ToArray();

        var continuous = new StreamingLinearResampler(sourceRate, targetRate);
        int continuousCount = continuous.Process(source, source.Length);
        var expected = continuous.OutputBuffer[..continuousCount].ToArray();

        var packetized = new StreamingLinearResampler(sourceRate, targetRate);
        var actual = new List<float>(expected.Length);
        int[] packetSizes = { 137, 733, 441, 64, 1000, 512, 89 };
        int sourceOffset = 0;
        int packetIndex = 0;
        while (sourceOffset < source.Length)
        {
            int count = Math.Min(packetSizes[packetIndex++ % packetSizes.Length], source.Length - sourceOffset);
            var packet = source[sourceOffset..(sourceOffset + count)];
            int outputCount = packetized.Process(packet, count);
            actual.AddRange(packetized.OutputBuffer[..outputCount]);
            sourceOffset += count;
        }

        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], actual[i], 6);
    }

    [Fact]
    public void ThirtyMinuteEquivalent_PacketizationDoesNotAccumulateFrameDrift()
    {
        // 441:480 has exactly the same conversion ratio as 44.1kHz:48kHz while keeping
        // this 30-minute continuity regression fast enough for the regular test suite.
        const int sourceRate = 441;
        const int targetRate = 480;
        const int durationSeconds = 30 * 60;
        int totalInputFrames = sourceRate * durationSeconds;
        long expectedOutputFrames = ((long)(totalInputFrames - 1) * targetRate / sourceRate) + 1;

        var resampler = new StreamingLinearResampler(sourceRate, targetRate);
        int[] packetSizes = { 1, 7, 3, 11, 2, 5, 13 };
        long actualOutputFrames = 0;
        int remaining = totalInputFrames;
        int packetIndex = 0;
        while (remaining > 0)
        {
            int count = Math.Min(packetSizes[packetIndex++ % packetSizes.Length], remaining);
            int produced = resampler.Process(new float[count], count);
            actualOutputFrames += produced;
            remaining -= count;
        }

        Assert.Equal(expectedOutputFrames, actualOutputFrames);
        Assert.InRange(Math.Abs(actualOutputFrames - (long)targetRate * durationSeconds), 0, 1);
    }
}
