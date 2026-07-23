using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

public class MicrophoneFrameProcessorTests
{
    private sealed class ImmediateDenoiser : IAudioFrameDenoiser
    {
        public bool Available => true;
        public void ProcessFrame(float[] input, float[] output) => Array.Copy(input, output, output.Length);
        public void Reset() { }
        public void Dispose() { }
    }

    private sealed class RecordingAec : IAudioFrameEchoCanceller
    {
        public bool Available => true;
        public int ReferenceChannels { get; private set; }
        public int Calls { get; private set; }

        public void Configure(int referenceChannels) => ReferenceChannels = referenceChannels;

        public void ProcessFrame(float[] microphone, float[] referenceInterleaved, int referenceChannels, float[] output)
        {
            Calls++;
            Assert.Equal(ReferenceChannels, referenceChannels);
            Array.Copy(microphone, output, output.Length);
        }

        public void Reset() { }
        public void Dispose() { }
    }

    private sealed class ConstantReference : IAecReferenceProvider
    {
        public int ChannelCount { get; set; } = 2;
        public bool TryReadFrame(float[] interleaved, int frameSize)
        {
            Array.Fill(interleaved, 0.1f, 0, frameSize * ChannelCount);
            return true;
        }
    }

    [Fact]
    public void AecEnabled_ProcessesCurrentFrameWithoutAddingAFrameDelay()
    {
        var aec = new RecordingAec();
        var reference = new ConstantReference();
        using var processor = new MicrophoneFrameProcessor(new ImmediateDenoiser(), aec);
        processor.EchoCancellationEnabled = true;
        processor.ReferenceProvider = reference;

        var frame = StereoFrame(0.3f);
        processor.ProcessStereo48k(frame, frame.Length);

        Assert.Equal(1, aec.Calls);
        Assert.All(frame, sample => Assert.Equal(0.3f, sample, 5));
    }

    [Fact]
    public void NativeSpeex_ProcessesZeroFrameWithoutFailure()
    {
        using var aec = new SpeexAecProcessor();
        Assert.True(aec.Available);
        aec.Configure(1);
        Assert.True(aec.Available);
        Assert.Equal(1, aec.ReferenceChannels);

        var mic = new float[MicrophoneFrameProcessor.FrameSize];
        var reference = new float[MicrophoneFrameProcessor.FrameSize];
        var output = new float[MicrophoneFrameProcessor.FrameSize];
        aec.ProcessFrame(mic, reference, 1, output);

        Assert.All(output, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void AecEnabledWithoutReference_FailsOpenToMicrophone()
    {
        var aec = new RecordingAec();
        using var processor = new MicrophoneFrameProcessor(new ImmediateDenoiser(), aec);
        processor.EchoCancellationEnabled = true;

        var frame = StereoFrame(-0.25f);
        processor.ProcessStereo48k(frame, frame.Length);

        Assert.Equal(0, aec.Calls);
        Assert.All(frame, sample => Assert.Equal(-0.25f, sample, 5));
    }

    private static float[] StereoFrame(float value)
    {
        var samples = new float[MicrophoneFrameProcessor.FrameSize * 2];
        Array.Fill(samples, value);
        return samples;
    }
}
