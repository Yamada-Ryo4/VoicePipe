using System;
using CsCheck;
using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

/// <summary>
/// Property-based tests for the microphone <see cref="NoiseGate"/> (Mic_Path only).
///
/// Ramp-step constants mirror the production constants in <c>NoiseGate</c>:
///   attackStep  = 1 / (0.005s * 44100Hz) ≈ 0.0045351  (gain rising toward 1)
///   releaseStep = 1 / (0.050s * 44100Hz) ≈ 0.00045351 (gain falling toward 0)
/// The maximum per-sample ramp step is the attack step (attack is faster/larger).
/// </summary>
public class NoiseGateTests
{
    private const float SampleRate = 44100f;
    private const float AttackSeconds = 0.005f;
    private const float ReleaseSeconds = 0.050f;

    private static readonly float AttackStep = 1f / (AttackSeconds * SampleRate);
    private static readonly float ReleaseStep = 1f / (ReleaseSeconds * SampleRate);

    /// <summary>Maximum per-sample gain change = max(attackStep, releaseStep) = attackStep.</summary>
    private static readonly float MaxRampStep = MathF.Max(AttackStep, ReleaseStep);

    /// <summary>
    /// Mic sample values: predominantly in the valid [-1, 1] range with a fraction of
    /// out-of-range magnitudes (up to ±2) mixed in. All finite (no NaN/Infinity) so that
    /// bit-exact element-for-element comparison is well-defined.
    /// </summary>
    private static readonly Gen<float> GenSample =
        Gen.OneOf(
            Gen.Float[-1f, 1f],   // in-range
            Gen.Float[-1f, 1f],   // weight toward in-range
            Gen.Float[-2f, 2f]);  // includes out-of-range

    /// <summary>Variable-length blocks of mic samples (1..2048).</summary>
    private static readonly Gen<float[]> GenBlock = GenSample.Array[1, 2048];

    // ──────────────────────────────────────────────────────────────────────────
    // Task 2.2 — Property 2: Noise gate disabled is passthrough identity
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DisabledGate_IsPassthroughIdentity()
    {
        // Feature: settings-and-audio-features, Property 2: For any block of mic samples, a disabled NoiseGate output equals input element-for-element
        // Validates: Requirements 4.5, 4.10
        Gen.Select(GenBlock, Gen.Float[0f, 1f])
            .Sample(t =>
            {
                var (input, threshold) = t;

                // Disabled gate must be a pure identity regardless of threshold.
                var gate = new NoiseGate { Enabled = false, Threshold = threshold };

                var output = (float[])input.Clone();
                gate.Process(output, output.Length);

                for (int i = 0; i < input.Length; i++)
                {
                    // Bit-exact comparison: the array must be untouched.
                    if (BitConverter.SingleToInt32Bits(output[i]) != BitConverter.SingleToInt32Bits(input[i]))
                        return false;
                }
                return true;
            }, iter: 200);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Task 2.3 — Property 3: Noise gate gain ramp continuity
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EnabledGate_GainRampIsContinuous()
    {
        // Feature: settings-and-audio-features, Property 3: For any mic block through an enabled NoiseGate, |delta gain| between consecutive samples <= max(attackStep, releaseStep)
        // Validates: Requirements 4.6

        // Allow a tiny epsilon for float subtraction rounding around the ramp step.
        const float Epsilon = 1e-6f;

        Gen.Select(GenBlock, Gen.Float[0f, 1.2f])
            .Sample(t =>
            {
                var (block, threshold) = t;

                var gate = new NoiseGate { Enabled = true, Threshold = threshold };

                float previousGain = gate.CurrentGain; // initial smoothed gain (1.0)
                var single = new float[1];

                for (int i = 0; i < block.Length; i++)
                {
                    single[0] = block[i];
                    gate.Process(single, 1); // advance exactly one sample so we can read the gain
                    float gain = gate.CurrentGain;

                    if (MathF.Abs(gain - previousGain) > MaxRampStep + Epsilon)
                        return false;

                    previousGain = gain;
                }
                return true;
            }, iter: 200);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Task 2.4 — Property 4: Noise gate threshold steady-state behavior
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EnabledGate_ConvergesToSteadyState()
    {
        // Feature: settings-and-audio-features, Property 4: Sustained signal >= threshold converges gain->1 (passthrough); sustained < threshold converges gain->0
        // Validates: Requirements 4.2, 4.3

        // Settle window long enough to cover the slowest ramp (release 1->0 ≈ 2205 samples).
        const int BlockLen = 8192;

        // Keep threshold strictly positive (so silence is clearly below) and bounded so that
        // threshold + 0.1 stays <= 1.0 (clearly above, still valid full-scale magnitude).
        Gen.Float[0.05f, 0.8f].Sample(threshold =>
        {
            // (a) Sustained signal clearly AT/ABOVE threshold -> gain converges to ~1 (passthrough).
            float aboveMag = MathF.Min(threshold + 0.1f, 1.0f);
            var aboveGate = new NoiseGate { Enabled = true, Threshold = threshold };
            var aboveBlock = new float[BlockLen];
            for (int i = 0; i < BlockLen; i++)
                aboveBlock[i] = (i % 2 == 0) ? aboveMag : -aboveMag; // |sample| == aboveMag >= threshold
            aboveGate.Process(aboveBlock, BlockLen);
            if (aboveGate.CurrentGain < 0.99f) return false;

            // (b) Sustained signal clearly BELOW threshold (silence) -> gain converges toward 0.
            var belowGate = new NoiseGate { Enabled = true, Threshold = threshold };
            var belowBlock = new float[BlockLen]; // all zeros, |0| < threshold
            belowGate.Process(belowBlock, BlockLen);
            if (belowGate.CurrentGain > 0.01f) return false;

            return true;
        }, iter: 100);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Deterministic examples complementing the properties above.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DisabledGate_LeavesOutOfRangeSamplesUntouched()
    {
        var gate = new NoiseGate { Enabled = false, Threshold = 0.5f };
        var input = new[] { -2f, -1f, -0.25f, 0f, 0.25f, 1f, 2f };
        var output = (float[])input.Clone();

        gate.Process(output, output.Length);

        Assert.Equal(input, output);
    }

    [Fact]
    public void EnabledGate_LoudSustainedSignal_StaysOpen()
    {
        var gate = new NoiseGate { Enabled = true, Threshold = 0.1f };
        var block = new float[4096];
        for (int i = 0; i < block.Length; i++) block[i] = (i % 2 == 0) ? 0.5f : -0.5f;

        gate.Process(block, block.Length);

        Assert.True(gate.CurrentGain >= 0.99f);
    }

    [Fact]
    public void EnabledGate_Silence_ClosesGate()
    {
        var gate = new NoiseGate { Enabled = true, Threshold = 0.1f };
        var block = new float[8192]; // silence, below threshold

        gate.Process(block, block.Length);

        Assert.True(gate.CurrentGain <= 0.01f);
    }
}
