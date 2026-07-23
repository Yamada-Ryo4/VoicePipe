using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Hardcodet.Wpf.TaskbarNotification;
using VoicePipe.Services;

namespace VoicePipe.UI;

public partial class MainWindow : Window
{
    private bool _isDark = true;
    private string _currentLang = "zh-CN";

    private ResourceDictionary? _themeDict;
    private ResourceDictionary? _langDict;      // active language, merged ON TOP (wins lookup)
    private ResourceDictionary? _langBaseDict;  // en-US fallback BASE, kept beneath the active language
    private LogConsoleWindow?   _consoleWindow;

    private const string FallbackLang = "en-US";

    // ── 全局热键 + 自启服务 ──
    private readonly HotkeyManager _hotkeys = new();
    private readonly AutoStartService _autoStart = new();
    private bool _realExit; // 托盘"退出"或 MinimizeToTray=false 时为 true，允许真正关闭

    public MainWindow()
    {
        // ★ 修复冷启动黑屏：资源字典加载前 Background 的 DynamicResource 解析为 null（透明/黑）。
        //   InitializeComponent 后立即设硬编码 fallback；UpdateResourceDictionaries 加载主题后
        //   重新 SetResourceReference 让 DynamicResource BgDeepBrush 接管（主题切换也能跟随）。
        InitializeComponent();
        Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12)); // fallback

        var settings = Core.AppSettings.Current;
        _isDark = settings.IsDarkTheme; // ★ 恢复上次的主题选择
        SetLanguage(settings.Language);

        // 同步主题图标/文字
        ThemeIcon.Text = _isDark ? "🌙" : "☀";
        string darkLbl = Application.Current.TryFindResource("StrThemeDark") as string ?? "Dark";
        string lightLbl = Application.Current.TryFindResource("StrThemeLight") as string ?? "Light";
        ThemeLabel.Text = " " + (_isDark ? darkLbl : lightLbl);

        foreach (ComboBoxItem item in LangSelector.Items)
        {
            if (item.Tag.ToString() == settings.Language)
            {
                LangSelector.SelectedItem = item;
                break;
            }
        }

        // ★ 窗口聚焦/失焦时切换可视化刷新率（挂后台时降帧省 CPU，波形仍在动）
        Activated   += (_, _) => (DataContext as ViewModels.MainViewModel)?.SetWindowFocused(true);
        Deactivated += (_, _) => (DataContext as ViewModels.MainViewModel)?.SetWindowFocused(false);

        // ★ 窗口句柄就绪后初始化全局热键并注册保存的绑定；同时对账开机自启状态
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var settings = Core.AppSettings.Current;

        // 托盘图标：从 exe 已嵌入的图标提取（GDI+，宽容），避免 WPF 严格解码器拒绝 .ico 导致崩溃。
        // try-catch 兜底：即使失败也只是托盘无图标，绝不影响启动。
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                TrayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "托盘图标加载失败，使用默认");
        }

        // 初始化热键管理器（挂到本窗口的消息泵）
        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkeys.Initialize(hwnd);
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        // ★ 初始注册时也要把冲突状态记下，传给 InitSettings 在 UI 上正确显示
        bool muteOk     = _hotkeys.Register(HotkeyManager.Action.ToggleMute, settings.MuteHotkey);
        bool pipelineOk = _hotkeys.Register(HotkeyManager.Action.TogglePipeline, settings.PipelineHotkey);

        // 注入设置服务到 ViewModel（内嵌设置面板用），并把初始冲突状态传过去
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.InitSettings(_hotkeys, _autoStart);
            // 仅当热键真有设值且注册失败时才标冲突；未设置（None）的不算
            vm.MuteHotkeyConflict     = settings.MuteHotkey.IsSet     && !muteOk;
            vm.PipelineHotkeyConflict = settings.PipelineHotkey.IsSet && !pipelineOk;
            if (vm.MuteHotkeyConflict || vm.PipelineHotkeyConflict)
                Serilog.Log.Warning("启动时热键冲突: Mute={MuteConflict} Pipeline={PipelineConflict}",
                    vm.MuteHotkeyConflict, vm.PipelineHotkeyConflict);
        }

        // 对账开机自启：以注册表实际状态为准回写持久化意图
        try { settings.AutoStartBoot = _autoStart.IsEnabled(); } catch { }

        // ★ 开机静默到托盘：仅当随系统启动（带 --boot 参数）时才 Hide，
        //   手动打开时不 Hide（否则每次手动开都要从托盘还原，且冷启动黑屏）。
        bool isBootLaunch = Environment.GetCommandLineArgs().Contains("--boot", StringComparer.OrdinalIgnoreCase);
        if (isBootLaunch && settings.StartMinimized && settings.MinimizeToTray)
        {
            Serilog.Log.Information("开机静默到托盘：隐藏主窗口（--boot 启动）");
            Hide();
        }
    }

    private void OnHotkeyPressed(object? sender, HotkeyManager.Action action)
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        Serilog.Log.Information("全局热键触发: {Action}", action);
        switch (action)
        {
            case HotkeyManager.Action.ToggleMute:
                vm.ToggleMicMute();
                // ★ 托盘气泡提示：游戏全屏看不到界面时，确认静音状态切换
                ShowTrayTip(vm.IsMicMuted
                    ? (Application.Current.TryFindResource("StrTipMicMuted") as string ?? "Microphone muted")
                    : (Application.Current.TryFindResource("StrTipMicUnmuted") as string ?? "Microphone unmuted"));
                break;
            case HotkeyManager.Action.TogglePipeline:
                vm.TogglePipeline();
                ShowTrayTip(vm.IsRunning
                    ? (Application.Current.TryFindResource("StrTipPipelineOn") as string ?? "Mixing started")
                    : (Application.Current.TryFindResource("StrTipPipelineOff") as string ?? "Mixing stopped"));
                break;
        }
    }

    /// <summary>用托盘气泡显示一条短提示（热键触发时，游戏全屏也能看到系统通知）。</summary>
    private void ShowTrayTip(string message)
    {
        try { TrayIcon?.ShowBalloonTip("VoicePipe", message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.None); }
        catch { /* 气泡失败不影响功能 */ }
    }

    /// <summary>
    /// 关闭请求：MinimizeToTray=true 时缩到托盘（取消关闭、隐藏窗口、管线继续运行）；
    /// 否则执行完整清理并退出。托盘"退出"会先置 _realExit=true 再关闭。(Req 3.1, 3.5, 3.6)
    /// </summary>
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_realExit && Core.AppSettings.Current.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            Serilog.Log.Information("窗口关闭->最小化到托盘（管线继续运行）");
            return;
        }


        // 完整清理：注销热键、清理管线、释放托盘
        Serilog.Log.Information("应用退出，开始清理");
        try { _hotkeys.HotkeyPressed -= OnHotkeyPressed; _hotkeys.Dispose(); } catch { }
        if (DataContext is ViewModels.MainViewModel vm)
        {
            try { vm.Cleanup(); } catch { }
        }
        _consoleWindow?.Close();
        try { TrayIcon?.Dispose(); } catch { }

        // OnExplicitShutdown 下需要显式退出
        Application.Current.Shutdown();
    }

    /// <summary>
    /// 触摸滑动到边界时阻止窗口抖动（rubber-band 反馈）。
    /// </summary>
    private void Window_ManipulationBoundaryFeedback(object sender, System.Windows.Input.ManipulationBoundaryFeedbackEventArgs e)
    {
        e.Handled = true;
    }

    // ── 系统托盘：还原 / 退出 ──
    private void Tray_Restore(object sender, RoutedEventArgs e)
    {
        Serilog.Log.Information("托盘：还原窗口");
        Show();
        WindowState = WindowState.Normal;
        // ★ 修复从托盘还原黑屏：Show() 后 WPF 需要完成一次 layout/render pass 才能画出内容。
        //   强制 Dispatcher 以 Render 优先级刷新，确保内容渲染完再 Activate（避免用户看到黑屏）。
        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, () => { });
        Activate();
    }

    private void Tray_Exit(object sender, RoutedEventArgs e)
    {
        Serilog.Log.Information("托盘：退出");
        _realExit = true;
        Close();
    }

    // ── 音量重置：App=70% Mic=100%（默认值）──
    private void ResetVolume_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        vm.AppGain = 0.70f;
        vm.MicGain = 1.0f;
        Serilog.Log.Information("音量已重置为默认值 App=70% Mic=100%");
    }

    // ── 热键重置：把两个热键都清为"未设置"（None），并注销已注册的全局热键 ──
    private void ResetHotkeys_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        vm.ResetHotkeys();
        Serilog.Log.Information("热键已重置为未设置");
    }

    // ── VB-Cable 驱动修复 ──
    private async void RepairVbCable_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        await vm.RepairVbCableAsync();
    }

    // ── 首次使用引导："知道了"关闭并持久化 ──
    private void GuideGotIt_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as ViewModels.MainViewModel)?.DismissFirstRunGuide();
        Serilog.Log.Information("首次使用引导已关闭");
    }

    // ── 设置面板切换（主页面内嵌视图 + 滑动淡入淡出动画）──
    private void ToggleSettings_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        vm.ShowSettings = !vm.ShowSettings;
        Serilog.Log.Information("设置面板: {State}", vm.ShowSettings ? "打开" : "关闭");
        AnimateSettings(vm.ShowSettings);
    }

    /// <summary>
    /// 设置页切入/切出动画（前进/后退镜像，符合直觉）：
    /// 进入设置（前进）：设置页从右侧滑入+淡入，主内容向左滑出+淡出；
    /// 返回（后退）：主内容从左侧滑入+淡入，设置页向右滑出+淡出（与进入完全镜像）。
    /// </summary>
    private void AnimateSettings(bool showSettings)
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(220));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        const double slide = 40;

        // ★ 方向随前进/后退镜像：
        //   前进(进设置): 新页从右(+slide)进，旧页向左(-slide)出
        //   后退(返回):   新页从左(-slide)进，旧页向右(+slide)出
        double inFrom = showSettings ? slide : -slide;
        double outTo  = showSettings ? -slide : slide;

        View incoming = showSettings
            ? new View(SettingsView, SettingsTransform)
            : new View(MainContentView, MainContentTransform);
        View outgoing = showSettings
            ? new View(MainContentView, MainContentTransform)
            : new View(SettingsView, SettingsTransform);

        // 进入视图：从 inFrom 滑到 0，透明度 0→1
        incoming.Element.Visibility = Visibility.Visible;
        incoming.Transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new DoubleAnimation(inFrom, 0, dur) { EasingFunction = ease });
        incoming.Element.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, dur) { EasingFunction = ease });

        // 退出视图：滑到 outTo，透明度 1→0，动画结束后隐藏
        outgoing.Transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new DoubleAnimation(0, outTo, dur) { EasingFunction = ease });
        var fadeOut = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };
        var hideTarget = outgoing.Element;
        fadeOut.Completed += (_, _) => { if (hideTarget.Opacity == 0) hideTarget.Visibility = Visibility.Collapsed; };
        outgoing.Element.BeginAnimation(OpacityProperty, fadeOut);
    }

    private readonly record struct View(FrameworkElement Element, System.Windows.Media.TranslateTransform Transform);

    private void OpenConsole_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_consoleWindow == null || !_consoleWindow.IsLoaded)
        {
            Serilog.Log.Information("打开实时日志控制台");
            _consoleWindow = new LogConsoleWindow();
            _consoleWindow.Show();
        }
        else
        {
            _consoleWindow.Activate();
            if (_consoleWindow.WindowState == WindowState.Minimized)
                _consoleWindow.WindowState = WindowState.Normal;
        }
    }

    private void ToggleTheme_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isDark = !_isDark;
        UpdateResourceDictionaries();

        ThemeIcon.Text = _isDark ? "🌙" : "☀";
        string dark = Application.Current.TryFindResource("StrThemeDark") as string ?? "Dark";
        string light = Application.Current.TryFindResource("StrThemeLight") as string ?? "Light";
        ThemeLabel.Text = " " + (_isDark ? dark : light);

        // ★ 持久化主题选择（共享单例，不会覆盖其他字段）
        var settings = Core.AppSettings.Current;
        settings.IsDarkTheme = _isDark;
        settings.Save();
        Serilog.Log.Information("主题切换: {Theme}", _isDark ? "暗色" : "亮色");
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LangSelector.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            string newLang = item.Tag.ToString()!;
            if (newLang != _currentLang)
            {
                SetLanguage(newLang);
                var settings = Core.AppSettings.Current;
                settings.Language = newLang;
                settings.Save();
                Serilog.Log.Information("语言切换: {Lang}", newLang);

                // Update theme button label after lang change
                string dark = Application.Current.TryFindResource("StrThemeDark") as string ?? "Dark";
                string light = Application.Current.TryFindResource("StrThemeLight") as string ?? "Light";
                ThemeLabel.Text = " " + (_isDark ? dark : light);
            }
        }
    }

    private void SetLanguage(string langCode)
    {
        _currentLang = langCode;
        UpdateResourceDictionaries();
    }

    private void UpdateResourceDictionaries()
    {
        var merged = Application.Current.Resources.MergedDictionaries;

        var newTheme = new ResourceDictionary
        {
            Source = new Uri(_isDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };

        if (_themeDict != null && merged.Contains(_themeDict))
            merged[merged.IndexOf(_themeDict)] = newTheme;
        else
            merged.Add(newTheme);
        _themeDict = newTheme;

        // en-US fallback BASE: always merged beneath the active language so any key missing
        // from the active language resolves to en-US. WPF searches merged dictionaries in
        // reverse order, so this base must remain BEFORE (beneath) the active-language dict.
        if (_langBaseDict == null || !merged.Contains(_langBaseDict))
        {
            var baseLang = new ResourceDictionary
            {
                Source = new Uri($"Langs/{FallbackLang}.xaml", UriKind.Relative)
            };
            merged.Add(baseLang);
            _langBaseDict = baseLang;
        }

        // Active language, merged ON TOP of the fallback base so its keys win the lookup.
        // When the active language IS en-US, the base already provides every key; we still
        // merge it on top so the resolution order is uniform and switching back works.
        var newLang = new ResourceDictionary
        {
            Source = new Uri($"Langs/{_currentLang}.xaml", UriKind.Relative)
        };

        if (_langDict != null && merged.Contains(_langDict))
            merged[merged.IndexOf(_langDict)] = newLang;
        else
            merged.Add(newLang);
        _langDict = newLang;

        // ★ 修复冷启动黑屏：主题/语言字典加载完毕后，让 Background 切回 DynamicResource。
        //   构造函数里设了硬编码 fallback #121212，现在资源已就绪，SetResourceReference 让
        //   BgDeepBrush DynamicResource 接管（主题切换也能跟随）。
        SetResourceReference(BackgroundProperty, "BgDeepBrush");
    }

    /// <summary>代理模式 RadioButton Click：直接设 ViewModel.ProxyMode（避免双向 binding 循环触发）</summary>
    private void ProxyMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string mode && DataContext is ViewModels.MainViewModel vm)
            vm.ProxyMode = mode;
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    // ── 降噪标题行点击：折叠/展开卡片（纯 UI，不动 NoiseGateEnabled 功能开关；点 CheckBox 自己除外） ──
    private void ToggleNoiseGate_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        if (IsInsideCheckBox(e.OriginalSource as System.Windows.DependencyObject)) return;
        vm.NoiseGateExpanded = !vm.NoiseGateExpanded;
    }

    // ── 监听标题行点击：折叠/展开卡片（纯 UI，不动 MonitorEnabled 功能开关；点 CheckBox 自己除外） ──
    private void ToggleMonitor_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel vm) return;
        if (IsInsideCheckBox(e.OriginalSource as System.Windows.DependencyObject)) return;
        vm.MonitorExpanded = !vm.MonitorExpanded;
    }

    private static bool IsInsideCheckBox(System.Windows.DependencyObject? src)
    {
        while (src != null)
        {
            if (src is System.Windows.Controls.CheckBox) return true;
            src = System.Windows.Media.VisualTreeHelper.GetParent(src);
        }
        return false;
    }

    // ── 麦克风下拉菜单打开/关闭：控制 PeakMonitor 是否临时监听全部麦克风 ──
    private void MicCombo_DropDownOpened(object sender, EventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
            vm.OnMicDropDownOpened();
    }

    private void MicCombo_DropDownClosed(object sender, EventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
            vm.OnMicDropDownClosed();
    }
}