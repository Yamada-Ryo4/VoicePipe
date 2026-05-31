using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace VoicePipe.Audio;

public record ProcessInfo(int Pid, string Name, string? IconPath);

public class ProcessEnumerator
{
    // ★ PID → iconPath 缓存。MainModule.FileName 是慢调用（要打开进程模块快照），
    // 而路径对同一 PID 不变，每 2 秒刷新无需重复查询。
    private static readonly Dictionary<int, string?> _iconPathCache = new();

    // 枚举失败的节流：每 2 秒调用一次 GetActiveAudioProcesses，
    // 失败时只记一次日志，间隔 60s 再记，避免刷屏。
    private static long _lastEnumErrorLogTick;
    private const int EnumErrorLogIntervalMs = 60_000;

    /// <summary>
    /// 枚举有音频会话的进程，<b>按进程名去重</b>：一个 app 只返回一条，
    /// 代表 PID 取该 app 的「根进程」（顺着父进程链往上找到的最顶层同名进程）。
    ///
    /// 为什么这样：Chrome/Edge/微信等多进程 app 会有主进程 + 多个子进程（标签页/渲染/音频服务）
    /// 同时持有音频会话，PID 各不相同。若每个会话显示一条，一个 app 会冒出好几条，既乱又不知道选哪个。
    /// 而 LoopbackCapturer 用 INCLUDE_TARGET_PROCESS_TREE：选「根进程」即可把整棵树（所有标签页 +
    /// 音频服务进程）的声音全抓到，一个不漏。所以列表按名归并、代表用根进程 PID。
    /// </summary>
    public static List<ProcessInfo> GetActiveAudioProcesses()
    {
        var result = new List<ProcessInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessionManager = device.AudioSessionManager;
            var sessions = sessionManager.Sessions;

            // 一次性快照：PID → PPID（父进程）、PID → 进程名。用于把音频会话进程归并到根进程。
            var (ppidMap, nameMap) = SnapshotProcesses();

            // 先收集所有「有音频会话」的 PID
            var audioPids = new List<int>();
            for (int i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                var pid = (int)session.GetProcessID;
                if (pid == 0) continue; // 跳过系统聚合会话
                if (!audioPids.Contains(pid)) audioPids.Add(pid);
            }

            // 按进程名归并：同名 app 只保留一条，代表 PID = 该名字的根进程（顺父链向上找最顶层同名进程）
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenRootPids = new HashSet<int>();

            foreach (var pid in audioPids)
            {
                string? name = nameMap.TryGetValue(pid, out var n) ? n : null;
                if (string.IsNullOrEmpty(name))
                {
                    // 快照里没有（极少见，可能刚启动）：退回用 Process 查一次
                    try { using var p0 = Process.GetProcessById(pid); name = p0.ProcessName; }
                    catch { continue; }
                }

                // 顺父进程链向上，找到最顶层仍同名的进程作为根
                int rootPid = FindRootSameName(pid, name, ppidMap, nameMap);

                // 按名字去重（注意：VoicePipe 自身也保留，选中时在 StartPipeline 拦截+提示）
                if (!seenNames.Add(name)) continue;
                if (!seenRootPids.Add(rootPid)) continue;

                // 图标路径缓存（慢调用）
                if (!_iconPathCache.TryGetValue(rootPid, out var iconPath))
                {
                    iconPath = null;
                    try
                    {
                        using var proc = Process.GetProcessById(rootPid);
                        try { iconPath = proc.MainModule?.FileName; }
                        catch { /* UWP/系统进程 MainModule 可能 AccessDenied，忽略 */ }
                    }
                    catch { /* 根进程可能已退出，仍用其名字显示 */ }
                    _iconPathCache[rootPid] = iconPath;
                }

                result.Add(new ProcessInfo(rootPid, name, iconPath));
            }

            // 清理已退出进程的缓存项
            var alive = new HashSet<int>(result.Select(r => r.Pid));
            if (_iconPathCache.Count > alive.Count)
            {
                var dead = _iconPathCache.Keys.Where(k => !alive.Contains(k)).ToList();
                foreach (var k in dead) _iconPathCache.Remove(k);
            }
        }
        catch (Exception ex)
        {
            // 进程列表枚举失败（COM 异常 / 设备断开），UI 会显示空列表。
            // 节流：60s 内最多记一次日志，避免持续失败时刷屏。
            var now = Environment.TickCount64;
            if (now - _lastEnumErrorLogTick >= EnumErrorLogIntervalMs)
            {
                _lastEnumErrorLogTick = now;
                Serilog.Log.Warning(ex, "ProcessEnumerator: 进程列表枚举失败");
            }
        }
        return result;
    }

    /// <summary>
    /// 顺着父进程链从 pid 向上爬，只要父进程仍是同名 app 就继续，返回最顶层同名进程的 PID。
    /// 带访问集合防环（PID 复用极端情况下父链可能成环）。
    /// </summary>
    private static int FindRootSameName(int pid, string name,
        Dictionary<int, int> ppidMap, Dictionary<int, string> nameMap)
    {
        int current = pid;
        var visited = new HashSet<int> { current };
        while (ppidMap.TryGetValue(current, out var parent) && parent != 0 && visited.Add(parent))
        {
            // 父进程必须仍存在且同名，才继续往上归并
            if (!nameMap.TryGetValue(parent, out var pname)) break;
            if (!pname.Equals(name, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
        return current;
    }

    /// <summary>
    /// 取 PID→进程名 的轻量快照（Toolhelp，一次性，便宜）。供 PeakMonitor 按名字聚合峰值用。
    /// </summary>
    public static Dictionary<int, string> GetPidNameMap()
    {
        var (_, name) = SnapshotProcesses();
        return name;
    }

    /// <summary>
    /// 用 Toolhelp 快照一次性取全部进程的 PID→PPID 与 PID→名字映射（比逐个 Process 查快得多）。
    /// </summary>
    private static (Dictionary<int, int> ppid, Dictionary<int, string> name) SnapshotProcesses()
    {
        var ppid = new Dictionary<int, int>();
        var name = new Dictionary<int, string>();

        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero || snapshot == INVALID_HANDLE_VALUE)
            return (ppid, name);

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    int pid = (int)entry.th32ProcessID;
                    ppid[pid] = (int)entry.th32ParentProcessID;
                    // exeFile 形如 "chrome.exe"，去掉 .exe 与 Process.ProcessName 对齐
                    string exe = entry.szExeFile ?? "";
                    if (exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        exe = exe.Substring(0, exe.Length - 4);
                    name[pid] = exe;
                }
                while (Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return (ppid, name);
    }

    // ── Toolhelp 快照 P/Invoke ──
    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
