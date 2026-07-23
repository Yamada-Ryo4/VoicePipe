using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

public class SpeexAecConvergenceTests
{
    [Fact]
    public void SyntheticSingleReference_ConvergesAndSuppressesEcho()
    {
        using var aec = new SpeexAecProcessor();
        Assert.True(aec.Available);
        aec.Configure(1);

        var random = new Random(0xAEC);
        var reference = new float[MicrophoneFrameProcessor.FrameSize];
        var microphone = new float[MicrophoneFrameProcessor.FrameSize];
        var output = new float[MicrophoneFrameProcessor.FrameSize];
        double microphoneEnergy = 0;
        double outputEnergy = 0;

        for (int frame = 0; frame < 500; frame++)
        {
            for (int i = 0; i < reference.Length; i++)
            {
                reference[i] = ((float)random.NextDouble() * 2f - 1f) * 0.18f;
                microphone[i] = reference[i] * 0.55f;
            }

            aec.ProcessFrame(microphone, reference, 1, output);
            if (frame < 300) continue;
            for (int i = 0; i < output.Length; i++)
            {
                microphoneEnergy += microphone[i] * microphone[i];
                outputEnergy += output[i] * output[i];
            }
        }

        double erleDb = 10d * Math.Log10(microphoneEnergy / Math.Max(outputEnergy, 1e-20));
        Assert.True(erleDb >= 12d, $"Expected at least 12 dB synthetic ERLE, got {erleDb:F2} dB");
    }

    [Fact]
    public void SyntheticSingleReference_With140MsAcousticDelay_ConvergesAndSuppressesEcho()
    {
        const int acousticDelaySamples = AudioFormat.SampleRate * 140 / 1000;
        using var aec = new SpeexAecProcessor();
        Assert.True(aec.Available);
        aec.Configure(1);

        var random = new Random(0xAEC140);
        var reference = new float[MicrophoneFrameProcessor.FrameSize];
        var microphone = new float[MicrophoneFrameProcessor.FrameSize];
        var output = new float[MicrophoneFrameProcessor.FrameSize];
        var referenceHistory = new float[acousticDelaySamples];
        int historyWrite = 0;
        double microphoneEnergy = 0;
        double outputEnergy = 0;

        for (int frame = 0; frame < 1200; frame++)
        {
            for (int i = 0; i < reference.Length; i++)
            {
                microphone[i] = referenceHistory[historyWrite] * 0.55f;
                reference[i] = ((float)random.NextDouble() * 2f - 1f) * 0.18f;
                referenceHistory[historyWrite] = reference[i];
                historyWrite = (historyWrite + 1) % referenceHistory.Length;
            }

            aec.ProcessFrame(microphone, reference, 1, output);
            if (frame < 800) continue;
            for (int i = 0; i < output.Length; i++)
            {
                microphoneEnergy += microphone[i] * microphone[i];
                outputEnergy += output[i] * output[i];
            }
        }

        double erleDb = 10d * Math.Log10(microphoneEnergy / Math.Max(outputEnergy, 1e-20));
        Assert.True(erleDb >= 12d, $"Expected at least 12 dB ERLE after a 140 ms acoustic delay, got {erleDb:F2} dB");
    }
}
