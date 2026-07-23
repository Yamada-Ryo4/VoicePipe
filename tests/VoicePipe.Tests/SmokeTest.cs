using Xunit;

namespace VoicePipe.Tests;

/// <summary>
/// Trivial smoke test confirming the xUnit test harness runs and that the
/// test project successfully references the VoicePipe WPF/WinExe project.
/// </summary>
public class SmokeTest
{
    [Fact]
    public void TestHarnessRuns()
    {
        Assert.True(true);
    }
}
