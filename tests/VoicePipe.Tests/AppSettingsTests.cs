using System;
using System.IO;
using CsCheck;
using VoicePipe.Core;
using Xunit;

namespace VoicePipe.Tests;

/// <summary>
/// Property 6: Settings persistence round-trip preserves all fields.
/// Validates: Requirements 1.4, 1.8, 2.3, 4.7, 4.8, 5.9
///
/// The path-based persistence entry points are the <c>internal</c> overloads
/// <c>AppSettings.Save(string)</c> / <c>AppSettings.Load(string)</c>, made visible to this
/// assembly via <c>InternalsVisibleTo("VoicePipe.Tests")</c> on the VoicePipe project, so the
/// round-trip targets a real temp file with no reflection.
/// </summary>
public class AppSettingsTests
{
    // ── Field enumeration (legacy coverage + AEC switch) ──
    private const int FieldCount = 15;

    private static object GetField(AppSettings s, int idx) => idx switch
    {
        0 => s.LastAppProcessName,
        1 => s.LastAppPid,
        2 => s.LastMicDeviceId,
        3 => s.AppGain,
        4 => s.MicGain,
        5 => s.MinimizeToTray,
        6 => s.AutoStartPipeline,
        7 => s.Language,
        8 => s.IsDarkTheme,
        9 => s.AutoStartBoot,
        10 => s.NoiseGateEnabled,
        11 => s.NoiseGateThreshold,
        12 => s.MuteHotkey,
        13 => s.PipelineHotkey,
        14 => s.EchoCancellationEnabled,
        _ => throw new ArgumentOutOfRangeException(nameof(idx)),
    };

    /// <summary>Copies the field at <paramref name="idx"/> from <paramref name="src"/> into <paramref name="dst"/>.</summary>
    private static void CopyField(AppSettings dst, AppSettings src, int idx)
    {
        switch (idx)
        {
            case 0: dst.LastAppProcessName = src.LastAppProcessName; break;
            case 1: dst.LastAppPid = src.LastAppPid; break;
            case 2: dst.LastMicDeviceId = src.LastMicDeviceId; break;
            case 3: dst.AppGain = src.AppGain; break;
            case 4: dst.MicGain = src.MicGain; break;
            case 5: dst.MinimizeToTray = src.MinimizeToTray; break;
            case 6: dst.AutoStartPipeline = src.AutoStartPipeline; break;
            case 7: dst.Language = src.Language; break;
            case 8: dst.IsDarkTheme = src.IsDarkTheme; break;
            case 9: dst.AutoStartBoot = src.AutoStartBoot; break;
            case 10: dst.NoiseGateEnabled = src.NoiseGateEnabled; break;
            case 11: dst.NoiseGateThreshold = src.NoiseGateThreshold; break;
            case 12: dst.MuteHotkey = src.MuteHotkey; break;
            case 13: dst.PipelineHotkey = src.PipelineHotkey; break;
            case 14: dst.EchoCancellationEnabled = src.EchoCancellationEnabled; break;
            default: throw new ArgumentOutOfRangeException(nameof(idx));
        }
    }

    private static bool FieldEqual(AppSettings a, AppSettings b, int idx) => Equals(GetField(a, idx), GetField(b, idx));

    private static bool AllFieldsEqual(AppSettings a, AppSettings b)
    {
        for (int i = 0; i < FieldCount; i++)
            if (!FieldEqual(a, b, i)) return false;
        return true;
    }

    private static AppSettings Clone(AppSettings s) => new()
    {
        LastAppProcessName = s.LastAppProcessName,
        LastAppPid = s.LastAppPid,
        LastMicDeviceId = s.LastMicDeviceId,
        AppGain = s.AppGain,
        MicGain = s.MicGain,
        MinimizeToTray = s.MinimizeToTray,
        AutoStartPipeline = s.AutoStartPipeline,
        Language = s.Language,
        IsDarkTheme = s.IsDarkTheme,
        AutoStartBoot = s.AutoStartBoot,
        NoiseGateEnabled = s.NoiseGateEnabled,
        NoiseGateThreshold = s.NoiseGateThreshold,
        MuteHotkey = s.MuteHotkey,
        PipelineHotkey = s.PipelineHotkey,
        EchoCancellationEnabled = s.EchoCancellationEnabled,
    };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort temp cleanup */ }
    }

    // ── Generators ──

    // Realistic-but-varied strings (process names, device ids, language tags), including empty.
    // Alphanumeric chars keep the JSON round-trip lossless (no unpaired-surrogate edge cases).
    private static readonly Gen<string> GenStr = Gen.String[Gen.Char.AlphaNumeric, 0, 16];

    // Finite, NaN/Infinity-free gains/thresholds in a reasonable range; JSON round-trips these exactly.
    private static readonly Gen<float> GenGain = Gen.Float[0f, 4f];

    // Random hotkeys: modifiers across the MOD_* power set (0..15), key across the VK ushort range.
    private static readonly Gen<HotkeyBinding> GenHotkey =
        Gen.Select(Gen.Int[0, 15], Gen.Int[0, 65535], (m, k) => new HotkeyBinding((uint)m, (uint)k));

    private static readonly Gen<AppSettings> GenSettings =
        from lastApp in GenStr
        from pid in Gen.Int
        from micId in GenStr
        from appGain in GenGain
        from micGain in GenGain
        from minimizeToTray in Gen.Bool
        from autoStartPipeline in Gen.Bool
        from language in GenStr
        from isDarkTheme in Gen.Bool
        from autoStartBoot in Gen.Bool
        from gateEnabled in Gen.Bool
        from gateThreshold in GenGain
        from muteHotkey in GenHotkey
        from pipelineHotkey in GenHotkey
        from echoCancellationEnabled in Gen.Bool
        select new AppSettings
        {
            LastAppProcessName = lastApp,
            LastAppPid = pid,
            LastMicDeviceId = micId,
            AppGain = appGain,
            MicGain = micGain,
            MinimizeToTray = minimizeToTray,
            AutoStartPipeline = autoStartPipeline,
            Language = language,
            IsDarkTheme = isDarkTheme,
            AutoStartBoot = autoStartBoot,
            NoiseGateEnabled = gateEnabled,
            NoiseGateThreshold = gateThreshold,
            MuteHotkey = muteHotkey,
            PipelineHotkey = pipelineHotkey,
            EchoCancellationEnabled = echoCancellationEnabled,
        };

    [Fact]
    public void SettingsPersistenceRoundTrip()
    {
        // Feature: settings-and-audio-features, Property 6: For any AppSettings, Load(Save(s)) == s field-for-field; updating one field leaves others unchanged

        // Part 1 — full round-trip: Load(Save(s)) equals s field-for-field.
        GenSettings.Sample(original =>
        {
            string path = Path.GetTempFileName();
            try
            {
                original.Save(path);
                AppSettings loaded = AppSettings.Load(path);
                return AllFieldsEqual(original, loaded);
            }
            finally
            {
                TryDelete(path);
            }
        }, iter: 100);

        // Part 2 — single-field-update corollary: changing exactly one field then persisting
        // leaves every other field equal to the baseline (and preserves the changed field).
        Gen.Select(GenSettings, GenSettings, Gen.Int[0, FieldCount - 1])
            .Sample((baseline, source, idx) =>
            {
                AppSettings modified = Clone(baseline);
                CopyField(modified, source, idx);

                string path = Path.GetTempFileName();
                try
                {
                    modified.Save(path);
                    AppSettings loaded = AppSettings.Load(path);

                    // Round-trip preserves the modified instance exactly.
                    if (!AllFieldsEqual(modified, loaded)) return false;

                    // Only the updated field may differ from the baseline.
                    for (int j = 0; j < FieldCount; j++)
                        if (j != idx && !FieldEqual(baseline, loaded, j)) return false;

                    // The updated field reflects the new value.
                    return FieldEqual(loaded, source, idx);
                }
                finally
                {
                    TryDelete(path);
                }
            }, iter: 100);
    }

    /// <summary>
    /// Deterministic example complementing the property: the default <see cref="AppSettings"/>
    /// round-trips through the path-based Save/Load with every field preserved.
    /// </summary>
    [Fact]
    public void DefaultSettings_RoundTrips()
    {
        var original = new AppSettings();
        string path = Path.GetTempFileName();
        try
        {
            original.Save(path);
            AppSettings loaded = AppSettings.Load(path);
            Assert.True(AllFieldsEqual(original, loaded));
        }
        finally
        {
            TryDelete(path);
        }
    }
}
