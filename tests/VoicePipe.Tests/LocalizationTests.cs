using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Xunit;

namespace VoicePipe.Tests;

/// <summary>
/// Property 9: Localization key completeness and fallback.
/// Validates: Requirements 7.1, 7.3
///
/// Every new string key introduced by this feature must be defined and non-empty in all
/// five language dictionaries; and a key absent from a non-English dictionary must resolve
/// to the en-US value (fallback). The dictionaries are loaded directly from the VoicePipe
/// project's Langs/*.xaml source files via XamlReader (STA thread required for WPF).
/// </summary>
public class LocalizationTests
{
    private static readonly string[] Languages = { "en-US", "zh-CN", "zh-TW", "ja-JP", "ko-KR" };

    private static readonly string[] NewKeys =
    {
        "StrSettings", "StrSettingsTitle", "StrHotkeys", "StrHotkeyMuteMic", "StrHotkeyTogglePipeline",
        "StrHotkeyRecording", "StrHotkeyClear", "StrHotkeyConflict", "StrHotkeyNone", "StrTrayBehavior",
        "StrMinimizeToTray", "StrTrayShow", "StrTrayExit", "StrNoiseGate", "StrNoiseGateEnable",
        "StrNoiseGateThreshold", "StrAutoStart", "StrAutoStartBoot", "StrAutoStartPipeline",
        "StrAutoStartNoSource", "StrAutoStartNoMic", "StrClipping", "StrLanguageTheme", "StrMicMuted",
        "StrEchoCancellation", "StrEchoCancellationTooltip",
    };

    /// <summary>Locate the VoicePipe project's Langs directory relative to the test output.</summary>
    private static string LangsDir()
    {
        // Walk up from the test assembly location to the repo root, then into src.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "VoicePipe", "Langs");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate src/VoicePipe/Langs from " + AppContext.BaseDirectory);
    }

    private static ResourceDictionary LoadDict(string lang)
    {
        string path = Path.Combine(LangsDir(), lang + ".xaml");
        Assert.True(File.Exists(path), $"Missing language file: {path}");
        using var fs = File.OpenRead(path);
        return (ResourceDictionary)System.Windows.Markup.XamlReader.Load(fs);
    }

    /// <summary>Runs an action on a dedicated STA thread (WPF XamlReader requirement).</summary>
    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var t = new System.Threading.Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        t.SetApartmentState(System.Threading.ApartmentState.STA);
        t.Start();
        t.Join();
        if (captured != null) throw captured;
    }

    [Fact]
    public void EveryNewKey_IsNonEmpty_InAllFiveDictionaries()
    {
        // Feature: settings-and-audio-features, Property 9: Every new key is non-empty in all 5 dictionaries; a key absent from a non-English dict resolves to en-US
        RunSta(() =>
        {
            foreach (var lang in Languages)
            {
                var dict = LoadDict(lang);
                foreach (var key in NewKeys)
                {
                    Assert.True(dict.Contains(key), $"{lang}.xaml missing key '{key}'");
                    var value = dict[key] as string;
                    Assert.False(string.IsNullOrWhiteSpace(value), $"{lang}.xaml key '{key}' is empty");
                }
            }
        });
    }

    [Fact]
    public void MissingKey_FallsBackToEnUs_WhenMergedBeneath()
    {
        // Feature: settings-and-audio-features, Property 9: Every new key is non-empty in all 5 dictionaries; a key absent from a non-English dict resolves to en-US
        // Simulate the App.xaml merge order: en-US base first, active language on top.
        // Remove a key from the active dictionary and confirm resolution returns the en-US value.
        RunSta(() =>
        {
            var enUs = LoadDict("en-US");
            var active = LoadDict("zh-CN");

            const string probe = "StrSettings";
            active.Remove(probe); // active no longer defines it

            var merged = new ResourceDictionary();
            merged.MergedDictionaries.Add(enUs);   // fallback base
            merged.MergedDictionaries.Add(active); // active on top

            // WPF resolves by searching merged dictionaries in reverse; missing-from-active
            // falls through to the en-US base.
            Assert.True(merged.Contains(probe));
            Assert.Equal(enUs[probe], merged[probe]);
        });
    }
}
