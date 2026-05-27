using NAudio.CoreAudioApi;
using VoicePipe.Core;
using System.Threading.Tasks;

namespace VoicePipe.Audio;

public class PipelineManager : IDisposable
{
    private readonly AudioMixEngine _mixer = new();
    // ★ 缓存所有用过的 LoopbackCapturer，按 PID 索引
    // Windows Per-Process Loopback API 不允许对已关闭的 PID 重新激活（E_UNEXPECTED），
    // 所以必须保持所有用过的 capturer 在后台运行，切换时直接复用。
    private readonly Dictionary<int, LoopbackCapturer> _loopbackCache = new();
    private int _currentPid;
    private MicCapturer? _micCapture;
    private VirtualMicWriter? _writer;

    /// <summary>
    /// 应用音频捕获彻底失败时触发（LoopbackCapturer 重试耗尽）。
    /// </summary>
    public event EventHandler<string>? CaptureFailed;

    public float AppGain
    {
        get => _mixer.AppGain;
        set => _mixer.AppGain = value;
    }

    public float MicGain
    {
        get => _mixer.MicGain;
        set => _mixer.MicGain = value;
    }

    public async Task StartAsync(int targetPid, string micId)
    {
        // 先停 Writer 和 Mic
        _writer?.Stop();
        _writer = null;
        _micCapture?.Dispose();
        _micCapture = null;

        // 切换当前 PID（影响哪个 capturer 的数据会被喂入 mixer）
        _currentPid = targetPid;

        // 查找或创建该 PID 的 LoopbackCapturer
        if (!_loopbackCache.TryGetValue(targetPid, out var capturer))
        {
            capturer = new LoopbackCapturer();

            // 闭包捕获 pid，只有当前活跃 PID 的数据才喂入 mixer
            int pid = targetPid;
            capturer.SamplesAvailable += (_, floats) =>
            {
                if (_currentPid == pid)
                    _mixer.FeedApp(floats);
            };

            capturer.CaptureFailed += (_, msg) => CaptureFailed?.Invoke(this, msg);

            _loopbackCache[targetPid] = capturer;
            await capturer.StartAsync(targetPid);
        }
        // else: 已有缓存的 capturer 在后台运行，直接复用

        // 重置混音器
        _mixer.Reset();

        // 重建 Writer 和 Mic
        _writer = new VirtualMicWriter();
        _writer.Initialize(_mixer);

        _micCapture = new MicCapturer();
        _micCapture.SamplesAvailable += (_, args) => _mixer.FeedMic(args.Samples, args.Format);
        _micCapture.Start(micId);
    }

    public async Task StopAsync()
    {
        _writer?.Stop();
        _writer = null;

        // 不销毁任何 LoopbackCapturer — 它们在后台保持运行
        // 因为 _currentPid 仍然设置着，数据会继续喂入 mixer
        // 但 writer 已停止所以不会输出

        _micCapture?.Dispose();
        _micCapture = null;

        await Task.CompletedTask;
    }

    /// <summary>
    /// 完全清理（应用退出时调用）。
    /// </summary>
    public void Dispose()
    {
        _writer?.Stop();
        _writer = null;

        foreach (var capturer in _loopbackCache.Values)
        {
            try { capturer.Dispose(); } catch { }
        }
        _loopbackCache.Clear();

        _micCapture?.Dispose();
        _micCapture = null;
    }
}