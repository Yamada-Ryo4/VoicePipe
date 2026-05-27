using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace VoicePipe.Audio;

/// <summary>
/// 将混音引擎（IWaveProvider）直接连接到 VB-Cable CABLE Input 虚拟设备。
/// Pull 模型：WasapiOut 按需从 AudioMixEngine.Read() 拉取数据。
/// 使用 WASAPI 替代 WaveOut，避免 Windows 音频路由混淆导致声音输出到扬声器。
/// </summary>
public class VirtualMicWriter : IDisposable
{
    private WasapiOut? _wasapiOut;
    private MMDevice? _device;
    private bool _disposed;

    public static readonly WaveFormat OutputFormat =
        WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    /// <summary>
    /// 初始化并启动输出。直接将 AudioMixEngine 作为数据源。
    /// </summary>
    public void Initialize(AudioMixEngine mixEngine)
    {
        Stop();
        try
        {
            _device = FindCableInputDevice();
            if (_device == null)
            {
                Serilog.Log.Warning("VirtualMicWriter: 未找到 CABLE Input，将无法输出虚拟麦克风信号。");
                return;
            }

            // 使用 WasapiOut 并指定具体的 MMDevice，防止输出被路由到默认扬声器
            _wasapiOut = new WasapiOut(
                _device,
                AudioClientShareMode.Shared,
                true,
                50); // 50ms 延迟
                
            _wasapiOut.Init(mixEngine);
            _wasapiOut.Play();
            Serilog.Log.Information("VirtualMicWriter: WASAPI 初始化完成 设备={Name} 格式={Rate}Hz/{Ch}ch/Float32",
                _device.FriendlyName, OutputFormat.SampleRate, OutputFormat.Channels);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "VirtualMicWriter: 初始化失败");
            Stop();
            throw;
        }
    }

    private static MMDevice? FindCableInputDevice()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            Serilog.Log.Debug("VirtualMicWriter: 扫描 WASAPI Render 设备，共 {N} 个", devices.Count);
            
            foreach (var device in devices)
            {
                Serilog.Log.Debug("  WASAPI: {Name}", device.FriendlyName);
                if (device.FriendlyName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
                {
                    Serilog.Log.Information("VirtualMicWriter: 找到 CABLE Input → {Name}", device.FriendlyName);
                    return device;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "VirtualMicWriter: 查找 CABLE Input 设备异常");
        }
        
        return null;
    }

    public static bool IsCableInputAvailable()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                if (device.FriendlyName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    public void Stop()
    {
        try
        {
            _wasapiOut?.Stop();
            _wasapiOut?.Dispose();
        }
        catch { }
        _wasapiOut = null;
        
        _device?.Dispose();
        _device = null;
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
