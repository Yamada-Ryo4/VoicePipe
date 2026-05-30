using System.Diagnostics;
using Microsoft.Win32;

namespace VoicePipe.Services;

/// <summary>
/// 管理 Windows 登录自启动项（per-user，无需管理员权限）。
/// 通过写入 / 删除 HKCU\Software\Microsoft\Windows\CurrentVersion\Run 下的 VoicePipe 值实现。
/// 所有注册表操作均捕获异常并经 Serilog 记录，绝不向 UI 抛出（组策略 / 权限受限场景）。
/// </summary>
public sealed class AutoStartService
{
    private const string RunKey    = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VoicePipe";

    /// <summary>
    /// 当前可执行文件路径。优先使用 Environment.ProcessPath，
    /// 回退到 Process.GetCurrentProcess().MainModule.FileName。
    /// </summary>
    private static string? GetExePath()
    {
        try
        {
            string? path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path)) return path;
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "AutoStartService: 获取可执行文件路径失败");
            return null;
        }
    }

    /// <summary>
    /// 读取 HKCU Run 项下的 VoicePipe 值，并与当前 exe 路径比较。
    /// 仅当注册表中存在该值且指向当前可执行文件时返回 true。
    /// </summary>
    public bool IsEnabled()
    {
        try
        {
            string? exePath = GetExePath();
            if (string.IsNullOrEmpty(exePath)) return false;

            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(ValueName) is not string stored) return false;

            // 存储形式带引号（"<exePath>"），比较前去除首尾引号。
            string normalized = stored.Trim().Trim('"');
            return string.Equals(normalized, exePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "AutoStartService: 读取自启动注册表项失败");
            return false;
        }
    }

    /// <summary>
    /// 启用时写入带引号的 exe 路径，禁用时删除该值。
    /// 失败（组策略 / 权限）时记录警告并安静返回，不抛出到 UI。
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null)
            {
                Serilog.Log.Warning("AutoStartService: 无法打开或创建 Run 注册表项");
                return;
            }

            if (enabled)
            {
                string? exePath = GetExePath();
                if (string.IsNullOrEmpty(exePath))
                {
                    Serilog.Log.Warning("AutoStartService: 可执行文件路径不可用，跳过自启动注册");
                    return;
                }
                key.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
                Serilog.Log.Information("AutoStartService: 已注册登录自启动项 {Path}", exePath);
            }
            else
            {
                if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                    Serilog.Log.Information("AutoStartService: 已移除登录自启动项");
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "AutoStartService: 设置自启动状态失败 (enabled={Enabled})", enabled);
        }
    }
}
