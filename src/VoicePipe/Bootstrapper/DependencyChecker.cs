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
        // ★ 以"真实激活的 CABLE Input 渲染端点是否存在"为准，与真正负责输出的
        //   VirtualMicWriter.FindCableInputDevice() 完全对齐（枚举 DataFlow.Render + DeviceState.Active，
        //   FriendlyName 含 "CABLE Input"）。
        //
        //   不再以注册表键（SOFTWARE\VB-Audio\Cable、Services\VBAudioVACMME 等）作为判据：
        //   VB-Cable 卸载后这些键经常残留，会导致"检测说已安装/可用，但 VirtualMicWriter
        //   实际找不到 CABLE Input 端点 → 没声音"的标准不一致。改为单一真实端点口径后，
        //   "检测说可用" ⇔ "真能找到 CABLE Input 输出"。
        //
        //   枚举/COM 异常时按"未安装"处理（提示用户安装），由 IsCableInputAvailable 内部 try/catch 兜底。
        try
        {
            return VoicePipe.Audio.VirtualMicWriter.IsCableInputAvailable();
        }
        catch
        {
            return false;
        }
    }
}
