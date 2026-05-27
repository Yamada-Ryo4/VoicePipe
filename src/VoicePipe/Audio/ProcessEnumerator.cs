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

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var pid = (int)session.GetProcessID;
                if (pid == 0) continue;

                try
                {
                    using var proc = Process.GetProcessById(pid);
                    result.Add(new ProcessInfo(pid, proc.ProcessName, proc.MainModule?.FileName));
                }
                catch { /* 进程可能已退出 */ }
            }
        }
        catch { }
        return result;
    }
}