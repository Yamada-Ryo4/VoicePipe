using CsCheck;
using VoicePipe.ViewModels;
using Xunit;

namespace VoicePipe.Tests;

/// <summary>
/// Property 8: Clip indicator threshold mapping.
/// Validates: Requirements 6.1, 6.2
///
/// A level bar is in the clip state if and only if the peak level is at or above
/// 0.99 of full scale. The VM items expose `IsClipping => PeakLevel >= 0.99f`.
/// </summary>
public class ClipIndicatorTests
{
    [Fact]
    public void IsClipping_IsTrueIffPeakAtOrAbove099()
    {
        // Feature: settings-and-audio-features, Property 8: For any peak level p, clip state is on iff p >= 0.99f, normal otherwise
        // Span values below, exactly at, and above the 0.99 threshold (incl. 1.0 and >1.0).
        Gen.Float[-0.5f, 2.0f].Sample(p =>
        {
            var proc = new AudioProcessItem { PeakLevel = p };
            var mic = new MicDeviceItem { PeakLevel = p };
            bool expected = p >= 0.99f;
            return proc.IsClipping == expected && mic.IsClipping == expected;
        }, iter: 100);
    }

    [Theory]
    [InlineData(0.0f, false)]
    [InlineData(0.5f, false)]
    [InlineData(0.989f, false)]
    [InlineData(0.99f, true)]
    [InlineData(1.0f, true)]
    [InlineData(1.5f, true)]
    public void IsClipping_BoundaryExamples(float peak, bool expected)
    {
        // Feature: settings-and-audio-features, Property 8: For any peak level p, clip state is on iff p >= 0.99f, normal otherwise
        Assert.Equal(expected, new AudioProcessItem { PeakLevel = peak }.IsClipping);
        Assert.Equal(expected, new MicDeviceItem { PeakLevel = peak }.IsClipping);
    }
}
