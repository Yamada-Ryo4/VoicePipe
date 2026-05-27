using NAudio.CoreAudioApi;
using VoicePipe.Core;
using System.Threading.Tasks;

namespace VoicePipe.Audio;

public class PipelineManager
{
    private readonly AudioMixEngine _mixer = new();
    private LoopbackCapturer? _appCapture;
    private MicCapturer? _micCapture;
    private VirtualMicWriter? _writer;
    private Action<byte[]>? _mixedHandler;

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
        await StopAsync();

        _mixer.Reset();

        _writer = new VirtualMicWriter();
        _writer.Initialize();

        _mixedHandler = (bytes) => _writer?.Write(bytes);
        _mixer.OnMixed += _mixedHandler;

        _appCapture = new LoopbackCapturer();
        _appCapture.SamplesAvailable += (_, floats) =>
        {
            _mixer.FeedApp(floats);
            _mixer.Tick();
        };

        _micCapture = new MicCapturer();
        _micCapture.SamplesAvailable += (_, args) =>
        {
            _mixer.FeedMic(args.Samples, args.Format);
        };

        _micCapture.Start(micId);
        await _appCapture.StartAsync(targetPid);
    }

    public async Task StopAsync()
    {
        // Unsubscribe handler BEFORE disposing to prevent writing to disposed writer
        if (_mixedHandler != null)
        {
            _mixer.OnMixed -= _mixedHandler;
            _mixedHandler = null;
        }

        _appCapture?.Dispose();
        _appCapture = null;

        _micCapture?.Dispose();
        _micCapture = null;

        _writer?.Dispose();
        _writer = null;

        await Task.CompletedTask;
    }
}