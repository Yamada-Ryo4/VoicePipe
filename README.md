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

## ⬇️ 下载与使用

- **直接下载**: [VoicePipeSetup.exe](https://github.com/Yamada-Ryo4/VoicePipe/releases/download/v1.2.5/VoicePipeSetup.exe) (包含运行环境及虚拟声卡驱动，双击一键安装)
- **使用方法**:
  1. 安装后打开 VoicePipe。
  2. 在顶部下拉菜单选择你想捕获声音的应用程序（如：某个游戏或音乐播放器）。
  3. 在麦克风下拉菜单选择你的物理麦克风。
  4. 点击 **开始混音** 开始混音。
  5. 在你的语音软件（如 Discord、KOOK、YY 等）中，将麦克风输入设备设置为 **CABLE Output (VB-Audio Virtual Cable)** 即可。

## ✨ 核心特性

- **基于进程的音频隔离**：使用 Windows 10/11 的 WASAPI Per-Process Loopback API，精确捕获单个应用程序的音频，自动忽略所有其他系统声音。
- **进程智能归并**：多进程应用（Chrome、Edge 等）会派生大量子进程，VoicePipe 按进程名归并，列表中一个应用只显示一条，并自动定位到「根进程」。配合进程树捕获，所有标签页 / 子进程的声音都能一并抓到，绝不遗漏。
- **零回声设计**：捕获的应用程序音频被直接路由至虚拟麦克风。您能像平常一样听到游戏声音，而您的听众只会听到干净的混音，绝不产生递归回声。麦克风列表自动过滤 VB-Cable 回环录音端，启动前还会校验，杜绝反馈啸叫。
- **AI 神经网络降噪**：集成 Xiph **RNNoise** 神经网络降噪，可在您说话的同时去除键盘声、风扇声、底噪等背景噪声。提供「降噪强度」滑块（干湿混合），可在「彻底去噪」与「保留人声自然质感」之间自由调节，避免人声发空。仅作用于麦克风，绝不影响应用音频。
- **本地监听**：可将混音实时回放到您的耳机，边混边听确认效果。支持单独监听麦克风 / 应用音频，或监听整个输出；监听输出设备可自由选择（默认跟随系统）。监听走完全独立的输出链，绝不影响发往 VB-Cable 的低延迟主路径。
- **全局热键**：可自定义「麦克风静音」「启停混音」全局热键，游戏全屏不切窗口也能一键操作。
- **系统托盘**：支持关闭到托盘后台常驻，配合开机自启与自动接续上次的音频源 / 麦克风，挂后台一步到位。
- **智能会话缓存**：针对 Windows Per-Process Loopback API 不允许快速重新激活的限制，采用按 PID 缓存的架构。切换音频源时即时复用，无需等待或重启应用。
- **Pull 模型混音引擎**：基于 `IWaveProvider` 的 Pull 架构，`WaveOutEvent` 按设备时钟精确拉取混音数据，从根本上消除了推送模型导致的卡顿与延迟问题。VB-Cable 输出端固定 10ms 超低延迟。
- **全链路 48kHz**：整条管线统一工作在 48kHz，与现代麦克风、系统混音、VB-Cable 及 RNNoise 的原生采样率一致，消除多余的来回重采样，音质更干净、延迟更低。
- **高保真音质**：支持 16-bit, 24-bit 以及 32-bit 浮点麦克风输入格式，自动将多声道降混至立体声。采用 Catmull-Rom 三次样条重采样与 tanh 软限幅，确保高频细节与动态余量。
- **削波指示**：音量条在接近满刻度（≥99%）时变红，提示您该降低增益以避免爆音。
- **实时诊断控制台**：内置实时诊断控制台（在界面任意处右键单击即可打开），可随时监控底层音频数据流、设备初始化状态与关键状态变化日志，轻松排查问题。
- **现代化极简 UI**：提供现代化的 WPF 极简界面，设置以内嵌页面滑动切换（带动画），支持全局深色/浅色模式、5 种语言（简中/繁中/英/日/韩），并带有实时的声波频谱可视化效果。所有音量、增益、降噪、监听等设置即时生效并自动保存。

## 🛠 系统要求

- **操作系统**: Windows 10 Build 19041 (Version 2004) 或更高版本，或 Windows 11。（由于需要使用 WASAPI 进程捕获接口）。
- **运行环境**: 安装包已内置 .NET 8.0 运行时（自包含），无需额外安装。
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
   dotnet publish -c Release -r win-x64 --self-contained true
   ```

## 📦 安装包打包

VoicePipe 使用 **Inno Setup** 将主程序与 VB-Cable 驱动打包成一个对小白友好的单文件安装程序。

1. 下载并安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)。
2. 在 Inno Setup 中打开仓库根目录的 `VoicePipe.iss`。
3. 点击 **Compile (编译)**。
4. 编译好的最终安装包 `VoicePipeSetup.exe` 将生成在 `Output` 文件夹中。

## 🧠 运行原理

```
┌──────────────┐    float[]     ┌─────────────────┐    IWaveProvider.Read()    ┌──────────────────┐
│ Loopback     │ ──────────────►│                 │ ◄──────────────────────── │                  │
│ Capturer     │   FeedApp()    │  AudioMixEngine │                           │ VirtualMicWriter │ ──► CABLE Input
│ (Per-PID     │                │  (RingBuffer ×2 │   mixed float[] PCM      │ (WaveOutEvent    │     (VB-Cable)
│  缓存池)      │                │   + Resampler   │ ─────────────────────────►│  10ms 低延迟)    │
│              │                │   @48kHz)       │                           └──────────────────┘
└──────────────┘                │                 │   monitor float[] PCM     ┌──────────────────┐
┌──────────────┐    float[]     │                 │ ─────────────────────────►│ MonitorOutput    │ ──► 耳机/默认设备
│ MicCapturer  │ ──► RNNoise ──►│                 │                           │ (独立输出链)      │     (本地监听)
│ (WASAPI)     │   FeedMic()    └─────────────────┘                           └──────────────────┘
└──────────────┘   仅麦克风降噪
```

1. **LoopbackCapturer**：通过 COM 底层接口 (`IAudioClient`, `ActivateAudioInterfaceAsync`) 发起 Per-Process Loopback 流，使用 `INCLUDE_TARGET_PROCESS_TREE` 精准捕获目标进程及其整个子进程树（覆盖浏览器多标签页 / 音频服务子进程）。所有已激活的会话按 PID 缓存在 `PipelineManager` 中，切换音频源时直接复用，避免 Windows API 重复激活失败。
2. **ProcessEnumerator**：用 Toolhelp 快照获取进程父子关系，把同名多进程应用归并到「根进程」，列表中一个应用只显示一条。
3. **MicCapturer**：使用 NAudio 的 `WasapiCapture` 捕获麦克风输入，并将 PCM 数据统一标准化为 32-bit IEEE Float 格式，完美兼容 16/24/32-bit 的各类专业声卡。
4. **RnnoiseDenoiser**：基于 Xiph RNNoise 神经网络的实时降噪，仅作用于麦克风路径（48kHz 单声道，原生采样率无需重采样）。采用干湿混合在去噪与人声自然度之间平衡。关闭时为纯直通，绝不影响应用音频。
5. **AudioMixEngine**：实现 `IWaveProvider` 接口，由 `WaveOutEvent` 按设备时钟拉取数据（Pull 模型）。通过线程安全的双 RingBuffer 读取音频流，统一工作在 48kHz，Catmull-Rom 三次样条重采样，tanh 软限幅保护动态范围，并在同一循环内生成本地监听信号与 UI 波形数据。
6. **VirtualMicWriter**：接收混合完成的 32-bit float PCM 数据，使用 WASAPI 共享模式（10ms 超低延迟）持续写入 VB-Cable 的 "CABLE Input" 端点。
7. **MonitorOutput**：独立于 VB-Cable 的本地监听输出链，从混音引擎的独立监听缓冲拉取数据，回放到用户选择的播放设备（默认系统默认）。完全隔离，绝不影响 VB-Cable 主路径的低延迟。
8. **PipelineManager**：管线协调中心。维护 `Dictionary<int, LoopbackCapturer>` 缓存池，通过闭包检查 `_currentPid` 确保仅活跃 PID 的数据参与混音。`StopAsync` 只停输出和麦克风，loopback 保持后台运行以支持即时恢复。

## 📄 开源协议

本项目采用 [GNU AGPL v3.0](LICENSE) 协议开源。详细信息请参阅 LICENSE 文件。
请注意：项目内附带的 VB-Cable 驱动安装器 (`deps/vbcable_extracted`) 受 VB-Audio 官方授权条款约束。
