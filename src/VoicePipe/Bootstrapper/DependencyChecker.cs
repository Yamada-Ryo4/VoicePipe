using Microsoft.Win32;
using System.Reflection;

namespace VoicePipe.Bootstrapper;

public record DependencyStatus(bool WindowsOk, bool VbCableOk, string WindowsVersion);

/// <summary>
/// 检测运行依赖：Windows 版本（>= 10 Build 19041）和 VB-Cable 驱动。
/// </summary>
public static class DependencyChecker
{
    public static DependencyStatus Check()
    {
        var winVer = Environment.OSVersion.Version;
        // Windows 10 Build 19041 → Version 10.0.19041
        bool winOk = winVer.Major >= 10 && winVer.Build >= 19041;
        bool cableOk = IsVbCableInstalled();
        string verStr = $"Windows {winVer.Major}.{winVer.Minor} Build {winVer.Build}";

        Serilog.Log.Information("DependencyChecker: Win={Win} Build={Build} Cable={Cable}",
            winVer, winVer.Build, cableOk);

        return new DependencyStatus(winOk, cableOk, verStr);
    }

    public static bool IsVbCableInstalled()
    {
        // 方法1：检查注册表 VB-Audio 键（里面有 VBAudioCableWDM_SR 等值）
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\VB-Audio\Cable"))
        {
            if (key?.GetValue("VBAudioCableWDM_SR") != null) return true;
        }

        // 方法2：检查驱动服务 VBAudioVACMME 是否已注册
        using (var svcKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\VBAudioVACMME"))
        {
            if (svcKey != null) return true;
        }

        // 方法3：检查 WaveOut 设备中是否存在 CABLE Input
        try
        {
            for (int i = 0; i < NAudio.Wave.WaveOut.DeviceCount; i++)
            {
                if (NAudio.Wave.WaveOut.GetCapabilities(i).ProductName
                        .Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { /* 枚举设备出错时忽略 */ }

        return false;
    }
}
