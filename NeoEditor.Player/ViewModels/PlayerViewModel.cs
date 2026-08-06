using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _levelFilter = "全部";
    [ObservableProperty] private bool _backgroundMode = true;   // 默认后台运行（失焦不暂停）
    [ObservableProperty] private ObservableCollection<PlayerLogLine> _visibleLines = [];

    /// <summary>Directory the file log sink writes to (surfaced in the log overlay).</summary>
    [ObservableProperty] private string? _fileLogDirectory;

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
        DataBrowser = new DataBrowserViewModel(dataBrowserService);
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
                    StatusText = L("Status.Quit");
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
