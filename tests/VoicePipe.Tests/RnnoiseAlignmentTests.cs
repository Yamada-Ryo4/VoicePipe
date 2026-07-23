using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

public class RnnoiseAlignmentTests
{
    private sealed class OneFrameDelayDenoiser : IAudioFrameDenoiser
    {
        private readonly float[] _previous = new float[MicrophoneFrameProcessor.FrameSize];

        public bool Available => true;

        public void ProcessFrame(float[] input, float[] output)
        {
            Array.Copy(_previous, output, output.Length);
            Array.Copy(input, _previous, input.Length);
        }

        public void Reset() => Array.Clear(_previous);
        public void Dispose() { }
    }

    [Fact]
    public void DryWetMix_UsesTheSameDelayedSourceFrame()
    {
        using var processor = new MicrophoneFrameProcessor(new OneFrameDelayDenoiser());
        processor.DenoiseEnabled = true;
        processor.DenoiseWetMix = 0.5f;

        var first = StereoFrame(0.2f);
        processor.ProcessStereo48k(first, first.Length);
        Assert.All(first, sample => Assert.Equal(0f, sample, 6));

        var second = StereoFrame(0.4f);
        processor.ProcessStereo48k(second, second.Length);

        Assert.All(second, sample => Assert.Equal(0.2f, sample, 5));
    }

    [Fact]
    public void DisabledProcessing_IsImmediateBitExactPassthrough()
    {
        using var processor = new MicrophoneFrameProcessor(new OneFrameDelayDenoiser());
        var input = Enumerable.Range(0, 137).Select(i => (i - 68) / 100f).ToArray();
        var expected = (float[])input.Clone();

        processor.ProcessStereo48k(input, input.Length);

        Assert.Equal(expected, input);
    }

    private static float[] StereoFrame(float value)
    {
        var samples = new float[MicrophoneFrameProcessor.FrameSize * 2];
        Array.Fill(samples, value);
        return samples;
    }
}
