using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoicePipe.Audio;

/// <summary>
/// 将混音后的 PCM 数据写入 VB-Cable CABLE Input 虚拟设备。
/// </summary>
public class VirtualMicWriter : IDisposable
{
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _provider;
    private bool _disposed;

    public static readonly WaveFormat OutputFormat =
        WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    public void Initialize()
    {
        Stop();
        try
        {
            _provider = new BufferedWaveProvider(OutputFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(500),
                DiscardOnBufferOverflow = true,
            };

            // 查找 CABLE Input 设备
            int deviceNumber = FindCableInputDevice();
            _waveOut = new WaveOutEvent { DeviceNumber = deviceNumber, DesiredLatency = 40 };
            _waveOut.Init(_provider);
            _waveOut.Play();
            Serilog.Log.Information("VirtualMicWriter: 初始化完成 设备={Dev} 格式={Rate}Hz/{Ch}ch/Float32",
                deviceNumber, OutputFormat.SampleRate, OutputFormat.Channels);
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Write(byte[] pcmData)
    {
        _provider?.AddSamples(pcmData, 0, pcmData.Length);
    }

    private static int FindCableInputDevice()
    {
        int total = WaveOut.DeviceCount;
        Serilog.Log.Debug("VirtualMicWriter: 扫描 WaveOut 设备，共 {N} 个", total);
        for (int i = 0; i < total; i++)
        {
            var name = WaveOut.GetCapabilities(i).ProductName;
            Serilog.Log.Debug("  WaveOut[{I}] = {Name}", i, name);
            if (name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
            {
                Serilog.Log.Information("VirtualMicWriter: 找到 CABLE Input → 设备[{I}] {Name}", i, name);
                return i;
            }
        }
        Serilog.Log.Warning("VirtualMicWriter: 未找到 CABLE Input，回落到默认设备(-1)，混音将输出到系统默认音频");
        return -1;
    }

    public static bool IsCableInputAvailable()
    {
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            if (WaveOut.GetCapabilities(i).ProductName.Contains("CABLE Input",
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public void Stop()
    {
        try
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
        }
        catch { }
        _waveOut = null;
        _provider = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
