using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using NeoEditor.Player.Services;
using Serilog;

namespace NeoEditor.Player;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // v2.69: the boot log exists BEFORE anything that can crash — silent startup
        // crashes (spinning cursor, then nothing) are diagnosed from
        // {exe}/logs/player-boot-*.log. Game/run logs stay in player-run-*.log.
        BootstrapLog.Initialize();
        BootstrapLog.Write(
            $"Program.Main 进入（args: {string.Join(" ", args)}；OS: {Environment.OSVersion}；" +
            $"CLR: {Environment.Version}；WebView2 Runtime: " +
            $"{(WebView2RuntimeCheck.IsInstalled() ? "已安装" : "未安装")}）");

        // Crash safety net from the very first line — App.axaml.cs adds run-log writers
        // later; these guarantee the boot log always carries the failure reason.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            BootstrapLog.WriteException("AppDomain 未处理异常", e.ExceptionObject as Exception
                ?? new Exception(e.ExceptionObject?.ToString() ?? "未知异常对象"));
        TaskScheduler.UnobservedTaskException += (_, e) =>
            BootstrapLog.WriteException("未观察任务异常", e.Exception);

        // WebView2 user data (cache / EBWebView) must NOT pollute the app folder — the
        // distribution stays clean (v2.27). Must run before any WebView2 environment exists.
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeoScavengerPlayer", "WebView2"));

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        // Dragging a SWF onto the exe passes its path as a command-line argument.
        App.StartupSwfPath = args
            .FirstOrDefault(a => a.EndsWith(".swf", StringComparison.OrdinalIgnoreCase) && File.Exists(a));

        try
        {
            BootstrapLog.Write("Avalonia 应用启动（StartWithClassicDesktopLifetime）");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            BootstrapLog.Write("Avalonia 正常退出");
        }
        catch (Exception ex)
        {
            // The boot log carries the reason; rethrow keeps the Windows error-reporting
            // behavior unchanged.
            BootstrapLog.WriteException("Avalonia 启动/运行异常", ex);
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseWin32()      // v2.27: Windows-only build target (Avalonia.Win32 backend)
            .UseSkia()       // v2.27: rendering is a separate package since Avalonia 12
            .UseHarfBuzz()   // v2.27: text shaping (Inter font) is separate too
            .WithInterFont()
            .LogToTrace();
}
