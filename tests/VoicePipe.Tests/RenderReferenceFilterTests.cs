using VoicePipe.Audio;
using Xunit;

namespace VoicePipe.Tests;

public class RenderReferenceFilterTests
{
    [Theory]
    [InlineData("CABLE Input (VB-Audio Virtual Cable)")]
    [InlineData("CABLE-A Input (VB-Audio Cable A)")]
    [InlineData("CABLE-B Input (VB-Audio Cable B)")]
    [InlineData("VoiceMeeter Input (VB-Audio VoiceMeeter VAIO)")]
    [InlineData("VoiceMeeter Aux Input")]
    [InlineData("Virtual Desktop Audio")]
    [InlineData("扬声器 (网易虚拟音频设备)")]
    [InlineData("NGENUITY - Microphone Mix (HyperX Virtual Audio Device)")]
    [InlineData("NGENUITY - Chat (HyperX Virtual Audio Device)")]
    [InlineData("NGENUITY - 8 Channel Spatial (HyperX Virtual Audio Device)")]
    [InlineData("NGENUITY - Stream Mix (HyperX Virtual Audio Device)")]
    [InlineData("扬声器 (DeskIn(R) Virtual Audio Device)")]
    public void VirtualRoutingEndpoints_AreExcluded(string name)
        => Assert.True(SystemRenderReferenceManager.IsExcludedEndpointName(name));

    [Theory]
    [InlineData("NEVirtualDevice\\nevaudio")]
    [InlineData("ROOT\\MEDIA\\0001 nevaudio")]
    [InlineData("VDVAD_WaveExtensible.NT")]
    [InlineData("VBCableInst.NTamd64")]
    [InlineData("NGENUITY Virtual Audio Device")]
    [InlineData("DeskIn Virtual Audio Device")]
    public void VirtualDriverMetadata_IsExcluded(string metadata)
        => Assert.True(SystemRenderReferenceManager.IsExcludedEndpointMetadata(metadata));

    [Theory]
    [InlineData("hdaudio.inf HdAudModel HDAUDIO\\FUNC_01")]
    [InlineData("usbaudio2.inf USB Audio Device")]
    public void PhysicalDriverMetadata_IsIncluded(string metadata)
        => Assert.False(SystemRenderReferenceManager.IsExcludedEndpointMetadata(metadata));

    [Theory]
    [InlineData("Speakers", "NEVirtualDevice\\nevaudio")]
    [InlineData("Speakers", "VDVAD_WaveExtensible.NT")]
    [InlineData("Speakers", "VBCableInst.NTamd64")]
    public void VirtualDriverMetadata_ExcludesRuntimeReference(string name, string metadata)
        => Assert.True(SystemRenderReferenceManager.IsExcludedEndpoint(name, metadata));

    [Theory]
    [InlineData("Speakers (Realtek(R) Audio)", "hdaudio.inf HdAudModel HDAUDIO\\FUNC_01")]
    [InlineData("USB Speaker", "usbaudio2.inf USB Audio Device")]
    public void PhysicalEndpointMetadata_IsIncludedAtRuntime(string name, string metadata)
        => Assert.False(SystemRenderReferenceManager.IsExcludedEndpoint(name, metadata));

    [Theory]
    [InlineData("Speakers (Realtek(R) Audio)")]
    [InlineData("DELL U2723QE (NVIDIA High Definition Audio)")]
    [InlineData("USB Speaker")]
    [InlineData("Headphones (Bluetooth Audio)")]
    public void OrdinaryRenderEndpoints_AreIncluded(string name)
        => Assert.False(SystemRenderReferenceManager.IsExcludedEndpointName(name));
}
