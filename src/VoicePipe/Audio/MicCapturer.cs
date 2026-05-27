using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace VoicePipe.Audio;

public record MicInfo(string Id, string Name);

public class MicCapturer : IDisposable
{
    private WasapiCapture? _capture;
    private MMDevice? _device;
    private bool _disposed;

    public event EventHandler<(float[] Samples, WaveFormat Format)>? SamplesAvailable;
    public WaveFormat? OutputFormat => _capture?.WaveFormat;

    /// <summary>在后台线程上初始化并启动 WASAPI 捕获，避免阻塞 UI 线程。</summary>
    public void Start(string deviceId)
    {
        Stop();

        // 在 MTA 线程中做 WASAPI 初始化
        Task.Run(() =>
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                _device = enumerator.GetDevice(deviceId);

                _capture = new WasapiCapture(_device)
                {
                    ShareMode = AudioClientShareMode.Shared,
                };

                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += (_, e) =>
                {
                    if (e.Exception != null)
                        Serilog.Log.Error(e.Exception, "MicCapturer: 录音停止异常");
                    else
                        Serilog.Log.Information("MicCapturer: 停止");
                };
                _capture.StartRecording();
                Serilog.Log.Information("MicCapturer: 开始捕获 {Id} 格式={Rate}Hz/{Ch}ch/{Bits}bit {Enc}",
                    deviceId,
                    _capture.WaveFormat.SampleRate,
                    _capture.WaveFormat.Channels,
                    _capture.WaveFormat.BitsPerSample,
                    _capture.WaveFormat.Encoding);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "MicCapturer: 初始化失败");
                Stop();
            }
        });
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0 || _capture == null) return;
        var fmt = _capture.WaveFormat;
        var samples = ConvertToFloat32(e.Buffer, e.BytesRecorded, fmt);
        SamplesAvailable?.Invoke(this, (samples, fmt));
    }

    private static float[] ConvertToFloat32(byte[] buffer, int bytesRecorded, WaveFormat fmt)
    {
        if (fmt.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            var floats = new float[bytesRecorded / 4];
            Buffer.BlockCopy(buffer, 0, floats, 0, bytesRecorded);
            return floats;
        }
        else
        {
            int bytesPerSample = fmt.BitsPerSample / 8;
            int sampleCount = bytesRecorded / bytesPerSample;
            var floats = new float[sampleCount];

            switch (fmt.BitsPerSample)
            {
                case 16:
                    for (int i = 0; i < sampleCount; i++)
                        floats[i] = BitConverter.ToInt16(buffer, i * 2) / 32768f;
                    break;
                case 24:
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int offset = i * 3;
                        // 24-bit signed: shift into high 3 bytes of int32, then shift back for sign extension
                        int sample = (buffer[offset] << 8) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 24);
                        floats[i] = (sample >> 8) / 8388608f; // 2^23
                    }
                    break;
                case 32:
                    for (int i = 0; i < sampleCount; i++)
                        floats[i] = BitConverter.ToInt32(buffer, i * 4) / 2147483648f; // 2^31
                    break;
                default:
                    Serilog.Log.Warning("MicCapturer: 不支持的位深度 {Bits}", fmt.BitsPerSample);
                    break;
            }
            return floats;
        }
    }

    public void Stop()
    {
        if (_capture != null)
        {
            try
            {
                _capture.StopRecording();
                _capture.DataAvailable -= OnDataAvailable;
                _capture.Dispose();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MicCapturer: 停止时异常");
            }
            _capture = null;
        }
        _device?.Dispose();
        _device = null;
    }

    public static List<MicInfo> GetAvailableMics()
    {
        var result = new List<MicInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var col = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            for (int i = 0; i < col.Count; i++)
                result.Add(new MicInfo(col[i].ID, col[i].FriendlyName));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "MicCapturer: 枚举麦克风失败");
        }
        return result;
    }

    public void Dispose()
    {
        if (!_disposed) { Stop(); _disposed = true; }
    }
}