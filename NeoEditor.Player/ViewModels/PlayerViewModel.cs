using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Player.Core.Data;
using NeoEditor.Player.Core.Logging;
using NeoEditor.Player.Core.Services;
using NeoEditor.Player.Services;
using Serilog;

namespace NeoEditor.Player.ViewModels;

/// <summary>
/// Standalone player view model (Docs/42 §3.8 / P5): opens a game SWF, serves it through
/// GameContentServer (disk mode), and exposes the run log store for the log viewer panel.
/// </summary>
public sealed partial class PlayerViewModel : ObservableObject, IDisposable
{
    public static readonly string[] LevelFilters = ["全部", "console", "clipboard", "error", "warn", "debug"];

    private readonly PlayerConfigService _config;
    private readonly GameContentServer _server;
    private readonly RunLogStore _logStore;
    private string _currentSwf = "";

    /// <summary>Side data browser (Docs/42 v2.12).</summary>
    public DataBrowserViewModel DataBrowser { get; }

    public event Action<Uri>? NavigateRequested;
    public event Action? RefreshRequested;
    public event Action? OpenFileDialogRequested;

    /// <summary>
    /// The game asked to quit — the host resets back to the idle "drop SWF" state
    /// (player torn down, placeholder visible). Closing the app stays on the window X.
    /// </summary>
    public event Action? ResetRequested;

    /// <summary>「后台运行」toggle changed — the view applies window.__backgroundMode.</summary>
    public event Action<bool>? BackgroundModeChanged;

    /// <summary>Fatal game error detected (R38) — the view shows the capture dialog.</summary>
    public event Action<string>? GameErrorDetected;

    /// <summary>Log export finished — the view opens the exported file in Explorer.</summary>
    public event Action<string>? LogExportCompleted;

    /// <summary>
    /// C#→JS bridge (R38, export feature): wired by the view after the webview exists
    /// (same InvokeScript pattern as the storage manager). Null while no page is loaded.
    /// </summary>
    public Func<string, Task<string?>>? ExecuteJs { get; set; }

    private DateTime _lastErrorDialogAt = DateTime.MinValue;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _levelFilter = "全部";
    [ObservableProperty] private bool _backgroundMode = true;   // 默认后台运行（失焦不暂停）
    [ObservableProperty] private ObservableCollection<PlayerLogLine> _visibleLines = [];

    /// <summary>Directory the file log sink writes to (surfaced in the log overlay).</summary>
    [ObservableProperty] private string? _fileLogDirectory;

    /// <summary>当前游戏根目录（About/导出用；未加载时为空串）。</summary>
    public string GameRootDir => _config.Config.GameRootDir;

    /// <summary>UI theme: "System" / "Light" / "Dark" (persisted via AppConfig, v2.28).</summary>
    [ObservableProperty] private string _theme = "System";

    /// <summary>UI language: "zh" / "en" (persisted, v2.28).</summary>
    [ObservableProperty] private string _language = "zh";

    /// <summary>True once a SWF is running (v2.42) — gates Data Browser / Save Manager menu items.</summary>
    [ObservableProperty] private bool _isSwfLoaded;

    private Uri? _currentUri;

    public PlayerViewModel(PlayerConfigService config, GameContentServer server, RunLogStore logStore,
        DataBrowserService dataBrowserService)
    {
        _config = config;
        _server = server;
        _logStore = logStore;
        // v2.29: incremental append via LineAppended (per line) — the old LogAdded path
        // rebuilt the whole VisibleLines collection on every batch, which made the log
        // overlay's scrollbar jump while a long game session streamed lines.
        _logStore.LineAppended += OnLineAppended;
        DataBrowser = new DataBrowserViewModel(dataBrowserService)
        {
            // R56: 图片缺失诊断 → 日志文件（跑完直接读 logs/ 定位）
            LogAction = msg => _logStore.Append("databrowser", "debug", msg),
        };
        Theme = config.Config.Theme;          // persisted theme (v2.28)
        Language = config.Language;           // persisted language (v2.28)
        StatusText = L("Status.NotLoaded");
        RefreshLines();
    }

    /// <summary>Localized string lookup (v2.28).</summary>
    private static string L(string key) => LocalizationManager.Instance[key];

    partial void OnThemeChanged(string value)
    {
        // "System" = follow the OS; Light/Dark force the variant. Every window inherits
        // the application-level variant (data browser / overlays no longer hard-code Dark).
        Application.Current!.RequestedThemeVariant = value switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        _config.Config.Theme = value;
        _ = _config.SaveAsync();
    }

    partial void OnLanguageChanged(string value)
    {
        LocalizationManager.Instance.SetLanguage(value);
        _config.Language = value;
        _ = _config.SaveAsync();
        // Re-apply the idle status text in the new language.
        if (_currentUri is null)
            StatusText = L("Status.NotLoaded");
    }

    /// <summary>
    /// React to detected game-side Flash events (Docs/42 v2.5). Fired from the log server
    /// thread — marshal to the UI thread before touching state / raising UI events.
    /// </summary>
    public void HandleGameEvent(object? sender, GameEventDetectedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            switch (e.Type)
            {
                case PlayerGameEventType.GameExit:
                    // Back to the idle "drop SWF" state — the host tears the player down
                    // and re-shows the placeholder (closing the app stays on window X).
                    // R38: 若本 run 出现过 error 级行，判定为异常退出（AVM 崩溃后退出）。
                    StatusText = LastRunHasErrors() ? L("Status.ErrorExit") : L("Status.Quit");
                    _currentUri = null;
                    _currentSwf = "";
                    ResetRequested?.Invoke();
                    break;
                case PlayerGameEventType.NavigationBlocked:
                    StatusText = L("Status.Blocked");
                    break;
                case PlayerGameEventType.ApiStub:
                    Log.Logger.ForContext("Source", "WebViewPreview")
                        .Information("[GameEvent] stub: {Detail}", e.Detail);
                    break;
                case PlayerGameEventType.GameError:
                    // R38: 报错捕捉——状态栏警示 + 弹窗（宿主侧）。去抖 30s：崩溃后
                    // 控制台持续刷错误行，不能让弹窗每 10s 弹一次。
                    StatusText = L("Status.ErrorDetected");
                    if (DateTime.UtcNow - _lastErrorDialogAt < TimeSpan.FromSeconds(30)) break;
                    _lastErrorDialogAt = DateTime.UtcNow;
                    GameErrorDetected?.Invoke(e.Detail.Length > 300 ? e.Detail[..300] + "…" : e.Detail);
                    break;
            }
        });
    }

    partial void OnBackgroundModeChanged(bool value) => BackgroundModeChanged?.Invoke(value);

    public ObservableCollection<PlayerRunRecord> Runs => _logStore.Runs;

    [RelayCommand]
    private void OpenSwf() => OpenFileDialogRequested?.Invoke();

    [RelayCommand]
    private void Reload()
    {
        // Reload the CURRENT page — a same-URL Navigate may be ignored by the webview,
        // so the view refreshes the page (old Ruffle instance is destroyed with it).
        if (_currentSwf.Length > 0) RefreshRequested?.Invoke();
    }

    /// <summary>
    /// v2.49: storage-manager restart — reloads the game page so Ruffle drops its
    /// SharedObject memory cache and re-reads localStorage (delete/restore take effect).
    /// </summary>
    public void RestartGame() => Reload();

    [RelayCommand]
    private void Stop()
    {
        // Explicit stop: navigate to a blank page — the old game (wasm/audio/timers)
        // is torn down with the document.
        _currentUri = null;
        StatusText = L("Status.Stopped");
        NavigateRequested?.Invoke(new Uri("about:blank"));
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _logStore.Clear();
        VisibleLines.Clear();   // LineAppended does not fire on Clear (v2.29)
    }

    /// <summary>
    /// R38: 导出日志——头部信息 + localStorage 快照 + 全部 run 日志行写入
    /// player-log-export-*.txt（日志目录），完成后 LogExportCompleted 交给宿主
    /// 在 Explorer 中定位文件。localStorage 快照走 __dumpLocalStorage()（webview
    /// 未加载时跳过）；报告格式见 RunLogReport。
    /// </summary>
    [RelayCommand]
    private async Task ExportLogs()
    {
        try
        {
            var dir = FileLogDirectory ?? FileRunLogWriter.ResolveDirectory(null);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"player-log-export-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

            string? ls = null;
            if (ExecuteJs is not null)
            {
                try
                {
                    ls = await ExecuteJs("window.__dumpLocalStorage ? window.__dumpLocalStorage() : []");
                }
                catch (Exception)
                {
                    ls = "(localStorage 读取失败)";
                }
            }

            var header = string.Join(Environment.NewLine,
                $"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"游戏 SWF: {_currentSwf}",
                $"游戏根目录: {_config.Config.GameRootDir}",
                $"日志目录: {dir}",
                $"WebView2 数据目录: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeoScavengerPlayer", "WebView2")}");

            await File.WriteAllTextAsync(path, RunLogReport.Build(header, _logStore.Runs, ls));
            StatusText = string.Format(L("Log.Exported"), path);
            LogExportCompleted?.Invoke(path);
        }
        catch (Exception ex)
        {
            StatusText = "导出失败: " + ex.Message;
        }
    }

    /// <summary>
    /// R43: 导出存档+日志 zip（试用反馈/存档迁移包）——localStorage 全量存档
    /// （__exportSaves）+ 当前日志文件 + save_backup 备份 + info.txt，打包为
    /// <c>NeoScavengerPlayer-export-{版本}-{时间戳}.zip</c>，完成后 LogExportCompleted
    /// 交给宿主在 Explorer 定位。
    /// </summary>
    [RelayCommand]
    private async Task ExportBundleZip()
    {
        try
        {
            var dir = FileLogDirectory ?? FileRunLogWriter.ResolveDirectory(null);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir,
                $"NeoScavengerPlayer-export-{AppInfo.Version}-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

            string? savesJson = null;
            if (ExecuteJs is not null)
            {
                try
                {
                    savesJson = await ExecuteJs("window.__exportSaves ? window.__exportSaves() : []");
                }
                catch (Exception)
                {
                    savesJson = null;   // webview 不可用 → 包里没有存档部分
                }
            }

            // v2.69: the feedback bundle also carries the boot log (startup crash diagnosis).
            var logFiles = Directory.EnumerateFiles(dir, "player-run-*.log")
                .Concat(Directory.EnumerateFiles(dir, "player-boot-*.log"))
                .ToList();
            var backupDir = Path.Combine(_config.Config.GameRootDir, "save_backup");
            var backupFiles = Directory.Exists(backupDir)
                ? Directory.EnumerateFiles(backupDir, "*.json").ToList()
                : [];

            var info = string.Join(Environment.NewLine,
                $"{AppInfo.ProductName} v{AppInfo.Version}",
                $"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"游戏根目录: {_config.Config.GameRootDir}",
                $"日志目录: {dir}",
                $"WebView2 数据目录: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeoScavengerPlayer", "WebView2")}",
                $"localStorage 存档: {(savesJson is null ? "(webview 未加载, 未包含)" : "已包含")}",
                $"日志文件: {logFiles.Count} 个",
                $"存档备份 (save_backup): {backupFiles.Count} 个");

            PlayerBundleExporter.Export(path, info, savesJson, logFiles, backupFiles);
            StatusText = string.Format(L("Log.Exported"), path);
            LogExportCompleted?.Invoke(path);
        }
        catch (Exception ex)
        {
            StatusText = "导出失败: " + ex.Message;
        }
    }

    /// <summary>Current run had error-level lines (R38: 游戏异常退出判定)。</summary>
    private bool LastRunHasErrors()
        => _logStore.Runs.LastOrDefault()?.Lines
            .Any(l => l.Level.Equals("error", StringComparison.OrdinalIgnoreCase)) ?? false;

    /// <summary>Start the loopback server (disk mode) and load the SWF through the host page.</summary>
    public async System.Threading.Tasks.Task StartAsync(string swfPath)
    {
        if (!RuffleWebAssets.IsAvailable())
        {
            StatusText = L("Status.ResourcesMissing");
            return;
        }

        var gameRoot = Path.GetDirectoryName(swfPath) ?? "";
        if (gameRoot.Length == 0 || !Directory.Exists(gameRoot))
        {
            StatusText = L("Status.RootUnknown");
            return;
        }

        _config.Config.GameRootDir = gameRoot;

        if (!_server.Start())
        {
            StatusText = L("Status.ServerFailed");
            return;
        }

        _currentSwf = swfPath;
        var relative = Path.GetFileName(swfPath);
        var uri = new Uri(_server.BaseUrl! + "?swf=" + Uri.EscapeDataString(relative));
        StatusText = string.Format(L("Status.Loading"), relative, gameRoot);
        IsSwfLoaded = true;
        RequestNavigate(uri);
    }

    /// <summary>
    /// Navigate-or-reload: a same-URL Navigate may be ignored by the webview, so when the
    /// target equals the current URL we refresh the page instead (old game torn down with
    /// the document, new one starts from the same query).
    /// </summary>
    private void RequestNavigate(Uri uri)
    {
        if (uri.Equals(_currentUri))
        {
            RefreshRequested?.Invoke();
            return;
        }

        _currentUri = uri;
        NavigateRequested?.Invoke(uri);
    }

    /// <summary>Append one line when it matches the current level filter (v2.29 incremental —
    /// the collection only grows, so the virtualized ListBox scrollbar stays stable).</summary>
    private void OnLineAppended(object? sender, PlayerLogLineAppendedEventArgs e)
        => Dispatcher.UIThread.Post(() =>
        {
            if (Matches(e.Line, LevelFilter))
                VisibleLines.Add(e.Line);
        });

    private void RefreshLines()
    {
        var filter = LevelFilter;
        var lines = new List<PlayerLogLine>();
        foreach (var run in _logStore.Runs)
        foreach (var line in run.Lines)
        {
            if (Matches(line, filter)) lines.Add(line);
        }

        VisibleLines = new ObservableCollection<PlayerLogLine>(lines);
    }

    private static bool Matches(PlayerLogLine line, string filter)
    {
        if (filter is "全部" or "") return true;
        if (string.Equals(line.Level, filter, StringComparison.OrdinalIgnoreCase)) return true;
        return line.Message.StartsWith("[" + filter + "]", StringComparison.OrdinalIgnoreCase);
    }

    partial void OnLevelFilterChanged(string value) => RefreshLines();

    public void Dispose()
    {
        _logStore.LineAppended -= OnLineAppended;
    }
}
