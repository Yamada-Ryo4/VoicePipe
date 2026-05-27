using Serilog;
using System.Windows;
using VoicePipe.Bootstrapper;
using VoicePipe.Sinks;

namespace VoicePipe;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 全局未捕获异常处理：防止静默闪退
        DispatcherUnhandledException += (s, args) =>
        {
            Log.Fatal(args.Exception, "未捕获的 UI 线程异常");
            MessageBox.Show(
                $"VoicePipe 遇到错误：\n\n{args.Exception.Message}\n\n详细信息已记录到日志文件。",
                "VoicePipe 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log.Fatal(ex, "未捕获的非UI线程异常");
        };

        // 初始化 Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                System.IO.Path.Combine(AppContext.BaseDirectory, "logs", "voicepipe.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .WriteTo.Sink(InMemoryLogSink.Instance)
            .CreateLogger();

        Log.Information("VoicePipe 启动");

        base.OnStartup(e);

        // 依赖检查
        var status = DependencyChecker.Check();
        if (!status.WindowsOk)
        {
            MessageBox.Show(
                $"VoicePipe 需要 Windows 10 Build 19041+ (20H1)\n\n当前: {status.WindowsVersion}",
                "系统版本不兼容", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        try
        {
            var mainWindow = new UI.MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "MainWindow 启动失败");
            MessageBox.Show(
                $"VoicePipe 启动失败：\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("VoicePipe 退出");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
