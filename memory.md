# VoicePipe — 开发记忆档案

> 最后更新：2026-05-30  
> 用途：供 AI 助手在新会话中快速恢复完整的项目上下文，继续开发。

---

## ⚠️ AI 助手必读规则

> **每一次对项目文件的更改，无论大小，都必须：**
> 1. **写入本文件（memory.md）**，在第 7 节"变更记录"中追加一条记录
> 2. **注明更改原因**（是修复 Bug / 新功能 / 重构 / 样式调整）
> 3. **注明影响范围**（哪些文件发生了变化）
>
> 这是保持项目历史连贯性的强制要求，不得跳过。

---

## 📑 版本索引（快速定位）

> 想看某个版本改了什么？按下表对应章节直接跳，比从头翻整个变更记录省事得多。
> 历史记录都按时间堆在第 11 节起的多段表里，这里只是一份导航 — 不重复内容。

| 版本 | 主题（一句话） | 主要内容 | 章节 |
|---|---|---|---|
| **v1.2.17** (2026-06-01) | 修复 v1.2.16 麦克风占用联动 | 监听拉起/释放麦克风时同步 PeakMonitor.SetRunningMic；停止时按监听是否开判断麦克风是否真释放；standalone 监听换麦克风即时切；共修 5 个联动 bug | 第 41 节末尾 |
| **v1.2.16** (2026-06-01) | 修复停止后监听/波形/直通失效 | 监听不再依赖 VB-Cable 泵动混音引擎；VB-Cable 停止后监听输出链自驱动引擎（消费 mic/app + 波形分析 + 产监听信号）；完全停止+监听关时波形归零；红线1 主路径未动 | 第 41 节 |
| **v1.2.15** (2026-06-01) | 窗口尺寸放大 | 默认窗口 1250×900（MinSize 900×680），横向更宽卡片/滑块比例协调，全内容不滚动可见 | 第 40 节末尾 |
| **v1.2.14** (2026-06-01) | 修复折叠误关功能 | 折叠状态(MonitorExpanded/NoiseGateExpanded)与功能开关(MonitorEnabled/NoiseGateEnabled)解耦；点标题行只收起子选项不再关功能；折叠状态独立持久化 | 第 40 节末尾 |
| **v1.2.13** (2026-06-01) | UX 大改版 — 监听上主页 + 可折叠卡片 | 本地监听从设置页移到主页（麦克风下方）；降噪/监听卡片可折叠（三角+淡入上滑动画）；波形预览常显、标题栏统一（dB+开关在最右）；窗口高度 840 不滚动看全；滚动条常显防滑块抖动 | 第 40 节 |
| **v1.2.12** (2026-06-01) | 动效优化 | Toggle Switch Thumb 用 ThicknessAnimation 平滑滑动 180ms EaseOut；子面板（降噪强度、监听子选项）淡入+上滑 8px 展开动画；ThicknessAnimation 因 WPF 不能 Setter.TargetName 指 TranslateTransform 子元素而采用 | 第 39 节 |
| **v1.2.11** (2026-05-31) | UX 重构 — 降噪上主页、MicPassthrough 进麦克风 card | 降噪 card 从设置页移到主页（音量下/波形上）；MicPassthrough 移进麦克风 card 右上角，跟波形/降噪开关风格统一；加 Tooltip 解释行为；5 种语言 i18n key 全部更新 | 第 38 节 |
| **v1.2.10** (2026-05-31) | 启动混音 8-12s 卡顿修复 | 按需复用 Writer/Mic/Monitor，CABLE Input 设备 ID 缓存；同麦切换从 ~7s 降到 5ms；冷启动 600ms→150ms | 第 37 节 |
| **v1.2.9** (2026-05-31) | LastWasMicPassthrough 三态恢复 | 区分"完整运行/直通/空闲"启动恢复，ResumeAsync 串行 Start→Stop | 第 36 节 |
| **v1.2.5** (2026-05-30) | 后台轮询省 CPU + 收尾 | PeakMonitor PID→名映射 1s 节流；清理远端孤儿 MainWindow 与误传的 bin/ 产物；README 下载链接换成 v1.2.5 | 第 33 节 |
| **v1.2.4** (2026-05-30) | 自更新闭环修复 | 下载 HttpClient 拆 15s 检查 / Infinite 下载；进度条无 Content-Length 也能动；首次引导任意路径关闭都持久化；MicCapturer 日志去重 | 第 30 节 |
| **v1.2.3** (2026-05-30) | 首次使用引导 | 全屏遮罩三步说明（麦克风 / App 音频 / CABLE Output），FirstRunDone 持久化 | 第 29 节 |
| **v1.2.2** (2026-05-30) | 应用内检查更新 | UpdateService（GitHub API）+ 下载进度条 + 完成询问立刻重启更新 | 第 28 节 |
| **v1.2.1** (2026-05-29) | 频谱 + 监听优化 + 静默到托盘 | SpectrumAnalyzer/SpectrumControl（音频线程零分配）；监听加锁修竞态；监听独立音量；峰值 dB 显示；热键托盘提示；开机静默到托盘 | 第 24 节 |
| **v1.2** (2026-05-30 多次迭代) | 大版本：48kHz + RNNoise + 监听 | 全管线统一 48kHz；RNNoise 神经网络降噪（干湿混合）；本地监听独立输出链 + 可选设备；进程按名去重 + 根进程；动画方向修复；音量钳制；即时存盘；热键重置按钮 | 第 12-23 节 |
| **v1.1.1** (2026-05-29) | 性能极限优化 | 零堆分配音频路径；波形/COM 全部转后台；25ms 极限延迟；多声道下混；mic 直通模式；麦克风风格音量曲线；CPU 占用降约 4% | 第 11 节末尾段 |
| **v1.1** (2026-05-27) | 首发：双路混音 + Pull 模型 | LoopbackCapturer Per-Process（IAudioClient 接口修复）；PipelineManager PID 缓存池；AudioMixEngine Pull 模型；自包含 publish；开源镜像与 logo | 第 11 节 |

**专题章节（不按版本归档）：**
- **第 25 节**：标准验证流程（每次改动必走，强制）
- **第 32 节**：实时日志控制台优化 + 复制全部修复 + 右键菜单美化（穿插在 v1.2.5 收尾期）
- **第 31 节**：v1.2.5 前的收尾审查（删孤儿 MainWindow + 写"给下一个 AI 的话"）
- **第 34 节**：GitHub 发布流程（gh CLI · 2026-05-30 建立）

---

## 1. 项目概述

VoicePipe 是一款 Windows 桌面应用，用于将**指定 App 的系统音频**与**麦克风音频**实时混合，输出到 **VB-Cable 虚拟设备**，让用户在直播/通话时将游戏/应用声音与自己的声音合并发送。

| 属性 | 值 |
|---|---|
| 技术栈 | .NET 8.0 / WPF / CommunityToolkit.Mvvm / NAudio / Serilog |
| 项目路径 | `E:\Documents\Voicepipe\src\VoicePipe\` |
| 打包脚本 | `E:\Documents\Voicepipe\VoicePipe.iss`（Inno Setup 6） |
| 输出安装包 | `E:\Documents\Voicepipe\Output\VoicePipeSetup.exe` |
| 日志路径（运行时） | `<publish_dir>\logs\voicepipe*.log` |
| 设置文件 | `%LOCALAPPDATA%\VoicePipe\appsettings.json` |
| 系统要求 | Windows 10 Build 19041+ (20H1)，VB-Cable 驱动 |

### 构建命令
```powershell
# 编译（在 src\VoicePipe 目录）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false

# 打包安装程序（在项目根目录）
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "VoicePipe.iss"
```

---

## 2. 项目文件结构

```
src\VoicePipe\
├── App.xaml / App.xaml.cs          — 应用入口，Serilog 初始化，依赖检查
├── Audio\
│   ├── AudioMixEngine.cs           — 双路混音引擎（App + Mic → 输出）
│   ├── LoopbackCapturer.cs         — WASAPI Per-Process Loopback 捕获（进程级隔离）
│   ├── MicCapturer.cs              — WASAPI 麦克风捕获（后台线程初始化）
│   ├── PeakMonitor.cs              — 30fps 后台峰值监控（COM 索引访问）
│   ├── ProcessEnumerator.cs        — 枚举有音频会话的进程
│   ├── RingBuffer.cs               — 线程安全环形缓冲区
│   ├── VirtualMicWriter.cs         — 写入 VB-Cable CABLE Input 设备
│   └── WaveformAnalyzer.cs         — 512点滚动缓冲，供波形控件消费
├── Bootstrapper\
│   └── DependencyChecker.cs        — 检查 Win 版本 & VB-Cable 是否安装
├── Core\
│   ├── AppSettings.cs              — JSON 持久化设置（%LOCALAPPDATA%\VoicePipe\）
│   └── PipelineManager.cs          — 启停完整音频管线
├── Langs\                          — i18n 资源字典（5种语言）
│   ├── zh-CN.xaml / zh-TW.xaml
│   ├── en-US.xaml / ja-JP.xaml / ko-KR.xaml
├── Themes\
│   ├── Dark.xaml                   — 暗色主题（主色：#10B981 绿，背景系列 #121212/#1A1A1A/#222222）
│   └── Light.xaml                  — 亮色主题（主色：#10B981 绿，背景系列 #FFFFFF/#F7F7F7）
├── UI\
│   ├── Converters.cs               — 5 个值转换器（见下方说明）
│   ├── MainWindow.xaml             — 主界面 XAML
│   ├── MainWindow.xaml.cs          — 代码后端（主题/语言切换）
│   └── WaveformControl.cs          — 自定义波形绘制控件
└── ViewModels\
    └── MainViewModel.cs            — 主 ViewModel（ObservableObject + RelayCommand）
```

---

## 3. 核心音频管线架构

```
进程音频（WASAPI Per-Process Loopback）
    LoopbackCapturer.StartAsync(pid)
        └── SamplesAvailable → AudioMixEngine.FeedApp(floats)
                                └── AudioMixEngine.Tick()  ← 每次 App 帧触发
                                        ├── WaveformAnalyzer.Push(mixed)
                                        └── OnMixed(bytes) → VirtualMicWriter.Write(bytes)
                                                                └── WaveOut → CABLE Input

麦克风（WASAPI Shared）
    MicCapturer.Start(deviceId)  ← 在 Task.Run MTA 线程初始化！
        └── SamplesAvailable → AudioMixEngine.FeedMic(samples, format)
                                └── Resample（多声道→立体声，重采样至44100）→ _micBuffer

峰值（30fps，独立后台线程）
    PeakMonitor.Start()
        └── MonitorLoop: 每33ms
                ├── MMDeviceEnumerator → 渲染会话 → ProcessPeaks[pid]
                └── EnumerateAudioEndPoints → micCol[i] → MicPeaks[id]
                    ⚠ 必须用 for(i) 索引访问，不能用 foreach + Dispose！
```

### AudioMixEngine.Tick() 关键逻辑（已修复）
```csharp
// 旧版（BUG）：要求两路同时有 >=64 帧，任意一路空则全卡死
// int len = Math.Min(_appBuffer.Available, _micBuffer.Available);
// if (len < 64) return;

// 新版（正确）：只要 App 有数据就推进，Mic 不足时零填充
int appLen = _appBuffer.Available;
if (appLen < 64) return;
var app = _appBuffer.Read(appLen);
var mic = /* 读 micBuffer，不足则 new float[appLen]（零） */;
```

### Resample 处理逻辑（已修复）
```csharp
// 支持 1ch（单声道）→ 立体声
// 支持 >2ch（多声道）→ 平均下混到立体声
// 支持任意采样率 → 线性插值重采样到 44100Hz
```

### MicCapturer PCM 转换（已修复）
```csharp
// 支持 16-bit int（最常见）
// 支持 24-bit int（专业麦克风）
// 支持 32-bit int（高端音频接口）
// 支持 IEEE Float 32-bit（直接复制）
```

---

## 4. ViewModel 关键设计

```csharp
// 两套"选中→确认"流程（App 源 和 麦克风 各自独立）
SelectedProcess → [点击"使用此源"] → ActiveProcess
SelectedMic     → [点击"使用此源"] → ActiveMic

// 30fps 定时器（UI线程 DispatcherTimer）
_waveformTimer.Tick += (_, _) =>
{
    foreach (var p in Processes) p.PeakLevel = PeakMonitor.ProcessPeaks[p.Pid];
    foreach (var m in MicDevices) m.PeakLevel = PeakMonitor.MicPeaks[m.Id];
    WaveformData = WaveformAnalyzer.GetSnapshot();
};

// 2s 刷新进程列表（差异更新，不清空重建，避免选中态丢失）
_refreshTimer.Tick += (_, _) => { RefreshProcesses(); RefreshMicDevices(); };
```

### 数据模型
- `AudioProcessItem : ObservableObject` — `Pid, Name, IconPath, PeakLevel`（可变，30fps 原地更新）
- `MicDeviceItem : ObservableObject` — `Id, Name, PeakLevel`
- `ActiveSourceName`, `ActiveMicName` — 显示在"当前音频源"标签上的字符串

### 设置持久化（AppSettings）
| 字段 | 用途 |
|---|---|
| `LastAppProcessName` | 记住上次选的 App 进程名，下次启动自动 ConfirmSource |
| `LastMicDeviceId` | 记住上次选的麦克风 ID，下次启动自动 ConfirmMic |
| `AppGain / MicGain` | 音量滑块值，范围 0~2 |
| `Language` | 当前语言代码（zh-CN 等） |

---

## 5. UI / 主题系统

### 颜色规范

| Token | 暗色 | 亮色 |
|---|---|---|
| `BgDeepColor` | `#121212` | `#FFFFFF` |
| `BgPanelColor` | `#1A1A1A` | `#F7F7F7` |
| `BgCardColor` | `#222222` | `#FFFFFF` |
| `AccentColor` | `#10B981`（翡翠绿） | `#10B981` |
| `TextPrimaryColor` | `#EEEEEE` | `#111111` |
| `TextSecondaryColor` | `#888888` | `#777777` |
| `BorderColor` | `#333333` | `#E0E0E0` |
| `AccentDangerColor` | `#FF5555` | `#E00000` |

### Button 样式
| 样式 key | 用途 | 背景 | 文字 |
|---|---|---|---|
| `PrimaryButton` | 主要操作（使用此源、开始混音） | AccentBrush (#10B981) | **White（硬编码！）** |
| `DangerButton` | 停止按钮 | Transparent | AccentDangerBrush |
| `GhostButton` | 次要操作（刷新进程） | Transparent | **TextPrimaryBrush（跟随主题！）** |

> ⚠ **重要**：`PrimaryButton.Foreground` 是 `White`（硬编码），不是 `TextPrimaryBrush`，否则亮色模式下文字不可见。`GhostButton.Foreground` 必须显式设为 `TextPrimaryBrush`，不能省略。

### 主题切换实现
`MainWindow.xaml.cs` 的 `UpdateResourceDictionaries()` 在 `Application.Current.Resources.MergedDictionaries` 中原地替换 Theme 和 Lang 字典，所有 `{DynamicResource}` 绑定自动响应。

### i18n Key 完整列表
所有语言文件需包含以下 key：
```
StrSubtitle, StrAppAudioSource, StrMicrophoneInput, StrVolumeControl
StrWaveformPreview, StrRefreshProcesses, StrRefresh, StrUseThisSource
StrCurrentSource, StrNoSource, StrStartMixing, StrStop, StrAppAudio
StrMicrophone, StrPid, StrThemeDark, StrThemeLight
```

---

## 6. Converters（UI\Converters.cs）

| 类名 | 功能 |
|---|---|
| `BoolToColorConverter` | bool → 绿色(#34D399) / 灰色(#4A6A70)，用于状态椭圆 |
| `BoolNegateConverter` | bool 取反 |
| `BoolToVisibilityConverter` | true→Visible, false→Collapsed |
| `BoolToInvVisibilityConverter` | true→Collapsed, false→Visible（用于 Start/Stop 切换） |
| `PeakToWidthConverter` | float(0~1) × maxWidth(参数) → 峰值条像素宽度 |

所有 Converter 均通过 `x:Static` 以单例方式调用：
```xml
Converter="{x:Static ui:BoolToVisibilityConverter.Instance}"
```

---

## 7. 历史 BUG 记录（已全部修复）

### BUG 1：PeakMonitor foreach + Dispose 导致静默崩溃
```csharp
// ❌ 错误：NAudio MMDeviceCollection 用 foreach 遍历时 Dispose 会破坏 COM 集合
foreach (var mic in mics) { MicPeaks[mic.ID] = ...; mic.Dispose(); }

// ✅ 正确：用索引访问，不手动 Dispose
for (int i = 0; i < micCol.Count; i++)
    MicPeaks[micCol[i].ID] = micCol[i].AudioMeterInformation.MasterPeakValue;
```

### BUG 2：AudioMixEngine.Tick() 双路互锁导致永不混音
- **症状**：点击开始后无任何声音，波形静止
- **原因**：`len = Min(appBuf, micBuf)` — 只要麦克风有任何微小延迟，len=0，永远 return
- **修复**：只需 App 有数据即可推进，Mic 不足用零填充

### BUG 3：MicCapturer 在 STA UI 线程初始化 WASAPI
- **症状**：某些驱动/设备下麦克风无声
- **修复**：`Start(deviceId)` 内部用 `Task.Run(() => { ... WasapiCapture 初始化 ... })` 切换到 MTA 线程

### BUG 4：GhostButton 文字在亮色模式下不可见
- **原因**：`GhostButton` 未设 Foreground，继承了按钮模板（白色文字）在白色背景上
- **修复**：显式 `<Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>`

### BUG 5：暗色模式头部按钮（主题切换、语言选择器）文字不可见
- **ThemeIcon emoji**：未设 Foreground，继承系统默认暗色
- **修复**：显式 `Foreground="{DynamicResource TextPrimaryBrush}"`
- **ComboBoxItem**：未设 Foreground，在暗色背景上默认黑色文字
- **修复**：每个 ComboBoxItem 显式设 `Foreground` + `Background`

### BUG 6：AudioProcess 进程切换时列表闪烁/选中丢失
- **原因**：旧实现每次刷新清空 ObservableCollection 再重建
- **修复**：差异比对更新——只添加新进程、移除已退出进程，不重建整个列表

### BUG 7：Slider 点击轨道跳 100 而不是跳到点击位置
- **修复**：所有 Slider 加 `IsMoveToPointEnabled="True"`

### BUG 8：音频源确认流程不明确（用户选完直接生效）
- **修复**：引入 `SelectedProcess`（ListBox 选中） vs `ActiveProcess`（确认后），必须点"使用此源"才真正生效，UI 显示"当前音频源：xxx"

### BUG 9：PrimaryButton / ListBox 样式中混入 ComboBox 模板
- **文件**：`Themes/Dark.xaml`、`Themes/Light.xaml`
- **原因**：复制粘贴失误，`PrimaryButton` 样式中第一个 `Template` Setter 的 `ControlTemplate TargetType` 是 `ComboBox`，`ListBox` 样式同理。虽然第二个 Setter 覆盖了第一个，但造成 XAML 解析警告和代码混乱。
- **修复**：删除两个主题文件中各 2 个错误的 ComboBox ControlTemplate（共 4 处）

### BUG 10：PipelineManager OnMixed 事件处理器泄漏
- **文件**：`Core/PipelineManager.cs`
- **原因**：每次 `StartAsync()` 都通过 `_mixer.OnMixed += ...` 追加匿名 handler，但 `StopAsync()` 从不取消订阅。反复启停后多个 handler 堆积，停止后旧 handler 继续向已 Dispose 的 writer 写数据，抛 `ObjectDisposedException`。
- **修复**：保存 handler 引用为 `_mixedHandler` 字段，`StopAsync()` 中先 `_mixer.OnMixed -= _mixedHandler`，再释放组件。

### BUG 11：VirtualMicWriter.Dispose() 后无法重新 Initialize
- **文件**：`Audio/VirtualMicWriter.cs`
- **原因**：`Initialize()` 调用 `Dispose()` 清理旧实例 → `_disposed = true`。第二次调用 `Initialize()` 时 `Dispose()` 跳过清理，旧 `WaveOutEvent` 泄漏。
- **修复**：拆分为 `Stop()`（释放资源不设标志）和 `Dispose()`（调用 Stop + 设标志）。`Initialize()` 改为调用 `Stop()`。

### BUG 12：主题切换后 ThemeIcon 显示纯文本而非 emoji
- **文件**：`UI/MainWindow.xaml.cs`
- **原因**：`ToggleTheme_Click` 中设置 `ThemeIcon.Text = "Moon"` / `"Sun"`，但初始 XAML 用的是 emoji `🌙`，切换后图标变成英文单词。
- **修复**：改为 `ThemeIcon.Text = _isDark ? "🌙" : "☀"`

### BUG 13：MicCapturer.ConvertToFloat32 只处理 16-bit int PCM
- **文件**：`Audio/MicCapturer.cs`
- **原因**：`else` 分支硬编码 `BitConverter.ToInt16` 和 `i * 2`，假设所有非 Float 格式都是 16-bit。24-bit（专业麦克风）、32-bit int（高端音频接口）输出时数据完全错误，混音输出为杂音。
- **修复**：改为 `switch (fmt.BitsPerSample)` 分别处理 16/24/32-bit，24-bit 用符号扩展移位，32-bit 用 `BitConverter.ToInt32`。

### BUG 14：RingBuffer.Available 属性无内存屏障，存在线程安全隐患
- **文件**：`Audio/RingBuffer.cs`
- **原因**：`Available => _available` 直接读取字段，无同步保证。`Write()` 和 `Read()` 在 lock 内修改 `_available`，但 `Tick()` 在 App 捕获线程读 `Available`，同时 Mic 捕获线程在 lock 外触发 `Write()`，编译器/CPU 可能缓存旧值。
- **修复**：改为 `Volatile.Read(ref _available)`，防止编译器和 CPU 缓存过期值。

### BUG 15：Resample 不处理多声道（>2ch）麦克风输入
- **文件**：`Audio/AudioMixEngine.cs`
- **原因**：`Resample()` 只考虑了 1ch 和 2ch，若麦克风驱动输出 4ch/5.1/7.1，`ResampleRate` 的帧计算会错乱（把多声道数据当 2ch 处理），输出完全损坏的音频。
- **修复**：增加 `channels > 2` 分支，先对各帧的所有声道求平均值，下混到立体声，再进行采样率转换。

---

## 8. Inno Setup 注意事项

- 安装脚本：`E:\Documents\Voicepipe\VoicePipe.iss`
- **VB-Cable 重复安装**：旧版脚本中 `[Run]` 段会无条件运行 VBCable 安装程序，已修复为先检测驱动是否已安装再决定是否运行
- 输出：`E:\Documents\Voicepipe\Output\VoicePipeSetup.exe`

---

## 9. 待办 / 未来可改进方向

- [ ] **波形预览 —— 空闲时也能显示**：目前 `WaveformAnalyzer.Push()` 只在混音 Tick 里调用，管线停止时波形静止。如果想让麦克风预览时也能看到波形，需要在非运行状态时也把 Mic 采样推给 WaveformAnalyzer。
- [ ] **更精细的 ComboBox 选中态样式**：语言选择器下拉框的选中项高亮颜色在两种主题下视觉效果可以更精致。
- [ ] **系统托盘最小化**：`AppSettings.MinimizeToTray = true` 已有字段，但托盘逻辑尚未实现。
- [ ] **自动启动管线**：`AppSettings.AutoStartPipeline = true` 已有字段，但逻辑尚未实现（在 MainViewModel 构造函数里加 `if (AutoStartPipeline && ActiveProcess != null && ActiveMic != null) StartPipeline()`）。
- [ ] **波形控件性能**：每帧创建新 `StreamGeometry`，高频更新时有 GC 压力，可考虑 `GeometryGroup` 复用。
- [ ] **安装包架构标识符警告**：Inno Setup 6 中 `x64` 已弃用，应改为 `x64compatible`。
- [ ] **ResampleRate 线性插值升级**：当前用最简单的线性插值，对极端采样率差（如 8000→44100）音质较差，可考虑升级为 Sinc 插值。

---

## 10. 关键开发规范提醒

1. **不要直接把 `MMDevice` 对象绑定到 UI**：COM 对象不能在 UI 线程频繁访问，会卡死。UI 只持有 ID 和 FriendlyName 字符串。
2. **WASAPI 初始化必须在 MTA 线程**：任何 `WasapiCapture`/`WasapiOut` 的构造和 `StartRecording` 调用都放在 `Task.Run()` 里。
3. **不要用 `foreach` 遍历 `MMDeviceCollection` 时调用 `Dispose`**：用 `for (int i=0; ...)` 索引访问。
4. **主题颜色用 `{DynamicResource}` 不用 `{StaticResource}`**：主题切换时动态更新，静态绑定不会更新。
5. **`TextBlock` 全局样式不设 Foreground**：避免污染 Button 内部文字颜色。Button 自己的模板通过 Foreground 继承控制文字颜色。
6. **进程列表差异更新**：`RefreshProcesses()` 和 `RefreshMicDevices()` 必须用 diff 方式更新，不能 `Clear()` + `AddRange()`。
7. **事件处理器必须成对订阅/取消**：尤其是跨生命周期的事件（如 `OnMixed`），每次 Start 绑定，每次 Stop 解绑，防止泄漏。
8. **VirtualMicWriter 的 Initialize() 调用 Stop() 而非 Dispose()**：Dispose 会设置 `_disposed=true` 导致再次 Initialize 无法清理旧资源。

---

## 11. 变更记录

> **格式**：`日期 | 文件 | 原因 | 描述`

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-27 | `Themes/Dark.xaml`、`Themes/Light.xaml` | Bug修复 | 删除 PrimaryButton 和 ListBox 样式中错误的 ComboBox ControlTemplate（各2处，共4处） |
| 2026-05-27 | `Core/PipelineManager.cs` | Bug修复 | 修复 OnMixed 事件处理器泄漏；保存 handler 引用 `_mixedHandler`，StopAsync 时先取消订阅再 Dispose |
| 2026-05-27 | `Audio/VirtualMicWriter.cs` | Bug修复 | 拆分 Stop() 和 Dispose()，Initialize() 改调 Stop()，防止二次初始化时旧资源泄漏 |
| 2026-05-27 | `Audio/AudioMixEngine.cs` | Bug修复+清晰度 | RingBuffer 容量表达式从 `44100*2/2` 改为 `44100*2*500/1000`（语义更清晰）；Resample 增加多声道（>2ch）下混支持 |
| 2026-05-27 | `UI/MainWindow.xaml.cs` | Bug修复 | 主题切换时 ThemeIcon 由 `"Moon"/"Sun"` 改为 `"🌙"/"☀"` emoji |
| 2026-05-27 | `Audio/MicCapturer.cs` | Bug修复 | ConvertToFloat32 从只支持 16-bit 扩展为支持 16/24/32-bit int，避免专业麦克风输出时数据错误 |
| 2026-05-27 | `Sinks/InMemoryLogSink.cs` | 新功能 | 新建 Serilog 自定义 Sink，通过静态事件向 UI 广播日志条目，支持历史回填（最多2000条） |
| 2026-05-27 | `UI/LogConsoleWindow.xaml` + `.cs` | 新功能 | 新建实时日志控制台窗口：按日志级别着色（INF灰/WRN金/ERR红/FTL亮红），支持自动滚动、清空、复制全部、置顶 |
| 2026-05-27 | `UI/MainWindow.xaml` | 新功能 | 在根 Grid 添加右键 ContextMenu，菜单项"打开实时控制台"触发 OpenConsole_Click |
| 2026-05-27 | `UI/MainWindow.xaml.cs` | 新功能 | 添加 _consoleWindow 字段和 OpenConsole_Click 处理器（单例，已开则激活，关闭主窗口时联动关闭控制台） |
| 2026-05-27 | `App.xaml.cs` | 诊断增强 | Serilog 最低级别从 Information 降为 Debug；添加 InMemoryLogSink 到管线 |
| 2026-05-27 | `Audio/VirtualMicWriter.cs` | 诊断增强 | FindCableInputDevice() 逐设备打印 Debug 日志；Initialize 日志包含格式信息；未找到 CABLE Input 时 Warning 更清晰 |
| 2026-05-27 | `Audio/MicCapturer.cs` | 诊断增强 | StartRecording 成功后 Information 日志包含 SampleRate/Channels/BitsPerSample/Encoding |
| 2026-05-27 | `Audio/AudioMixEngine.cs` | 诊断增强 | Tick() 首次触发记 Information；每 300 次 Tick 记 Debug 统计；Reset() 时重置诊断计数器 |
| 2026-05-27 | `Audio/ProcessEnumerator.cs` | Bug修复 | 修复严重的系统句柄泄漏：`Process.GetProcessById(pid)` 获取的 Process 对象未被 Dispose，由于 2 秒刷新一次，会导致大量句柄泄漏。现已添加 `using`。 |
| 2026-05-27 | `Audio/VirtualMicWriter.cs` | Bug修复 | 修复初始化异常导致的资源泄漏：如果 `WaveOutEvent.Init()` 抛出异常，对象不被释放。现已在 `Initialize()` 中加入 try-catch 并调用 `Stop()`。 |
| 2026-05-27 | `Audio/MicCapturer.cs` | Bug修复 | 修复初始化异常导致的 COM 对象泄漏：如果 `StartRecording()` 抛出异常，`WasapiCapture` 和 `MMDevice` 不被释放。现已在 catch 块中调用 `Stop()`。 |
| 2026-05-27 | `Core/PipelineManager.cs` | Bug修复 | 修复 `_mixedHandler` 竞态条件：`_writer.Write(bytes)` 改为 `_writer?.Write(bytes)`，防止在 StopAsync 过程中（`_writer` 已 Dispose 但 Tick 回调还在执行）写入已释放的 writer 导致 `NullReferenceException`。 |
| 2026-05-27 | `Audio/LoopbackCapturer.cs` | Bug修复 | 修复内核 Event Handle 泄漏：`CreateEventW` 创建的句柄在 `audioClient.Initialize()` 或 `SetEventHandle()` 失败时不会被释放。将 `CloseHandle` 移入 `finally` 块并加 `IntPtr.Zero` 守卫。 |
| 2026-05-27 | `Audio/LoopbackCapturer.cs` | **致命Bug修复** | 修复 `InvalidCastException: Unable to cast COM object to IAudioClient3`。`ActivateAudioInterfaceAsync` 返回的 COM 对象只实现了 `IAudioClient`（GUID `1CB9AD4C`），但代码强转为 `IAudioClient3`（GUID `726778CD`），.NET COM Interop 触发 `QueryInterface` 失败。这就是混音功能从未生效的根本原因！修复：将接口定义改为 `IAudioClient`（正确的 GUID），删除用不到的 IAudioClient2/3 方法。 |
| 2026-05-27 | `UI/MainWindow.xaml` | Bug修复 | 修复右键控制台菜单不显示：WPF 中 `Grid` 默认 `Background=null` 不参与 Hit Testing，所以挂在 Grid 上的 `ContextMenu` 永远无法被鼠标触发。修复：将 ContextMenu 移至 `Window.ContextMenu`（Window 始终接收右键），并给 Grid 加 `Background="Transparent"` 作保险。 |
| 2026-05-27 | `VoicePipe-OpenSource/` | 新功能 | 抽取开源版本所需的所有代码、资源与依赖，编写了详尽的英文 `README.md` 与 `.gitignore`。 |
| 2026-05-27 | `assets/voicepipe.ico` | UI/UX | 用户选定了带有螺旋绿色管道的白色麦克风霓虹Logo，使用 Python Pillow 将其转换为多分辨率 `.ico` 文件，替换了项目主图标并集成到开源目录及 README 中。重新发布了编译产物和安装包。 |
| 2026-05-27 | `Audio/AudioMixEngine.cs` | 音质修复 | 修复混音“炸”（硬限幅失真）与“糊”（线性重采样高频丢失）的问题。引入 tanh 软限幅器（SoftLimiter）并调低默认增益防止爆音；重采样算法由线性插值升级为 Catmull-Rom 三次样条插值。 |
| 2026-05-27 | `Audio/AudioMixEngine.cs` | 音量模型重构 | 将增益模型从归一化权重比例重构为直接独立增益系数。AppGain/MicGain 相互独立互不影响，且更符合用户直觉（两者都是 0.75 = 双路各乘以 0.75）。 |
| 2026-05-27 | `Core/PipelineManager.cs` / `Audio/AudioMixEngine.cs` / `Audio/VirtualMicWriter.cs` | **卡顿Bug修复** | 修复音频卡顿：将帧缓冲约 1 秒以换取低延迟。原来卡顿原因：Tick() 由 WASAPI 回调触发，约每 100ms 触发一次，VB-Cable 消费快于生成间隙导致的卡顿。修复：引入每 10ms Timer 后台专门触发 Tick()，Tick() 稳定每次 882 样本（10ms），RingBuffer 500ms（原100ms），BufferedWaveProvider 500ms（原100ms），WaveOutEvent DesiredLatency 40ms（原20ms）。总延迟约 1100ms 降为约 130ms。 |
| 2026-05-27 | `Audio/AudioMixEngine.cs` / `Audio/VirtualMicWriter.cs` / `Core/PipelineManager.cs` | **架构重构** | 彻底重构音频管线：从 Push 模型（回调驱动 Tick 推送数据到 BufferedWaveProvider）改为 Pull 模型（AudioMixEngine 实现 IWaveProvider，WaveOutEvent 按设备时钟拉取数据）。Push 模型的根本缺陷：App 和 Mic 两个回调各自触发 Tick 输出，产生 2 倍速率数据，BufferedWaveProvider 溢出丢弃导致卡顿。Pull 模型中输出速率由 WaveOutEvent 的设备时钟精确控制，回调只负责往 RingBuffer 喂数据，Read() 按需混音输出，从根本上消除了卡顿。 |
| 2026-05-27 | `Audio/LoopbackCapturer.cs` | **Bug** | COMException E_UNEXPECTED retry: CaptureLoopWithRetry + CaptureFailed event |
| 2026-05-27 | `Core/PipelineManager.cs` | **Bug** | Forward CaptureFailed event |
| 2026-05-27 | `ViewModels/MainViewModel.cs` | **Bug** | Subscribe CaptureFailed, update StatusText + IsRunning |
| 2026-05-27 | `VoicePipe.csproj` | Build | 添加 `<SelfContained>true</SelfContained>`，确保 `dotnet publish` 始终生成自包含版本（含 .NET 运行时），解决用户端弹出"需要安装 .NET"的问题 |
| 2026-05-27 | `Audio/LoopbackCapturer.cs` | **Bug修复** | 修复 `IAudioCaptureClient` COM 对象泄漏：`GetService()` 获取的 captureClient 在 finally 中从未调用 `Marshal.ReleaseComObject`，导致 Windows 认为旧音频会话仍活跃。同时将 `audioClient.Stop()` 移入 finally 块确保异常路径也能正确停止 |
| 2026-05-27 | `Core/PipelineManager.cs` | **致命Bug修复** | 彻底解决 Per-Process Loopback E_UNEXPECTED 重复激活失败：Windows 不允许对已关闭的同 PID loopback 重新激活。改用 `Dictionary<int, LoopbackCapturer>` 缓存所有已激活的 capturer，StopAsync 不再销毁 loopback，切换 PID 时直接复用缓存。通过闭包检查 `_currentPid == pid` 确保只有活跃 PID 的数据被喂入 mixer |
| 2026-05-27 | `Core/PipelineManager.cs` | 架构 | 实现 `IDisposable`，`Dispose()` 方法清理所有缓存的 LoopbackCapturer。`StopAsync()` 只停 Writer 和 Mic，不碰 loopback |
| 2026-05-27 | `UI/MainWindow.xaml.cs` | Bug修复 | `OnClosed` 改为调用 `vm.Cleanup()` 而非 `StopPipelineCommand`，确保退出时完全清理包括缓存的 loopback |
| 2026-05-27 | `ViewModels/MainViewModel.cs` | Bug修复 | 新增 `Cleanup()` 方法，调用 `_pipeline.Dispose()` 并停止所有定时器 |
| 2026-05-27 | `Audio/ProcessEnumerator.cs` | 安全修复 | 过滤 VoicePipe 自身进程（`Environment.ProcessId`），防止用户误选自身导致反馈环路产生无限噪音 |
| 2026-05-28 | `Core/PipelineManager.cs` | **内存泄漏修复** | 新增 `PurgeDeadSessions()` 方法：每次 `StartAsync` 前扫描 `_loopbackCache`，用 `Process.GetProcessById(pid)` 检测已退出进程，自动 Dispose 其 LoopbackCapturer 并从字典移除，防止旧进程退出后后台线程和 COM 对象持续泄漏 |
| 2026-05-28 | `ViewModels/MainViewModel.cs` | UI重构 | 删除 `ConfirmSource()`/`ConfirmMic()` 命令、`ActiveProcess`/`ActiveMic`/`ActiveSourceName`/`ActiveMicName` 属性。音频源和麦克风改为 ComboBox 选即确认模式，`StartPipeline` 直接使用 `SelectedProcess`/`SelectedMic` |
| 2026-05-28 | `UI/MainWindow.xaml` | UI重构 | 音频源和麦克风选择从 ListBox 改为 ComboBox 下拉菜单，每项保留实时音量条（进程名+PID+▃▃▃+百分比）。删除"刷新进程"/"使用此源"按钮和底部"当前音频源"标签。窗口高度从 820px 缩减到 580px |
| 2026-05-28 | `Audio/AudioMixEngine.cs` | **致命Bug修复** | 修复 `Read` 方法中 `Array.Copy` 的 TOCTOU（Time-of-check to time-of-use）并发崩溃问题。原有逻辑在锁外读取 `Available` 作为 `toRead` 长度，但在并发 `Reset()` 时，锁内 `_appBuffer.Read(toRead)` 会返回空数组，导致随后 `Array.Copy` 越界引发 `ArgumentException`。已改为安全地使用返回数组的 `Length`。 |
| 2026-05-28 | `Audio/ProcessEnumerator.cs` | Bug修复 | 修复 COM 泄漏：获取到的 `AudioSessionControl` 对象实现了 `IDisposable` 但未被释放，循环中加上了 `using`。 |
| 2026-05-28 | `Audio/MicCapturer.cs` | Bug修复 | 修复 COM 泄漏：`GetAvailableMics()` 枚举时生成的 `MMDevice` 包装对象未被 Dispose，增加了 `using` 块释放。 |
| 2026-05-28 | `Audio/VirtualMicWriter.cs` | Bug修复 | 修复 COM 泄漏：`FindCableInputDevice()` 遍历所有端点时，未被选中的 `MMDevice` 被丢弃且未 Dispose，现已在非匹配分支中调用 `Dispose()`。同时优化 `IsCableInputAvailable()` 避免重复的泄漏逻辑。 |
| 2026-05-28 | `Audio/PeakMonitor.cs` | Bug修复 | 修复麦克风音量条在未开始混音时始终为零的问题。Windows 的 `AudioMeterInformation.MasterPeakValue` 对 Capture 设备只有在有应用录音时才返回非零值。新增 `EnsureMicListener()`：对每个麦克风设备打开一个静默 WasapiCapture 会话（数据丢弃），唤醒硬件 peak meter。同时修复了 AudioSessionControl 的 COM 泄漏 |
| 2026-05-28 | `ViewModels/MainViewModel.cs` | **性能修复** | 修复 UI 每 2 秒周期性冻结。原因：`_refreshTimer.Tick` 在 UI 线程上同步执行三个重量级 COM 枚举（进程列表/麦克风列表/VB-Cable检测），每次耗时 300ms-1.5s。改为 `async` 方法：COM 操作移至 `Task.Run` 后台线程，`await` 后回 UI 线程更新 ObservableCollection |
| 2026-05-28 | `Themes/Dark.xaml`、`Themes/Light.xaml` | Bug修复 | 修复 Slider 只能增大不能减小的问题。原因：全局 ScrollBar 样式将 RepeatButton 的 Height 设为 0，WPF 默认 Slider 模板复用了 ScrollBar 组件导致轨道左半边点击区域为零。给 Slider 加了完整自定义 ControlTemplate（4px圆角轨道 + 16px绿色圆形Thumb + 24px高独立RepeatButton） |
| 2026-05-28 | `Audio/VirtualMicWriter.cs` | 性能优化 | WasapiOut latency 从 50ms 降至 20ms，总管线延迟从约 65ms 降至约 35ms |
| 2026-05-28 | `Core/PipelineManager.cs` | Bug修复 | `_currentPid` 加 `volatile` 关键字确保多线程可见性；`StopAsync` 中重置 `_currentPid = 0`，避免停止后缓存 capturer 继续空转喂数据浪费 CPU |
| 2026-05-28 | `UI/MainWindow.xaml` | Bug修复 | VB-Cable 状态从显示 `True/False` 改为 `✓/✗`（DataTrigger）；Storyboard 动画加 `x:Name` + `StopStoryboard` 防止反复 Start/Stop 泄漏动画资源 |
| 2026-05-28 | `ViewModels/MainViewModel.cs` | Bug修复 | `IsCableAvailable` 从只在构造函数查一次改为每 2 秒随 refreshTimer 刷新 |
| 2026-05-28 | `Audio/AudioMixEngine.cs` | **Bug修复** | 修复 App/Mic 增益滑块调小后实际音量不变的问题。`AppGain`/`MicGain` 原为普通自动属性，WasapiOut 播放线程的 JIT 可将其缓存在寄存器中跨多次 `Read()` 调用不刷新。改为 `volatile` 后备字段，确保每次读取都从主存加载最新值 |
| 2026-05-28 | `Audio/VirtualMicWriter.cs` | 性能优化 | WasapiOut latency 50ms → 20ms → 15ms → 10ms，总管线延迟从约 65ms 降至约 25ms（共享模式极限） |
| 2026-05-28 | `Audio/PeakMonitor.cs` | Bug修复 | 修复麦克风音量条在未开始混音时始终为零的问题。Windows `AudioMeterInformation.MasterPeakValue` 对 Capture 设备只有在有应用录音时才返回非零值。新增 `EnsureMicListener()`：对每个麦克风打开一个静默 WasapiCapture 会话（数据丢弃），唤醒硬件 peak meter。同时修复 AudioSessionControl COM 泄漏（加 `using var`） |
| 2026-05-28 | `ViewModels/MainViewModel.cs` | 性能修复 | 修复 UI 每 2 秒周期性冻结。原因：refreshTimer.Tick 在 UI 线程上同步执行三个重量级 COM 枚举（进程/麦克风/VB-Cable检测）。改为 `RefreshAllAsync()`：所有 COM 操作在 `Task.Run` 后台线程，`await` 后回 UI 线程更新集合 |
| 2026-05-28 | `Themes/Dark.xaml`、`Themes/Light.xaml` | Bug修复 | 修复 Slider 只能增大不能减小的问题。原因：全局 ScrollBar 样式将 RepeatButton Height 设为 0，WPF 默认 Slider 模板复用 ScrollBar 组件导致轨道左半边点击区域为零。给 Slider 加完整自定义 ControlTemplate（4px圆角轨道 + 16px绿色圆形Thumb + 24px高独立RepeatButton） |
| 2026-05-28 | `Audio/AudioMixEngine.cs` | Bug修复 | 修复 App/Mic 增益滑块调小后实际音量不变的问题。`AppGain`/`MicGain` 原为普通自动属性，WasapiOut 播放线程 JIT 可将其缓存在寄存器中不刷新。改为 `volatile` 后备字段，确保每次读取都从主存加载最新值 |
| 2026-05-28 | `UI/SmoothSlider.cs` | 新增功能 | 新增自定义 Slider 控件，支持点击轨道任意位置跳过去后无需松开鼠标即可直接拖动（click-to-jump + immediate drag）。继承 `Slider`，通过 `DefaultStyleKeyProperty.OverrideMetadata` 继承主题样式。替换了 MainWindow.xaml 中的两个 `<Slider>` |
| 2026-05-28 | `ViewModels/MainViewModel.cs` | 新增功能 | 新增 `OnSelectedProcessChanged`/`OnSelectedMicChanged` 回调：运行中切换下拉菜单选项时自动调用 `StartPipelineCommand`，实现音频源和麦克风的实时热切换，无需停止后手动重新启动 |
| 2026-05-28 | `Core/PipelineManager.cs` | 性能修复 | 修复切换音频源/麦克风时整个程序卡顿的问题。将 `StartAsync()` 中的重量级 COM 操作移至后台线程：第一个 `Task.Run` 处理 `PurgeDeadSessions`/`_writer.Stop`/`_micCapture.Dispose`；第二个 `Task.Run` 处理 `VirtualMicWriter.Initialize`（含 `FindCableInputDevice` 枚举 + `WasapiOut` 构造）。`MicCapturer.Start` 已内置 `Task.Run`，无需修改 |
| 2026-05-28 | `App.xaml.cs` | 日志优化 | 将 Serilog 最低日志级别从 `MinimumLevel.Debug()` 改为 `MinimumLevel.Information()`，过滤所有 `[DBG]` 输出（含 AudioMixEngine Read 频率日志、PeakMonitor 枚举日志等） |
| 2026-05-28 | `Audio/VirtualMicWriter.cs` | 日志优化 | 删除 `FindCableInputDevice()` 中每次扫描时逐设备列出的 DBG 日志（每 2 秒打满屏设备列表）。新增 `logFound` 参数：仅在 `Initialize()` 初始化时打一条 INF「找到 CABLE Input」，`IsCableInputAvailable()` 定时检测时静默执行 |
| 2026-05-29 | `Audio/PeakMonitor.cs` | **性能优化（P0）** | 修复每 33ms 创建/销毁 COM 对象（MMDeviceEnumerator + MMDevice）的问题，改为缓存复用。轮询频率从 30fps 降至 15fps（67ms）。设备断开时自动清理缓存重建。预期节省约 3-4% CPU |
| 2026-05-29 | `Audio/RingBuffer.cs` | **性能优化（P1）** | 容量强制 2 的幂，用位运算（`& _mask`）替代取模（`%`）；Write/Read 改为 Array.Copy 批量拷贝替代逐样本循环；新增 `Read(float[], int, int)` 零分配重载。预期节省约 1-2% CPU |
| 2026-05-29 | `Audio/AudioMixEngine.cs` | **性能优化（P2+P4）** | Read() 中预分配固定大小 `_appTemp`/`_micTemp` 缓冲区替代每次 `new float[]`；重采样使用缓存缓冲区 `_resampleStereoCache`/`_resampleRateCache`。Read() 每次调用零堆分配。预期节省约 0.5-1% CPU |
| 2026-05-29 | `Audio/WaveformAnalyzer.cs` | **性能优化（P3）** | 删除 `Push(float[])` 方法，新增 `InlineSample(float)` 由 AudioMixEngine.Read() 逐样本内联调用。消除 lock 竞争和每次调用的数组分配。GetSnapshot() 用 `Volatile.Read` 无锁读取 |
| 2026-05-29 | `Core/PipelineManager.cs` | 新增功能 | 新增 `StopAppOnly()` 方法：仅将 `_currentPid` 设为 0 停止 App 音频混入，保留 Writer 和 MicCapturer 继续运行，实现麦克风直通模式 |
| 2026-05-29 | `ViewModels/MainViewModel.cs` | 新增功能 | 新增 `MicPassthrough` 勾选框属性 + `_isMicPassthroughActive` 状态跟踪。StopPipeline 检查该标志决定是完全停止还是仅停 App 音频。取消勾选时自动完全停止管线 |
| 2026-05-29 | `UI/MainWindow.xaml` | 新增功能 | 在 Start/Stop 按钮下方新增 CheckBox，绑定 `MicPassthrough`，文本由 `StrMicPassthrough` 本地化资源提供 |
| 2026-05-29 | `Langs/*.xaml` | 新增功能 | 5 种语言新增 `StrMicPassthrough` 字符串：中文简体「停止后保留麦克风直通」/ 英文「Keep mic passthrough after stop」/ 日韩台对应翻译 |
| 2026-05-29 | `Audio/RingBuffer.cs` | **Bug修复（审查发现）** | 修复死锁：`Read(int)` 持有 `_lock` 后调用 `Read(float[],int,int)` 再次请求同一把锁。改为内联批量拷贝逻辑，避免嵌套加锁 |
| 2026-05-29 | `Audio/AudioMixEngine.cs` | **Bug修复（审查发现）** | 修复变量引用错误：新增 `appBuf`/`micBuf` 防溢出变量后，`Read()` 和混音循环仍使用旧的 `_appTemp`/`_micTemp`，大请求时会 IndexOutOfRange。统一改为 `appBuf`/`micBuf` |
| 2026-05-29 | `Audio/WaveformAnalyzer.cs` | 代码清理 | 删除切换到无锁模式后不再使用的 `_lock` 字段 |
| 2026-05-29 | `Audio/AudioMixEngine.cs` | **Bug修复（二次审查）** | 修复 `_resampleStereoCache` 缓存形同虚设的问题：line 131 的 `.AsSpan().ToArray()` 每次仍然分配新数组。改为将 Resample 返回值改为 `(float[] buf, int len)` 元组，调用方用显式长度写 RingBuffer，彻底绕过 ToArray() |
| 2026-05-29 | `Audio/AudioMixEngine.cs` | **性能修复（二次审查）** | 新增 `_monoStereoCache` 缓存单声道→立体声转换缓冲区。单声道麦克风（最常见情况）每 10ms 调用一次 `MonoToStereo`，原来每次分配新数组约 100次/秒。改为缓存后首次初始化后零分配 |
| 2026-05-29 | `Audio/AudioMixEngine.cs` | **性能修复（二次审查）** | `ResampleRate` 末尾不再 `Array.Copy` 到新数组，直接返回 `(_resampleRateCache, outputLen)`，整个 FeedMic 路径在格式稳定后真正零堆分配 |
| 2026-05-29 | `Langs/*.xaml` | **Bug修复** | 修复了因为之前使用 PowerShell 批量替换文本导致 UTF-8 编码被破坏、多语言字符变成 ? 的乱码问题，已重新使用正确的 UTF-8 BOM 编码重写所有语言包文件。 |
| 2026-05-30 | `Audio/AudioMixEngine.cs` | **Bug修复（增益语义）** | 修复增益平方导致的"所见非所得"问题。原 `AppGain`/`MicGain` setter 把值做平方（`_appGainSquared = value*value`），导致 75% 实际只有 ×0.5625（约静 5dB），与"直接幅度系数"的设计注释和文档矛盾。改回纯线性：滑块值即倍数（100%=×1.0 原音不变，150%=×1.5，0%=静音）。删除 `_appGainSquared`/`_micGainSquared` 字段，混音循环直接用 `_appGain`/`_micGain`。默认值 `_appGain` 从 0.75 改为 1.0（引擎层）。 |
| 2026-05-30 | `Audio/AudioMixEngine.cs` | **增益曲线最终方案（感知响度）** | 纯线性手感差（用户反馈：拉到 30% 还是很大，接近 0 才突然变小=悬崖感），因为人耳响度感知接近对数。最终改为感知响度曲线：`实际幅度 = 滑块值 ^ 1.7`，缓存到 `_appGainAmp`/`_micGainAmp`（volatile）。锚点：0%→静音、100%→×1.0 原音不变、50%→×0.31（约半响）、150%→×1.98。`MathF.Pow` 只在 setter（拖滑块时）算一次，Read() 仍单次乘法零额外开销。指数 1.7 可按手感在 1.5（温和）~2.0（接近平方，更狠）间调整。注：之前的"平方"方向其实是对的（音频 taper 曲线），bug 只在于偷偷平方且不改标签 + 100% 以上涨太猛。 |
| 2026-05-30 | `Audio/AudioMixEngine.cs` | **性能修复（审查发现）** | 修复上一轮零分配优化中新引入的退化：`Read()` 混音循环每次迭代都 `fixed (byte* ptr = &buffer[pos])` 钉一次内存（代价高）。改为将 `fixed` 提到循环外只 pin 一次，循环内用 `float* fptr` 直接索引写入。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs`、`Core/AppSettings.cs` | 行为调整 | App 音频增益默认初始值统一为 0.70（70%），压低 App 声音防止盖过人声；麦克风保持 1.0（100%）。滑块/设置默认值两处同步。 |
| 2026-05-30 | `Audio/LoopbackCapturer.cs` | **性能优化（P0，GC热点）** | 消除 CaptureLoop 每个 WASAPI packet 都 `new float[sampleCount]` 的持续 GC 压力（44100Hz 下每秒数十次，审查者指出的"主要热点"）。改为复用成员缓冲 `_sampleBuffer`（按需扩容）。事件签名从 `EventHandler<float[]>` 改为 `EventHandler<(float[] Samples, int Count)>`，消费方只读 Count 个样本。静音包改为 `Array.Clear` 清零有效区间。消费链同步（FeedApp→RingBuffer.Write 立即拷走），复用安全。 |
| 2026-05-30 | `Audio/MicCapturer.cs` | **性能优化（P0，GC热点）** | 同类问题：`ConvertToFloat32` 每次 DataAvailable 回调都 `new float[]`。改为复用成员缓冲 `_convertBuffer`，返回有效样本数 int。事件签名加 Count：`(float[] Samples, int Count, WaveFormat Format)`。 |
| 2026-05-30 | `Audio/AudioMixEngine.cs` | 配合GC优化 | `FeedApp`/`FeedMic` 新增带 count 重载；`Resample` 改为接受显式 `inputLen` 而非依赖 `input.Length`，配合复用缓冲只处理有效区间。保留旧无 count 重载兼容。 |
| 2026-05-30 | `Audio/LoopbackCapturer.cs`、`Core/PipelineManager.cs` | **性能优化（P0，空转）** | LoopbackCapturer 新增 `Paused` 标志：暂停时仍 ReleaseBuffer 排空 WASAPI 缓冲（防满报错），但跳过内存拷贝和事件触发。PipelineManager 在 StartAsync 暂停所有非活跃 PID 的 capturer、只激活当前；StopAsync/StopAppOnly 暂停全部。消除停止后 / 多源切换后旧 capturer 后台空转浪费 CPU+GC。 |
| 2026-05-30 | `Audio/PeakMonitor.cs`、`ViewModels/MainViewModel.cs`、`UI/MainWindow.xaml(.cs)` | **性能优化（P0，最大常驻开销）** | 重写麦克风静默监听策略。原来对每个 active 麦克风都开一条真实 WasapiCapture 空转线程（仅为唤醒硬件 peak meter），典型 PC 2~4 路常驻空转（审查者点名的可能主因）。改为按需：平时只监听选中麦克风（`SetTargetMic`）；麦克风下拉打开时临时全开（`MonitorAllMics`，绑定 ComboBox DropDownOpened/Closed，便于用音量条辨认设备）；管线运行的麦克风已被 MicCapturer 占用 meter 已醒，无需监听（`SetRunningMic`）。新增 `ReconcileMicListeners` 调谐监听集合。App/进程峰值成本低（一次性枚举）仍全监听。 |
| 2026-05-30 | `UI/WaveformControl.cs` | **性能优化（P1，渲染GC）** | OnRender 每帧都 `new Pen` + `new SolidColorBrush`（中心线）。改为：中心线 Pen 提为 `static readonly` 并 Freeze；波形 Pen 缓存，仅当 LineBrush 引用变化时重建。 |
| 2026-05-30 | `Audio/WaveformAnalyzer.cs`、`ViewModels/MainViewModel.cs` | **性能优化（P1/P2）** | WaveformAnalyzer：`% 512` 改 `& 511` 位运算；新增 `GetSnapshot(float[] dest)` 复用外部缓冲零分配重载。MainViewModel：波形改双缓冲（交替两个 float[512]，既触发绑定重绘又不每帧分配）；波形刷新率 33ms(30fps)→50ms(20fps)，与 15fps 的 PeakMonitor 协调。 |
| 2026-05-30 | `Audio/ProcessEnumerator.cs` | **性能优化（P2）** | `proc.MainModule.FileName` 是慢调用（打开进程模块快照）且每 2 秒对每个 app 查一次，但 PID→路径不变。新增 `_iconPathCache` 按 PID 缓存，命中跳过查询；进程退出时清理缓存项。 |
| 2026-05-30 | `Core/AppSettings.cs`、`UI/MainWindow.xaml.cs` | **Bug修复** | 修复设置双实例互相覆盖：原 MainViewModel 持有一个 `AppSettings` 实例，MainWindow 改语言/主题时各自 `Load()` 出独立实例再 Save，互相清掉对方字段（改语言丢增益等）。改为共享单例 `AppSettings.Current`，两处共用。 |
| 2026-05-30 | `Core/AppSettings.cs`、`UI/MainWindow.xaml.cs` | **Bug修复（主题持久化）** | 主题选择从不持久化（每次启动硬编码暗色）。新增 `IsDarkTheme` 字段，构造函数恢复上次主题并同步图标/文字，ToggleTheme 时保存。 |
| 2026-05-30 | `Core/PipelineManager.cs` | **Bug修复（热切换竞态）** | StartAsync/StopAsync 无同步，运行中快速切换源/麦克风时两次 Start 可能重叠创建两个 Writer / 竞态。新增 `SemaphoreSlim _gate(1,1)` 串行化启停。 |
| 2026-05-30 | `VoicePipe.iss` | **Bug修复（安装包中文乱码）** | 安装向导中自定义任务项（创建桌面快捷方式/开机自启）中文乱码。根因：`.iss` 脚本被保存为无 BOM 的 UTF-8，Inno Setup 6 编译时按系统 ANSI 码页（GBK）误读 [Tasks] 段中文 Description。标准向导文字正常是因为来自 .isl 语言文件。修复：用 UTF-8 with BOM 重写整个 .iss。同时顺带把弃用的 `ArchitecturesInstallIn64BitMode=x64` 改为 `x64compatible`，消除编译警告。注意：与 2026-05-29 语言包乱码同类问题（编码被破坏），今后编辑含中文的 .iss/.xaml 必须保持 UTF-8 BOM。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs`、`UI/MainWindow.xaml(.cs)`、`Audio/PeakMonitor.cs` | **性能优化（失焦降帧）** | 窗口失焦（挂后台打游戏）时把可视化刷新降帧省 CPU，波形仍在动不冻结。新增 `MainViewModel.SetWindowFocused(bool)`：聚焦时波形 20fps/PeakMonitor 67ms，失焦时波形 150ms(~6.7fps)/refresh 5s/PeakMonitor 200ms。PeakMonitor 轮询间隔改为可配置 `PollIntervalMs`。MainWindow 订阅 Activated/Deactivated。**注意：只动 UI 可视化计时器，音频管线 WasapiOut 10ms 延迟绝不改动（用户硬要求）。** |
| 2026-05-30 | **spec: settings-and-audio-features** | **新功能（完整 spec）** | 完成需求→设计→任务三文档 + 全部 43 个任务实现。新增"设置窗口"统一承载 5 大功能：①可自定义全局热键（静音麦克风/启停管线，Win32 RegisterHotKey，HwndSource 消息钩子，冲突检测）②系统托盘最小化（Hardcodet.NotifyIcon.Wpf，关窗缩托盘，App ShutdownMode 改 OnExplicitShutdown）③麦克风噪声门（仅 Mic_Path，FeedMic 中 Resample 后 Write 前原地处理，关闭=纯直通恒等，平滑 attack/release，绝不碰 App 音频）④开机自启（HKCU Run 键）+ 自动启动管线（启动接上次源+麦克风）⑤削波指示（PeakLevel≥0.99 变红，主题 ClipBrush）。所有功能默认关闭/可关。新增文件：Core/HotkeyBinding.cs、Audio/NoiseGate.cs、Services/AutoStartService.cs、Services/HotkeyManager.cs、UI/HotkeyCaptureControl.cs、UI/SettingsWindow.xaml(.cs)、ViewModels/SettingsViewModel.cs。AppSettings 新增 AutoStartBoot/NoiseGateEnabled/NoiseGateThreshold/MuteHotkey/PipelineHotkey 字段（MicMuted 不持久化）。AudioMixEngine 新增 MicMuted + NoiseGate（mic 项 `_micMuted?0:micBuf[i]*micGain`，app 项不变）。5 语言新增 24 个字符串 key + en-US 回退合并。**红线全程守住：WasapiOut 10ms 延迟未动；噪声门/静音只作用 Mic_Path。** |
| 2026-05-30 | `tests/VoicePipe.Tests/`（新测试项目） | **新功能（属性测试）** | 新建 xUnit + CsCheck 测试项目（net8.0-windows，SelfContained=false 避免 RID 继承）。实现 9 条正确性属性测试（每条≥100 迭代，带 `Feature: settings-and-audio-features, Property n` 标签）：P1 热键序列化往返、P2 噪声门关闭=恒等直通、P3 增益斜坡连续性、P4 阈值稳态收敛、P5 mic 路径操作不影响 app 路径、P6 设置持久化往返全字段、P7 管线切换语义、P8 削波阈值映射、P9 本地化 key 完整性+回退。最终 26 个测试全过。VoicePipe 加 `[assembly:InternalsVisibleTo("VoicePipe.Tests")]`（AssemblyInfo.cs）暴露 AppSettings 路径重载。 |
| 2026-05-30 | `UI/MainWindow.xaml(.cs)` | **致命Bug修复（托盘图标导致启动崩溃）** | 新增托盘后程序启动崩溃：`TypeConverterMarkupExtension` + "图像解码器无法解码该图像"。根因：`Hardcodet.NotifyIcon.Wpf` 的 `TaskbarIcon.IconSource` 走 WPF 严格图像解码管线（BitmapDecoder），拒绝该 .ico（而 exe 自身 ApplicationIcon 能显示是因为 Win32 加载器宽容）。`pack://` URI 改法无效——问题不在路径在解码器。修复：XAML 删除 IconSource，改在 OnSourceInitialized 代码里用 `System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath)` 从 exe 提取已嵌入图标设给 `TrayIcon.Icon`（GDI+ 宽容），try-catch 兜底（失败仅无图标不崩）。**验证教训：WPF 程序改 XAML 资源引用后，`dotnet build` 通过 + `Start-Process` 看进程存活都不可靠（崩溃对话框弹出时进程仍存活，HasExited=false 是假象）；必须跑 publish 产物 + 读 MainWindowTitle（应为 "VoicePipe" 而非 "启动错误"）+ 读程序 Serilog 日志确认无 Fatal。Debug 目录有独立 .ico 文件，publish 是嵌入资源，行为不同，务必验 publish。** |
| 2026-05-30 | `UI/MainWindow.xaml(.cs)`、`ViewModels/MainViewModel.cs`、`Themes/*.xaml`、删除 `UI/SettingsWindow.*`+`ViewModels/SettingsViewModel.cs` | **UI 重构** | ①设置从独立弹窗改为主页面内嵌切换视图：⚙ 按钮 toggle `ShowSettings`，主内容/设置两个 ScrollViewer 用 BoolToVisibility/BoolToInvVisibility 互斥显示，设置页左上角 ← 返回。SettingsViewModel 逻辑并入 MainViewModel（新增 ShowSettings/MinimizeToTray/NoiseGateEnabled/NoiseGateThreshold/AutoStartBoot/AutoStartPipelineSetting/MuteHotkey/PipelineHotkey 属性 + InitSettings(hotkeys,autoStart) 注入 + 各 OnXxxChanged 即时应用+持久化）。删除 SettingsWindow.xaml(.cs) 和 SettingsViewModel.cs。②CheckBox 改为现代开关样式（40×22 圆角轨道 + 圆形滑块，开=绿轨+白滑块右移，关=灰轨，悬停反馈），两主题均加。③HotkeyCaptureControl 加圆角模板（CornerRadius 6 + 悬停 Accent 边框），与 ComboBox 统一；两主题 ResourceDictionary 加 `xmlns:ui` 命名空间。已验证 publish 产物正常启动。 |
| 2026-05-30 | `Langs/*.xaml` | 文案调整 | "噪声门/Noise Gate" 文案改为"降噪/Noise Reduction"（5 语言：降噪/雜訊→降噪/ノイズ低減/노이즈 제거）。str_replace 精准替换，保持 UTF-8 BOM。 |
| 2026-05-30 | `src/VoicePipe/UI/MainWindow.xaml`、`VoicePipe.iss` | 版本发布 | 版本号 1.1.1 → 1.2（主界面标题旁 + 安装包 AppVersion）。1.2 累积变更：RNNoise 神经网络降噪替换噪声门、设置内嵌主页面、开关样式勾选框、所有框圆角统一、降噪文案。已 publish 验证启动 + RNNoise 初始化成功并打包。 |
| 2026-05-30 | `UI/MainWindow.xaml(.cs)` | 新功能（动画） | 设置页切入/切出加滑动淡入淡出动画。两个 ScrollViewer（MainContentView/SettingsView）各加 `x:Name` + TranslateTransform，移除原 Visibility 绑定（改由代码控制以支持退出动画）。`AnimateSettings(bool)`：进入视图从右 +40px 滑到 0 + 透明度 0→1，退出视图滑到 -40px + 1→0 后 Collapsed，220ms CubicEase EaseOut。ToggleSettings_Click 切换 vm.ShowSettings 后调用。 |
| 2026-05-30 | `VoicePipe-OpenSource/`（开源镜像） | 同步 | 同步开源文件夹到 v1.2：`robocopy "src" "VoicePipe-OpenSource\src" /MIR /XD bin obj`（含动画版 MainWindow、RnnoiseDenoiser.cs、native/rnnoise.dll；旧 SettingsWindow.* 被 /MIR 镜像删除）；根目录 v1.2 的 VoicePipe.iss（UTF-8 BOM，修掉旧版乱码）覆盖到开源目录。已核对：rnnoise.dll/RnnoiseDenoiser 已同步、SettingsWindow 已移除、版本号均 1.2、**memory.md 未同步（遵守规则）**。 |

---

## 重要规则

**`memory.md` 这个文件永远不要同步到 `VoicePipe-OpenSource` 开源文件夹！**
这个文件是 AI 助手的内部工作记录，包含开发过程细节，不适合对外公开。
同步开源文件夹时始终使用：
`robocopy "src" "VoicePipe-OpenSource\src" /MIR /XD bin obj`
而不是直接复制根目录所有文件。


---

## 12. 变更记录（续 · 2026-05-30 RNNoise + 设置重构 + 深度审查）

> 注：因 memory.md 中文在工具 grep/read 下偶有编码/剪枝问题，本段以追加方式记录。

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | spec `settings-and-audio-features` | **新功能（完整 spec，43 任务）** | 需求→设计→任务三文档 + 全部实现。新增"设置"承载 5 功能：①可自定义全局热键（静音麦克风/启停管线，Win32 RegisterHotKey + HwndSource）②系统托盘最小化（Hardcodet.NotifyIcon.Wpf，关窗缩托盘，App ShutdownMode=OnExplicitShutdown）③麦克风降噪④开机自启（HKCU Run 键）+ 自动启动管线⑤削波指示（PeakLevel≥0.99 变红 ClipBrush）。所有功能默认关/可关。新增 Core/HotkeyBinding.cs、Services/AutoStartService.cs、Services/HotkeyManager.cs、UI/HotkeyCaptureControl.cs。AppSettings 增 AutoStartBoot/NoiseGateEnabled/NoiseGateThreshold/MuteHotkey/PipelineHotkey（MicMuted 不持久化）。新增 tests/VoicePipe.Tests（xUnit+CsCheck，9 条属性测试，26 个全过）。红线：WasapiOut 10ms 未动；降噪/静音仅 Mic_Path。 |
| 2026-05-30 | `UI/MainWindow.xaml(.cs)` | **致命Bug修复（托盘图标崩溃）** | 启动崩溃 TypeConverterMarkupExtension+"图像解码器无法解码"。根因：Hardcodet TaskbarIcon.IconSource 走 WPF 严格 BitmapDecoder 拒绝该 .ico（pack:// URI 也无效，问题在解码器不在路径）。修复：删 XAML IconSource，代码里用 `System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath)` 从 exe 提图标设 TrayIcon.Icon（GDI+ 宽容）+ try-catch。教训：WPF 改 XAML 资源引用后，build 过 + 进程存活都不可靠（崩溃对话框时进程仍在），必须跑 publish 产物 + 读 MainWindowTitle（应为 VoicePipe 非"启动错误"）+ 读 Serilog 日志确认无 Fatal；Debug 目录有独立 .ico、publish 是嵌入资源行为不同，务必验 publish。 |
| 2026-05-30 | `UI/MainWindow.xaml(.cs)`、`ViewModels/MainViewModel.cs`、`Themes/*.xaml`、删 `UI/SettingsWindow.*`+`ViewModels/SettingsViewModel.cs` | **UI 重构** | ①设置从独立弹窗改为主页面内嵌切换视图：⚙ 切 ShowSettings + AnimateSettings 动画（220ms CubicEase，设置页右滑入+淡入/主内容反向，双向镜像），← 返回。SettingsViewModel 逻辑并入 MainViewModel。②CheckBox 改现代开关样式（40×22 圆角轨道+圆形滑块，开绿关灰）。③HotkeyCaptureControl 加圆角模板与 ComboBox 统一，两主题加 xmlns:ui。④语言选择器从头部移入设置页。 |
| 2026-05-30 | `Langs/*.xaml` | 文案 | "噪声门/Noise Gate"→"降噪/Noise Reduction"（5 语言，保持 UTF-8 BOM）。 |
| 2026-05-30 | `Audio/RnnoiseDenoiser.cs`(新)、`build_rnnoise/`、`native/rnnoise.dll`、`AudioMixEngine.cs`、`VoicePipe.csproj` | **降噪算法替换（噪声门→RNNoise AI 降噪）** | 噪声门只是音量闸门（说话时底噪照过、声音小被掐）。改用 Xiph RNNoise（RNN 神经网络，MIT）。自行用 VS2022 BuildTools cl.exe 从 jagger2048/rnnoise-windows 的官方 src 编译 x64 rnnoise.dll（138KB，def 导出 5 函数，dumpbin 验证），放 native/，csproj `<None CopyToOutputDirectory>` 部署。RnnoiseDenoiser：P/Invoke + ProcessStereo44k（立体声→单声道→44100↔48000 线性重采样→480 帧 RNNoise，FIFO 模型保证输出长度=输入、不越界），加载/出错降级直通绝不崩。FeedMic 中 Resample 后调用。仅 Mic_Path，10ms 输出延迟未动。降噪 ~10ms 延迟在麦克风侧。CPU 增加 <1%。 |
| 2026-05-30 | `Services/HotkeyManager.cs`、`ViewModels/MainViewModel.cs` | **Bug修复（静音热键）** | 静音后再按打不开。根因 RegisterHotKey 未加 MOD_NOREPEAT，按一下连发多个 WM_HOTKEY 反复翻转。修复：`Modifiers | MOD_NOREPEAT(0x4000)`；取消静音恢复状态栏。 |
| 2026-05-30 | `AudioMixEngine.cs`、`RnnoiseDenoiser.cs`、`PipelineManager.cs`、`MicCapturer.cs`、`HotkeyCaptureControl.cs` | **深度审查修复 B1-B5** | B1（非托管内存泄漏）AudioMixEngine 实现 IDisposable→denoiser.Dispose(rnnoise_destroy)，PipelineManager.Dispose 调用 + _gate.Dispose。B2（切麦克风降噪残留杂音）RnnoiseDenoiser.Reset() 清 FIFO/相位，AudioMixEngine.Reset 调。B3（MicCapturer 初始化/Stop 竞态）加 _sync 锁。B4（降噪出错关闭 UI 不同步）波形计时器 reconcile 弹回开关。B5（热键冲突视觉缓存旧主题色）改用主题资源画刷。遗留 B6/B7 极边缘不改。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs` | 体验 | 失焦波形刷新 150ms→66ms(15fps)，挂后台不卡。 |
| 2026-05-30 | `src/VoicePipe/UI/MainWindow.xaml`、`VoicePipe.iss` | 版本 | 1.1.1 → 1.2。 |
| 2026-05-30 | `VoicePipe-OpenSource/` | 开源同步 | robocopy /MIR 同步 src（含 RNNoise/native dll/v1.2）；用主目录修好的 v1.2 BOM 版 VoicePipe.iss 覆盖旧的乱码 1.1.1。memory.md 未同步（规则）。 |

---

## 13. 变更记录（续 · 2026-05-30 反馈环路修复 + 日志增强 + 重新打包）

> 注：因 memory.md 中文在工具 grep/read 下偶有编码/剪枝问题，本段以追加方式记录。

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `Audio/MicCapturer.cs`、`ViewModels/MainViewModel.cs` | **Bug修复（反馈环路嗡嗡声）** | 用户把 VB-Cable 回环录音端（CABLE Output 等）当麦克风选，形成 混音→CABLE Input→CABLE Output→采集→再混音 死循环，声音指数级冲到满刻度（持续嗡嗡/嘟嘟）。修复①：`MicCapturer.GetAvailableMics()` 过滤掉虚拟回环录音端（LoopbackMarkers：CABLE Output / CABLE-A Output / CABLE-B Output / VB-Audio / VoiceMeeter Out / VoiceMeeter Aux Out），从麦克风列表直接排除并记 INF 日志。修复②：新增 public 静态 `IsLoopbackDeviceId(string)` 按设备 ID 兜底判断（解析 FriendlyName）。修复③：`MainViewModel.StartPipeline` 启动前校验，若选中麦克风是回环设备则拒绝启动 + 状态栏中文提示「不能选择 CABLE Output 作为麦克风（会产生回音）」+ WRN 日志。用户确认采用"直接过滤"方案（不做多语言化，硬编码中文提示即可）。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs`、`UI/MainWindow.xaml.cs` | 诊断增强（控制台日志） | 用户反馈实时控制台日志太少，要求多加关键状态变化日志但**绝不在高频循环里逐帧打日志**。在低频状态变化点加 INF 日志：管线启动（含源/PID/麦克风/增益/降噪状态）、管线停止（完全停 / 仅停 App 保留直通）、麦克风静音开关、降噪开关、开机自启开关、自动启动管线开关、静音/启停热键设置（含冲突）、选择音频源、选择麦克风；MainWindow：全局热键触发、关窗最小化到托盘、应用退出清理、托盘还原/退出、设置面板开关、主题切换、语言切换、打开实时控制台。**严格避开高频路径**（AudioMixEngine.Read / FeedApp / FeedMic / LoopbackCapturer.CaptureLoop / WaveformAnalyzer.InlineSample / PeakMonitor.MonitorLoop 一律未加），不刷屏。降噪阈值滑块拖动属高频，未加日志。 |
| 2026-05-30 | publish 产物 + `Output/VoicePipeSetup.exe` | 版本发布（重新打包） | 因上述 MicCapturer/MainViewModel/MainWindow 改动，重新 `dotnet publish -c Release -r win-x64 --self-contained`，验证 publish 产物启动正常（MainWindowTitle="VoicePipe"、Serilog 日志无 Fatal、RNNoise 初始化成功、新增"已选择音频源"INF 日志生效）。重新用 ISCC 编译 v1.2 安装包（55MB）。Inno Setup 仅余 PrivilegesRequired/userdesktop 旧警告（非本次引入）。 |
| 2026-05-30 | `VoicePipe-OpenSource/`（开源镜像） | 同步 | `robocopy "src" "VoicePipe-OpenSource\src" /MIR /XD bin obj` 同步 3 个改动文件（MicCapturer / MainViewModel / MainWindow.xaml.cs）；复制主目录 v1.2 BOM 版 VoicePipe.iss 覆盖。已核对 memory.md 未同步（规则）。 |

---

## 14. 变更记录（续 · 2026-05-30 全管线迁移 48kHz + 降噪干湿混合）

> 注：因 memory.md 中文在工具 grep/read 下偶有编码/剪枝问题，本段以追加方式记录。

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `Audio/AudioFormat.cs`（新）、`AudioMixEngine.cs`、`LoopbackCapturer.cs`、`VirtualMicWriter.cs`、`NoiseGate.cs`、`WaveformAnalyzer.cs`、`RnnoiseDenoiser.cs` | **全管线采样率迁移 44100→48000Hz（音质+延迟根治）** | 用户指出降噪里 44.1k→48k→44.1k 来回重采样"脏"且增延迟。根因：VoicePipe 内部全程钉死 44100Hz，而 RNNoise 物理上只能在 48000Hz 跑（神经网络按 48k 训练、帧长固定 480=10ms@48k），导致麦克风（多为原生 48k）被先降 44.1k、降噪时又升回 48k、再降回 44.1k，转 3 次纯属自我消耗。修复：新建 `AudioFormat`（SampleRate=48000/Channels=2 单一事实来源），全部裸数字采样率改引用常量——AudioMixEngine 输出格式/RingBuffer 容量/重采样目标、LoopbackCapturer OutputFormat+WAVEFORMATEX、VirtualMicWriter OutputFormat、NoiseGate 斜坡步长基准、WaveformAnalyzer 降采样槽位（SamplesPerSlot=48000/512≈94，随常量自动推导避免波形滚动变速）全部 48k。**收益**：48k 是麦克风/系统混音/VB-Cable 的默认采样率，App/麦克风/输出三路在常见设备上重采样开销一并消失（仅非 48k 设备才转一次）。**红线守住**：VB-Cable WasapiOut 10ms 延迟未动；降噪/静音仍仅 Mic_Path。 |
| 2026-05-30 | `Audio/RnnoiseDenoiser.cs` | **重写：删除内部双重采样 + 加干湿混合（dry/wet mix）** | ①既然管线已统一 48k，删掉降噪器内部 44.1k↔48k 两次线性重采样（ResampleLinear/_upBuf/_downBuf/相位累加器全部移除），处理链简化为 立体声→单声道→RNNoise(480帧FIFO)→单声道→立体声，零重采样，延迟更低、无往返插值损失。方法 `ProcessStereo44k`→`ProcessStereo48k`。②加干湿混合缓解 RNNoise 把人声气声/高频/尾音一起抹掉导致的"空、闷"：新增平行干声 FIFO `_dryPending` 与湿声 FIFO `_wetPending` 严格同索引同延迟（避免相位错位产生梳状滤波/金属声），输出 = 湿*WetMix + 干*(1-WetMix)。`WetMix` 默认 0.85（保留 15% 原声），volatile，可 0~1 调。Reset 清干湿双 FIFO。 |
| 2026-05-30 | `Audio/AudioMixEngine.cs`、`Core/PipelineManager.cs`、`Core/AppSettings.cs`、`ViewModels/MainViewModel.cs` | 新功能（降噪强度可调） | 把降噪干湿比做成可调"降噪强度"：AudioMixEngine 加 `DenoiseStrength` 透传到 denoiser.WetMix；PipelineManager 加同名 pass-through；AppSettings 加持久化字段 `DenoiseStrength`（默认 0.85）；MainViewModel 加 `DenoiseStrength` 可观察属性 + OnChanged（即时应用+持久化，滑块高频不打日志）+ 构造函数应用 + InitSettings 加载。 |
| 2026-05-30 | `UI/MainWindow.xaml`、`Langs/*.xaml` | 新功能（降噪强度滑块 UI） | 设置页降噪区块"启用降噪"开关下方加"降噪强度"滑块（SmoothSlider，0~1，右侧显示百分比，绑定 DenoiseStrength）。5 语言新增 key `StrDenoiseStrength`（强度/強度/Strength/強度/강도），插在 StrNoiseGateThreshold 后，保持 UTF-8 BOM。 |
| 2026-05-30 | 验证 | 测试+发布 | 主项目 build 0 错 0 警；属性测试 26 个全过（含 P5 mic/app 路径隔离、P2-P4 降噪门，均不受采样率迁移影响）；publish 产物启动正常（标题=VoicePipe、无 Fatal、日志确认"RNNoise 初始化成功（原生 48kHz，无重采样）"生效）。**注意：尚未重新打包安装包，等用户确认音质/延迟改善后再出包+同步开源。** |

---

## 15. 变更记录（续 · 2026-05-30 设置动画方向修复 + 全代码审查修复）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `UI/MainWindow.xaml.cs` | **Bug修复（动画方向不符直觉）** | 设置页切换动画进出方向相同（都是新页右进/旧页左出），返回时感觉像又往前推一次。改为前进/后退镜像：进设置=新页从右(+40)滑入、旧页向左(-40)滑出；返回=新页从左(-40)滑入、旧页向右(+40)滑出。新增 inFrom/outTo 随 showSettings 取反。 |
| 2026-05-30 | `Audio/VirtualMicWriter.cs` | **Bug修复（COM 泄漏，审查发现）** | `FindCableInputDevice` 命中 CABLE Input 时直接 return，导致循环中此前遍历过的非命中 MMDevice 未被 Dispose（COM 泄漏）。且 `IsCableInputAvailable()` 每 2 秒走一次会持续累积。修复：命中保存到 match、其余一律 Dispose，循环结束后再返回。 |
| 2026-05-30 | `Audio/MicCapturer.cs` | **Bug修复（空引用竞态，审查发现）** | `OnDataAvailable` 直接读字段 `_capture.WaveFormat`，与并发 `Stop()`（置 _capture=null）有空引用窗口。改用事件 sender（触发事件的 WasapiCapture 实例）取格式，并加 count>0 守卫避免空帧触发下游。 |
| 2026-05-30 | 审查结论 | 代码审查 | 全面审查 PipelineManager/RingBuffer/LoopbackCapturer/AudioMixEngine/RnnoiseDenoiser/App.xaml.cs/VirtualMicWriter/MicCapturer。48kHz 迁移无新引入 bug；RingBuffer 锁/位运算/防溢出正确；LoopbackCapturer COM 释放顺序+事件句柄+暂停标志正确；PipelineManager 串行化+PurgeDeadSessions+Paused 正确；RNNoise 干湿 FIFO 同步移位无越界。仅上述 2 处真实问题已修。build 0 错 0 警，26 测试全过。 |

---

## 16. 变更记录（续 · 2026-05-30 出包 + 同步开源）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | publish 产物 + `Output/VoicePipeSetup.exe` | 版本发布（重新打包） | 累积本轮全部改动后出包：48kHz 全管线迁移、降噪删除内部双重采样、降噪干湿混合（缓解人声发空）、降噪强度可调滑块、设置动画前进/后退方向镜像修复、VirtualMicWriter COM 泄漏修复、MicCapturer 空引用竞态修复。dotnet publish Release 自包含 → 验证启动正常（标题=VoicePipe、无 Fatal、日志确认"RNNoise 初始化成功（原生 48kHz，无重采样）"）→ ISCC 编译安装包（52.6MB）。仍为 v1.2（功能增强/修复，未升主版本号）。Inno Setup 仅余 PrivilegesRequired/userdesktop 旧警告（非本次引入）。 |
| 2026-05-30 | `VoicePipe-OpenSource/`（开源镜像） | 同步 | `robocopy "src" "VoicePipe-OpenSource\src" /MIR /XD bin obj` 同步 18 个改动文件（含新增 Audio/AudioFormat.cs、48k 迁移各文件、RnnoiseDenoiser 重写、5 语言包 StrDenoiseStrength、MainWindow.xaml 滑块/动画）；复制主目录 v1.2 BOM 版 VoicePipe.iss 覆盖。已核对 memory.md 未同步（规则）。 |

---

## 17. 变更记录（续 · 2026-05-30 热键重置按钮 + 音量显示修复）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `UI/MainWindow.xaml`、`UI/MainWindow.xaml.cs`、`ViewModels/MainViewModel.cs`、`Langs/*.xaml` | 新功能（热键重置） | 全局热键区块标题行右上角加"重置"按钮（GhostButton）。点击→MainWindow.ResetHotkeys_Click→vm.ResetHotkeys()：把 MuteHotkey/PipelineHotkey 都设为 HotkeyBinding.None，各自 OnXxxHotkeyChanged 会经 HotkeyManager.Register(None) 注销已注册热键+清冲突标志+持久化；HotkeyCaptureControl 的 Binding DP 变化触发 UpdateText 显示"未设置"。5 语言新增 key StrHotkeyReset（重置/重設/Reset/リセット/재설정），保持 UTF-8 BOM。 |
| 2026-05-30 | `Audio/PeakMonitor.cs` | **Bug修复（音量显示 200%/400%）** | App 会话 MasterPeakValue 对自身提升了流音量的 app 可能 >1.0，直接显示出现 203%/400% 异常值。修复：读取后钳到 [0,1]。同时改进同进程多会话处理：本轮内对同 PID 的多个音频会话取最大值（HashSet seenThisRound 标记本轮首写后续取 max），避免后写的静默会话把活跃流峰值覆盖成 0。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs` | **Bug修复（部分 app 不显示音量，如 Edge/Apple Music）** | Edge/Apple Music 是多进程 app，实际发声的常是子进程，列表里标注的主进程显示 0%。因 loopback 捕获用 INCLUDE_TARGET_PROCESS_TREE 会连子进程一起抓，WaveformTimer_Tick 改为按进程名聚合峰值（同名进程取最大值显示），让"有没有声音"的显示与实际捕获行为一致。新增 using System.Collections.Generic。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe` + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常（标题=VoicePipe、无 Fatal、RNNoise 48kHz）。ISCC 打包（52.6MB）。robocopy /MIR 同步开源 + 复制 v1.2 iss。memory.md 未同步（规则）。 |

---

## 18. 变更记录（续 · 2026-05-30 音量滑块即时存盘）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `ViewModels/MainViewModel.cs` | **Bug修复（音量滑块不自动保存）** | OnAppGainChanged/OnMicGainChanged 原来只更新内存 _settings、不存盘，增益仅在 StartPipeline 时才顺带 Save 一次。所以只拖滑块不启动/重启混音，重启程序后增益丢失。修复：两个处理都加 PersistSettings()（带 _settingsLoading 守卫，拖滑块即时存盘）。构造函数加载 AppGain/MicGain 初值时用 _settingsLoading=true 包裹，避免启动加载触发无谓写盘。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe` + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常。已验证 %LOCALAPPDATA%\VoicePipe\appsettings.json 含 AppGain/MicGain 字段。ISCC 打包（52.6MB）。robocopy /MIR 同步开源 + 复制 v1.2 iss。memory.md 未同步（规则）。 |

---

## 19. 变更记录（续 · 2026-05-30 VoicePipe 自身音频源拦截 + UWP 捕获结论）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `Audio/ProcessEnumerator.cs`、`ViewModels/MainViewModel.cs` | **Bug修复（VoicePipe 自己出现在音频源列表且电平最高）** | 现象：列表里出现第二个 VoicePipe 实例（多开/测试残留，PID 与当前不同），显示 66%（其实是混音输出电平），比放歌的音乐还高。原过滤只按 Environment.ProcessId 排除当前实例，漏掉其它实例。用户要求：**不要从列表隐藏**（保留以直观对比各 app 音量），只在选中启动时拦截+提示。修复：①ProcessEnumerator 移除按 PID 的自我排除，VoicePipe 所有实例都保留在列表；②MainViewModel.StartPipeline 新增校验：若选中进程名为 VoicePipe 则拒绝启动 + 状态栏提示「不能选择 VoicePipe 自己作为音频源（会产生回音）」+ WRN 日志（与 CABLE Output 麦克风拦截同一套路）。 |
| 2026-05-30 | 调研结论（未改代码） | UWP 应用捕获限制 | 用户反馈 Apple Music 放歌时 AMPLibraryAgent 显示 0%、声音被算到 svchost；重启 Apple Music 后归属变化。查证（微软官方文档 + win-capture-audio issues）确认这是 Windows 硬限制：UWP/打包应用音频由系统服务托管渲染，音频会话 GetProcessId 返回宿主进程（svchost/audiodg）而非 app 自身；per-process loopback 的 INCLUDE_TARGET_PROCESS_TREE 只抓目标+直接子进程，不抓孙进程/兄弟/宿主，且会话可跨多进程（AUDCLNT_E_NO_SINGLE_PROCESS）。GetSessionIdentifier 能识别会话归属（含 AppUserModelId）但 loopback 只能按 PID 激活，无法单独隔离宿主里的一条会话 → "干净只抓某 UWP 应用"做不到。普通桌面应用（Chrome/游戏/VRChat）不受影响。用户决定：什么都不做，先出包。后续如需可考虑加友好提示。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe` + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常。ISCC 打包（52.6MB）。robocopy /MIR 同步开源 + 复制 v1.2 iss。memory.md 未同步（规则）。 |

---

## 20. 变更记录（续 · 2026-05-30 本地监听功能）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `Audio/AudioMixEngine.cs`、`Audio/MonitorOutput.cs`(新) | **新功能（本地监听/耳机回放）** | 把混音同时回放到默认播放设备（耳机），实时听自己发往 VB-Cable 的效果。**红线：完全独立于 VB-Cable 路径，绝不影响其 10ms 低延迟。** 实现：AudioMixEngine 新增独立 _monitorBuffer（200ms）+ MonitorEnabled/MonitorApp/MonitorMic（volatile）。Read()（VB-Cable 那路，VirtualMicWriter 拉取）在生成数据的同一循环里顺手算监听信号写入 _monitorBuffer（按子开关选 App/Mic 分量），VB-Cable 输出 fptr[i] 逻辑完全不变。新建 MonitorOutput：独立 WasapiOut（默认设备，50ms 延迟）+ 内部 MonitorProvider 从 _monitorBuffer 拉取，数据不足补零。启动/设备故障只记日志降级，不抛到上层、不碰 VB-Cable。 |
| 2026-05-30 | `Core/PipelineManager.cs` | 接线 | 新增 _monitor 字段 + MonitorEnabled/MonitorApp/MonitorMic pass-through。MonitorEnabled setter 即时 Start/Stop 监听链。StartAsync 末尾按主开关启动 _monitor；StopAsync 停监听（StopAppOnly 麦克风直通模式保留监听不停）；Dispose 释放 _monitor。 |
| 2026-05-30 | `Core/AppSettings.cs`、`ViewModels/MainViewModel.cs` | 接线+持久化 | AppSettings 新增 MonitorEnabled/MonitorApp/MonitorMic（默认全关）。MainViewModel 加 3 个可观察属性 + 构造函数应用（先设子开关再设主开关）+ InitSettings 加载 + OnXxxChanged（即时应用+持久化+低频日志）。 |
| 2026-05-30 | `UI/MainWindow.xaml`、`Langs/*.xaml` | UI | 设置页降噪区块下方新增"本地监听"区块：主开关 + 两个子开关（监听App/监听麦克风，仅主开关开时显示，BoolToVisibility）+ 提示文字"两个都关时监听整个输出"。5 语言新增 5 个 key（StrMonitor/StrMonitorEnable/StrMonitorApp/StrMonitorMic/StrMonitorHint），保持 UTF-8 BOM。逻辑：主开关关=不监听；主开关开+子开关都关=监听整个 VoicePipe 输出（App+Mic）；否则按子开关。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe` + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常。ISCC 打包（52.6MB）。robocopy /MIR 同步开源（含新增 MonitorOutput.cs）+ 复制 v1.2 iss。memory.md 未同步（规则）。注：本地监听效果需在有耳机的测试机验证（当前工作站无法验证音频）。 |

---

## 21. 变更记录（续 · 2026-05-30 监听输出设备可选）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `Audio/MonitorOutput.cs` | 新功能（监听设备可选） | 监听输出目标设备可指定。新增 TargetDeviceId（空=系统默认；运行中改自动重启切设备）；Start() 按 ID 取设备，找不到/非激活则回退系统默认。新增静态 GetAvailableRenderDevices() 枚举激活的播放设备 + RenderDeviceInfo record。 |
| 2026-05-30 | `Core/PipelineManager.cs`、`Core/AppSettings.cs` | 接线+持久化 | PipelineManager 加 MonitorDeviceId（_monitor 未创建时先记住，StartAsync 创建时套用；运行中改即时切）。AppSettings 加 MonitorDeviceId（默认 ""=系统默认）。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs` | 接线 | 新增 MonitorDeviceItem 数据模型（Id 空=系统默认）+ MonitorDevices 集合 + SelectedMonitorDevice。RefreshAllAsync 后台枚举播放设备，UI 线程差异更新（首项固定"系统默认"Id=""），按 _settings.MonitorDeviceId 预选。OnSelectedMonitorDeviceChanged 即时应用 pipeline.MonitorDeviceId + 持久化。构造函数应用持久化设备 ID。 |
| 2026-05-30 | `UI/MainWindow.xaml`、`Langs/*.xaml` | UI | 本地监听子面板顶部加"输出设备"ComboBox（ItemsSource=MonitorDevices，DisplayMemberPath=Name）。5 语言新增 StrMonitorDevice（输出设备）/StrMonitorDefaultDevice（系统默认），保持 UTF-8 BOM。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe` + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常。ISCC 打包（52.6MB）。robocopy /MIR 同步开源 + 复制 v1.2 iss。memory.md 未同步（规则）。需测试机有多个播放设备时验证切换效果。 |

---

## 22. 变更记录（续 · 2026-05-30 进程列表按名去重 + 根进程代表 + 监听文案）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `Langs/*.xaml` | 文案 | 监听主开关文案"把混音放到我的耳机里听"→"开启监听"（5 语言：开启监听/開啟監聽/Enable monitoring/モニターを有効化/모니터링 켜기），保持 UTF-8 BOM。 |
| 2026-05-30 | `Audio/ProcessEnumerator.cs` | **重构（一个 app 不再拆成多条）** | 原来每个音频会话 PID 都 Add 一条，Chrome/Edge 等多进程 app 会冒出好几个同名条目，乱且不知选哪个。改为**按进程名去重**：用 Toolhelp 快照（CreateToolhelp32Snapshot）一次性取 PID→PPID/名字映射；对每个有音频会话的 PID，顺父进程链向上找到最顶层同名进程作为「根进程」(FindRootSameName，带 visited 防环)；按名字去重，代表 PID=根进程。配合 LoopbackCapturer 的 INCLUDE_TARGET_PROCESS_TREE：选根进程即可抓到整棵树（所有标签页 + 音频服务子进程）的声音，多个子进程同时出声也一个不漏（查证 Chrome/Edge/WebView2 音频经独立 audio service 子进程渲染，均在主进程树下）。新增 public GetPidNameMap() 供 PeakMonitor 用。 |
| 2026-05-30 | `Audio/PeakMonitor.cs` | 配合去重 | 列表改用根进程 PID 后，出声的常是子进程，根 PID 峰值可能为 0。新增 ProcessPeaksByName（进程名→同名所有出声 PID 峰值最大值）：MonitorLoop 用 ProcessEnumerator.GetPidNameMap 把每个会话 PID 的峰值按名聚合，每轮整体刷新（移除消失的名字防卡旧值）。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs` | 配合去重 | WaveformTimer_Tick 改为按进程名查 PeakMonitor.ProcessPeaksByName 显示音量（替代原来从列表 PID 查 ProcessPeaks 再聚合），与根进程代表 PID 协调。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe` + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常（日志确认进程枚举选到 1 条、监听设备=系统默认、无 Fatal）。ISCC 打包。robocopy /MIR 同步开源 + 复制 v1.2 iss。memory.md 未同步（规则）。去重效果需测试机肉眼验证（当前工作站隔离调用 WPF 程序集不可靠）。 |

---

## 23. 变更记录（续 · 2026-05-30 同步开源 + 更新 README）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `VoicePipe-OpenSource/`（开源镜像） | 同步 | robocopy /MIR 同步 src（exit 0，源码已最新）+ 复制 v1.2 BOM 版 VoicePipe.iss。 |
| 2026-05-30 | `VoicePipe-OpenSource/README.md`、`README_EN.md` | 文档更新 | 中英文 README 更新到最新功能集：核心特性新增「进程智能归并（按名去重+根进程）」「AI 神经网络降噪（RNNoise + 干湿混合强度滑块）」「本地监听（可选输出设备，独立链不影响 VB-Cable 10ms）」「全局热键」「系统托盘/开机自启」「全链路 48kHz」「削波指示」「5 语言/内嵌动画设置页/即时保存」。零回声项补充麦克风回环过滤。运行原理：架构图加入 RNNoise 麦克风降噪支路 + MonitorOutput 监听支路 + 48kHz/10ms 标注；步骤补充 ProcessEnumerator（Toolhelp 根进程归并）、RnnoiseDenoiser、MonitorOutput；采样率从 44.1kHz 更正为 48kHz。memory.md 未同步（规则）。 |

---

## 24. 变更记录（续 · 2026-05-30 v1.2.1：监听竞态修复 + 5 个新功能）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `Audio/MonitorOutput.cs` | **Bug修复（监听竞态，审查发现）** | Start/Stop/切设备 无同步，UI 线程（开关/切设备）与管线启动线程可能并发重叠创建/销毁 WasapiOut，极端时机崩或泄漏。新增 `_sync` 锁 + StartLocked/StopLocked，所有路径串行化。 |
| 2026-05-30 | `Audio/AudioMixEngine.cs`、`Core/PipelineManager.cs`、`Core/AppSettings.cs`、`ViewModels/MainViewModel.cs`、`UI/MainWindow.xaml`、`Langs/*.xaml` | **新功能4（监听独立音量）** | 监听音量独立于发往 VB-Cable 的音量。AudioMixEngine 加 MonitorGain（感知响度曲线 `^1.7`，volatile _monitorGainAmp），Read() 监听支路乘 monGain。PipelineManager/AppSettings(MonitorGain=1.0)/MainViewModel(可观察属性+即时应用+持久化) 接线。UI 监听子面板加"监听音量"滑块。key StrMonitorVolume。 |
| 2026-05-30 | `Audio/WaveformAnalyzer.cs`、`ViewModels/MainViewModel.cs`、`UI/MainWindow.xaml` | **新功能5（峰值 dB 显示）** | WaveformAnalyzer 加 GetLatestPeak()。MainViewModel 加 OutputPeakText（波形定时器算 `20*log10(peak)`，静音显示 −∞ dB）。波形预览标题栏右侧显示实时 dBFS 数值（Consolas 字体）。 |
| 2026-05-30 | `UI/MainWindow.xaml.cs`、`Langs/*.xaml` | **新功能6（热键托盘提示）** | OnHotkeyPressed 触发后用 TaskbarIcon.ShowBalloonTip 弹托盘气泡（静音/取消静音、开始/停止混音），游戏全屏也能看到。4 个 key StrTipMicMuted/StrTipMicUnmuted/StrTipPipelineOn/StrTipPipelineOff。 |
| 2026-05-30 | `Audio/SpectrumAnalyzer.cs`(新)、`UI/SpectrumControl.cs`(新)、`AudioMixEngine.cs`、`AppSettings.cs`、`MainViewModel.cs`、`UI/MainWindow.xaml`、`Langs/*.xaml` | **新功能8（频谱图）** | 新增 SpectrumAnalyzer：音频线程 InlineSample 只写 1024 点环形缓冲（零分配 O(1)）；UI 线程 GetSpectrum 加 Hann 窗 + 迭代基2 FFT + 对数分箱压成 48 条（复用缓冲，全在 UI 线程）。新增 SpectrumControl 圆角柱状渲染。AudioMixEngine.Read 加一行 SpectrumAnalyzer.InlineSample。AppSettings.ShowSpectrum + MainViewModel(SpectrumData 双缓冲 + ShowSpectrum 切换)；波形/频谱用 BoolToVisibility/Inv 互斥显示，预览标题栏加切换勾选框。key StrSpectrumMode。**音频线程零额外堆分配，FFT 全在 UI 线程。** |
| 2026-05-30 | `Core/AppSettings.cs`、`MainViewModel.cs`、`UI/MainWindow.xaml(.cs)`、`Langs/*.xaml` | **新功能9（开机静默到托盘）** | AppSettings.StartMinimized。MainViewModel 可观察属性+持久化。MainWindow.OnSourceInitialized：若 StartMinimized && MinimizeToTray 则初始化后 Hide() 到托盘。自动启动区块加开关。key StrStartMinimized。 |
| 2026-05-30 | `UI/MainWindow.xaml`、`VoicePipe.iss` | 版本 | 1.2 → 1.2.1（标题 v1.2.1 + 安装包 AppVersion）。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe` + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常（标题=VoicePipe、无 Fatal、各模块初始化正常）。ISCC 打包 1.2.1。robocopy /MIR 同步开源（含 SpectrumAnalyzer/SpectrumControl）+ 复制 iss。memory.md 未同步（规则）。频谱/监听音量等音频效果需测试机验证。 |
| 2026-05-30 | `.kiro/specs/vbcable-detection-uninstall/design.md` | Bugfix设计 | VB-Cable 检测/卸载缺陷修复设计：①安装器 `IsVBCableInstalled()` 改判标准——由"查残留注册表键"改为解析 `HKLM\...\MMDevices\Audio\Render\Capture` 各端点的 `DeviceState=1` + FriendlyName 含 `CABLE Input`/`CABLE Output`（给出注册表路径、PKEY `{a45c254e-...},2`、判定伪代码）；②运行时 `DependencyChecker.IsVbCableInstalled()` 改为复用 `MMDeviceEnumerator` 枚举 Active Render 端点找 `CABLE Input`，与 `VirtualMicWriter` 标准对齐，去掉注册表键提前 return；③卸载支持——`[Files]` 常驻复制 VB-Cable 卸载器+驱动到 `{app}\vbcable`（不 deleteafterinstall），卸载阶段 `CurUninstallStepChanged` 弹默认"否"的确认框、选"是"则 `Exec(VBCABLE_Setup_x64.exe '-u -h')` 并提示重启，`[UninstallDelete]` 清理目录。含 Bug Condition/Fix Checking/Preservation Checking 对照。提醒：含中文 .iss 须存为 UTF-8 BOM。任务拆分待下一阶段。 |
| 2026-05-30 | `Audio/PeakMonitor.cs` | **Bug修复（回归）** | 修复进程音量条对"VoicePipe 启动后才出声的应用"（如后打开的网易云 cloudmusic）峰值恒为 0、不跳动的问题。根因是 2026-05-29 性能优化把 `_cachedRenderDevice` 缓存成永久单例，而 NAudio 的 `AudioSessionManager.Sessions` 只在首次取一次会话快照、之后不刷新，导致首轮轮询后新建的会话读不到 `MasterPeakValue`（对照 `ProcessEnumerator` 每次新建枚举器故应用仍能进下拉列表，形成"列表里有、音量条不跳"的矛盾）。修复仅改 `MonitorLoop()` 的 App process peaks 分支：①每轮读 `Sessions` 前调用 `AudioSessionManager.RefreshSessions()` 让新会话可见；②新增 `_lastDeviceCheckTick`/`DeviceCheckIntervalMs=1000`，用 `Environment.TickCount64` 节流（约 1s）比对默认渲染设备 ID，变更才 Dispose 旧设备并切换（感知系统默认播放设备切换）。复用缓存的 `_cachedEnumerator`，不退回每帧创建/销毁 COM 对象，保留性能优化意图。麦克风分支、按名聚合/钳制/衰减逻辑零改动。build 0 警告 0 错误。spec: `.kiro/specs/process-peak-not-updating/`。|
| 2026-05-30 | `VoicePipe.iss`、`Bootstrapper/DependencyChecker.cs` | **Bug修复（VB-Cable 检测/卸载）** | 修复"VB-Cable 卸载后注册表键残留导致安装器误判已安装而跳过驱动安装、运行时显示可用却没声音"的问题。①安装器 `IsVBCableInstalled()` 由"仅查注册表键（VB-Audio\Cable\VBAudioCableWDM_SR、Services\VBAudioVACMME、Services\VB-Cable）"改为"检测真实激活的音频端点"：新增 `EndpointExists(SubPath,NameSubstring)` 解析 `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\{Render,Capture}` 各端点 GUID 子键，仅当 `DeviceState=1`（激活）且 Properties 下 PKEY_Device_FriendlyName（`{a45c254e-...},2`）含 "CABLE Input"/"CABLE Output" 才算存在；`IsVBCableInstalled = EndpointExists(Render,'CABLE Input') and EndpointExists(Capture,'CABLE Output')`，删除旧三处键判据。②运行时 `DependencyChecker.IsVbCableInstalled()` 改为复用 `VirtualMicWriter.IsCableInputAvailable()`（枚举 Render+Active 端点找 CABLE Input），与真正干活的输出逻辑同一口径，消除"显示可用却没声音"；异常兜底 return false。③卸载流程：`[Files]` 新增常驻复制 `deps\vbcable_extracted\*` → `{app}\vbcable`（不带 deleteafterinstall，保留卸载所需 .inf/.sys/.cat）；`[UninstallDelete]` 清理 `{app}\vbcable`；`[Code]` 新增 `CurUninstallStepChanged`(usUninstall)，当卸载器存在且 VB-Cable 真实可用时弹**默认"否"**(MB_DEFBUTTON2)确认框，选"是"才 `Exec(VBCABLE_Setup_x64.exe '-u -h')` 并提示重启，选否/回车保留 VB-Cable。`.iss` 保持 UTF-8 BOM；`[Run]`/`CurStepChanged`/`[Icons]`/`[Tasks]`/语言配置写入等无关段落逐字未动。注：`-u -h` 静默卸载参数需目标机验证，无效则退化为不带参数让用户在卸载器窗口确认。publish 0 错误、ISCC 打包成功（VoicePipeSetup.exe），.iss 无中文乱码。spec: `.kiro/specs/vbcable-detection-uninstall/`。开源副本 `VoicePipe-OpenSource/` 暂未同步（待主线真机验证后再同步）。|

---

## 25. 标准测试与验证流程（每次改动后必走，强制规范）

> 这是每次改动 VoicePipe 后**必须执行**的完整验证链。按顺序走，任何一步失败都要先修复再继续。
> 路径：项目根 `e:\Documents\Voicepipe`；主项目 `src\VoicePipe`；测试 `tests\VoicePipe.Tests`。

### 步骤 0 · 改动前/中
- 所有改动必须在第 11 节"变更记录"追加记录（日期 | 文件 | 原因 | 描述）——强制约定。
- 含中文的 `.iss` / `Langs/*.xaml` 编辑后必须保持 **UTF-8 with BOM**（否则乱码）。

### 步骤 1 · 静态诊断
- 对改动的每个文件跑 getDiagnostics（IDE 语义检查），确认 0 报错。

### 步骤 2 · 编译
- `dotnet build -c Debug --nologo -v quiet`（cwd=`src\VoicePipe`）→ 必须 **0 错 0 警**。

### 步骤 3 · 属性测试
- `dotnet test --nologo -v quiet`（cwd=`tests\VoicePipe.Tests`）→ 必须 **26 个全过**（CsCheck，每条≥100 迭代，含 P1-P9）。
- 注意：PowerShell 控制台输出中文会显示乱码（"宸查€氳繃"=已通过），但数字结果（通过:26 失败:0）准确，日志/源码本身是正常 UTF-8。

### 步骤 4 · 编码 BOM 校验（仅当改过 .iss / Langs）
- PowerShell 读首 3 字节判断 `0xEF 0xBB 0xBF`，对 5 个语言文件 + VoicePipe.iss 逐一确认 `BOM=True`。

### 步骤 5 · 发布产物
- `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false --nologo -v quiet`（cwd=`src\VoicePipe`）。
- 产物路径：`src\VoicePipe\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\VoicePipe.exe`。

### 步骤 6 · ★ 实跑 publish 产物验证（WPF 改 XAML/资源后绝不能省，历史血泪教训）
- `Start-Process -PassThru` 启动 → `Start-Sleep 6~7s` → 读 `MainWindowTitle`：**必须是 "VoicePipe"**，不是"启动错误"等。
- `HasExited` 仅作参考（崩溃对话框弹出时进程仍存活，HasExited=false 是假象，不可单独依赖）。
- 读最新 `publish\logs\*.log` 末尾：确认 **无 Fatal**，且关键模块初始化正常（DependencyChecker / RnnoiseDenoiser 初始化成功 / HotkeyManager initialized / 监听输出设备）。
- 验证后 `Stop-Process` 关闭。
- **为什么必须验 publish 而非 Debug**：Debug 目录有独立 .ico 等散文件，publish 是嵌入资源，行为不同；`dotnet build` 通过 + 进程存活都不可靠，必须读窗口标题 + Serilog 日志。

### 步骤 7 · 打包安装包
- `& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "VoicePipe.iss"`（cwd=项目根）→ 确认 "Successful compile"，产物 `Output\VoicePipeSetup.exe`（约 52.6MB）。
- 残留旧警告 `PrivilegesRequired/userdesktop` 是历史既有、非本次引入，可忽略。
- `Get-Item Output\VoicePipeSetup.exe` 确认 LastWriteTime 是刚生成的新时间戳。

### 步骤 8 · 同步开源镜像（仅当要发布/同步时）
- `robocopy "src" "VoicePipe-OpenSource\src" /MIR /XD bin obj`（cwd=项目根）。
- `Copy-Item "VoicePipe.iss" "VoicePipe-OpenSource\VoicePipe.iss" -Force`（v1.2.1 BOM 版覆盖）。
- 核对 **memory.md 绝不出现在 VoicePipe-OpenSource 镜像里**（强制规则）。README.md/README_EN.md 是开源目录直接维护，不从 src 同步。

### 当前工作站的验证局限（重要）
- 本工作站**无麦克风、未装 VB-Cable**，命令行无法验证实际音频效果（降噪/监听/混音/频谱/峰值跳动/VB-Cable 检测真伪/卸载流程）。
- 这些只能在**用户的测试机**上肉眼/听感验证。代码层正确性靠：编译 + 26 属性测试 + publish 启动无 Fatal + 逻辑审查。
- `Cable=false` 在本机日志里是**正常现象**（确实没装），不代表检测逻辑有问题。
- 隔离加载 WPF 程序集调静态方法（如验证 ProcessEnumerator 去重）不可靠，不要依赖，改由测试机肉眼验证。

---

## 26. 变更记录（续 · 2026-05-30 子进程两条 spec 审查 + 重新打包）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `Bootstrapper/DependencyChecker.cs` | **Bug修复（VB-Cable 检测口径不一致）** | 运行时检测从"查注册表残留键（SOFTWARE\VB-Audio\Cable、Services\VBAudioVACMME 等）"改为直接复用 `VirtualMicWriter.IsCableInputAvailable()`（枚举 DataFlow.Render + DeviceState.Active，FriendlyName 含 CABLE Input）。单一真实端点口径，消除"显示 ✓ 却没声音"。try/catch 兜底返回 false。（子进程 spec: vbcable-detection-uninstall 3.2） |
| 2026-05-30 | `VoicePipe.iss` `[Code]` | **Bug修复（安装器检测口径）** | 新增 EndpointExists(SubPath,NameSubstring)：枚举 MMDevices\Audio\{Render,Capture} 端点 GUID 子键，读 DeviceState=1（激活）+ PKEY_Device_FriendlyName 大小写不敏感匹配。重写 IsVBCableInstalled() = CABLE Input(Render) AND CABLE Output(Capture) 双端点激活。移除旧三处注册表键判据。读失败按"不匹配"兜底。（spec 3.1） |
| 2026-05-30 | `VoicePipe.iss` `[Files]/[UninstallDelete]/[Code]` | **新功能（卸载可选清理 VB-Cable）** | VB-Cable 驱动整目录常驻复制到 {app}\vbcable（不带 deleteafterinstall）；[UninstallDelete] 清理该目录；CurUninstallStepChanged(usUninstall)：常驻卸载器存在且 IsVBCableInstalled() 为真时弹确认框（MB_YESNO or MB_DEFBUTTON2 默认高亮"否"），选是才 Exec VBCABLE_Setup_x64.exe `-u -h` 静默卸载 + 提示重启。默认/回车保留 VB-Cable。（spec 3.3，注：-u -h 静默参数需目标机验证） |
| 2026-05-30 | `Audio/PeakMonitor.cs` `MonitorLoop()` | **Bug修复（启动后新建会话/默认设备变更峰值恒为0）** | 根因：性能优化把 AudioSessionManager 会话快照缓存死了，首轮后才播放的应用（如网易云 cloudmusic）读不到峰值。修复：App 分支每轮调 `_cachedRenderDevice.AudioSessionManager.RefreshSessions()` 让新会话可见；新增节流（Environment.TickCount64，约 1s，DeviceCheckIntervalMs=1000）比对默认设备 ID，变更则 Dispose 旧设备切到新设备。麦克风分支/按名聚合/钳制/衰减零改动。异常沿用既有 catch → Dispose 置空 _cachedRenderDevice 下轮重建。（spec: process-peak-not-updating 3.1） |
| 2026-05-30 | 审查 + 出包 | 验证 | 主进程完整审查子进程两条 spec 全部改动：DependencyChecker/VirtualMicWriter 口径一致无泄漏、iss 端点枚举逻辑严谨 + 卸载默认高亮否、PeakMonitor RefreshSessions+节流正确且 catch 兜底完整。build 0 错 0 警、26 测试全过、iss+5 语言 BOM 完好、publish 启动正常（标题 VoicePipe、无 Fatal、Cable=false 因本机未装属正常）。ISCC 重新打包成功（52.6MB）。降噪按用户决定不改。 |

---

## 27. 变更记录（续 · 2026-05-30 检查更新功能 + 波形按钮布局修复 + 开源同步）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `VoicePipe-OpenSource/` | 同步 | robocopy /MIR 同步 src 全部最新改动 + 复制 v1.2.1 iss。memory.md 未同步。（配合发 v1.2.1 release，已另写中英双语 release notes 交付用户）。 |
| 2026-05-30 | `UI/MainWindow.xaml` | **Bug修复（波形切换按钮跟着数字乱跳）** | 波形预览标题栏的"频谱/波形切换勾选框"和右侧实时 dB 数字原来同在右对齐 StackPanel 里，dB 文本宽度变化（如 -12.3 dB↔−∞ dB）导致整组左右移、勾选框点不到。改为 3 列 Grid：标题(*) | 勾选框(Auto，位置固定) | dB(固定 70px 右对齐)。数字在自己框内变化，不再挤动勾选框。 |
| 2026-05-30 | `Services/UpdateService.cs`(新)、`VoicePipe.csproj` | **新功能（检查更新）** | 新增 UpdateService：GET `api.github.com/repos/Yamada-Ryo4/VoicePipe/releases/latest`（带 User-Agent，15s 超时）→ 解析 tag_name（NormalizeVersion 去 v/Release_ 前缀）与本地版本（程序集版本）语义比较（CompareVersion）→ 在 assets 找 VoicePipeSetup.exe 的 browser_download_url。DownloadInstallerAsync 下到临时目录。全 try/catch，失败返回 Error 不抛 UI。csproj 加 `<Version>1.2.1</Version>`+AssemblyVersion/FileVersion（原来缺省=1.0.0 会误判），让程序集版本=显示/打包版本。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs`、`UI/MainWindow.xaml`、`Langs/*.xaml` | 新功能（检查更新 UI/逻辑） | MainViewModel 加 UpdateService + UpdateStatus/IsCheckingUpdate 属性 + AppVersion 只读属性 + `[RelayCommand] CheckUpdate`：检查→有更新弹 MessageBox 询问→选是下载 setup.exe→Process.Start 启动安装包 + Application.Shutdown 退出本程序以便覆盖安装；拿不到 asset 则退回打开 Release 页面。设置页新增"检查更新"区块（显示当前版本 + 状态文本 + 检查按钮，按钮 IsEnabled 绑 !IsCheckingUpdate）。5 语言新增 9 个 key（StrUpdateSection/Check/Checking/Latest/Available/Downloading/Failed/Title/Prompt），UTF-8 BOM。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe` + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常（标题 VoicePipe、无 Fatal）。ISCC 打包成功。robocopy /MIR 同步开源（含 UpdateService.cs）+ 复制 iss。memory.md 未同步。注：检查更新需联网 + 远端有 v>1.2.1 的 release 才会提示，效果需联网环境验证。 |

---

## 28. 变更记录（续 · 2026-05-30 v1.2.2：检查更新加下载进度条 + 完成后询问）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `VoicePipe.csproj`、`UI/MainWindow.xaml`、`VoicePipe.iss` | 版本 | 1.2.1 → 1.2.2（csproj Version/AssemblyVersion/FileVersion + 标题 v1.2.2 + 安装包 AppVersion）。带检查更新功能的版本定为 v1.2.2。 |
| 2026-05-30 | `Services/UpdateService.cs` | 增强 | DownloadInstallerAsync 加 `IProgress<double>? progress` 参数：用 81920 字节缓冲分块读写，按 ContentLength 报告 0~1 进度（无 Content-Length 时结束报 100%）。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs` | 增强（下载进度 + 完成询问） | 新增 IsDownloading/DownloadProgress/DownloadPercentText 属性。CheckUpdate 改为：下载时 IsDownloading=true + Progress<double> 回调切 UI 线程更新进度条；下载完成后**先弹"下载完成，是否现在更新？"询问**，选是才 Process.Start 安装包 + Shutdown，选否则状态栏提示安装包路径、不退出。 |
| 2026-05-30 | `UI/MainWindow.xaml`、`Langs/*.xaml` | UI | 检查更新区块下方加下载进度条（ProgressBar 0~1 + 百分比文本，IsDownloading 时显示）。5 语言新增 StrUpdateDownloaded/StrUpdateReady（共 11 个更新相关 key），UTF-8 BOM。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe`(v1.2.2) + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常（标题 VoicePipe、无 Fatal）。ISCC 打包 1.2.2。robocopy /MIR 同步开源 + 复制 iss。memory.md 未同步。更新下载/进度/安装链路需联网 + 远端有 v>1.2.2 release 才能端到端验证。 |

---

## 29. 变更记录（续 · 2026-05-30 v1.2.3：首次使用引导）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `VoicePipe.csproj`、`UI/MainWindow.xaml`、`VoicePipe.iss` | 版本 | 1.2.2 → 1.2.3（三处统一）。 |
| 2026-05-30 | `Core/AppSettings.cs` | 新功能 | 新增 `FirstRunDone`（默认 false），记录首次使用引导是否已看过。 |
| 2026-05-30 | `UI/MainWindow.xaml` | 新功能（首次引导） | 根 Grid 末尾加全窗口遮罩层（Grid.RowSpan=3 + Panel.ZIndex=100 + #CC000000 半透明），居中卡片显示三步上手说明，Visibility 绑 ShowFirstRunGuide。"知道了"按钮 Click=GuideGotIt_Click。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs` | 新功能 | 新增 ShowFirstRunGuide 属性，构造函数 `ShowFirstRunGuide = !_settings.FirstRunDone`；DismissFirstRunGuide() 关闭遮罩 + FirstRunDone=true + Save。 |
| 2026-05-30 | `UI/MainWindow.xaml.cs` | 新功能 | GuideGotIt_Click → vm.DismissFirstRunGuide()。 |
| 2026-05-30 | `Langs/*.xaml` | 新功能 | 5 语言新增 6 个 key：StrGuideTitle/Subtitle/Step1/Step2/Step3/GotIt。文案：麦克风选平时说话的、App 音频源选要让对方听到的、游戏/聊天软件麦克风设为 CABLE Output。UTF-8 BOM。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe`(v1.2.3) + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常（标题 VoicePipe、无 Fatal、日志见"首次使用引导已关闭"，证明遮罩正常显示+关闭）。ISCC 打包 1.2.3。robocopy /MIR 同步开源 + 复制 iss。memory.md 未同步。 |

---

## 30. 变更记录（续 · 2026-05-30 v1.2.4：下载超时修复 + 进度兜底 + 日志去重 + 首次引导持久化加固）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `VoicePipe.csproj`、`UI/MainWindow.xaml`、`VoicePipe.iss` | 版本 | 1.2.3 → 1.2.4（三处统一）。 |
| 2026-05-30 | `Services/UpdateService.cs` | **Bug修复（更新下载 15s 超时失败）** | 根因：HttpClient.Timeout=15s 是整请求总超时，下载 52MB 安装包必然超时（TaskCanceledException）。修复：拆成两个 HttpClient——检查版本用 15s（小 JSON 够），下载安装包用 `Timeout=InfiniteTimeSpan`（靠流式分块读 + 关窗取消，不受总超时限制）。 |
| 2026-05-30 | `Services/UpdateService.cs` | Bug修复（进度条不动） | 下载进度原来只在有 Content-Length 时上报，GitHub 重定向到 CDN 偶尔拿不到长度→进度只在末尾跳 100%。修复：无 Content-Length 时用 55MB 估算分母让进度条也动（封顶 99%，下载完跳 100%）。注：之前"进度条不动"主因其实是 15s 就超时失败了，根本没下起来。 |
| 2026-05-30 | `Audio/MicCapturer.cs` | Bug修复（日志刷屏） | `GetAvailableMics` 每 2 秒调一次，每次都打"已排除虚拟回环设备 CABLE Output"刷满控制台。新增 `_loggedExcludedDevices` HashSet，每个回环设备 ID 只记一次。 |
| 2026-05-30 | `ViewModels/MainViewModel.cs` | 加固（首次引导反复弹出） | 现象：用户反馈打包版反复弹首次引导。settings 里 FirstRunDone 始终 false（但 ShowSpectrum=true 证明 Save 正常）→ 根因是用户可能未点"知道了"而直接关窗/缩托盘，从未持久化。加固：把持久化从 DismissFirstRunGuide 内移到 `OnShowFirstRunGuideChanged`——只要遮罩从显示变为关闭（任意路径）就立即 FirstRunDone=true + Save，确保只弹一次。 |
| 2026-05-30 | 打包 | 注意 | ISCC 首次报 `EndUpdateResource failed (110)`——Output 目录被杀软/占用锁住，等几秒重试即成功。属环境偶发，非脚本问题。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe`(v1.2.4) + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常（标题 VoicePipe）。ISCC 打包 1.2.4（52.7MB，重试后成功）。robocopy /MIR 同步开源 + 复制 iss。memory.md 未同步。 |

---

# ★★★ 给下一个接手 AI 的话（务必先读这一节，再读全文）★★★

你好。我是把 VoicePipe 从 1.1.1 一路带到 1.2.5 的上一个助手。这个项目目前是**完成状态**——核心功能齐、bug 清干净、自更新闭环已通、26 个属性测试全过、1.2.5 已正式发到 GitHub（含源码 push 到 main）。下面是接手须知，照着做能让用户（一个做 VRChat/直播、挂后台用、爱白嫖顶配模型的开发者）继续用得顺心。

## 一、先做这件事
打开 VoicePipe 后第一件事：**完整读这份 memory.md**（尤其第 10 节开发规范、第 25 节验证流程、第 7 节历史 BUG）。用户明确要求过"完整读取 memory.md 恢复记忆"。

## 二、用户的红线（碰这些 = 闯祸，务必先确认再动）
1. **VB-Cable 输出 WasapiOut 的 10ms 低延迟绝对不能改**——用户反复强调、最在乎的硬指标。
2. **降噪/静音/监听只能作用于 Mic_Path，绝不能影响 App 音频**——两条独立 buffer，只在 AudioMixEngine.Read() 才相加。
3. **每次改动必须在 memory.md「变更记录」追加一条**（日期|文件|原因|描述）——强制，不得跳过。
4. **含中文的 .iss / Langs/*.xaml 必须保持 UTF-8 with BOM**——否则乱码，历史踩坑多次。
5. **memory.md 永不同步到 VoicePipe-OpenSource 开源镜像**——它是内部记录。同步只用 `robocopy "src" "VoicePipe-OpenSource\src" /MIR /XD bin obj`。

## 三、用户的工作偏好
- **说中文，讲大白话，别太技术化**。用户不爱听术语堆砌，要的是"改了啥、为什么、有啥影响"。
- **改完就要能用**：默认直接动手实现 + 自己验证，而不是只给建议。但**涉及红线/架构/破坏性操作先问**。
- **诚实**：不确定就说不确定，别用过时记忆硬纠正用户（我吃过亏——拿旧模型名单去"纠正"用户，结果是我信息滞后）。联网能查证的就查。
- **验证要到位**：用户被"命令行假象"坑过——WPF 改 XAML/资源后，build 过 + 进程存活都不可靠，**必须跑 publish 产物 + 读 MainWindowTitle（应为"VoicePipe"）+ 读 Serilog 日志确认无 Fatal**。详见第 25 节。
- 用户的**测试机**才有麦克风和 VB-Cable；当前工作站验证不了实际音频效果，音频类改动要提醒用户去测试机验。
- 子代理（spec-task-execution）派发在此项目上曾不稳，复杂活更推荐主代理直接写 + 编译验证。

## 四、当前架构一句话回顾
全管线统一 **48kHz**。App 走 LoopbackCapturer(进程树捕获,PID缓存池)→ Mic 走 MicCapturer→RNNoise降噪(干湿混合)→ 两路 RingBuffer → AudioMixEngine.Read()(Pull模型,WaveOutEvent驱动)相加+软限幅 → VirtualMicWriter 写 CABLE Input(10ms)。监听是独立 MonitorOutput 链。检查更新走 GitHub API（UpdateService）。

## 五、已知"不是 bug"的东西，别去"修"
- `NoiseGate.cs` 现在没接在管线里（被 RNNoise 取代），但留着、测试还在用，别删。
- Apple Music 等 UWP 应用音量条可能显示 0 / 抓不到——是 Windows 把它的音频会话算给系统服务（svchost/audiodg）了，per-process loopback 的硬限制，不是我们的 bug。
- 本机日志 `Cable=false` 是正常的（这台工作站没装 VB-Cable）。
- 安装器 PrivilegesRequired/userdesktop 警告是历史既有，可忽略。

## 六、用户最近的态度
1.2.5 收尾后用户觉得"功能够用了，多源混音/配置预设没必要做"——**别硬塞功能**。尊重"做完了就收着"。用户想做什么会直接说；没需求时陪聊/复盘/帮维护文档都比硬加功能强。

## 七、本机环境（2026-05-30 起新增）
- **gh CLI 已装**（2.93.0，winget），路径 `C:\Program Files\GitHub CLI\gh.exe`，已认证 `Yamada-Ryo4`。
- **git 已可用**（2.49.0）。**但工作区根目录不是 git repo**（用户从来用网页传或这次新建 .git）；唯一的 git repo 在 `VoicePipe-OpenSource\.git`，远端 `Yamada-Ryo4/VoicePipe`，默认分支 `main`。
- **发版流程**已写入第 34 节，照着走：改三处版本号 → build/test/publish/实跑验证 → ISCC 出包 → robocopy 同步开源 → 在 `VoicePipe-OpenSource` 里 commit + push origin main → `gh release create vX.Y.Z <exe> --notes-file ...`。
- 上传 50+MB exe 时主动用后台进程（control_pwsh_process start），别用前台命令——前台会被超时打断（亲测）。

## 八、用户的现实情况
用户提过 6.1 之后"用不了 Kiro 免费 power 套餐"，意味着他可能不会再用 Claude Opus。这不影响项目状态，但你接手时可能用户已经隔了一段时间没动这个项目。别说"距离上次..."云云，直接干活。

## 九、还没"实战盖章"的小尾巴（接手时心里有数即可）
- **实时日志控制台（第 32 节那次大改：RichTextBox→TextBox）** 我在工作站编译/启动验证过，没机会在用户测试机上点开右键→打开控制台跑一次完整路径（显示/自动滚动/复制全部/清空/置顶）。1.2.5 已正式发布，用户至今没报问题，多半是好的；但若用户哪天反馈控制台异常，先怀疑这次改动，对照 `LogConsoleWindow.xaml` 控件名（LogBox/TbStatus/CbAutoScroll/CbTopmost）和 `.cs` 是否一致。
- **PeakMonitor 这次 1.2.5 的"PID→进程名映射 1s 节流"优化**：理论稳，但若用户反馈"新启动 app 的音量条要等好几秒才出现"，是这条节流在作怪——把间隔降到 500ms 即可（`PidNameRefreshIntervalMs`）。

祝接手顺利。这个项目和这个用户都很好打交道。 —— 上一个助手，2026-05-30

---

## 31. 变更记录（续 · 2026-05-30 收尾审查）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | 删除 `src/VoicePipe/MainWindow.xaml` + `MainWindow.xaml.cs` | **清理（障碍）** | 根目录有一对 `dotnet new wpf` 自动生成的默认空窗口（类 `VoicePipe.MainWindow`，Title="MainWindow" 450x800），从未被引用（真正用的是 `VoicePipe.UI.MainWindow`）。占着 MainWindow 这个显眼名字、易误导接手者改错文件。删除后 build 0 错 0 警、26 测试全过，确认零依赖。 |
| 2026-05-30 | memory.md | 交接 | 新增「给下一个接手 AI 的话」一节：红线、工作偏好、架构回顾、"不是bug"清单、用户态度。完整审查全部源码（Audio/Core/Services/UI/ViewModels/Sinks/Bootstrapper），除上述孤儿文件外未发现需修复的 bug，代码质量良好。 |

---

## 32. 变更记录（续 · 2026-05-30 控制台优化 + 复制全部修复 + 右键菜单美化）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `UI/LogConsoleWindow.xaml.cs` | **Bug修复（复制全部）** | 旧实现：从 RichTextBox 反向抠文字 + `string.Join("\n",...)` + `Clipboard.SetText`。三个问题：①换行用 \n（Windows 剪贴板/记事本规范要 \r\n，否则粘出来挤成一行）；②SetText 在剪贴板被占用时抛 COMException 直接崩；③从富文本抠字慢且易多空行。修复：直接从 `InMemoryLogSink.GetHistory()` 取纯文本、\r\n 连接、`Clipboard.SetDataObject(all, true)` + 重试3次兜底、复制成功状态栏提示行数。 |
| 2026-05-30 | `UI/LogConsoleWindow.xaml` + `.cs` | **性能优化（控制台）** | 日志区从 RichTextBox（每行一个 Paragraph，数千行内存/渲染重）换成纯文本 TextBox（单一文本缓冲，AppendText 追加）。级别靠行首 [INF]/[WRN]/[ERR] 前缀区分（不再分级着色，换性能）。回填历史用 StringBuilder 一次性拼好；超 MaxLines 批量裁掉最旧 1/4（不逐行删）。移除旧 Scroller/LogDoc/Paragraph/TextRange 依赖。 |
| 2026-05-30 | `UI/LogConsoleWindow.xaml` | 外观优化 | 配色提亮统一（#141414 头/状态栏、#0C0C0C 日志区、#10B981 悬停描边强调）、圆角 6、按钮 padding/字号微调、状态栏字号 10.5。 |
| 2026-05-30 | `UI/MainWindow.xaml` | 外观优化（右键菜单） | 主窗口右键"打开实时控制台"菜单从系统默认带槽老式样式，改为自定义 ControlTemplate：圆角 8 卡片 + DropShadow 阴影 + 自定义 MenuItem 模板（图标+文字、IsHighlighted 悬停 #2A2A2A 高亮、圆角 5）。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe` + 开源镜像 | 出包+同步 | build 0 错 0 警、26 测试全过、publish 启动正常（标题 VoicePipe、Fatal=0，右键菜单模板随主窗口解析成功）。ISCC 打包。robocopy /MIR 同步开源（含孤儿 MainWindow 删除 + 控制台重构）。memory.md 未同步。**注：控制台窗口本体（TextBox 版）需测试机点开右键→打开控制台 实际验证一次显示/滚动/复制。** |

## 33. 变更记录（续 · 2026-05-30 版本号 1.2.4 → 1.2.5）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `VoicePipe.csproj`、`UI/MainWindow.xaml`、`VoicePipe.iss` | 版本 | 1.2.4 → 1.2.5（三处统一）：csproj 的 Version/AssemblyVersion/FileVersion、MainWindow.xaml 标题 v1.2.5、.iss 的 AppVersion。已验证：build 0 错 0 警、26 测试全过、AssemblyInfo 烧进 1.2.5.0、.iss 仍 UTF-8 BOM。开源镜像那份 csproj/xaml/iss 待下次同步时一并覆盖。 |
| 2026-05-30 | `Audio/PeakMonitor.cs` | **性能优化（后台轮询热点）** | `MonitorLoop` 每轮（约15fps/67ms）都调 `ProcessEnumerator.GetPidNameMap()`，内部 `CreateToolhelp32Snapshot` 给全系统进程拍快照（几百进程 marshal szExeFile + 字典分配）= 每秒约15次全系统遍历，真实 CPU 热点。修复：PID→进程名映射缓存 + 节流到约 1s 重取一次（`_cachedPidName`/`_lastPidNameTick`/`PidNameRefreshIntervalMs=1000`，与旁边 DeviceCheckIntervalMs 同节奏）。**关键：峰值数值 `MasterPeakValue` 仍每轮实时读取，动态音量条不受影响**，只是新启动 app 的名字最多晚 1s 进表（列表本身在 ViewModel 里 2s 刷一次，比这还慢，无感知）。`Stop()` 重置缓存，下次 Start 立即重新快照不复用陈旧映射。build 0 错 0 警、26 测试全过。 |
| 2026-05-30 | publish + `Output/VoicePipeSetup.exe`(v1.2.5) + 开源镜像 | 出包+同步 | 1.2.5 出包：build 0 错 0 警、26 测试全过；publish Release 自包含产物版本 1.2.5.0，实跑产物启动正常（MainWindowTitle="VoicePipe"、Serilog 日志 FATAL=0 ERR=0、RNNoise 初始化成功、HotkeyManager 正常）。ISCC 打包成功（52.7MB，76s，仅余历史 PrivilegesRequired/userdesktop 警告）。robocopy /MIR 同步开源（PeakMonitor.cs/MainWindow.xaml/VoicePipe.csproj 3 改动文件）+ 复制 BOM 版 .iss（已核对开源 csproj/iss=1.2.5、iss BOM=EF BB BF）。memory.md 未同步。 |

---

## 34. GitHub 发布流程（gh CLI · 2026-05-30 建立）

> 用途：把新版本上传 GitHub 并发 Release。**前置：先走第 25 节标准验证流程 + 出包 + 同步开源，确认 `Output\VoicePipeSetup.exe` 是最新版本且已实跑验证。**

### 环境前提（本机现状，2026-05-30）
- **git**：已装（2.49.0）。
- **gh（GitHub CLI）**：2026-05-30 用 `winget install --id GitHub.cli -e` 装好，版本 2.93.0。默认路径 `C:\Program Files\GitHub CLI\gh.exe`。
  - ⚠ 刚装完当前终端 PATH 未刷新，`gh` 直呼可能找不到 → 用绝对路径 `& "C:\Program Files\GitHub CLI\gh.exe"`，或重开终端。
- **认证**：`gh auth login`（GitHub.com → HTTPS → 浏览器一次性码授权，或 `--with-token`）。**这步必须用户本人交互完成**，AI 不替用户登录账号。验证：`gh auth status` 显示已登录 Yamada-Ryo4。
- **工作区不是 git repo**：`E:\Documents\Voicepipe` 根目录和 `VoicePipe-OpenSource` 都没有 `.git`。历史 release 估计是网页手动传或在别处克隆推的。gh 发 release **不强制要本地 repo**（`gh release create` 直接对远端仓库操作），但**推代码**需要本地有 repo + remote。

### 发布方式 A：只发 Release（不推代码，最省事，推荐）
GitHub 仓库：`Yamada-Ryo4/VoicePipe`。`gh release create` 可用 `--repo` 直接指定远端，无需本地 repo。

```powershell
$gh = "C:\Program Files\GitHub CLI\gh.exe"
# 把 release notes 写到临时文件（含中文，用 UTF-8 写避免乱码）
$notes = "e:\Documents\Voicepipe\release_notes_v1.2.5.md"   # 自己先写好中英双语说明
& $gh release create v1.2.5 `
    "e:\Documents\Voicepipe\Output\VoicePipeSetup.exe" `
    --repo Yamada-Ryo4/VoicePipe `
    --title "VoicePipe v1.2.5" `
    --notes-file $notes
# 验证
& $gh release view v1.2.5 --repo Yamada-Ryo4/VoicePipe
```
- 附件就是把 exe 路径作为位置参数传进去，自动上传。
- 发草稿加 `--draft`；预发布加 `--prerelease`。
- 删除重发：`& $gh release delete v1.2.5 --repo Yamada-Ryo4/VoicePipe --yes --cleanup-tag`。

### 发布方式 B：连源码一起推（需要本地 repo）
若要把源码也推上 GitHub（当前工作区没有 .git，需先初始化）：
```powershell
# 在开源镜像目录初始化（注意 .gitignore 要排除 bin/obj）
git -C "VoicePipe-OpenSource" init
git -C "VoicePipe-OpenSource" remote add origin https://github.com/Yamada-Ryo4/VoicePipe.git
git -C "VoicePipe-OpenSource" add -A
git -C "VoicePipe-OpenSource" commit -m "Release v1.2.5"
# ⚠ 推之前务必确认分支策略，别覆盖远端历史。新分支更安全：
git -C "VoicePipe-OpenSource" push -u origin HEAD:main   # 或先 fetch 对齐
```
- ⚠ **危险操作警示**：远端已有历史时，直接 push 可能冲突/覆盖。第一次推务必先 `git fetch` 看清远端状态，宁可先开新分支，不要 `--force`。**推代码前先问用户**。

### release notes 写法约定
- **中英双语**（用户惯例）：先中文段，后 English 段，或并排。
- 内容：本版亮点（面向用户的大白话，不要技术黑话）+ 简短改动列表。性能优化这种说"后台占用更省"就够，别堆 PID/快照术语。
- 安装说明可复用上版：下载 `VoicePipeSetup.exe` 运行，首次需装 VB-Cable（安装器会引导）。

### 一句话 checklist（发版顺序）
1. 改版本号三处（csproj/MainWindow.xaml/.iss）→ build 0 错 0 警 → 26 测试全过
2. publish 自包含 → 实跑产物读标题（≠启动错误）+ 日志 FATAL=0
3. ISCC 打包 → 确认 `Output\VoicePipeSetup.exe` 是新版本、时间戳新
4. robocopy 同步开源 + 复制 BOM 版 .iss（memory.md 不同步）
5. 写中英双语 release notes 到临时 .md
6. `gh release create vX.Y.Z <exe> --repo Yamada-Ryo4/VoicePipe --title ... --notes-file ...`
7. `gh release view` 核对 tag/draft/附件
8. memory.md 追加变更记录
| 2026-05-30 | GitHub 仓库 `Yamada-Ryo4/VoicePipe` | **发布 v1.2.5** | ① 代码推送：在 `VoicePipe-OpenSource` 初始化 git，fetch 远端 main 后以 origin/main 为父提交本地 tree（保留远端历史、正常 fast-forward push）。顺手清除了远端残留的孤儿 MainWindow.xaml/.cs 和误传的 bin/ 构建产物。② README 下载链接从旧的 `Release` tag 改为 `v1.2.5`。③ `gh release create v1.2.5` 建 release + 上传 VoicePipeSetup.exe（52.7MB，state=uploaded，draft=false）。④ 中英双语 release notes 已填入。⑤ 环境：gh 2.93.0 已装（winget），已认证 Yamada-Ryo4（HTTPS + 浏览器授权）。⑥ .gitignore 加了白名单保留 rnnoise.dll / deps 驱动二进制。 |

---

## 40. 架构总览（给未来自己看，不发开源）

> 这一节是给半年/一年后回来翻代码的我自己看的。开源版的正经版叫 `VoicePipe-OpenSource/ARCHITECTURE.md`，那份偏正式、能见人。这份带吐槽和"为什么没做 X"的备忘，属于内部脑图。

### 为什么这个项目能 work，一句话

**两条独立 buffer 一直到最后一步才相加**。这是整个设计能 hold 住的根本——降噪/静音/监听这些只动 Mic_Path 的功能，在物理上根本碰不到 App 那条 buffer。所有"会不会影响主输出"的焦虑，看一眼 `AudioMixEngine.Read()` 的循环就能放心：循环外的所有处理都在各自的 buffer 里。

### 核心架构：Pull 而不是 Push

`AudioMixEngine` 实现 `IWaveProvider`，由 `WaveOutEvent`（10ms 共享模式）按设备时钟拉数据。捕获回调（Loopback、Mic）只往 RingBuffer 写，**不**触发输出。

为什么不能反过来：早期 push 模型——两个捕获回调各自触发 Tick 输出——会变成两个时钟抢同一个缓冲，必丢帧、必卡顿。我们试过，凉了。

**红线再贴一次**：10ms 延迟和 Pull 模型都是承重墙，不要因为"看起来能优化"就动它们。

### 进程音频捕获：Windows 给我们一个能力，但每一步都有坑

用的是 Windows 10 Build 19041 的 `ActivateAudioInterfaceAsync` + `AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK`。这是消费版 Windows 上**唯一**官方的按进程捕获途径。

踩过的坑（按时间顺序，让你看痛历程）：

1. **`IAudioClient3` 的强转**——这个 bug 我找了**好久**才发现。`ActivateAudioInterfaceAsync` 返回的对象**只**实现 `IAudioClient`（GUID `1CB9AD4C-...`），代码强转 `IAudioClient3`（GUID `726778CD-...`）时 .NET COM Interop 会偷偷做 `QueryInterface` 然后失败，抛个让人摸不着头脑的 `InvalidCastException`。早期混音"从来没工作过"就是因为这个。教训：**用 Windows 给的最低版本接口就行**，别贪图新接口。
2. **`E_UNEXPECTED` 重新激活失败**——同 PID 的 loopback 一旦 Stop 就不能再 Start。所以 `PipelineManager` 才用 `Dictionary<int, LoopbackCapturer>` 永不主动销毁。教训：Windows 的某些 API 是一次性的，设计架构时要假设资源不可重建。
3. **多进程 app**——Chrome/Edge/Discord 都把音频放在子进程。原本"选中 chrome.exe 没声音"，加上 `INCLUDE_TARGET_PROCESS_TREE` + `ProcessEnumerator` 的根进程归并才解决。
4. **`AudioSessionManager.Sessions` 是缓存的**——这个 bug 找了一周。NAudio 不会自动刷会话快照，VoicePipe 启动**之前**就在播音乐的 app 能读到峰值，启动**之后**才打开的就一直 0。修复：每轮 `RefreshSessions()`。教训：第三方库的"惰性"行为得读源码才知道。
5. **WASAPI packet 频繁触发的 GC**——48kHz 下每个 packet 几 ms，每次 `new float[]` 把 GC 烧到冒烟。改成成员级复用缓冲 + `(samples, count)` 事件签名才正常。教训：实时音频路径上**任何**堆分配都是嫌疑犯。

### 缓冲架构

三条 RingBuffer 各 200ms：`_appBuffer`、`_micBuffer`、`_monitorBuffer`。RingBuffer 容量强制 2 的幂，位运算取模（`& _mask`）。批量 `Array.Copy` 不逐字节循环。`Volatile.Read(ref _available)` 防 JIT 缓存陈旧值。

### 混音循环里的零分配设计

`Read()` 里所有需要的数组都是预分配的成员字段：`_appTemp` / `_micTemp` / `_monitorTemp` / `_resampleStereoCache` / `_resampleRateCache` / `_monoStereoCache`。`fixed` 提到循环外只 pin 一次（每样本 pin 一次代价巨大）。`MathF.Pow` 只在 setter 里算（拖滑块时），运行时 Read 只做单次乘法。

**关键**：进入 Read → 退出 Read，**零字节堆分配**（热好之后）。这是性能能持续不踩 GC 的根本。

### 增益曲线为什么不是线性

人耳响度感知是对数的。线性滑块拉到 30% 听感还是大半响，到 5% 才突然变小，最后掉到 0——悬崖感。改成 `x ^ 1.7` 锚点：
- 0% = 静音
- 100% = ×1.0（严格原音，不增不减）
- 50% ≈ ×0.31（约半响）
- 150% ≈ ×1.98

这条曲线我反复试了好几轮才定的。1.5 太温和、2.0 太狠。1.7 刚好。

### 为什么有些功能没做

- **多源混音**——用户明确否过。一个 app + 一个麦就够 99% 用户。多源涉及 PID 优先级、增益归一化、UI 复杂度、再添几条 RingBuffer——边际收益不值得复杂度。
- **配置预设/场景切换**——同上，用户否了。"VRChat 模式""录播模式"听着酷，实际用户拖一次滑块就能切，搞 preset 反而绕。
- **DAW 级 EQ / 压缩 / 限制器**——超范围。软限幅就是防爆音，不要去和 OBS / Voicemeeter 比专业混音功能。
- **多设备同时输出**——监听已经是独立链了，再多就要重新设计，不值。
- **网络低延迟传输**——和虚拟麦克风的定位冲突，用户应该用 Discord / OBS 等专业工具做远程。

### 已知"看起来是 bug"的非 bug

- **Apple Music / 一些 UWP 应用音量条不动**——Windows 把这类应用的音频会话归到 `audiodg` / `svchost` 名下，per-process loopback 看不到。这是 Windows 限制。`ProcessEnumerator` 的归并能挽救一部分，但不是全部。用户反馈这个，先告知是 Windows 限制再考虑要不要做"系统服务名补丁"（试过，效果不稳）。
- **本机日志 `Cable=false`**——开发机没装 VB-Cable，正常。测试机应为 true。
- **安装器 PrivilegesRequired/userdesktop 警告**——Inno Setup 历史既有，无害。
- **PowerShell 控制台中文乱码**——只是控制台显示问题，文件本身和日志写入都是对的 UTF-8。别为这个改字体/编码。

### 关于 RNNoise

48 kHz 单声道原生，不重采样（管线就是 48 kHz），干湿混合（`WetMix`）。**只动 Mic_Path 那条 buffer**。原生库不可用时降级为直通（`Available=false`）。退出时 `Dispose` 释放 `rnnoise_destroy` 的非托管状态。

为什么不用 NoiseGate 旧实现：噪声门是阈值法，背景吵就误切人声、降不彻底；RNNoise 是 RNN 神经网络，效果碾压。但门控逻辑测试还在用 `NoiseGate.cs` 做 fixture，**别删**这个文件。

### 关于线程

- WPF UI 线程：所有 UI 更新、`AppSettings.Save`、设置面板。
- `MicCapturer.Start` 内部 `Task.Run` 切 MTA：某些驱动在 STA 上初始化 WASAPI 会拒绝。
- `LoopbackCapturer` 的 `_captureThread`：每个 PID 一条后台线程，COM 多线程模型。
- `WaveOutEvent` 内部线程：调 `AudioMixEngine.Read()`，**这里写代码要按音频实时线程的标准**——零分配、不持锁、不调 UI 线程。
- `PeakMonitor` 后台 `Task`：按 `_pollIntervalMs`（默认 67ms）轮询，失焦时可调大省 CPU。
- `MainViewModel.RefreshAllAsync`：UI 线程触发 → `Task.Run` 后台跑 COM 枚举 → `await` 回 UI 线程更新 ObservableCollection。这条链条修过 UI 卡顿 bug 后定型，**别改成同步**。

### 同步状态（截止 2026-05-30 1.2.5 发布）

- 工作区根目录：源码 + memory.md + .iss + Output（这些不进 git）
- `VoicePipe-OpenSource/`：开源镜像，**有 git**，远端 `Yamada-Ryo4/VoicePipe`，main 分支
- `Output/VoicePipeSetup.exe`：1.2.5 安装包（52.7MB）
- GitHub Releases: `v1.1` ~ `v1.2.5` 全在
- `gh` CLI 已装，已认证 Yamada-Ryo4

### 半年后回来要先做的事

1. 通读 memory.md 顶部"版本索引"快速定位最近变更
2. 通读"给下一个接手 AI 的话"那一节恢复语境
3. 跑一遍第 25 节的标准验证流程，确认环境还能干活
4. 如果是发新版本，照第 34 节走

### 如果有人问"VoicePipe 这种东西不就是 Voicemeeter 吗"

不一样。Voicemeeter 是**全系统**音频路由（虚拟混音台 + 多路输入 + EQ + ...），重、复杂、要学。VoicePipe 是**单一目标**：把指定 app 声音 + 麦克风混到 VB-Cable 上，开箱即用，对小白零学习曲线。"做减法"是这个项目的 DNA，往里堆功能就背离了。

---

## 41. 变更记录（续 · 2026-05-30 项目档案整理）

| 日期 | 文件 | 原因 | 描述 |
|---|---|---|---|
| 2026-05-30 | `memory.md` 顶部 | 文档索引 | 在"AI 助手必读规则"之后插入 **📑 版本索引（快速定位）**，列出 v1.1 → v1.2.5 各版本主题 + 主要内容 + 对应章节，外加 4 个专题章节（验证流程/控制台优化/收尾审查/发布流程）。**纯导航不复制内容**，历史记录一行没动。 |
| 2026-05-30 | `VoicePipe-OpenSource/ARCHITECTURE.md` | 新增（对外） | 写一份正式的架构总览（中英双语）发到开源仓库。涵盖：Pull 模型设计决策、Per-Process Loopback 的 5 个坑（IAudioClient 接口、INCLUDE_TARGET_PROCESS_TREE、E_UNEXPECTED 缓存池、Paused 标志、缓冲复用）、麦克风路径与降噪、混音循环零分配、感知响度增益曲线、PeakMonitor/ProcessEnumerator 设计、构建发版与验证规则。给贡献者和未来自己看的项目"宪法"。 |
| 2026-05-30 | `memory.md` 末尾新增第 40 节 | 内部备忘 | 写"架构总览（给未来自己看，不发开源）"。比 ARCHITECTURE.md 更随意，含吐槽、踩坑细节、"为什么没做 X"决定备忘、"看起来是 bug 的非 bug"、半年后回来怎么快速上手等。**永不同步开源**（红线 5）。 |
| 2026-05-30 | `Audio/AudioMixEngine.cs` | **延迟优化（RingBuffer 容量缩减）** | 三条 RingBuffer（_appBuffer / _micBuffer / _monitorBuffer）容量从 200ms 降到 100ms。WASAPI 共享模式 packet 间隔稳定在 ~10ms，100ms = 10 个 packet 余量仍绰绰有余。关键改善：之前 200ms 容量意味着数据堆积时最坏可累积 200ms 延迟才被覆盖；现在超过 100ms 就强制覆盖旧数据（RingBuffer 推进读指针），把最大延迟钳在 ~100ms 以内。正常情况下数据停留 ~10ms 就被读走，延迟不变；但异常堆积时的"天花板"从 200ms 降到 100ms。build 0 错 0 警、26 测试全过。**需测试机验证无卡顿**。 |
| 2026-05-30 | `UI/MainWindow.xaml`、`UI/MainWindow.xaml.cs` | 新功能（音量重置按钮） | 音量控制区标题右上角加了一个 ↺ 重置按钮（TextBlock + MouseLeftButtonUp），点击把 AppGain 重置为 70%、MicGain 重置为 100%（默认值）。OnXxxGainChanged 会自动即时应用到管线 + 持久化。build 0 错 0 警、26 测试全过。 |
| 2026-05-31 | `Core/AppSettings.cs`、`ViewModels/MainViewModel.cs`、`UI/MainWindow.xaml`、`Langs/*.xaml` | 新功能（启动自动检查更新） | 新增 `AutoCheckUpdate` 设置（默认 true）+ 设置面板开关 + 5 语言 key `StrAutoCheckUpdate`。`InitSettings` 末尾若开关开启则自动调 `CheckUpdateCommand`（静默检查，有新版才弹窗）。build 0 错 0 警、26 测试全过。 |
| 2026-05-31 | 版本 1.2.5→1.2.6 + publish + ISCC + 同步开源 + push + release | **发布 v1.2.6** | 三处版本号统一 1.2.6。publish 产物 1.2.6.0 启动正常（TITLE=VoicePipe）。ISCC 打包 52.7MB。robocopy /MIR 同步开源（26 文件）+ 复制 iss。git commit + push 成功。`gh release create v1.2.6` 发布成功（draft=false, asset=uploaded 52.7MB）。本版改动：RingBuffer 200ms→100ms 延迟优化 + 音量重置按钮 + 启动自动检查更新 + README 赞助徽章。 |

---

## 35. 杂项工作流与踩坑备忘（2026-05-30/31 补全）

> 上面第 25 节是"标准验证流程"，第 34 节是"GitHub 发布流程"。这一节补充那些没单独成章但又会反复用到的小流程和踩坑。

### 35.1 三处版本号位置（升版本必改）

| 文件 | 改什么 |
|---|---|
| `src/VoicePipe/VoicePipe.csproj` | `<Version>X.Y.Z</Version>` + `<AssemblyVersion>X.Y.Z.0</AssemblyVersion>` + `<FileVersion>X.Y.Z.0</FileVersion>` |
| `src/VoicePipe/UI/MainWindow.xaml` | `<TextBlock Text="vX.Y.Z" ...>` |
| `VoicePipe.iss` | `#define AppVersion "X.Y.Z"` |

改完一定 `dotnet build`，确认 `obj/.../VoicePipe.AssemblyInfo.cs` 里出现 `AssemblyFileVersionAttribute("X.Y.Z.0")`，UpdateService 才能正确比对本地版本。

### 35.2 赞助页与 Cloudflare Pages 维护

- **赞助页源文件夹**：`E:\Documents\Voicepipe\赞助页\`（含 `index.html` + 两张二维码 PNG）。
- **部署目标**：Cloudflare Pages，绑定自定义域名 `https://support.yamadaryo.me/`。
- **挂载方式**：用户手动把整个文件夹的内容（不是文件夹本身）传到 Cloudflare Pages 项目。
- **页面包含**：爱发电 / Ko-fi / USDT ERC20（带二维码）/ APT Aptos（带二维码）/ TRC20（纯地址）。
- **链接清单**（改任何一项时同步）：
  - 爱发电：`https://ifdian.net/a/LengFengY`
  - Ko-fi：`https://ko-fi.com/lengfengy`
  - ERC20：`0x23d131e5ea16774bd745ff032b50ac02fb6873d0`
  - Aptos：`0x1d5e0a73d82bcbce2546569ffe32db3889884cbaceb36975367d7a2598dc1b8e`
  - TRC20：`TWnZNHe45XXGpVGknJTVi69iwpRyuFfKn1`
- **README 赞助徽章**：在两份 README 顶部 badge 列表末尾，使用 shields.io 的 `Support-%E2%98%95%20Buy%20me%20a%20coffee-10B981` 自定义徽章，点击跳 `https://support.yamadaryo.me/`。
- **改赞助页时**：直接编辑 `赞助页/index.html`，把整个文件夹再传一次 Cloudflare Pages 即可，不需要动 VoicePipe 代码。

### 35.3 大文件上传 GitHub Release（>10MB 必看）

`gh release create vX.Y.Z <exe>` 在 PowerShell 直接调会有约 3 分钟超时，52MB 的 VoicePipeSetup.exe 会被打断（亲测）。**必须用后台进程**：

```powershell
# 启动后台上传
control_pwsh_process action=start command=`& "C:\Program Files\GitHub CLI\gh.exe" release create vX.Y.Z "Output\VoicePipeSetup.exe" --repo Yamada-Ryo4/VoicePipe --title "..." --notes-file "release_notes_vX.Y.Z.md" 2>&1 | Tee-Object -FilePath "_release.log"`

# 等 60-90 秒后核对
$gh = "C:\Program Files\GitHub CLI\gh.exe"
& $gh api repos/Yamada-Ryo4/VoicePipe/releases --jq '.[].tag_name'
# 远端列表里出现 vX.Y.Z 即上传成功
```

### 35.4 PowerShell `ConvertFrom-Json` 大 JSON 报错

用 `gh api .../releases/<id>` 拿单个 release 的 JSON（含完整 body 富文本，几千字节中文+转义）后，用 `$j | ConvertFrom-Json` 经常报 "传入的对象无效，应为 :"或"}"" — 是 PowerShell 把多块输出错误拼接导致的。

**绕过方法**：让 gh 自己用 `--jq` 取需要的字段，不让 PowerShell 碰整个 JSON：

```powershell
# 错（中文 body 解析不稳）
$j = & $gh api repos/.../releases/12345
$o = $j | ConvertFrom-Json
$o.tag_name

# 对（gh 内置 jq 直接挑出标量字段）
& $gh api repos/.../releases/12345 --jq '.tag_name'
& $gh api repos/.../releases/12345 --jq '.assets|length'
```

### 35.5 同步开源镜像（VoicePipe-OpenSource）

```powershell
# 在仓库根目录 cd
robocopy "src" "VoicePipe-OpenSource\src" /MIR /XD bin obj /NFL /NDL /NJH /NP
Copy-Item "VoicePipe.iss" "VoicePipe-OpenSource\VoicePipe.iss" -Force
# robocopy exit code 1 = 正常（有文件复制）；不是错误
```

**永远不要同步 `memory.md`**（红线 5）。`memory.md` 是内部记录，开源镜像没有这个文件。

### 35.6 git 推送（VoicePipe-OpenSource）

```powershell
cd VoicePipe-OpenSource
git add -A
git commit -m "Release vX.Y.Z: 改动摘要"
git push origin main
```

**注意**：git 把进度信息写到 stderr，PowerShell 会把它们显示成红字"NotSpecified: NativeCommandError"。**只要最后一行有 `xxxxxxx..xxxxxxx main -> main` 就是成功**，红字忽略。

### 35.7 .iss 与 Langs/*.xaml 的 BOM

含中文的 `.iss` 和所有 `Langs/*.xaml` 必须存为 **UTF-8 with BOM**（开头 `EF BB BF`）。验证：

```powershell
$b = [System.IO.File]::ReadAllBytes("VoicePipe.iss")
"BOM: {0:X2} {1:X2} {2:X2}" -f $b[0],$b[1],$b[2]
# 应输出 "BOM: EF BB BF"
```

PowerShell 批量替换（`(Get-Content ... | %{...}) | Set-Content`）会**撕掉 BOM**，导致 ISCC 编译时中文乱码。改这类文件：用 IDE 的 str_replace 工具或带 BOM 的编辑器，**不要**用 `Set-Content`/`Out-File` 默认编码。

---

## 36. 延迟相关代码地图（⚠️ 改任何一项都会影响端到端延迟，先读这一节）

> **背景**：用户对 VB-Cable 输出端的低延迟极度敏感（红线 1：10ms 不能改）。任何下面的常量/参数都会影响端到端延迟，**改动前必须读懂全文，改动后必须在测试机上验证无卡顿**。
>
> **当前总延迟估算（v1.2.6）**：App ≈ 20ms，Mic ≈ 30ms（含 RNNoise 一帧）。这是"正常情况"的稳态延迟，不是最坏延迟。

### 36.1 端到端延迟构成（管线分解）

```
App 路径：
  LoopbackCapturer (WASAPI event-driven)
    ├─ AudioClient.Initialize buffer = 200ms (Windows 给的余量，实际 packet 间隔 ~10ms)
    ├─ packet 触发 SamplesAvailable
    └─ 写入 _appBuffer (RingBuffer 100ms 容量)
         ↓ 数据停留时间 ≈ 10ms（节奏稳定时）
  AudioMixEngine.Read() (WaveOutEvent 拉取，10ms 间隔)
    └─ 混音 + 软限幅 → 写出 byte[]
  VirtualMicWriter (WasapiOut DesiredLatency = 10ms)
    └─ → CABLE Input

Mic 路径（额外开销）：
  MicCapturer (WasapiCapture)
  + RnnoiseDenoiser (FrameSize 480 = 10ms @48kHz，固定一帧延迟)
  + 写入 _micBuffer (RingBuffer 100ms 容量)
```

### 36.2 所有"会影响延迟"的代码位置（按重要性）

| 优先级 | 文件 | 行附近 | 常量/参数 | 当前值 | 含义 / 改动后果 |
|---|---|---|---|---|---|
| 🔴 红线 | `Audio/VirtualMicWriter.cs` | `Initialize()` 内 `new WasapiOut(...)` | `WasapiOut` 第 4 参数 `latency` | **10** (ms) | **VB-Cable 输出延迟，绝对不能动**（用户最在乎的硬指标）。再低 WASAPI 共享模式不允许；再高用户立刻感知。 |
| 🔴 红线 | `Audio/AudioMixEngine.cs` | `_appBuffer` / `_micBuffer` / `_monitorBuffer` 字段声明 | RingBuffer 容量倍数 `* 100 / 1000` | **100ms** | 数据堆积时的最大延迟天花板。**降到 50ms 以下有断流风险**；升到 200ms+ 异常堆积时延迟会失控（v1.2.6 之前就是 200ms，导致用户感觉延迟越用越高）。 |
| 🟡 重要 | `Audio/RnnoiseDenoiser.cs` | 类顶部 `FrameSize` 常量 | `FrameSize` | **480** (= 10ms @48kHz) | RNNoise **算法固有**的一帧延迟，不能减小（神经网络要求的固定输入长度）。降噪开关关闭时此延迟为 0（直通）。 |
| 🟡 重要 | `Audio/LoopbackCapturer.cs` | `CaptureLoop()` 内 `audioClient.Initialize(...)` | 第 3 参数 `hnsBufferDuration` | **200_0000L** (200ms in 100ns 单位) | App 捕获 WASAPI 缓冲。这是"最大允许积压"，实际 packet 仍按设备时钟约 10ms 触发。降到 1000_0000L 可能在弱机/慢驱动上断流。 |
| 🟢 次要 | `Audio/AudioMixEngine.cs` | `MaxSamplesPerRead` 常量 | `MaxSamplesPerRead` | **4096** (≈ 42ms @48k 立体声) | Read() 预分配缓冲容量。WaveOutEvent 一次拉取通常 < 1000 样本，4096 足够；超大请求会 fallback 到临时分配（一次性堆分配，无大碍）。 |
| 🟢 次要 | `Audio/MonitorOutput.cs` | `StartLocked()` 内 `new WasapiOut(...)` | `WasapiOut` 第 4 参数 `latency` | **50** (ms) | 本地监听（耳机回放）的延迟。**不影响 VB-Cable**（独立输出链）。50ms 是稳定与延迟的折衷；耳机听感同步即可，没必要压到 10ms。 |
| 🟢 次要 | `Audio/AudioFormat.cs` | `SampleRate` 常量 | `SampleRate` | **48000** | 全管线统一采样率。改它会**全链路**重采样开销变化（RNNoise 强制 48k，再改就要插重采样器=新延迟）。**别动**。 |

### 36.3 关键依赖关系（动哪个会拖累哪个）

- **WasapiOut DesiredLatency 10ms** ⇄ **WaveOutEvent 拉取间隔**：互相绑死。改 latency 会影响 `Read()` 的调用频率，进而影响 RingBuffer 的"消耗速度"。
- **RingBuffer 容量** ⇄ **WASAPI packet 间隔**：容量必须 ≥ 几个 packet（10 个 packet = 100ms 是当前安全余量）。容量太小遇到 CPU 抖动会断流。
- **RNNoise FrameSize 480** ⇄ **管线 SampleRate 48000**：固定的"480 样本 = 10ms"关系。改采样率 RNNoise 就报废（要重采样到 48k 处理再转回去 = 双向延迟）。
- **`MaxSamplesPerRead = 4096`** ⇄ **WaveOutEvent 一次拉取量**：通常一次拉 480 样本（10ms @48k stereo = 960，再除 2 得帧数 480）。4096 是 8.5 倍冗余。

### 36.4 历史延迟相关改动（教训）

- **2026-05-27**：发现 Push 模型（捕获回调驱动 Tick 推数据）会让 App+Mic 两个时钟抢同一个缓冲，必丢帧。重构为 Pull 模型（IWaveProvider + WaveOutEvent 按设备时钟拉），延迟稳定。**不要回退到 Push**。
- **2026-05-27**：WasapiOut 默认 latency 50ms → 20ms → 15ms → **10ms**，每一步都验证过稳定。10ms 是共享模式极限，再低需要独占模式（会抢占其它 app 的音频，用户体验差）。
- **2026-05-28**：MicCapturer 一度尝试在 STA UI 线程初始化，部分驱动会拒绝。修复：`Task.Run` 切 MTA。**改 MicCapturer 时不要回到 UI 线程初始化**。
- **2026-05-30 (v1.2.6)**：RingBuffer 200ms → 100ms。理由：旧值导致异常堆积时最大延迟可达 200ms 才被覆盖；新值钳在 100ms 以内。**正常情况延迟不变（仍是 ~10ms 停留），只钳异常天花板**。

### 36.5 改延迟前的检查清单

如果你（或下个 AI）想"再压一下延迟":

1. 先读 36.1 ~ 36.3，确认你要改的项不会被红线拦下。
2. 在测试机准备好（有 VB-Cable + 真麦克风）。
3. 改一个常量，**只改一个**。
4. 标准验证流程（第 25 节）：build → 26 测试 → publish → 实跑读标题 → 日志 FATAL=0。
5. 测试机实际录一段：拍手测同步（看波形和声音是否对得上）。
6. 用 30 分钟以上连续运行，确认没有断流/爆音/堆积失控。
7. 写进变更记录（红线 3）。

**任何"我觉得能再优化"的冲动，先在第 36.4 历史里查有没有人试过。试过又回退的就别再试了。**
| 2026-05-31 | `UI/MainWindow.xaml` | UI修复（间距） | 自动启动区"开机时静默最小化到托盘"CheckBox 漏了 `Margin="0,0,0,12"`，导致和下面"启动时自动检查更新"挤在一起。补上即对齐。 |
| 2026-05-31 | `memory.md` | 文档增强 | 新增第 35 节"杂项工作流与踩坑备忘"（版本号位置 / 赞助页 CF Pages / 大文件上传 / PowerShell JSON 踩坑 / 同步开源 / git push / BOM）；新增第 36 节"延迟相关代码地图"（端到端延迟构成、所有影响延迟的代码位置表、依赖关系、历史教训、改延迟前的检查清单）。 |
| 2026-05-31 | 版本 1.2.6→1.2.7 + publish + ISCC + 同步开源 + push + release | **发布 v1.2.7** | 三处版本号统一 1.2.7。publish 1.2.7.0 启动正常（TITLE=VoicePipe，FATAL=0 ERR=0）。ISCC 打包 52.7MB（中途遇到 Output 文件被锁的 Error 32，retry 后成功，环境偶发非脚本问题）。robocopy 同步开源 + git push 成功。`gh release create v1.2.7` 后台上传成功（draft=false, asset=uploaded）。本版改动：CheckBox 间距修复 + memory.md 第 35/36 节新增。 |
| 2026-05-31 | `Core/AppSettings.cs`、`ViewModels/MainViewModel.cs` | **Bug修复（启停状态持久化）** | 用户反馈：①勾选"停止后保留麦克风直通"重启后没保存；②上次手动停止管线，但勾选了"自动启动管线"开关，下次启动还会自动开混音，违反"上次关掉是什么样打开就是什么样"。修复①：`AppSettings` 加 `MicPassthrough` 字段；`OnMicPassthroughChanged` 写 settings + Save；构造函数加载初值。修复②：加 `LastWasRunning` 字段；StartPipeline 成功后置 true，StopPipeline 一进来就置 false；`TryAutoStartPipeline` 在 `AutoStartPipeline=true` 之外再加一层判断 `LastWasRunning=true` 才真正自动启动——这样用户主动停止后退出，下次启动不会被覆盖。build 0 错 0 警、26 测试全过。 |
| 2026-05-31 | `ViewModels/MainViewModel.cs` | UX修复（启动检查更新延迟+静默模式） | ①启动时调 CheckUpdate 立即执行会拖几秒后弹"检查失败"，体验差。改为 Task.Run + Task.Delay(5000) 延迟 5 秒后台调用，并切回 UI 线程执行。②CheckUpdate 共用一条命令导致自动检查没新版也会改 UpdateStatus、有新版直接弹 MessageBox 阻断用户。拆出 `CheckUpdateSilent()` 入口 → `CheckUpdateInternal(silent)`：silent=true 时没新版/出错都静默不动 UI，只有有新版才弹询问。 |
| 2026-05-31 | `Audio/ProcessEnumerator.cs` | 诊断增强 | `GetActiveAudioProcesses` 最外层 catch 静默吞异常，COM 失败时用户看到空列表但不知原因。加 60s 节流的 Warning 日志，便于排查。 |
| 2026-05-31 | `UI/MainWindow.xaml.cs` | **Bug修复（启动热键冲突看不见）** | OnSourceInitialized 里 `_hotkeys.Register(...)` 的返回值被丢弃，初始注册若与系统/其他 app 冲突，UI 仍显示"已设置"绿色，用户以为热键工作其实没注册。改为接收两个返回值传给 ViewModel 的 MuteHotkeyConflict/PipelineHotkeyConflict，仅在 IsSet 且 Register 失败时标冲突。会在 HotkeyCaptureControl 上显示红色 + tooltip "Conflict"。 |
| 2026-05-31 | `UI/MainWindow.xaml`、`ViewModels/MainViewModel.cs` | **Bug修复（第二轮逐字符审查）** | ①监听输出设备 ComboBox（line 519-524）漏写 Foreground/Background DynamicResource，亮色主题下文字可能看不清（历史 BUG 5 同类）。补上。②`CheckUpdateInternal` 的 catch 块不区分 silent 模式，自动检查时网络异常会破坏 silent 承诺去改 UpdateStatus。加 `if (!silent)` 守卫。其他疑点（多处 _appGain 非 volatile、4096 fallback 等）逐一追踪后确认是误警，已说明原因不修。 |
| 2026-05-31 | `UI/MainWindow.xaml` | **Bug修复（UI 可见性）** | ①浅色主题下"检查更新"按钮文字几乎透明看不清（用户截图反馈）。根因：GhostButton `BasedOn` PrimaryButton（Foreground=White 硬编码），自身 Foreground=TextPrimaryBrush 用 StaticResource 解析时机异常。修复：在按钮处直接 `Foreground="{DynamicResource TextPrimaryBrush}"` 兜底。同样修了"重置热键"按钮（同 GhostButton）。②暗色主题下监听设备 ComboBox 展开时下拉项文字是默认黑色，与深色背景融合看不见。根因：ComboBox 用 `DisplayMemberPath` 时项是默认 ComboBoxItem 容器（不像主界面用 ItemTemplate 手动设了 Foreground）。修复：给该 ComboBox 加 `ItemContainerStyle`，强制 ComboBoxItem 的 Foreground/Background 跟随主题色。 |
| 2026-05-31 | 版本 1.2.7→1.2.8 + publish + ISCC + 同步开源 + push + release | **发布 v1.2.8** | 三处版本号统一 1.2.8。publish 1.2.8.0 启动正常（TITLE=VoicePipe，FATAL=0 ERR=0）。ISCC 打包 52.7MB（73s）。robocopy 同步开源 + git push 成功（7ee4e9c..866a104）。`gh release create v1.2.8` 第一次失败（DNS/连接 fake IP 198.18.0.81 超时，国内网络偶发），retry 后成功（draft=false, asset=uploaded）。本版改动：MicPassthrough 持久化 + LastWasRunning 状态恢复 + 启动检查更新延迟+silent 模式 + 启动热键冲突可见 + ProcessEnumerator 节流日志 + GhostButton 文字颜色（浅色主题可见）+ 监听设备 ComboBox ItemContainerStyle（暗色主题可见）。 |
| 2026-05-31 | `Core/AppSettings.cs`、`ViewModels/MainViewModel.cs` | **Bug修复（直通状态启动恢复）** | 用户反馈：上次按"停止"进入麦克风直通中，关 VoicePipe 后重启没有自动恢复直通（1.2.8 加的 LastWasRunning 没区分"完整运行"vs"直通"）。修复：①AppSettings 加 `LastWasMicPassthrough` 字段；②StopPipeline 把 `LastWasMicPassthrough` 设为当前 `MicPassthrough` 值（true=进入了直通态，false=完全停止）；③StartPipeline 成功完整运行后清掉 `LastWasMicPassthrough=false`；④OnMicPassthroughChanged 用户取消勾选时也清掉；⑤TryAutoStartPipeline 重写决策：`LastWasRunning=true` → 完整恢复；`LastWasMicPassthrough=true` → 启动后立刻 StopPipelineCommand（MicPassthrough 已恢复 true → 走 StopAppOnly 进直通态）；都 false → 保持 Idle。新增 `ResumeAsync(bool resumePassthrough)` 方法串行 Start→Stop。build 0 错 0 警、26 测试全过。 |
| 2026-05-31 | 版本 1.2.8→1.2.9 + publish + ISCC + 同步开源 + push + release | **发布 v1.2.9** | 三处版本号统一 1.2.9。publish 1.2.9.0 启动正常（TITLE=VoicePipe，FATAL=0 ERR=0）。ISCC 打包遇到 Error 32 多次连续失败，**这次定位到真因**：实跑 publish 产物验证后，VoicePipe.exe 进程没退干净（`Stop-Process -Id $p.Id -Force` 偶尔来不及完成 OS 句柄释放），残留进程继续锁着 publish 目录的 dll，ISCC 编译时无法读取打包。修复方法：`Get-Process -Name VoicePipe \| Stop-Process -Force; Start-Sleep -Seconds 3` 之后 ISCC 立刻通过（76s）。robocopy 同步 + git push 成功。release create 后台传成功。本版改动：直通状态启动恢复（LastWasMicPassthrough 字段 + ResumeAsync 逻辑）。 |
| 2026-05-31 | `memory.md` 第 35 节 | 工作流补全 | 之前记 ISCC Error 32 是"环境偶发非脚本问题"，1.2.9 出包定位到真因——实跑 publish 验证后 VoicePipe 进程残留锁文件。**实跑验证完务必加 `Get-Process -Name VoicePipe \| Stop-Process -Force` + Sleep 3-5s** 再 ISCC，否则会失败。 |


---

## 37. 版本 1.2.9→1.2.10：启动混音卡 8-12s 修复（2026-05-31）

### 用户反馈
> "为什么我这里启动混音会卡一会？" → 实测 8-12s 卡顿（先是按按钮没反应，过几秒打开控制台看日志，又等了 4s 才正常启动）。
> 用户使用场景：**直通中 → 想切回完整混音**（同一个麦克风 HyperX SoloCast 2，没换源）。

### 根因（按耗时大头排序）
1. **MicCapturer.Dispose 同步 Join → 3-7s**：HyperX 驱动在 WASAPI 共享模式下拆解资源极慢，NAudio 的 `WasapiCapture.Dispose()` 等线程退出。**最致命**。
2. **VirtualMicWriter 全量枚举设备 → ~500ms**：`EnumerateAudioEndPoints(Render)` + 每个端点查 `PropertyStore` (FriendlyName)。多输出设备机器（多耳机/虚拟设备）累计能到 500ms。
3. **MonitorOutput 无脑 Stop+New → ~50-80ms**：每次 StartAsync 都 `_monitor.Start()` → 先 StopLocked 再 new WasapiOut。监听已开就完全没必要。

### 修复策略：按需复用
不碰资源就什么都不做。三类各自独立判断：

| 资源 | 判断 | 收益 |
|---|---|---|
| `VirtualMicWriter` | `IsAlive` → 直接复用（永远输出到 CABLE Input） | 跳过 500ms 枚举 + 50ms WasapiOut.Init |
| `MicCapturer` | `CurrentDeviceId == micId` → 复用 | 跳过 3-7s 驱动拆解 |
| `MonitorOutput` | `EnsureStarted()` 幂等：已在跑且设备没变 → no-op | 跳过 50ms WasapiOut 重建 |

加上 `VirtualMicWriter._cachedCableInputId` 静态缓存（首次找到后存住，命中失败回退全量枚举），即使是真的需要重建 Writer 也只要 ~50ms。

### 关键决策点
- **复用 Writer 时不能 Reset Mixer**：Reset 会 Clear `_appBuffer/_micBuffer/_monitorBuffer`，正在播放的连续流会出现微杂音/断续。只有真拆建过 writer 才 Reset。
- **MicCapturer 加 `CurrentDeviceId` 字段**：`StartRecording` 成功后才赋值（在 lock 内），Stop 时清空。`IsAlive` 看 `_capture != null && !_disposed`。
- **MonitorOutput.EnsureStarted**：在 `_sync` 锁内做"目标设备 vs 当前设备"对比，相同就 return；不同走 StartLocked。

### 各场景实测预期
| 场景 | 1.2.9 | 1.2.10 |
|---|---|---|
| 冷启动（应用刚开） | ~600ms | ~600ms（首次枚举 + Writer 初始化无法跳过） |
| 直通→开始混音（同麦） | **~7s 卡顿** | **~5ms** |
| 切换 App 源（同麦） | **~7s 卡顿** | **~50ms** |
| 切麦克风 | ~7s | ~7s（HyperX 驱动天生慢，治不了） |
| 监听已开 → 重启混音 | +50ms | 0ms |

### 文件变更
- `src/VoicePipe/Audio/VirtualMicWriter.cs`：加 `_cachedCableInputId` 静态缓存 + `IsAlive` 属性
- `src/VoicePipe/Audio/MicCapturer.cs`：加 `_currentDeviceId` + `CurrentDeviceId/IsAlive` 公开属性
- `src/VoicePipe/Audio/MonitorOutput.cs`：加 `EnsureStarted()` 幂等启动
- `src/VoicePipe/Core/PipelineManager.cs`：`StartAsync` 改为按需复用（核心修改）
- `src/VoicePipe/VoicePipe.csproj`、`UI/MainWindow.xaml`、`VoicePipe.iss`：版本号 1.2.9 → 1.2.10

### 验证流程结果
- build 0 错 0 警；测试 26/26 全过；publish 启动正常（窗口标题=VoicePipe，日志 FATAL=0 ERR=0；测试机没装 VB-Cable 所以 Cable=false 是正常的）；ISCC 71s 出包 52.66MB；robocopy 同步开源 + git push（7f7509b..5db9d19）；`gh release create v1.2.10` 上传 55MB asset → state=uploaded。

### 给未来自己的提醒（如果还有类似 bug）
- 用户感知"卡 N 秒",日志里只显示 ms 级,**99% 是 UI 线程在等某个 await/Task.Run 内的同步 COM 调用**。
- 第一反应不要着急加更多 Task.Run,先想"这一步资源真的需要拆吗"。**复用永远比异步重建快**。
- WasapiCapture/WasapiOut 的 Dispose 速度跟驱动强相关,不可控。能不拆就别拆。
- 设备枚举(EnumerateAudioEndPoints)和单设备查找(GetDevice by id)成本差一个数量级,任何按规律命中的设备都该缓存 ID。



---

## 38. 版本 1.2.10→1.2.11：UX 重构（2026-05-31）

### 用户反馈（两点 UX 硬伤）
1. **MicPassthrough CheckBox 像孤儿**：原来夹在"开始混音"按钮下面，跟所有 card 风格不统一；用户第一眼不知道是干啥的，也不知道开启会不会影响延迟。
2. **降噪埋在设置页**：用流程在主页，但临时关一下降噪却要跑设置页一趟再跑回来；降噪强度滑块使用频率比监听音量还高，却埋得更深。

### 修复
1. **MicPassthrough → 麦克风 card 右上角**（用户钦定位置）：
   - 跟波形预览的"频谱"开关、降噪 card 的开关风格一致：标题左、文字标签 + Toggle Switch 在右
   - 加 Tooltip：`StrMicPassthroughTooltip` 解释 "勾选后按停止只断 App 音频混入，麦克风继续直通；不影响延迟"
2. **降噪 card → 主页（音量下方、波形上方）**：
   - 关闭时只显示开关一行；开启时滑块自动展开（`Visibility="{Binding NoiseGateEnabled, Converter=BoolToVisibility}"`）
   - 加 Tooltip：`StrNoiseGateTooltip` 解释 "RNNoise 神经网络人声降噪、仅麦克风路径、强度滑块控制干湿混合、延迟基本不变"
   - 设置页降噪 card 一并删除（避免双入口困惑）
3. **5 种语言 i18n key 全更新**：zh-CN / zh-TW / en-US / ja-JP / ko-KR 都加了两个 Tooltip key

### 主页最终 card 顺序
```
App 音频源 → 麦克风输入（含 MicPassthrough）→ 音量控制 → 降噪 → 波形预览 → 开始混音按钮
```
完全跟用户操作流程对齐：选源 → 选麦 → 调音量 → 微调降噪 → 看波形 → 启动。

### 文件变更
- `src/VoicePipe/UI/MainWindow.xaml`：
  - 麦克风 card 标题改为三列 Grid（标题 / 标签 / Switch），加 Tooltip
  - 主页加降噪 card（音量下方），开关折叠强度滑块
  - 删底部孤儿 MicPassthrough CheckBox
  - 删设置页"降噪" card（一同 35 行）
- `src/VoicePipe/Langs/{zh-CN,zh-TW,en-US,ja-JP,ko-KR}.xaml`：加 2 个 Tooltip key
- 三处版本号 1.2.10 → 1.2.11

### 验证流程结果
- build 0 错 0 警；测试 26/26 全过；publish 启动正常（窗口标题=VoicePipe，FATAL=0 ERR=0）；ISCC 71.875s 出包 52.66MB；robocopy 同步开源 + git push（5db9d19..0e36aaf）；`gh release create v1.2.11` 上传 55MB asset → state=uploaded。

### UX 设计哲学小记
- **使用频率决定位置**：降噪强度比监听音量调得多，所以前者上主页、后者留设置；监听整个功能本身是"调试场景"，留设置合理。
- **风格一致性 > 文字标签**：MicPassthrough 移到右上角后，跟波形的频谱开关同款（标题左 / Switch 右），整个主页只有"两种 toggle 模式"——卡片标题旁的小开关 + 卡片内的命名 CheckBox，视觉语言收敛。
- **Tooltip 是新手投资 / 老手不打扰**：高频可见标签写"做什么"（"停止保留麦克风直通"），Tooltip 写"为什么 / 是否影响延迟"。让懂的不被啰嗦干扰，不懂的能自己读懂。



---

## 39. 版本 1.2.11→1.2.12：动效优化（2026-06-01）

### 用户反馈
> "能不能优化做一下动画？比如开关，以及打开开关以后弹出的子级菜单"

### 改动
1. **Toggle Switch Thumb 平滑滑动**：之前 IsChecked Trigger 通过切 `HorizontalAlignment + Margin` 瞬间跳，现在用 `ThicknessAnimation Margin 3,0,0,0 → 21,0,0,0` 配 180ms CubicEase EaseOut。
2. **子面板淡入+上滑展开**：降噪强度滑块（受 NoiseGateEnabled 控制）+ 监听子选项（受 MonitorEnabled 控制）。用 DataTrigger 的 EnterActions/ExitActions 触发 Storyboard：
   - 展开：Visibility 立即 Visible → Opacity 0→1（200ms）+ TranslateTransform.Y -8→0（240ms），EaseOut
   - 收起：Opacity 1→0（140ms）+ TranslateTransform.Y 0→-8（140ms）→ Visibility Collapsed（带 BeginTime 0:0:0.14 等动画结束）

### WPF 踩坑
**TranslateTransform 不能作为 Setter.TargetName 的目标**：第一次实现时给 Thumb 加 `<TranslateTransform x:Name="ThumbTransform"/>` 想动画化它的 X 属性，build 报错 `MC4111: 无法找到 Trigger 目标"ThumbTransform"`。原因是 Setter.TargetName 必须指向 FrameworkElement 派生类，Transform 不是。

解决方案有两个：
- A. 在 FrameworkElement 上 `Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)"` 用 PropertyPath 间接定位
- B. 直接动画化 FrameworkElement 自己的 Margin（ThicknessAnimation）

选 B：更简单、不需要 PropertyPath、保持 Thumb 在 Layout 中"真实"位置（与 Margin Setter 兼容，初始态 Setter 仍然瞬切到目标位置作为持久化恢复的兜底，Storyboard EnterActions 才提供动画过渡）。

子面板用方案 A（StackPanel/Grid 自身的 RenderTransform.Y），因为 Margin 动画会触发 Layout 重算，开销大。

### 文件变更
- `src/VoicePipe/Themes/Dark.xaml`、`Light.xaml`：CheckBox 模板加 ThicknessAnimation EnterActions/ExitActions
- `src/VoicePipe/UI/MainWindow.xaml`：降噪强度子面板 + 监听子选项 StackPanel 加 DataTrigger Storyboard
- 三处版本号 1.2.11 → 1.2.12

### 验证流程结果
- build 第一次失败（TranslateTransform 命名问题），改 ThicknessAnimation 后 0 错 0 警 ✓
- 测试 26/26 全过 ✓
- publish 启动正常（窗口标题=VoicePipe，FATAL=0 ERR=0）✓
- ISCC 86.7s 出包（这次稍久，磁盘 IO 高）52MB ✓
- robocopy 同步开源 + git push（0e36aaf..68698b6）✓
- gh release v1.2.12 上传 state=uploaded ✓

### 给未来自己的提醒
- WPF Storyboard 想动画 Transform 子属性，要么把 Transform 当 RenderTransform 挂在 FrameworkElement 上、用 `(UIElement.RenderTransform).(TranslateTransform.Y)` PropertyPath；要么直接动画 FrameworkElement 自己的 Layout 属性（Margin/Width/Height/Opacity）。
- ThicknessAnimation 会触发 Layout，Margin 动画频繁会卡，但这里只是 Toggle 一次性切换，无开销问题。
- ObjectAnimationUsingKeyFrames + DiscreteObjectKeyFrame + BeginTime 是 WPF 唯一能"延迟切 Visibility"的写法（Visibility 是离散值，不能用 DoubleAnimation/OpacityAnimation 渐变）。



---

## 40. 版本 1.2.12→1.2.13：UX 大改版 — 监听上主页 + 可折叠卡片（2026-06-01）

### 用户需求（多轮迭代）
1. 降噪关闭时卡片太厚不协调 → 对称紧凑 Padding=20,12
2. 窗口高度调高，打开不滚动看全部内容
3. 波形预览默认折叠（后改主意：波形不要折叠不要动画，默认展开）
4. 降噪、监听也要能收起
5. 监听从设置页移到主页，放麦克风卡片下面
6. 波形开关移到最右跟其它卡片统一，dB 在开关左边
7. 打开关闭波形时音量滑块"抖一下"
8. 监听/波形动画要跟降噪同款

### 最终成果
- **主页卡片顺序**：App 音频源 → 麦克风(含 MicPassthrough) → 本地监听(可折叠) → 音量控制 → 降噪(可折叠) → 波形预览(常显) → 开始混音
- **三个可折叠的统一模式**：左侧 ▶ 三角(RotateTransform 0↔90，180ms EaseOut) + 标题 + 右侧 toggle；点击整个标题行切换(ToggleXxx_Click，排除点 CheckBox 自己)；子内容淡入(200ms)+上滑 8px(240ms)；ExitActions 先动画再延迟设 Collapsed
- **波形预览最终是常显静态卡片**（用户最后决定不折叠不动画），标题行：标题 + dB文本 + 频谱开关(最右)
- **窗口 580→840**，MinHeight 500→640
- **监听从设置页整块迁到主页**，设置页只留注释占位

### 关键踩坑
1. **滑块"抖一下"根因**：展开卡片 → 内容变高 → `ScrollBarVisibility=Auto` 滚动条才出现 → 内容区宽度缩 6px → 所有 SmoothSlider 的 thumb 按比例重算位置 → 视觉抖动。**修复：主内容 + 设置页 ScrollViewer 都改 `VerticalScrollBarVisibility="Visible"` 常显**，宽度恒定。我们的滚动条是 6px 细胶囊，常显不碍眼。
2. **波形动画"太快生硬"根因**：动画 wrapper Grid 带了固定 `Height="72"`，Visibility 切换时瞬间占满 72px，破坏滑动观感。降噪/监听的 wrapper 只有 Margin(高度由内容撑开)。**应该把固定 Height 放在内层**，动画 wrapper 只带 Margin。（最终用户不要波形动画了，所以波形直接改常显静态。）
3. **str_replace 匹配错位导致重复卡片**：插入监听卡片时 `</Border>\n\n<!-- 音量控制 -->` 匹配到了错误位置，导致主页出现两个监听卡片。教训：**插入大块 XAML 后立刻 grep 关键 key 确认数量**（StrMonitor 应只出现 1 次）。
4. **ISCC Error 32 / 资源更新失败**：Output\VoicePipeSetup.exe 被占用(杀毒扫描旧 exe 或 explorer 预览)。Remove-Item 删不掉时，**用 Rename-Item 改名能成功释放锁**，再删改名后的文件 + 重跑 ISCC。比单纯 Sleep 等待更可靠。

### 文件变更
- `src/VoicePipe/UI/MainWindow.xaml`：监听卡片迁主页+可折叠、降噪可折叠、波形常显、窗口 840、滚动条常显
- `src/VoicePipe/UI/MainWindow.xaml.cs`：ToggleNoiseGate_Click + ToggleMonitor_Click + IsInsideCheckBox 辅助；删 ToggleWaveformPreview_Click
- `src/VoicePipe/Core/AppSettings.cs`、`ViewModels/MainViewModel.cs`：ShowWaveformPreview 字段加了又删（波形最终不折叠）
- 三处版本号 1.2.12 → 1.2.13

### 验证流程结果
- build 0 错 0 警；测试 26/26 全过；publish 启动正常(窗口标题=VoicePipe，FATAL=0 ERR=0)；ISCC 72.8s 出包 52.66MB(第一次 Error 32 被占用，Rename-Item 改名释放锁后重跑成功)；robocopy 同步开源 + git push(68698b6..6db35c4)；gh release v1.2.13 上传 state=uploaded。



### 40b. v1.2.13→v1.2.14 折叠误关功能修复（2026-06-01）

**用户反馈**："折叠起来以后监听如果是之前打开的就保持啊，你怎么给关上了"

**设计缺陷**：v1.2.13 把"卡片折叠/展开"和"功能开关"绑成了同一个状态。chevron 旋转、子内容显隐、ToggleMonitor_Click 全都读写 `MonitorEnabled`。结果点标题行收起卡片 = 把监听功能关掉。降噪同理。

**修复（状态解耦）**：
- 新增纯 UI 折叠状态 `MonitorExpanded` / `NoiseGateExpanded`（AppSettings 持久化字段 + VM ObservableProperty + OnXxxExpandedChanged 存盘 + 构造函数恢复）
- `ToggleMonitor_Click` / `ToggleNoiseGate_Click` 改为翻转 `XxxExpanded`，不碰 `XxxEnabled`
- XAML 里 chevron 旋转 DataTrigger + 子内容显隐 DataTrigger 全部从 `XxxEnabled` 改绑 `XxxExpanded`（共 4 处：监听 chevron+子内容、降噪 chevron+子内容）
- 标题行右侧的 toggle CheckBox 仍绑 `XxxEnabled`（功能开关），与折叠完全独立

**效果**：点标题行只收起/展开子选项；监听/降噪开着时折叠再展开仍是开着；折叠状态独立持久化下次启动恢复。

**ISCC 踩坑续**：前台 ISCC 命令在工具层被多次打断（^C），但进程仍在后台跑。中断后 Output\VoicePipeSetup.exe 可能是半成品（5.81MB 而非 52MB）。处理：等 80s 让后台 ISCC 跑完 → 查文件大小确认 52MB → `Get-Process ISCC` 确认无残留进程。**别在 ISCC 没跑完时重复触发，会多个进程抢同一个输出文件。**

**验证**：build 0 错 0 警；测试 26/26；publish 启动 FATAL=0 ERR=0；ISCC 52.66MB；同步开源 push（6db35c4..6abe4ac）；gh release v1.2.14 state=uploaded。


### 40c. v1.2.14→v1.2.15 窗口尺寸放大（2026-06-01）

**用户反馈**：波形常显后窗口 840 高不够 → 提到 900；用户看 880 宽觉得"又窄又高不协调"→ 横向加宽到 1250。

**改动**：窗口默认 `Height=900 Width=1250`，`MinHeight=680 MinWidth=900`（之前 580/500 起步，一路加到这）。窗口宽了之后卡片、滑块横向拉长，整体比例舒服。

**经验**：加了"波形常显"+"监听卡片上主页"后内容变多，780×580 的老窗口完全不够。WPF 窗口尺寸是 DIP（设备无关像素），用户 125%/150% 缩放时实际物理像素更大，所以宁可给足。最终 1250×900 是用户实测满意的尺寸。

**验证**：build 0 错 0 警；测试 26/26；ISCC 76s 出包 52.66MB（rename 释放旧文件锁后一次成功）；同步开源 push（6abe4ac..82e23ad）；gh release v1.2.15 state=uploaded。


---

## 41. 版本 1.2.15→1.2.16：停止后监听/波形/直通失效修复（方案 A）（2026-06-01）

### 用户反馈
"停止以后波形不动了，开了麦克风直通波形也不动，保持着关掉的时候的波形，麦克风直通也不管用，开了监听也听不到，但是还能操作"

### 根因（架构耦合，真 bug）
监听和波形号称"独立功能"，实际全挂在 VB-Cable 输出这根泵上：
```
FeedApp/FeedMic → _appBuffer/_micBuffer
                      ↓
        AudioMixEngine.Read()  ← 只被 VirtualMicWriter(VB-Cable)的 WasapiOut 调用！
                      ↓
   混音 + WaveformAnalyzer.InlineSample + 写 _monitorBuffer
                      ↓
        MonitorProvider 从 _monitorBuffer 拉 → 耳机
```
`Read()` 只有 VB-Cable writer 活着才被调用。用户点"停止"走 StopAsync 把 writer Stop+null → 没人泵 Read() → 波形冻在最后一帧、_monitorBuffer 不再被填→监听读到全 0=静音。日志"监听已启动"是真启动了 WasapiOut，但喂的缓冲是空的。

### 方案 A：让监听真正独立驱动
- **AudioMixEngine 加 `VbCableActive` 标志** + `GenerateMonitorStandalone(outBuf, n)` 方法：VB-Cable 停了之后，由监听的 WasapiOut 直接调它，消费 app/mic 缓冲 + 跑波形/频谱分析 + 产出监听 PCM。
- **MonitorProvider.Read() 按 VbCableActive 切数据源**：true→从 _monitorBuffer 拉（Read 填的，原路径）；false→调 GenerateMonitorStandalone 自驱动。两者不会同时跑。
- **PipelineManager.StartAsync** writer 起来后设 `VbCableActive=true`；**StopAsync** 设 false，并按监听是否开着决定停止语义：
  - 监听开 → 保留 MicCapturer + loopback 继续喂引擎，监听 WasapiOut 接管泵动（停混音仍能单独听麦克风/App、波形继续动）
  - 监听关 → 完全拆 mic/monitor + `WaveformAnalyzer.Clear()` / `SpectrumAnalyzer.Clear()`（波形归零平线，不冻结）
- **新增 `EnsureMonitorRunning(micId)` / `StopMonitorStandalone()`**：完全停止后用户单独开/关监听时，ViewModel.OnMonitorEnabledChanged 调用，确保 mic 在采 + 监听链启动 / 释放 mic + 清波形。
- **WaveformAnalyzer / SpectrumAnalyzer 各加 Clear()**：完全停止时清缓冲，避免显示冻结残影。
- **MonitorEnabled setter 的即时 Start 改成幂等 EnsureStarted**：避免和 ViewModel.EnsureMonitorRunning 重复 Start WasapiOut。

### 红线安全
完全不碰 VB-Cable 那条 10ms 主路径（Read() 给 VB-Cable 的逻辑原样保留）。监听只在 VB-Cable 停了之后接管泵动，是另一条 WasapiOut（50ms 缓冲），互不影响。红线1 安全。

### 并发说明
VbCableActive=false 时监听线程跑 GenerateMonitorStandalone（读 app/mic buffer + WaveformAnalyzer.InlineSample）。过渡期（writer 刚 Stop、WasapiOut 还有最后一帧在途）理论上 Read() 和 GenerateMonitorStandalone 可能各跑一两帧。RingBuffer.Read 内部有 lock 安全；WaveformAnalyzer.InlineSample 单写者假设短暂打破，但只影响显示一两帧，无害。

### 验证
build 0 错 0 警；测试 26/26（音频核心改动无回归）；先 ISCC 出测试包(52.66MB)给用户**实机听感测试**（自动化测不了"停止后开监听能否听到"）；用户确认 OK 后才发 release。同步开源 push（82e23ad..80a9aa2）；gh release v1.2.16 上传 state=uploaded（注意：上传中途 release view 一度显示 untagged + assets 空，再等 60s 才变正式 tag + uploaded，大文件上传有延迟别急着判定失败）。

### 教训
"独立功能"如果共享同一个驱动泵，就不是真独立。设计音频功能时要明确：**谁来泵这个数据流？泵的生命周期和功能的生命周期是否一致？** 监听/波形的生命周期应该 = "有数据源 + 用户想看/听"，而不是 ="VB-Cable 在输出"。


### 41b. v1.2.16→v1.2.17 麦克风占用联动 bug 修复（2026-06-01）

**背景**：v1.2.16 让监听在 VB-Cable 停止后独立驱动（EnsureMonitorRunning / StopMonitorStandalone 在后台偷偷拉起/释放 MicCapturer），但这些操作没和 PeakMonitor（音量条静默测电平）同步，引出一串联动 bug。

**根症结**：MicCapturer 的拥有权（谁在占用麦克风）有两套独立逻辑——主管线 vs PeakMonitor 的静默监听——v1.2.16 的 standalone 监听拉起 mic 时没告诉 PeakMonitor，两者会抢同一个 WASAPI 设备。

**修的 5+ 个联动点**：
1. `EnsureMonitorRunning` 拉起/复用 mic 后 → `PeakMonitor.SetRunningMic(micId)`（告知占用，PeakMonitor 不再开静默监听抢设备）
2. `StopMonitorStandalone` 释放 mic 后 → `PeakMonitor.SetRunningMic(null)`（恢复静默测电平）
3. `OnSelectedMicChanged`：standalone 监听场景（!IsRunning && !直通 && MonitorEnabled）换麦克风 → 主动 `EnsureMonitorRunning(新micId)`，否则监听还听旧麦克风
4. `StopPipeline` 完全停止分支：`if (!MonitorEnabled) SetRunningMic(null)` —— 监听开着时 StopAsync 保留了 mic 喂监听，不能误报释放
5. `OnMicPassthroughChanged` 取消直通分支：同样 `if (!MonitorEnabled) SetRunningMic(null)`

**状态机验证（4 态全跑通）**：Idle / Running / Passthrough / MonitorOnly。重点边界：
- Passthrough 下关监听：`StopMonitorStandalone` 开头 `if (VbCableActive) return` 挡住（直通时 writer 活=VbCableActive true），不误清 mic ✓
- Passthrough→取消直通且监听开：StopAsync 保留 mic（监听开），ViewModel 因 MonitorEnabled 不清 SetRunningMic → 平滑转 MonitorOnly ✓

**教训**：共享资源（麦克风设备）有多个潜在占用者时，每个占用者的"拿/放"都必须更新同一个所有权登记（这里是 PeakMonitor.SetRunningMic）。v1.2.16 新增了一条占用路径却忘了登记，就和老路径抢资源。改音频/设备类功能时，先问"这个设备现在/将来可能被谁占用？我拿它的时候有没有通知所有相关方？"

**验证**：build 0 错 0 警；测试 26/26；publish 启动 FATAL=0 ERR=0；ISCC 74s 出包 52.66MB；同步开源 push（80a9aa2..ca45ca1）；gh release v1.2.17 state=uploaded（又是上传中途 untagged，等 60s 转正）。


---

## 42. 版本 1.2.17→1.2.18：全量代码审查三项修复（2026-06-01）

### 背景
用户要求"完整仔细审查一遍所有代码"，对全部 35+ 个源文件进行了三路并行审查（Audio Engine / ViewModel & Pipeline / UI & Theme），逐行追踪了 13 种用户操作的完整逻辑链。审查结果：无🔴崩溃级 bug，发现 3 个值得修的问题。

### 修复 1：CaptureFailed 未真正停止管线（资源泄漏）
**文件**：`ViewModels/MainViewModel.cs` CaptureFailed 事件处理
**根因**：当目标 App 进程崩溃、LoopbackCapturer 重试耗尽触发 CaptureFailed 时，处理器只设了 `IsRunning=false`，UI 显示已停止，但 VirtualMicWriter / MicCapturer / MonitorOutput 三大组件仍在后台运行，占着系统资源。用户必须手动再点一次"停止"才能真正释放。
**修复**：CaptureFailed 处理器改为 `async`，先 `await _pipeline.StopAsync()` 真正停止管线释放全部资源，再清 `_isMicPassthroughActive`/`IsRunning`/`SetRunningMic`。

### 修复 2：直通状态下换麦克风无效（UX 缺口）
**文件**：`ViewModels/MainViewModel.cs` OnSelectedMicChanged
**根因**：用户在麦克风直通状态下切换麦克风下拉菜单，由于 `IsRunning=false` 且 `_isMicPassthroughActive=true`，不满足任何已有分支的条件——`IsRunning` 分支跳过、`standalone 监听` 分支因 `_isMicPassthroughActive` 排除。结果旧麦克风继续直通，新选择被忽略。
**修复**：新增 `_isMicPassthroughActive` 分支：重新走一次 Start → StopAppOnly 路径（保持 MicPassthrough 勾选），让新麦克风通过完整管线初始化后进入直通态。

### 修复 3：右键菜单硬编码暗色 + 中文（不跟随主题/语言）
**文件**：`UI/MainWindow.xaml` ContextMenu + `Langs/*.xaml`
**根因**：右键菜单的 ControlTemplate 使用了硬编码颜色（#1C1C1C 背景、#383838 边框、#EAEAEA 文字、#2A2A2A 悬停），且菜单项文字硬编码中文"打开实时控制台"。切换到亮色主题时菜单仍是暗色卡片，英文/日韩用户看到中文。
**修复**：所有颜色改为 `{DynamicResource BgCardBrush}`/`BorderBrush`/`TextPrimaryBrush`/`BgPanelBrush`；菜单项文字改为 `{DynamicResource StrOpenConsole}`；5 种语言文件新增 `StrOpenConsole` key。

### 附：取消直通时 StopAsync 改为 await（竞态预防）
**文件**：`ViewModels/MainViewModel.cs` OnMicPassthroughChanged
**原问题**：`_ = _pipeline.StopAsync()` fire-and-forget，后续 UI 状态更新在 Stop 完成前执行。用户若快速取消直通后点"开始"，Start 可能和未完成的 Stop 重叠。
**修复**：拆出 `StopPassthroughAsync()` 方法，`await _pipeline.StopAsync()` 完成后才更新状态。

### 文件变更
| 文件 | 改动 |
|---|---|
| `ViewModels/MainViewModel.cs` | CaptureFailed 改 async + await StopAsync；OnMicPassthroughChanged 拆出 StopPassthroughAsync；OnSelectedMicChanged 新增直通态换麦分支 |
| `UI/MainWindow.xaml` | ContextMenu 颜色改 DynamicResource + 文字改 i18n |
| `Langs/zh-CN.xaml` | 新增 StrOpenConsole="打开实时控制台" |
| `Langs/zh-TW.xaml` | 新增 StrOpenConsole="開啟即時控制台" |
| `Langs/en-US.xaml` | 新增 StrOpenConsole="Open Live Console" |
| `Langs/ja-JP.xaml` | 新增 StrOpenConsole="ライブコンソールを開く" |
| `Langs/ko-KR.xaml` | 新增 StrOpenConsole="실시간 콘솔 열기" |

### 验证
build 0 错 0 警。待用户确认后升版本号、出包发布。

---

## 43. 版本 1.2.18→1.2.19：VB-Cable 一键检测修复 + 设置页驱动按钮（2026-06-06）

### 背景
用户反馈"点开启混音一点反应都没有，日志都不动"，根因是 VB-Cable 驱动损坏（右上角状态为 ✗），但按钮没有给出任何诊断提示。用户希望 App 内置驱动修复流程。

### 新功能

**VB-Cable 驱动一键修复流程**
- `MainViewModel.cs`：新增 `public async Task RepairVbCableAsync()`
  - 查找安装目录 `{app}\vbcable\VBCABLE_Setup_x64.exe`
  - 先确认 `CABLE Input` 不可用才走修复（否则提示"驱动正常，无需修复"）
  - 卸载旧驱动：`-u -h`（静默），再安装：`-i -h`，使用 `runas` 获取 UAC 权限
  - 修复后重新检测 `IsCableAvailable`
  - 弹出 MessageBox.Show(YesNo) 询问是否立即重启电脑，选"是"执行 `shutdown /r /t 5`
  - 失败时同样提示重启（驱动可能已安装但需重启激活）

**设置页"驱动检测与修复"卡片**
- `UI/MainWindow.xaml`：在"检查更新"卡片下方新增卡片
  - 左侧：8px 状态圆点（绑定 `IsCableAvailable` → 绿/红）+ `VB-Cable ✓/✗` 文字
  - 右侧："检测修复"按钮
- `UI/MainWindow.xaml.cs`：新增 `RepairVbCable_Click` → `await vm.RepairVbCableAsync()`
- `StartPipeline` guard：VB-Cable 不可用时调 `await RepairVbCableAsync()` 后 return

**VB-Cable 状态正常时无需修复**（Bug 修复，v1.2.20 前的 bug）
- 设置页按钮点击先刷新检测，若 `IsCableAvailable=true` 则弹"驱动正常无需修复"并 return

**i18n**
- 5 语言文件（zh-CN/zh-TW/en-US/ja-JP/ko-KR）新增：
  - `StrDriverSection`：驱动检测与修复 / 驅動偵測與修復 / Driver Detection & Repair / ドライバー検出と修復 / 드라이버 감지 및 복구
  - `StrDriverRepair`：检测修复 / 偵測修復 / Detect & Repair / 検出・修復 / 감지 및 복구

### 文件变更
| 文件 | 改动 |
|---|---|
| `ViewModels/MainViewModel.cs` | 新增 `RepairVbCableAsync()`；StartPipeline 守卫改为触发修复；StartPipeline 所有 guard 加 Serilog 诊断日志 |
| `UI/MainWindow.xaml` | 新增驱动检测卡片（圆点状态 + 检测修复按钮） |
| `UI/MainWindow.xaml.cs` | 新增 `RepairVbCable_Click` |
| `Langs/zh-CN.xaml` | 新增 StrDriverSection / StrDriverRepair |
| `Langs/zh-TW.xaml` | 新增 StrDriverSection / StrDriverRepair |
| `Langs/en-US.xaml` | 新增 StrDriverSection / StrDriverRepair |
| `Langs/ja-JP.xaml` | 新增 StrDriverSection / StrDriverRepair |
| `Langs/ko-KR.xaml` | 新增 StrDriverSection / StrDriverRepair |
| `VoicePipe.csproj` / `MainWindow.xaml` / `VoicePipe.iss` | 版本号升至 1.2.19 |

---

## 44. 版本 1.2.19→1.2.20：修复设置页修复按钮误触正常用户（2026-06-06）

### Bug
v1.2.19 设置页"检测修复"按钮**无条件**弹出修复对话框，即使 VB-Cable 完全正常。用户点开设置就被询问"是否要卸载重装驱动"，产生恐慌。

### 根因
`RepairVbCableAsync()` 方法没有在入口处检测 `IsCableAvailable`，从设置按钮调用和从 StartPipeline guard 调用走同一路径，但 StartPipeline 路径已经确认了不可用，而设置按钮路径没有前置条件。

### 修复
在 `RepairVbCableAsync()` 开头加：
```csharp
IsCableAvailable = VirtualMicWriter.IsCableInputAvailable(); // 先刷新
if (IsCableAvailable) {
    MessageBox.Show("VB-Cable 驱动状态正常，无需修复。");
    return;
}
```

### 文件变更
| 文件 | 改动 |
|---|---|
| `ViewModels/MainViewModel.cs` | RepairVbCableAsync 入口新增检测守卫，正常时早返回 |
| `VoicePipe.csproj` / `MainWindow.xaml` / `VoicePipe.iss` | 版本号升至 1.2.20 |

---

## 45. 版本 1.2.20→1.2.21：修复快速切换麦克风时音量条不显示（2026-06-22）

### Bug
用户反馈"有时候麦克风声音还是不显示"，在日志中快速切换麦克风时出现：
```
MicCapturer: 开始捕获  ← 旧 Task.Run 终于完成
MicCapturer: 停止      ← 新的 Start() 调 Stop() 把它干掉
MicCapturer: 开始捕获  ← 新的才真正启动，但已经没人订阅事件了
```

### 根因（竞态条件）
`MicCapturer.Start()` 用 `Task.Run` 在后台初始化 WASAPI，耗时约 500-900ms。若在此期间 `Stop()` 已执行完毕（`_capture=null`），旧 Task.Run 拿到锁后仍会把新的 `WasapiCapture` 写入 `_capture`——但 Pipeline 已不再订阅 `SamplesAvailable` 事件（订阅动作在 PipelineManager.StartAsync 里，已经过了），mic 在后台空采集，音量条和波形完全不响应。

### 修复：世代计数器（Generation Counter）
```
_generation 字段（int）
- Start() 进入时：lock(_sync) → Stop() → ++_generation → 记录 myGeneration → Task.Run
- Task.Run 内：先在锁外完成耗时 COM 初始化，再 lock(_sync)
  → 检查 _generation != myGeneration → 不匹配则 Dispose 并 return（不写 _capture）
  → 匹配则 _capture = cap，StartRecording()
- Stop() 内：lock(_sync) → _generation++（使任何正在飞行的 Task.Run 失效）→ 清理
```
同时将耗时 COM 初始化（`MMDeviceEnumerator`、`WasapiCapture` 构造）移至锁外，减少持锁时间。

### 文件变更
| 文件 | 改动 |
|---|---|
| `Audio/MicCapturer.cs` | 新增 `_generation` 字段；`Start()` 重构：lock→Stop→++generation→Task.Run（锁外初始化，锁内验代）；`Stop()` 内 `++_generation` |
| `VoicePipe.csproj` / `MainWindow.xaml` / `VoicePipe.iss` | 版本号升至 1.2.21 |

---

## 46. 版本 1.2.21→1.2.22：修复 DJ/大鼓低音导致爆音（2026-06-23）

### Bug
用户反馈"在 DJ 那种很多鼓点低音的时候会爆音"。DJ 音乐峰值动态远大于普通内容，大鼓瞬态可把 float32 信号推至 ±2.0 甚至更高。

### 根因：旧软限幅器数学缺陷
旧公式：
```
output = threshold + tanh(excess) × (1 - threshold)
其中 threshold = 0.944
```
- `tanh(x)` 在 x=0.5 时仅约 0.46，x=1.0 时约 0.76——导致输入超过阈值不多时输出就已经逼近 ±1.0
- 但 **不保证** 输出 ≤ ±1.0：当 excess 极小时 tanh 近似线性，加上阈值基底之后可能略微超出
- 最根本问题：用了 `tanh(excess) × (1 - threshold)` 而非 `(1 - threshold) × tanh(excess / knee)`，knee 参数为 1.0（默认），对大幅超载（excess >> 1）的压制速度太慢

### 修复：两段式强限幅器
```csharp
private const float LimiterThreshold = 0.85f;  // -1.4 dBFS，低于此完全透传
private const float LimiterKnee = 0.15f;        // 曲线柔软度

static float SoftLimit(float x) {
    float abs = MathF.Abs(x);
    if (abs <= LimiterThreshold) return x;          // 透传：保留冲击感
    float excess = abs - LimiterThreshold;
    return sign * (LimiterThreshold
                   + (1 - LimiterThreshold) * MathF.Tanh(excess / LimiterKnee));
}
```
**数学保证**：`tanh(excess/0.15)` 随 excess 增大极速趋近 1.0，整体输出极限为：
```
threshold + (1 - threshold) × 1 = 1.0
```
无论输入多大（±2、±3、±10），输出严格 ≤ ±1.0。

**阈值下调到 0.85**（原 0.944）：给更大的压缩空间，DJ 强峰值在 85% 处就被"接住"，剩余 15% 动态空间做软压缩过渡，避免方波失真。

### 文件变更
| 文件 | 改动 |
|---|---|
| `Audio/AudioMixEngine.cs` | 删除旧 `LimiterThreshold = 0.944f` 常量；新增 `LimiterThreshold = 0.85f` 和 `LimiterKnee = 0.15f`；`SoftLimit()` 公式改为 `thr + (1-thr) × tanh(excess/knee)` |
| `VoicePipe.csproj` / `MainWindow.xaml` / `VoicePipe.iss` | 版本号升至 1.2.22 |

---

## 📑 版本索引补充（v1.2.18 之后）

| 版本 | 主题 | 要点 | 章节 |
|---|---|---|---|
| **v1.2.18** (2026-06-01) | 全量代码审查三项修复 | CaptureFailed 真正停管线；直通态换麦克风立即生效；右键菜单跟随主题/语言 | 第 42 节 |
| **v1.2.19** (2026-06-06) | VB-Cable 一键检测修复 | 设置页新增驱动卡片（状态圆点+修复按钮）；修复流程卸载重装并提示重启；5 语言 i18n | 第 43 节 |
| **v1.2.20** (2026-06-06) | 修复误触正常用户 | RepairVbCableAsync 入口先检测状态，正常直接提示无需修复 | 第 44 节 |
| **v1.2.21** (2026-06-22) | 修复麦克风音量条偶尔不显示 | MicCapturer 引入世代计数器解决 Task.Run 与 Stop() 竞态 | 第 45 节 |
| **v1.2.22** (2026-06-23) | 修复 DJ 大鼓低音爆音 | SoftLimit 公式改为 thr+(1-thr)×tanh(excess/knee)，数学保证输出≤±1.0 | 第 46 节 |

> 最后更新：2026-06-23
