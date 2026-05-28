using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Collections.Generic;

namespace VoicePipe.Audio;

public record ProcessInfo(int Pid, string Name, string? IconPath);

public class ProcessEnumerator
{
    public static List<ProcessInfo> GetActiveAudioProcesses()
    {
        var result = new List<ProcessInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessionManager = device.AudioSessionManager;
            var sessions = sessionManager.Sessions;

            var selfPid = Environment.ProcessId;

            for (int i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                var pid = (int)session.GetProcessID;
                if (pid == 0 || pid == selfPid) continue; // 跳过系统进程和自身（防止反馈环路）

                try
                {
                    using var proc = Process.GetProcessById(pid);
                    string? iconPath = null;
                    try
                    {
                        iconPath = proc.MainModule?.FileName;
                    }
                    catch 
                    { 
                        // 访问 UWP 或系统进程的 MainModule 可能会抛出 AccessDenied 异常
                        // 忽略异常，继续保留该进程，iconPath 为 null
                    }
                    
                    result.Add(new ProcessInfo(pid, proc.ProcessName, iconPath));
                }
                catch { /* 进程可能已退出 */ }
            }
        }
        catch { }
        return result;
    }
}