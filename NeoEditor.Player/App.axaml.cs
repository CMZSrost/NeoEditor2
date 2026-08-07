using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using NeoEditor.Player.Core;
using NeoEditor.Player.Core.Data;
using NeoEditor.Player.Core.Logging;
using NeoEditor.Player.Core.Services;
using NeoEditor.Player.Services;
using NeoEditor.Player.ViewModels;
using NeoEditor.Player.Views;
using Serilog;

namespace NeoEditor.Player;

/// <summary>Standalone player application (Docs/42 §3.8 / P5): no editor session — disk mode.</summary>
public partial class App : Application
{
    public static PlayerServices Services { get; private set; } = null!;

    /// <summary>File run-log sink (v2.25) — crash handlers append fatal lines to it.</summary>
    public static FileRunLogWriter FileLog => Services.FileLog;

    /// <summary>SWF path passed as a command-line argument (drag onto exe).</summary>
    public static string? StartupSwfPath { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The game-quit path closes the main window and must end the process even if
            // the log overlay is still open (default OnLastWindowClose would linger).
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;

            Services = PlayerServices.Create();
            // v2.28: apply persisted theme + language before the window shows.
            if (Services.Config.Config.Theme is "Light" or "Dark")
                RequestedThemeVariant = Services.Config.Config.Theme == "Light"
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark;
            LocalizationManager.Instance.SetLanguage(Services.Config.Language);
            // Crash safety net (v2.25): managed exceptions land in the run log file too —
            // with per-line flush the file already has everything up to the crash.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                FileLog.WriteCrash(e.ExceptionObject?.ToString() ?? "未处理异常");
            TaskScheduler.UnobservedTaskException += (_, e) =>
                FileLog.WriteCrash("未观察任务异常: " + e.Exception);
            desktop.MainWindow = new PlayerWindow
            {
                DataContext = Services.ViewModel,
                StartupSwfPath = StartupSwfPath,
            };
            desktop.Exit += (_, _) => Services.Dispose();
            // R43: 启动日志首行带版本（调试菜单「关于」同源）。
            Log.Logger.Information("[Player] {Product} v{Version} standalone player started" +
                                   (StartupSwfPath is null ? "" : $" (swf: {StartupSwfPath})"),
                NeoEditor.Player.Services.AppInfo.ProductName, NeoEditor.Player.Services.AppInfo.Version);
        }

        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Manual composition root for the standalone player (no DI container needed for this
/// small app). Disk mode: ProxyEnabled=false → every request is served from disk.
/// </summary>
public sealed class PlayerServices : IDisposable
{
    private PlayerServices(RunLogStore logStore, PlayerConfigService config,
        GameContentServer server, PlayerViewModel viewModel, FileRunLogWriter fileLog)
    {
        LogStore = logStore;
        Config = config;
        Server = server;
        ViewModel = viewModel;
        FileLog = fileLog;
    }

    public RunLogStore LogStore { get; }
    public PlayerConfigService Config { get; }
    public GameContentServer Server { get; }
    public PlayerViewModel ViewModel { get; }
    public FileRunLogWriter FileLog { get; }

    public static PlayerServices Create()
    {
        var config = new PlayerConfigService();
        config.LoadAsync().GetAwaiter().GetResult();   // v2.28: persisted theme/language
        // v2.36: persist a loopback port on first run — a stable origin keeps the
        // game's localStorage saves (Ruffle SharedObject) across launches.
        if (config.Config.ServerPort <= 0)
        {
            config.Config.ServerPort = Random.Shared.Next(10000, 20000);
            config.SaveAsync().GetAwaiter().GetResult();
        }
        var logStore = new RunLogStore();
        var logs = new SwfLogBridge(logStore);
        var proxy = new ProxyHttpModule(config, new GamePhpGenerator(), new DiskGameDataExportService())
        {
            // Standalone: no editor session → pure static serving (Docs/42 §3.8).
            ProxyEnabled = false,
        };
        var server = new GameContentServer(config, proxy, logs);
        var fileLog = new FileRunLogWriter(logStore);   // per-run files, newest 2 kept (v2.25)
        var viewModel = new PlayerViewModel(config, server, logStore, new DataBrowserService(config))
        {
            FileLogDirectory = fileLog.LogDirectory,
        };
        // Game-side Flash events (exit / blocked navigation / stubs) → host reactions.
        logs.GameEventDetected += viewModel.HandleGameEvent;
        return new PlayerServices(logStore, config, server, viewModel, fileLog);
    }

    public void Dispose()
    {
        Server.Dispose();
        FileLog.Dispose();
        ViewModel.Dispose();
    }
}
