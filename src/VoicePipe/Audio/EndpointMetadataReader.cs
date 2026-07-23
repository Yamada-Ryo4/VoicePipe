using Microsoft.Win32;

namespace VoicePipe.Audio;

/// <summary>读取 Windows MMDevices 端点登记的驱动元数据，用于可靠排除虚拟音频路由。</summary>
internal static class EndpointMetadataReader
{
    private const string AudioEndpointRegistryPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio";

    public static string GetRenderEndpointMetadata(string endpointId)
        => GetEndpointMetadata("Render", endpointId);

    private static string GetEndpointMetadata(string flow, string endpointId)
    {
        if (!TryGetEndpointGuid(endpointId, out string guid)) return string.Empty;

        try
        {
            using RegistryKey? properties = Registry.LocalMachine.OpenSubKey(
                $"{AudioEndpointRegistryPath}\\{flow}\\{guid}\\Properties");
            if (properties == null) return string.Empty;

            var values = new List<string>();
            foreach (string name in properties.GetValueNames())
            {
                if (properties.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }
            return string.Join(" ", values);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool TryGetEndpointGuid(string endpointId, out string guid)
    {
        guid = string.Empty;
        int start = endpointId.LastIndexOf('{');
        if (start < 0 || !endpointId.EndsWith('}')) return false;

        string candidate = endpointId[start..];
        if (!Guid.TryParse(candidate, out _)) return false;
        guid = candidate;
        return true;
    }
}
