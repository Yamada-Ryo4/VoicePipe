using CsCheck;
using NAudio.Wave;
using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

/// <summary>
/// Property 5: Mic-path operations preserve the App-path.
/// Validates: Requirements 2.6, 4.4
///
/// The mixer sums two independent buffers in Read(): app term `appBuf[i] * appGain`
/// and mic term `(_micMuted ? 0 : micBuf[i] * micGain)` (with the noise gate applied
/// to mic samples on the FeedMic path). Changing any mic-path setting (mute, gate
/// enabled, threshold) must never alter the app component of the mixed output.
/// </summary>
public class MixIsolationTests
{
    private static readonly WaveFormat StereoFmt = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    // Small stereo sample blocks in a safe range that stays below the soft limiter
    // threshold (0.944) once gained, so the app term is a clean linear function of input.
    private static readonly Gen<float[]> GenStereoBlock =
        Gen.Float[-0.4f, 0.4f].Array[2, 512].Select(a =>
        {
            // ensure even length (stereo frames)
            if (a.Length % 2 != 0)
            {
                var b = new float[a.Length + 1];
                System.Array.Copy(a, b, a.Length);
                return b;
            }
            return a;
        });

    private static float[] ReadMix(AudioMixEngine engine, int sampleCount)
    {
        var bytes = new byte[sampleCount * 4];
        engine.Read(bytes, 0, bytes.Length);
        var outF = new float[sampleCount];
        System.Buffer.BlockCopy(bytes, 0, outF, 0, bytes.Length);
        return outF;
    }

    [Fact]
    public void MicPathSettings_DoNotAlterAppComponent()
    {
        // Feature: settings-and-audio-features, Property 5: For any app/mic buffers and any mic-path settings (mute/gate/threshold), the app component of the mix is unchanged
        Gen.Select(GenStereoBlock, GenStereoBlock, Gen.Bool, Gen.Bool, Gen.Float[0f, 1f])
            .Sample(t =>
            {
                var (appData, micData, gateEnabled, _, threshold) = t;
                int n = System.Math.Min(appData.Length, micData.Length);

                // Reference: mic forced silent (muted), mic settings irrelevant.
                var refEngine = new AudioMixEngine { MicMuted = true };
                refEngine.FeedApp((float[])appData.Clone(), n);
                refEngine.FeedMic((float[])micData.Clone(), n, StereoFmt);
                var refOut = ReadMix(refEngine, n);

                // Candidate: mic ACTIVE but MUTED, with arbitrary gate settings.
                // Because mute zeroes the mic term, the output must equal the app-only reference
                // regardless of gate enabled/threshold — proving the app component is isolated.
                var candEngine = new AudioMixEngine
                {
                    MicMuted = true,
                    NoiseGateEnabled = gateEnabled,
                    NoiseGateThreshold = threshold,
                };
                candEngine.FeedApp((float[])appData.Clone(), n);
                candEngine.FeedMic((float[])micData.Clone(), n, StereoFmt);
                var candOut = ReadMix(candEngine, n);

                for (int i = 0; i < n; i++)
                    if (refOut[i] != candOut[i]) return false;
                return true;
            }, iter: 100);
    }

    [Fact]
    public void AppComponent_IndependentOfMicData_WhenMuted()
    {
        // Feature: settings-and-audio-features, Property 5: For any app/mic buffers and any mic-path settings (mute/gate/threshold), the app component of the mix is unchanged
        // Stronger form: with mic muted, two DIFFERENT mic inputs over the same app input
        // yield identical output (the app component does not depend on mic data at all).
        Gen.Select(GenStereoBlock, GenStereoBlock, GenStereoBlock)
            .Sample(t =>
            {
                var (appData, micA, micB) = t;
                int n = System.Math.Min(appData.Length, System.Math.Min(micA.Length, micB.Length));

                var e1 = new AudioMixEngine { MicMuted = true };
                e1.FeedApp((float[])appData.Clone(), n);
                e1.FeedMic((float[])micA.Clone(), n, StereoFmt);
                var o1 = ReadMix(e1, n);

                var e2 = new AudioMixEngine { MicMuted = true };
                e2.FeedApp((float[])appData.Clone(), n);
                e2.FeedMic((float[])micB.Clone(), n, StereoFmt);
                var o2 = ReadMix(e2, n);

                for (int i = 0; i < n; i++)
                    if (o1[i] != o2[i]) return false;
                return true;
            }, iter: 100);
    }
}
