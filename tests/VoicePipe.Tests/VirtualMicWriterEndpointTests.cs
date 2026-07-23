using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

public class VirtualMicWriterEndpointTests
{
    [Theory]
    [InlineData("CABLE Input (VB-Audio Virtual Cable)")]
    [InlineData("CABLE In 16 Ch (VB-Audio Virtual Cable)")]
    public void CableRenderEndpointNames_AreAccepted(string name)
        => Assert.True(VirtualMicWriter.IsCableInputEndpointName(name));

    [Theory]
    [InlineData("CABLE Output (VB-Audio Virtual Cable)")]
    [InlineData("Speakers (High Definition Audio Device)")]
    [InlineData("CABLE In 16 Ch (Other Virtual Cable)")]
    public void NonCableRenderEndpointNames_AreRejected(string name)
        => Assert.False(VirtualMicWriter.IsCableInputEndpointName(name));
}
