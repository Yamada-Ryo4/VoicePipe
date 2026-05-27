<div align="center">
  <img src="logo.png" alt="VoicePipe Logo" width="128" height="128" />
  <h1>VoicePipe</h1>

  <p>
    <a href="https://github.com/Yamada-Ryo4/VoicePipe/releases"><img src="https://img.shields.io/github/v/release/Yamada-Ryo4/VoicePipe?style=flat-square&color=2ea44f" alt="Release"></a>
    <a href="https://dotnet.microsoft.com/download/dotnet/8.0"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 8.0"></a>
    <a href="https://github.com/Yamada-Ryo4/VoicePipe/blob/main/LICENSE"><img src="https://img.shields.io/github/license/Yamada-Ryo4/VoicePipe?style=flat-square&color=007ec6" alt="License"></a>
    <a href="https://github.com/Yamada-Ryo4/VoicePipe/stargazers"><img src="https://img.shields.io/github/stars/Yamada-Ryo4/VoicePipe?style=flat-square&color=e3b341" alt="Stars"></a>
    <img src="https://img.shields.io/badge/Platform-Windows%2010%2B-blue?style=flat-square&logo=windows" alt="Windows">
  </p>

  <p>
    <a href="README.md">中文</a> | <b>English</b>
  </p>
</div>

VoicePipe is a lightweight, high-performance audio routing and mixing utility for Windows. It allows you to isolate audio from a specific application and seamlessly mix it with your microphone input in real-time, outputting the result to a virtual audio cable (e.g., VB-Cable). 

This is incredibly useful for streamers, podcasters, and gamers who want to share game audio or music through voice chat applications (like Discord, Zoom, or Teams) without transmitting system-wide sounds (like notification pings or background videos).

## Features

- **Per-Process Audio Isolation**: Uses the Windows 10/11 WASAPI Per-Process Loopback API to capture audio from exactly one application, ignoring all other system sounds.
- **Zero Echo**: Your captured application audio is routed directly to the virtual mic. You hear the game normally, but your audience hears the clean mix without any recursive echo.
- **Real-Time Mixing Engine**: Efficiently mixes the application audio and microphone input using a dual RingBuffer approach with dynamic zero-padding and resampling.
- **High Fidelity**: Supports 16-bit, 24-bit, and 32-bit float microphone inputs. Automatically downmixes multi-channel inputs to stereo.
- **Live Diagnostics Console**: Built-in real-time diagnostic console (accessible via right-clicking anywhere on the window) to monitor audio streams, device initialization, and troubleshoot issues.
- **Sleek UI**: A modern, minimalist WPF interface with dark/light mode support and real-time waveform visualization.

## Prerequisites

- **OS**: Windows 10 Build 19041 (Version 2004) or later, or Windows 11. (Required for WASAPI Per-Process Loopback).
- **Runtime**: .NET 8.0 Desktop Runtime (if not using the self-contained build).
- **Virtual Audio Cable**: VB-Cable is required and included in the installer. VoicePipe will automatically detect and output to the "CABLE Input" device.

## Building from Source

1. Clone the repository.
2. Ensure you have the .NET 8.0 SDK installed.
3. Navigate to the `src/VoicePipe` directory.
4. Run the build command:
   ```bash
   dotnet build -c Release
   ```
5. To create a self-contained executable for distribution:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
   ```

## Installer Packaging

VoicePipe uses **Inno Setup** to package the application along with the VB-Cable driver into a single, user-friendly installer.

1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php).
2. Open `VoicePipe.iss` in Inno Setup.
3. Click **Compile**.
4. The compiled installer `VoicePipeSetup.exe` will be generated in the `Output` folder.

## How It Works

1. **LoopbackCapturer**: Uses COM interfaces (`IAudioClient`, `ActivateAudioInterfaceAsync`) to initiate a Per-Process Loopback stream targeting the selected application's PID.
2. **MicCapturer**: Captures microphone input using NAudio's `WasapiCapture` and normalizes the PCM data to 32-bit IEEE Float format, handling 16/24/32-bit depths.
3. **AudioMixEngine**: Reads from both capture streams via thread-safe RingBuffers. It resamples the microphone input to match the 44.1kHz output format, mixes the channels with adjustable gain, applies hard clipping protection, and feeds the `WaveformAnalyzer`.
4. **VirtualMicWriter**: Receives the mixed 32-bit float PCM data and writes it to the VB-Cable "CABLE Input" endpoint using NAudio's `WaveOutEvent`.

## License

This project is licensed under the [GNU AGPL v3.0](LICENSE). See the LICENSE file for details. 
Note that the included VB-Cable driver installer (`deps/vbcable_extracted`) is subject to VB-Audio's licensing terms.
