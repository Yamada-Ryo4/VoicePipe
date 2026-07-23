using System.Text.Json;
using CsCheck;
using VoicePipe.Core;
using Xunit;

namespace VoicePipe.Tests;

/// <summary>
/// Property 1: Hotkey binding serialization round-trip.
/// Validates: Requirements 2.3, 2.12
///
/// <see cref="HotkeyBinding"/> persists raw Win32 codes (MOD_* flags + VK code). A
/// System.Text.Json round-trip must reproduce the exact value so the saved form matches
/// what <c>RegisterHotKey</c> consumes.
/// </summary>
public class HotkeyBindingTests
{
    // Win32 MOD_* modifier flags. The persisted form stores these raw codes directly.
    private const uint MOD_ALT = 1u;
    private const uint MOD_CONTROL = 2u;
    private const uint MOD_SHIFT = 4u;
    private const uint MOD_WIN = 8u;

    /// <summary>
    /// Modifier mask drawn from the power set of {ALT, CONTROL, SHIFT, WIN}: each flag is
    /// independently included (or not) and OR'd together, covering all 16 combinations.
    /// </summary>
    private static readonly Gen<uint> GenModifiers =
        Gen.Select(Gen.Bool, Gen.Bool, Gen.Bool, Gen.Bool,
            (alt, ctrl, shift, win) =>
                (alt ? MOD_ALT : 0u)
                | (ctrl ? MOD_CONTROL : 0u)
                | (shift ? MOD_SHIFT : 0u)
                | (win ? MOD_WIN : 0u));

    /// <summary>Virtual-key code across the full VK byte range (0..255).</summary>
    private static readonly Gen<uint> GenKey = Gen.Int[0, 255].Select(k => (uint)k);

    /// <summary>
    /// Random bindings drawn from the modifier power set and the VK range, plus the
    /// explicit None (0, 0) case mixed in.
    /// </summary>
    private static readonly Gen<HotkeyBinding> GenBinding =
        Gen.OneOf(
            Gen.Select(GenModifiers, GenKey, (m, k) => new HotkeyBinding(m, k)),
            Gen.Const(HotkeyBinding.None));

    [Fact]
    public void Serialization_RoundTrip_PreservesValue()
    {
        // Feature: settings-and-audio-features, Property 1: For any HotkeyBinding value, Deserialize(Serialize(b)) == b
        GenBinding.Sample(b =>
        {
            string json = JsonSerializer.Serialize(b);
            HotkeyBinding roundTripped = JsonSerializer.Deserialize<HotkeyBinding>(json);
            return roundTripped.Equals(b);
        }, iter: 100);
    }

    /// <summary>
    /// Deterministic examples complementing the property: the empty binding and fully-loaded
    /// modifier combinations round-trip exactly.
    /// </summary>
    [Theory]
    [InlineData(0u, 0u)] // None
    [InlineData(MOD_CONTROL | MOD_SHIFT, 77u)] // Ctrl+Shift+M
    [InlineData(MOD_ALT | MOD_CONTROL | MOD_SHIFT | MOD_WIN, 255u)] // all modifiers, max VK
    public void Serialization_RoundTrip_ExplicitExamples(uint modifiers, uint key)
    {
        var binding = new HotkeyBinding(modifiers, key);

        string json = JsonSerializer.Serialize(binding);
        HotkeyBinding roundTripped = JsonSerializer.Deserialize<HotkeyBinding>(json);

        Assert.Equal(binding, roundTripped);
    }
}
