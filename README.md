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
    <b>中文</b> | <a href="README_EN.md">English</a>
  </p>
</div>

VoicePipe 是一款适用于 Windows 的轻量级、高性能音频路由与混音工具。它可以让您精准捕获指定应用程序的音频，将其与您的麦克风输入实时无缝混合，并将混音结果输出到虚拟音频线（例如 VB-Cable）。

这对于主播、播客和游戏玩家非常有用，您可以在语音聊天软件（如 Discord、Zoom 或 Teams）中共享游戏声音或音乐，而不会同时将系统其他杂音（如通知提示音、后台网页视频声音等）广播出去。

## ✨ 核心特性

- **基于进程的音频隔离**：使用 Windows 10/11 的 WASAPI Per-Process Loopback API，精确捕获单个应用程序的音频，自动忽略所有其他系统声音。
- **零回声设计**：捕获的应用程序音频被直接路由至虚拟麦克风。您能像平常一样听到游戏声音，而您的听众只会听到干净的混音，绝不产生递归回声。
- **实时混音引擎**：使用具有动态零填充和重采样功能的双 RingBuffer 架构，极其高效地同步混合应用程序与麦克风音频。
- **高保真音质**：支持 16-bit, 24-bit 以及 32-bit 浮点麦克风输入格式，并自动将多声道麦克风降维混音至立体声。
- **实时诊断控制台**：内置实时诊断控制台（在界面任意处右键单击即可打开），可随时监控底层音频数据流、设备初始化状态，轻松排查问题。
- **现代化极简 UI**：提供现代化的 WPF 极简界面，支持全局深色/浅色模式切换，并带有实时的声波频谱可视化效果。

## 🛠 系统要求

- **操作系统**: Windows 10 Build 19041 (Version 2004) 或更高版本，或 Windows 11。（由于需要使用 WASAPI 进程捕获接口）。
- **运行环境**: .NET 8.0 桌面运行时（若未打包独立运行版）。
- **虚拟音频线**: 必须安装 VB-Cable（安装包已内置）。VoicePipe 启动后会自动寻找并输出到 "CABLE Input" 设备。

## 🏗 从源码构建

1. 克隆本仓库。
2. 确保您已安装 `.NET 8.0 SDK`。
3. 进入 `src/VoicePipe` 目录。
4. 运行编译命令：
   ```bash
   dotnet build -c Release
   ```
5. 若要打包为独立运行文件（无需安装 .NET 环境）：
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
   ```

## 📦 安装包打包

VoicePipe 使用 **Inno Setup** 将主程序与 VB-Cable 驱动打包成一个对小白友好的单文件安装程序。

1. 下载并安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)。
2. 在 Inno Setup 中打开仓库根目录的 `VoicePipe.iss`。
3. 点击 **Compile (编译)**。
4. 编译好的最终安装包 `VoicePipeSetup.exe` 将生成在 `Output` 文件夹中。

## 🧠 运行原理

1. **LoopbackCapturer**：通过 COM 底层接口 (`IAudioClient`, `ActivateAudioInterfaceAsync`) 发起 Per-Process Loopback 流，精准捕获目标进程 (PID) 的音频。
2. **MicCapturer**：使用 NAudio 的 `WasapiCapture` 捕获麦克风输入，并将 PCM 数据统一标准化为 32-bit IEEE Float 格式，完美兼容 16/24/32-bit 的各类专业声卡。
3. **AudioMixEngine**：通过线程安全的 RingBuffer 读取双端音频流，自动对麦克风输入进行重采样以匹配 44.1kHz 的输出格式，进行硬限幅防爆音保护，并生成 UI 渲染所需的音频波形数据。
4. **VirtualMicWriter**：接收混合完成的 32-bit float PCM 数据，并使用 NAudio 的 `WaveOutEvent` 持续将其写入 VB-Cable 的 "CABLE Input" 端点。

## 📄 开源协议

本项目采用 [GNU AGPL v3.0](LICENSE) 协议开源。详细信息请参阅 LICENSE 文件。
请注意：项目内附带的 VB-Cable 驱动安装包 (`deps/vbcable_extracted`) 受 VB-Audio 官方授权条款约束。
