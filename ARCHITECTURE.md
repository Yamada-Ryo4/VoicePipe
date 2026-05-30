# VoicePipe Architecture

> A walkthrough of how VoicePipe routes per-app audio + mic to a virtual cable.
> Aimed at contributors and future-me. Not exhaustive — see source comments for the gory details.

[中文版](#架构总览中文)

---

## TL;DR

```
[App PID] ──Per-Process Loopback──▶ LoopbackCapturer ─┐
                                                      │
[Mic Device] ──WASAPI Capture──▶ MicCapturer ─▶ RNNoise ─┐
                                                      │  │
                                          float[] PCM @ 48 kHz
                                                      │  │
                                              ┌───────▼──▼──────┐
                                              │  AudioMixEngine  │  (IWaveProvider)
                                              │  RingBuffer × 2  │
                                              │  + Resampler     │
                                              └───────┬──────────┘
                                                      │  pulled at device clock
                                              ┌───────▼──────────┐
                                              │ VirtualMicWriter │ ── WASAPI ──▶ CABLE Input
                                              │  WaveOutEvent    │     (10 ms latency)
                                              │  10 ms latency   │
                                              └──────────────────┘
                                              ┌──────────────────┐
                                              │  MonitorOutput   │ ──▶ Headphones (optional)
                                              │  (independent)   │
                                              └──────────────────┘
```

Whole pipeline runs at **48 kHz / float32 / stereo**. App and mic are mixed only inside `AudioMixEngine.Read()` — they live in two separate ring buffers right up to that point. This is what makes mic-only effects (denoise, mute, monitor) safe.

---

## Pull, not push

The big decision is that **the output side drives the clock**, not the capture side.

`AudioMixEngine` implements `NAudio.Wave.IWaveProvider`. `WaveOutEvent` (configured at 10 ms latency) calls `Read(buffer, offset, count)` whenever it needs the next chunk for the speaker — or in our case, the CABLE Input device. We mix and return exactly what was asked for, padding with zeros if either ring buffer is short.

Why this matters:

- A push model (capture → "Tick" → write) would have the App and Mic callbacks each independently trying to drive output — two clocks fighting for the same buffer. We tried this in early prototypes; it stuttered and dropped frames under load.
- Pull means the device clock is the single source of truth. If a capture is briefly slow, `Read` just returns silence for the missing samples and continues. No back-pressure to manage, no overflow to drop.
- VB-Cable's WasapiOut at 10 ms shared mode is the latency floor. The whole pipeline above it is sized so it never has to wait on us.

**Don't change the 10 ms latency or the pull model.** Both are load-bearing.

---

## Per-process audio capture — the Windows 10/11 trick

`LoopbackCapturer` uses Windows 10 Build 19041's `ActivateAudioInterfaceAsync` with `AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK`. This is the only way to capture audio from a single process on consumer Windows.

### Things that bit us, so they don't bite you:

1. **`IAudioClient`, NOT `IAudioClient3`.** The COM object returned by `ActivateAudioInterfaceAsync` only implements the 12-method `IAudioClient` interface (GUID `1CB9AD4C-...`). Casting to `IAudioClient3` (GUID `726778CD-...`) silently calls `QueryInterface`, fails, and you get a confusing `InvalidCastException`. This single mistake kept the original prototype broken for a long time.

2. **`INCLUDE_TARGET_PROCESS_TREE`** is the right flag. Modern apps (Chrome, Edge, Discord, even some games) spawn many child processes — renderer, audio service, GPU, etc. — and the actual audio session lives in a child PID, not the one you can see in Task Manager. Capturing the tree gets all of them.

3. **You cannot re-activate a closed loopback for the same PID.** Windows returns `E_UNEXPECTED`. So `PipelineManager` keeps a `Dictionary<int, LoopbackCapturer>` that **never disposes** a capturer for a still-running process. Switching audio sources just swaps which one feeds the mixer — capture sessions stay alive in the background, paused. They get cleaned up only when the source process actually exits (`PurgeDeadSessions`).

4. **The `Paused` flag** is a hot-path optimization. A paused capturer still calls `ReleaseBuffer` on the WASAPI ring (otherwise Windows fills it and errors), but skips the `Buffer.MemoryCopy` and `SamplesAvailable` event invocation. Background capturers cost almost nothing.

5. **Buffer reuse.** Each WASAPI packet at 48 kHz fires every few ms. Allocating a fresh `float[]` per packet was the #1 GC source in profiling. Now there's a single growable `_sampleBuffer` per capturer, and the event signature is `(float[] Samples, int Count)` so consumers know the valid range. Safe because the consumer (`FeedApp` → `RingBuffer.Write`) does an `Array.Copy` before returning, so the buffer is free to be overwritten on the next packet.

6. **Retry with exponential backoff.** Process Loopback occasionally throws transient `COMException` E_UNEXPECTED. Instead of bailing, `CaptureLoopWithRetry` catches and retries up to 10× (1s → 5s capped). If it really can't recover, `CaptureFailed` bubbles up to the UI.

---

## Mic path — denoise without touching the app audio

`MicCapturer` is a regular `WasapiCapture` with two non-obvious bits:

- **Initialization runs on a background MTA thread** (`Task.Run`). Some drivers refuse to initialize on the WPF UI thread.
- **Format normalization** to 32-bit float happens before anything else. The reusable `_convertBuffer` handles 16-bit / 24-bit / 32-bit int and IEEE float source formats. 24-bit is the surprise: many "pro" mics output 24-bit packed, and missing this gave us garbled audio for a release.

Then `RnnoiseDenoiser`:

- Runs at 48 kHz mono natively (matches the pipeline rate, no resample needed).
- Wet/dry mix via `WetMix` (0…1) — full denoise sounds hollow, full dry defeats the purpose. Slider lets users dial in.
- Process is **in-place on the mic float[] only**. App audio is in a separate ring buffer and never sees this path.

Mic mute, mic gain, denoise on/off — all of these are flags on the engine that take effect inside the mix loop. They flip in O(1) on whichever side of `AudioMixEngine.Read` reads them.

---

## The mix loop itself (`AudioMixEngine.Read`)

Three ring buffers feed in: `_appBuffer`, `_micBuffer`, and (for headphone monitoring) `_monitorBuffer`. The mixer:

1. Reads up to `samplesNeeded` from each into pre-allocated `_appTemp` / `_micTemp` (zero-padded if short).
2. Loop body: `mixed = SoftLimit(app * appGain + mic * micGain)`. Soft limit is `tanh`-based, kicks in at -0.5 dBFS — keeps loudness margin without slamming a wall on transients.
3. Writes the mixed sample to the output buffer, plus pushes one sample into `WaveformAnalyzer.InlineSample` and `SpectrumAnalyzer.InlineSample` for the UI. Inline = no event, no array copy, no lock — just a write to a shared ring with `Volatile.Read` on the snapshot side.
4. If headphone monitoring is on, also writes to `_monitorBuffer` (gated by App/Mic toggles, with its own gain). `MonitorOutput` pulls from that buffer on its own WASAPI device.

All buffers are sized once at construction. The mix loop allocates **zero** bytes per call once warmed up. The `fixed` for the unsafe write is hoisted out of the loop, so it pins memory once per `Read`, not per sample.

### Perceptual gain curve

`AppGain` and `MicGain` are linear sliders 0…2. Internally they're stored squared-ish (`x ^ 1.7`) into `_appGainAmp` / `_micGainAmp` — `MathF.Pow` happens once per slider drag, the audio thread reads the cached `volatile` field. The curve makes "50%" actually sound roughly half as loud (linear amplitude doesn't, because human loudness is logarithmic).

100% always equals exactly ×1.0 — slider at the top means "passthrough". 150% goes to ~×1.98 (about +6 dB). Anchor points were chosen by feel after several iterations.

---

## Output

`VirtualMicWriter` is a thin shell around `WaveOutEvent`. It enumerates audio render endpoints, finds the one named "CABLE Input" (case-insensitive), opens it shared-mode at 10 ms latency, and pulls from the mixer.

`MonitorOutput` is the same idea but for the user's chosen monitor device (defaults to system default). Completely independent ring buffer and WaveOutEvent — that's what guarantees monitoring can never affect VB-Cable latency.

Both writers are wrapped in `Stop()`/`Dispose()` carefully: there used to be a leak where `Initialize()` calling `Dispose()` would set `_disposed = true` and skip cleanup on the next round. Now `Initialize → Stop` (releases without disposing flag) and `Dispose → Stop + flag`.

---

## UI side — the things that look ordinary but aren't

- **`PeakMonitor`** runs at ~15 fps on a background `Task`. It caches the COM enumerator and the default render device (refreshing the device check every 1 s, the PID→name snapshot every 1 s — both throttled because they're surprisingly expensive). Per-tick it calls `AudioSessionManager.RefreshSessions()` because NAudio caches the sessions snapshot otherwise, and apps that started after VoicePipe wouldn't show their levels (that one took a week to find).

- **`ProcessEnumerator`** uses the Toolhelp32 snapshot (`CreateToolhelp32Snapshot`) for PID→name and parent→child relationships. Audio sessions get grouped by **root same-named process** so multi-process apps (Chrome with 30 PIDs) collapse to one row. The tree-capture flag in the loopback API then captures everything underneath.

- **Mic listener wakeup.** Windows' capture-side `AudioMeterInformation` returns 0 unless an app is recording from that mic. So `PeakMonitor` opens a silent `WasapiCapture` on each "interesting" mic just to wake its meter. This is expensive — it's a real WASAPI session per mic — so the strategy is:
  - The currently selected mic is always woken.
  - When the mic dropdown is open, all active mics are woken (so you can see them light up).
  - The mic the pipeline is currently using doesn't need a wake — it's already being captured.
  - Everything else is ignored.

- **Diff updates** for the process and mic ObservableCollections. Clearing and rebuilding triggers WPF to drop selection state mid-stream, which is awful UX. Diff add/remove preserves it.

---

## What's *not* in this diagram (intentional)

- **`NoiseGate`** — predates RNNoise. Still in the codebase, still in the test suite, but not wired into the pipeline. Don't delete it; tests use it as a known-good DSP fixture.
- **VB-Cable detection on UWP/Apple Music** — Windows attributes some store apps' audio sessions to system services (`audiodg`, `svchost`), so per-process loopback can't see them as the user-facing app. Not a bug, a Windows limitation. The smart-grouping in `ProcessEnumerator` mitigates it slightly, but if a user picks Apple Music and gets nothing, that's why.
- **A real DAW-grade limiter / EQ / compressor.** Out of scope. The soft limiter exists only to prevent clipping, not to shape sound.

---

## Building & releasing

```powershell
# Build (from src/VoicePipe)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false

# Tests (from tests/VoicePipe.Tests)
dotnet test
# 26 property-based tests via CsCheck

# Installer (from repo root)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "VoicePipe.iss"
# → Output\VoicePipeSetup.exe
```

Verification rules that have saved us:

1. **Don't trust "build succeeded" alone.** WPF resource errors only show up at XAML parse time, which is at window construction. Run the published exe and read its main window title — if it's "启动错误" (startup error), there's a problem the build didn't catch.
2. **Read the Serilog output** for `[FTL]` or unexpected `[ERR]` after a startup. We've shipped builds where the window opened but RNNoise failed to load — looked fine, sounded broken.
3. **Audio behavior must be verified on a machine with VB-Cable + a real mic.** The dev box doesn't have it. `Cable=false` in logs is normal for the dev box; on a test machine, it should be `true`.
4. The `.iss` script and any `Langs/*.xaml` files **must be saved as UTF-8 with BOM**. PowerShell-batch-replacing them strips the BOM and produces `?` corruption in Chinese strings. Verified by checking `EF BB BF` at the file head.

---

<a id="架构总览中文"></a>

# 架构总览（中文）

> 用大白话讲一遍 VoicePipe 是怎么把"指定 app 的声音"和"麦克风"凑到一起送进 VB-Cable 的。
> 给贡献者和半年后看自己代码的我看的，不求面面俱到，细节看源码注释。

## 一句话流程

```
[某个 App 的 PID] ──Per-Process Loopback──▶ LoopbackCapturer ─┐
                                                              │
[麦克风设备] ──WASAPI 捕获──▶ MicCapturer ─▶ RNNoise 降噪 ─┐
                                                              │
                                              48 kHz / float32 / 立体声
                                                              │
                                                  ┌───────────▼─────────┐
                                                  │   AudioMixEngine    │  (IWaveProvider)
                                                  │   两个 RingBuffer    │
                                                  │   重采样 + 软限幅    │
                                                  └───────────┬─────────┘
                                                              │ 由设备时钟拉取
                                                  ┌───────────▼─────────┐
                                                  │  VirtualMicWriter   │ ──▶ CABLE Input
                                                  │   10ms 低延迟       │     (VB-Cable)
                                                  └─────────────────────┘
                                                  ┌─────────────────────┐
                                                  │   MonitorOutput     │ ──▶ 耳机（可选）
                                                  │  完全独立的输出链    │
                                                  └─────────────────────┘
```

整条管线统一 **48 kHz / float32 / 立体声**。App 和 Mic 是**两条独立的 RingBuffer**，只在 `AudioMixEngine.Read()` 里才相加。这就是为什么"麦克风降噪/静音/监听"绝不会影响 App 音频——它们物理上根本碰不到 App 那条 buffer。

## 关键决策：Pull 模型，不是 Push

输出端拉数据，不是输入端推数据。`WaveOutEvent` 配 10ms 低延迟，按设备时钟主动调用 `AudioMixEngine.Read()`，要多少拿多少；混音器内部从两个 RingBuffer 各读一段、相加、写出。任一路慢了？补零继续，不卡顿。

为什么不 Push：早期版本用过两个捕获回调各自触发 Tick 写输出——两个时钟抢同一个缓冲，必丢帧、必卡顿。Pull 让设备时钟成为唯一真相，问题就消失了。

**10ms 低延迟和 Pull 模型都是承重墙，别碰。**

## 进程音频捕获——Windows 10/11 才有的能力

`LoopbackCapturer` 用 Windows 10 Build 19041 引入的 `ActivateAudioInterfaceAsync` + `AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK`。这是消费版 Windows 上**唯一**能按进程捕获音频的官方途径。

### 几个踩过的坑

1. **必须用 `IAudioClient`，不能用 `IAudioClient3`**——返回的 COM 对象只实现 12 方法的 `IAudioClient` 接口（GUID `1CB9AD4C-...`），强转 `IAudioClient3` 会触发 `QueryInterface` 失败抛 `InvalidCastException`。这个 bug 让早期原型一直不工作。

2. **用 `INCLUDE_TARGET_PROCESS_TREE`**——Chrome/Edge/Discord 这种多进程 app 把声音放在子进程里，主 PID 反而没声。捕获整棵树才能抓到。

3. **关闭后的 PID 不能重新激活**——Windows 会返回 `E_UNEXPECTED`。所以 `PipelineManager` 用 `Dictionary<int, LoopbackCapturer>` 缓存所有用过的 capturer，**永不主动 Dispose 还活着的进程的会话**。切换音源时只是切"当前喂入混音器的那一个"，其它继续在后台暂停（`Paused=true`）。等源进程真的退出了（`PurgeDeadSessions` 检测到），才清理。

4. **`Paused` 标志**是个热路径优化。暂停的 capturer 还得调 `ReleaseBuffer` 排空 WASAPI 缓冲（不然缓冲满了报错），但跳过 `Buffer.MemoryCopy` 和事件触发。后台 capturer 几乎零开销。

5. **缓冲区复用**——48 kHz 下每个 WASAPI packet 几毫秒触发一次。每次 `new float[]` 的 GC 压力是分析里排第一的。改成成员级 `_sampleBuffer` 按需扩容，事件签名带 `(float[] Samples, int Count)` 让消费方只读有效区间。安全因为消费链是同步的（`FeedApp → RingBuffer.Write` 立刻 `Array.Copy` 拷走）。

6. **指数退避重试**——Per-Process Loopback 偶尔抛瞬态 `COMException`。`CaptureLoopWithRetry` 最多重试 10 次（1s→5s 上限），实在不行才向 UI 抛 `CaptureFailed`。

## 麦克风路径——降噪只动麦克风

`MicCapturer` 是普通 `WasapiCapture`，但有两个不显眼的关键点：

- **初始化跑在后台 MTA 线程**（`Task.Run`）。某些驱动在 WPF UI 线程上初始化 WASAPI 会拒绝。
- **PCM 格式归一化到 32-bit float**。复用 `_convertBuffer`，支持 16/24/32-bit int 和 IEEE float。24-bit 是个意外坑——很多专业麦克风出 24-bit packed，少处理这个就出杂音。

接着是 `RnnoiseDenoiser`：

- 原生 48 kHz 单声道，匹配管线采样率，不重采样。
- 干湿混合 `WetMix`（0~1）。全湿（彻底降噪）听着空，全干没意义。滑块给用户调。
- **原地处理麦克风 float[]**。App 音频在另一个 RingBuffer 里，物理上碰不到这里。

麦克风静音、增益、降噪开关——都是引擎上的 flag，混音循环每次 `Read` 进入时读一次，O(1) 切换。

## 混音循环本身（`AudioMixEngine.Read`）

三个 RingBuffer：`_appBuffer`、`_micBuffer`、`_monitorBuffer`（耳机监听用）。混音器：

1. 各读 `samplesNeeded` 到预分配的 `_appTemp` / `_micTemp`（不够就补零）。
2. 循环体：`mixed = SoftLimit(app * appGain + mic * micGain)`。软限幅基于 `tanh`，-0.5 dBFS 起作用——保留动态余量、不暴力削顶。
3. 写出，并 inline 推一个采样到 `WaveformAnalyzer` 和 `SpectrumAnalyzer` 给 UI。inline = 不发事件、不拷数组、不加锁，只写一个共享环形缓冲，UI 端 `Volatile.Read` 读快照。
4. 如果开了耳机监听，也写一份到 `_monitorBuffer`（受 App/Mic 子开关控制，独立增益）。`MonitorOutput` 自己的 WASAPI 设备从那里拉。

所有缓冲在构造时一次分配。**热好之后混音循环每次调用零堆分配**。`fixed` 提到循环外只 pin 一次。

### 感知响度增益曲线

`AppGain` / `MicGain` 滑块是线性 0~2，但内部存的是 `x ^ 1.7` 到 `_appGainAmp` / `_micGainAmp`——`MathF.Pow` 只在拖滑块时算一次，音频线程读 `volatile` 缓存。这条曲线让"50%"听起来真的差不多半响（人耳响度感知是对数的，纯线性幅度不行）。

100% 严格等于 ×1.0（滑块顶 = 原音不变），150% 约 ×1.98（约 +6 dB）。锚点是反复试感觉调出来的。

## 输出端

`VirtualMicWriter` 就是 `WaveOutEvent` 的薄壳。枚举所有渲染端点找名为 "CABLE Input" 的（不区分大小写），共享模式 10ms 延迟打开，从混音器拉数据。

`MonitorOutput` 一样的思路，但走用户选的监听设备（默认系统默认）。**独立 RingBuffer + 独立 WaveOutEvent**，这就是为什么监听绝不会影响 VB-Cable 延迟。

两个 Writer 的 `Stop()` / `Dispose()` 拆得很小心——历史上有过 bug：`Initialize()` 调 `Dispose()` 会把 `_disposed=true`，下次 `Initialize` 跳过清理。现在 `Initialize → Stop`（释放不设标志），`Dispose → Stop + 设标志`。

## UI 侧——看着普通其实有讲究的几样

- **`PeakMonitor`** 后台 Task 跑约 15 fps。COM 枚举器和默认渲染设备都缓存复用（默认设备每秒比对一次 ID 检测变更，PID→名字映射也每秒重取一次——后者是个出乎意料的 CPU 热点，全系统进程快照不便宜）。每轮调 `RefreshSessions()`，否则 NAudio 缓存死会话，VoicePipe 启动后才打开的 app 读不到音量（这个 bug 找了一周）。

- **`ProcessEnumerator`** 用 Toolhelp32 快照取 PID→名字 + 父子关系。**按"根同名进程"归并**——Chrome 30 个进程归一行，配合 Loopback 的 `INCLUDE_TARGET_PROCESS_TREE` 一抓一整窝。

- **麦克风 meter 唤醒**——Windows 的 capture 端 `AudioMeterInformation` 在没有 app 录音时返回 0。所以 `PeakMonitor` 对每个"需要的"麦克风开一个**静默 `WasapiCapture`** 唤醒硬件电平表。这玩意儿贵，所以策略是：
  - 当前选中的麦克风始终唤醒。
  - 麦克风下拉打开时全部唤醒（让用户能用电平条认设备）。
  - 管线正在用的麦克风不需要唤醒（它本来就被 MicCapturer 占着）。
  - 其它都不开。

- **进程列表和麦克风列表用差异更新**——`Clear()` 后重建会让 WPF 中途丢选中态，体验很差。`Add` / `Remove` 差量保留选中。

## 这张图里**没有**的东西（故意的）

- **`NoiseGate`**——RNNoise 之前的旧降噪。代码还在、测试还在用，但管线没接它。别删，测试套件需要它做 DSP fixture。
- **UWP / Apple Music 抓不到**——Windows 把这类应用的音频会话归到系统服务（`audiodg`、`svchost`）名下，per-process loopback 看不见用户层 app。这是 Windows 限制，不是 bug。`ProcessEnumerator` 的归并能挽救一部分，但用户选了 Apple Music 没声音是这个原因。
- **DAW 级的限制器 / EQ / 压缩**——超范围。软限幅只防爆音，不修音色。

## 构建和发版

```powershell
# 编译（cwd: src/VoicePipe）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false

# 测试（cwd: tests/VoicePipe.Tests）
dotnet test
# CsCheck 26 个属性测试

# 打包（cwd: 项目根）
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "VoicePipe.iss"
# → Output\VoicePipeSetup.exe
```

被坑过的验证规则：

1. **不要只信"build succeeded"**——WPF 资源错误只在 XAML 解析时（窗口构造时）才报。一定要跑 publish 产物、读窗口标题——是"启动错误"就有问题。
2. **看 Serilog 输出有没有 `[FTL]` 或异常 `[ERR]`**——发过窗口正常打开但 RNNoise 没加载的版本，看着没事其实坏了。
3. **音频效果必须在装了 VB-Cable + 真麦克风的机器上验**。开发机 `Cable=false` 是正常的，测试机应为 `true`。
4. **`.iss` 和所有 `Langs/*.xaml` 必须 UTF-8 with BOM**。PowerShell 批量替换会撕掉 BOM，中文变 `?`。验证方式：检查文件头是否 `EF BB BF`。
