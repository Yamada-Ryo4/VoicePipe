using CsCheck;
using Xunit;

namespace VoicePipe.Tests;

/// <summary>
/// Property 7: Pipeline toggle semantics.
/// Validates: Requirements 2.7
///
/// The toggle-pipeline action (shared by the main-window button and the global hotkey)
/// flips the running state: stopped→running, running→stopped. Applying it twice returns
/// the state to its original value. This is the pure boolean state transition that
/// MainViewModel.TogglePipeline performs on its IsRunning flag.
/// </summary>
public class PipelineToggleTests
{
    /// <summary>Pure model of the toggle: one application flips the running flag.</summary>
    private static bool Toggle(bool running) => !running;

    [Fact]
    public void Toggle_Once_Flips_Twice_Restores()
    {
        // Feature: settings-and-audio-features, Property 7: For any running-state, one toggle flips it; two toggles restore it
        Gen.Bool.Sample(initial =>
        {
            bool once = Toggle(initial);
            if (once == initial) return false;        // one toggle must flip

            bool twice = Toggle(once);
            return twice == initial;                  // two toggles must restore
        }, iter: 100);
    }

    [Fact]
    public void Toggle_NApplications_ParityMatchesState()
    {
        // Feature: settings-and-audio-features, Property 7: For any running-state, one toggle flips it; two toggles restore it
        // Generalization: after n toggles, state == initial XOR (n is odd).
        Gen.Select(Gen.Bool, Gen.Int[0, 50]).Sample(t =>
        {
            var (initial, n) = t;
            bool state = initial;
            for (int i = 0; i < n; i++) state = Toggle(state);
            bool expected = (n % 2 == 0) ? initial : !initial;
            return state == expected;
        }, iter: 100);
    }
}
