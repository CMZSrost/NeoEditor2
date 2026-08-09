using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using NeoEditor.Player.Core.Services;
using NeoEditor.Player.Core.ViewModels;
using NeoEditor.Player.Services;
using NeoEditor.Player.ViewModels;
using Serilog;

namespace NeoEditor.Player.Views;

public partial class PlayerWindow : Window
{
    private NativeWebView? _webView;
    private LogOverlayWindow? _logOverlay;
    private DataBrowserWindow? _dataBrowser;
    private StorageManagerWindow? _storageWindow;
    private StorageManagerViewModel? _storageVm;
    private SaveEditorWindow? _saveEditor;
    private bool _attached;
    private bool _loadFailed;
    private bool _backgroundMode;

    /// <summary>SWF dragged onto the exe (command-line arg) — started once the view is loaded.</summary>
    public string? StartupSwfPath { get; set; }

    public PlayerWindow()
    {
        InitializeComponent();
        // R43: 标题带版本（csproj <Version>）——试用反馈必须能报出版本。
        Title = $"{NeoEditor.Player.Services.AppInfo.ProductName} (Ruffle Web) v{NeoEditor.Player.Services.AppInfo.Version}";
        Loaded += OnLoaded;
        // F11 toggles fullscreen; ESC exits; F12 opens DevTools (R38); F10 toggles the
        // log window (v2.62 — covers the Avalonia-chrome-focus case; while the game
        // itself is focused, host.html forwards F10 via the page bridge). Works while
        // the Avalonia chrome has focus — the WebView2 child window may swallow keys
        // while the game itself is focused.
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.F11) ToggleFullScreen();
            else if (e.Key == Avalonia.Input.Key.F12) OpenDevTools();
            else if (e.Key == Avalonia.Input.Key.F10) ToggleLogOverlay();
            else if (e.Key == Avalonia.Input.Key.Escape && WindowState == WindowState.FullScreen)
                WindowState = WindowState.Normal;
        };

        // Drag a SWF onto the window to load it (top bar area — the WebView2 child
        // surface owns its own drag handling).
        AddHandler(DragDrop.DropEvent, OnDrop);
        DragDrop.SetAllowDrop(this, true);

        // Reliable focus-loss pause: WebView2 does NOT deliver window blur to the page,
        // so drive pause/play from the host window's own activation events instead
        // (the page-side blur handler in host.html stays as a secondary path).
        Deactivated += (_, _) => { if (!_backgroundMode) PauseGame(); };
        Activated += (_, _) => { if (!_backgroundMode) ResumeGame(); };

        // Release the player before the window goes away: destroy() stops Ruffle's
        // audio/AVM immediately (a plain process exit can leave WebView2 audio playing
        // for a moment).
        Closing += (_, _) =>
        {
            TryDestroyPlayer();
            _webView = null;
        };
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // No eager WebView creation: the drag placeholder stays visible until the first
        // SWF loads (a native child window cannot be covered by Avalonia controls).
        AttachViewModel();
        BootstrapLog.Write("主窗口已显示（OnLoaded）");
        // v2.68: WebView2 Runtime is the player's only rendering path — detect at startup
        // and alert with the official install link. The lazy WebView creation would only
        // surface a bare error text after the user already dragged a SWF.
        if (!WebView2RuntimeCheck.IsInstalled())
        {
            Log.Logger.Warning("[Player] WebView2 Runtime 未检测到 — 弹窗提示安装（{InstallUrl}）",
                WebView2RuntimeCheck.InstallUrl);
            BootstrapLog.Write("WebView2 Runtime 未检测到 — 弹出安装提示");
            // Deferred: the window must be visible before a modal dialog can show.
            Dispatcher.UIThread.Post(() => _ = ShowWebView2MissingAsync());
        }
        if (StartupSwfPath is { Length: > 0 } path && DataContext is PlayerViewModel vm)
            _ = vm.StartAsync(path);
    }

    /// <summary>Create the webview on first navigation; hides the drag placeholder.</summary>
    private NativeWebView? GetOrCreateWebView()
    {
        if (_webView is not null) return _webView;
        if (_loadFailed) return null;
        try
        {
            var webView = new NativeWebView();
            webView.WebMessageReceived += OnWebMessageReceived;
            // v2.29: invoke page scripts only AFTER a page is loaded — InvokeScript before
            // NavigationCompleted throws (and leaks an unobserved task from the control),
            // which surfaced as a [FATAL] crash-log entry on startup.
            webView.NavigationCompleted += (_, _) =>
                TryInvoke($"window.__backgroundMode = {(_backgroundMode ? "true" : "false")}; 'ok'");
            WebViewHost.Children.Add(webView);
            _webView = webView;
            DropPlaceholder.IsVisible = false;
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            WebViewHost.Children.Add(new TextBlock
            {
                Text = $"WebView unavailable: {ex.Message}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(8),
            });
            return null;
        }
        return _webView;
    }

    /// <summary>
    /// Page bridge (host.html Shift+Tab → chrome.webview.postMessage): toggle the log
    /// overlay — a Steam-style overlay floating above the native WebView surface.
    /// </summary>
    private void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Body)) return;
        if (e.Body.Contains("toggle-overlay", StringComparison.OrdinalIgnoreCase))
            ToggleLogOverlay();
    }

    private void AttachViewModel()
    {
        if (_attached || DataContext is not PlayerViewModel vm) return;
        _attached = true;
        _backgroundMode = vm.BackgroundMode;   // sync initial switch state (default: true)

        vm.NavigateRequested += uri =>
        {
            // Navigating away (stop/about:blank) — destroy the Ruffle player first so
            // audio/AVM stop immediately instead of lingering through the navigation.
            if (uri.Scheme == "about") TryDestroyPlayer();
            GetOrCreateWebView()?.Navigate(uri);
        };
        vm.RefreshRequested += () => GetOrCreateWebView()?.Refresh();
        vm.OpenFileDialogRequested += () => _ = PickAndOpenSwfAsync(vm);
        vm.ResetRequested += OnGameReset;                  // game quit → back to placeholder
        vm.BackgroundModeChanged += SetBackgroundMode;     // 后台运行 switch
        // R38 调试工具：导出日志的 JS 桥（localStorage 快照）+ 报错捕捉弹窗 + 导出完成定位。
        vm.ExecuteJs = script => _webView?.InvokeScript(script) ?? Task.FromResult<string?>(null);
        vm.GameErrorDetected += ShowGameErrorDialog;
        vm.LogExportCompleted += path => OpenLogFolder(Path.GetDirectoryName(path), path);
    }

    /// <summary>Invoke Ruffle's public destroy() to stop audio/AVM immediately (best effort).
    /// v2.52: Ruffle 实例 Drop 时会 flush_shared_objects 把缓存旧档写回 localStorage——
    /// 本会话删除过存档（__savesCleared）且 localStorage 尚无新存档时才拦截（删档复活）；
    /// 已有新档（删除后玩新游戏已自动保存）则放行 flush，避免丢失新档最后一段进度。
    /// 存档管理（VM）显式设置的 __blockSaves 不被覆盖。</summary>
    private void TryDestroyPlayer() => TryInvoke(
        "if (!window.__blockSaves) {" +
        "  var __has = false;" +
        "  for (var i = 0; i < localStorage.length; i++) { var k = localStorage.key(i);" +
        "    if (k && k.indexOf('nsSGv1') !== -1) { __has = true; break; } }" +
        "  window.__blockSaves = window.__savesCleared && !__has;" +
        "}" +
        "window.__player && window.__player.destroy && window.__player.destroy(); 'ok'");

    private void PauseGame() => TryInvoke(
        "window.__player && window.__player.pause && window.__player.pause(); 'ok'");

    private void ResumeGame() => TryInvoke(
        "window.__player && window.__player.play && window.__player.play(); 'ok'");

    private void TryInvoke(string script)
    {
        try
        {
            _webView?.InvokeScript(script);
        }
        catch (Exception)
        {
            // page not loaded / webview gone — nothing to do
        }
    }

    /// <summary>
    /// Game quit (fscommand quit detected): destroy the player, unload the page so the
    /// WebView2 audio stops, tear the webview down and re-show the "drop SWF" placeholder.
    /// Also closes the data browser + log overlay — the player returns to its idle state.
    /// </summary>
    private async void OnGameReset()
    {
        TryDestroyPlayer();
        try { _webView?.Navigate(new Uri("about:blank")); } catch (Exception) { }

        // Unloading the page is what actually stops WebView2 audio (removing the control
        // from the tree alone does not tear down the browser process immediately).
        await Task.Delay(300);

        if (_webView is not null)
        {
            WebViewHost.Children.Remove(_webView);
            _webView = null;
        }
        // Reset the data browser (stale catalog must not survive into the next SWF)
        // and close both overlay windows — fully clean idle state.
        if (DataContext is PlayerViewModel vm)
        {
            vm.IsSwfLoaded = false;   // v2.42: menu items gray out until the next SWF
            vm.DataBrowser.Reset();
        }
        _dataBrowser?.Hide();
        _logOverlay?.Hide();
        _saveEditor?.Hide();
        DropPlaceholder.IsVisible = true;
    }

    /// <summary>
    /// 「后台运行」switch：true = 失焦不暂停（宿主 Deactivated 处理读 _backgroundMode）。
    /// 切换瞬间按当前窗口焦点状态立即生效。
    /// </summary>
    private void SetBackgroundMode(bool enabled)
    {
        _backgroundMode = enabled;
        TryInvoke($"window.__backgroundMode = {(enabled ? "true" : "false")}; 'ok'");

        // Apply immediately for the current focus state: into background → resume if
        // unfocused; out of background → pause if unfocused.
        if (!IsActive)
        {
            if (enabled) ResumeGame();
            else PauseGame();
        }
    }

    private void ToggleLogOverlay()
    {
        if (DataContext is not PlayerViewModel vm) return;

        if (_logOverlay is null)
        {
            _logOverlay = new LogOverlayWindow { DataContext = vm };
            _logOverlay.Closed += (_, _) => _logOverlay = null;
        }

        if (_logOverlay.IsVisible)
        {
            _logOverlay.Hide();
            // Return focus to the main window (ideally the WebView2 child) so the page
            // bridge hears the next Shift+Tab and re-opens the log window.
            Activate();
        }
        else
        {
            _logOverlay.Show(this);
        }
    }

    private async Task PickAndOpenSwfAsync(PlayerViewModel vm)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择游戏 SWF（NEOScavenger.swf）",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Shockwave Flash") { Patterns = new[] { "*.swf" } },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count > 0)
            // Path.LocalPath, not AbsolutePath: the storage URI's AbsolutePath is
            // "/D:/..." on Windows and breaks Path.GetDirectoryName/Directory.Exists.
            await vm.StartAsync(files[0].Path.LocalPath);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        string? swf = null;
        foreach (var item in e.DataTransfer.Items)
        {
            if (item.TryGetRaw(DataFormat.File) is IStorageItem file
                && file.Path.AbsolutePath.EndsWith(".swf", StringComparison.OrdinalIgnoreCase))
            {
                swf = file.Path.LocalPath;
                break;
            }
        }

        if (swf is not null && DataContext is PlayerViewModel vm)
            await vm.StartAsync(swf);
    }

    private void ToggleFullScreen()
        => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    private void OnOpenSwfClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerViewModel vm)
            vm.OpenSwfCommand.Execute(null);
    }

    private void OnPlaceholderClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is PlayerViewModel vm)
            vm.OpenSwfCommand.Execute(null);
    }

    private void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerViewModel vm)
            vm.ReloadCommand.Execute(null);
    }

    private void OnFullScreenClick(object? sender, RoutedEventArgs e) => ToggleFullScreen();

    private void OnLogClick(object? sender, RoutedEventArgs e) => ToggleLogOverlay();

    private void OnExitMenuClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>R38: F12 / 调试菜单 → Chromium DevTools（Network / Application-localStorage /
    /// Console）。COM 桥失败（非 Windows/接口漂移）时状态栏提示。</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void OpenDevTools()
    {
        if (DataContext is not PlayerViewModel vm) return;
        if (_webView is null || !WebView2DevTools.TryOpen(_webView))
            vm.StatusText = LocalizationManager.Instance["Debug.DevToolsUnavailable"];
    }

    /// <summary>R38: 报错捕捉弹窗——错误详情 + 「打开日志目录」。确认=打开日志目录。</summary>
    private async void ShowGameErrorDialog(string detail)
    {
        if (DataContext is not PlayerViewModel vm) return;
        var open = await PromptDialogWindow.PromptAsync(this,
            LocalizationManager.Instance["Error.DialogTitle"],
            string.Format(LocalizationManager.Instance["Error.DialogBody"], detail),
            okText: LocalizationManager.Instance["Log.OpenFolder"],
            cancelText: LocalizationManager.Instance["Common.Ok"]);
        if (open is not null) OpenLogFolder(vm.FileLogDirectory);
    }

    /// <summary>
    /// v2.68: WebView2 Runtime 缺失 → 启动弹窗（提示 + 官方安装链接）；「打开安装页面」
    /// 用默认浏览器打开 evergreen bootstrapper，装完重启播放器即可。
    /// </summary>
    private async Task ShowWebView2MissingAsync()
    {
        var open = await PromptDialogWindow.PromptAsync(this,
            LocalizationManager.Instance["WebView2.MissingTitle"],
            string.Format(LocalizationManager.Instance["WebView2.MissingBody"], WebView2RuntimeCheck.InstallUrl),
            okText: LocalizationManager.Instance["WebView2.OpenInstallPage"],
            cancelText: LocalizationManager.Instance["Common.Close"]);
        if (open is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(WebView2RuntimeCheck.InstallUrl)
                {
                    UseShellExecute = true,
                });
            }
            catch (Exception)
            {
                // No default browser / URL blocked — the alert already showed the link.
            }
        }
    }

    /// <summary>R38: 在 Explorer 中定位最新运行日志（或打开日志目录）。</summary>
    public static void OpenLogFolder(string? directory, string? selectFile = null)
    {
        var dir = directory ?? NeoEditor.Player.Core.Logging.FileRunLogWriter.ResolveDirectory(null);
        var target = selectFile ?? Directory.EnumerateFiles(dir, "player-run-*.log")
            .OrderByDescending(Path.GetFileName).FirstOrDefault();
        try
        {
            Process.Start("explorer.exe",
                target is not null ? $"/select,\"{target}\"" : $"\"{dir}\"");
        }
        catch (Exception)
        {
            // Explorer unavailable — nothing useful to do.
        }
    }

    private void OnDevToolsClick(object? sender, RoutedEventArgs e) => OpenDevTools();

    private void OnOpenLogFolderClick(object? sender, RoutedEventArgs e)
        => OpenLogFolder((DataContext as PlayerViewModel)?.FileLogDirectory);

    private void OnExportLogsClick(object? sender, RoutedEventArgs e)
        => (DataContext as PlayerViewModel)?.ExportLogsCommand.Execute(null);

    /// <summary>R43: 导出存档+日志 zip（试用反馈包）——完成后 Explorer 定位。</summary>
    private void OnExportBundleClick(object? sender, RoutedEventArgs e)
        => (DataContext as PlayerViewModel)?.ExportBundleZipCommand.Execute(null);

    /// <summary>R45/R46: 存档修改工具——加载/编辑/保存 localStorage 存档（存档管理「修改」入口）。</summary>
    private void OpenSaveEditor(SaveEntry? entry)
    {
        if (DataContext is not PlayerViewModel vm) return;
        if (_webView is null)
        {
            vm.StatusText = LocalizationManager.Instance["Storage.ReadFailed"];
            return;
        }

        if (_saveEditor is null)
        {
            var saveEditorVm = new SaveEditorViewModel(
                script => _webView?.InvokeScript(script) ?? Task.FromResult<string?>(null),
                key => LocalizationManager.Instance[key],
                // 保存并加载：写回 localStorage 后重载页面（清 Ruffle SharedObject 内存缓存）
                () => (DataContext as PlayerViewModel)?.RestartGame());
            // 保存并加载后存档管理窗口的列表（大小）也刷新
            saveEditorVm.SavedAndLoaded += () => _storageVm?.RefreshCommand.Execute(null);
            _saveEditor = new SaveEditorWindow { DataContext = saveEditorVm };
            _saveEditor.Closed += (_, _) => _saveEditor = null;
        }

        if (entry is not null && _saveEditor.DataContext is SaveEditorViewModel evm)
            _ = evm.LoadEntryAsync(entry);   // 预载选中存档
        _saveEditor.Show(this);
    }

    /// <summary>R43: 调试菜单「关于」——版本/Ruffle/平台/关键目录（MessageBox，右上角关闭）。</summary>
    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayerViewModel vm) return;
        var body = string.Format(LocalizationManager.Instance["About.Body"],
            NeoEditor.Player.Services.AppInfo.Version,
            NeoEditor.Player.Services.AppInfo.RuffleVersion,
            "Windows x64",
            vm.FileLogDirectory ?? "",
            vm.GameRootDir,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeoScavengerPlayer", "WebView2"));
        var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ContentTitle = LocalizationManager.Instance["About.Title"],
            ContentMessage = body,
            MinWidth = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        });
        await box.ShowWindowDialogAsync(this);
    }

    /// <summary>Theme radio item clicked — Tag carries "System"/"Light"/"Dark" (v2.28).</summary>
    private void OnThemeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerViewModel vm && sender is MenuItem { Tag: string tag })
            vm.Theme = tag;
    }

    /// <summary>Language radio item clicked — Tag carries "zh"/"en" (v2.28).</summary>
    private void OnLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerViewModel vm && sender is MenuItem { Tag: string tag })
            vm.Language = tag;
    }

    private void OnDataBrowserClick(object? sender, RoutedEventArgs e) => ToggleDataBrowser();

    /// <summary>
    /// Save manager (Docs/42 v2.36): lists/clears the game's localStorage saves through
    /// the host webview. Requires a loaded page — otherwise the status bar explains.
    /// </summary>
    private void OnStorageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayerViewModel vm) return;
        if (_webView is null)
        {
            vm.StatusText = LocalizationManager.Instance["Storage.ReadFailed"];
            return;
        }

        if (_storageWindow is null)
        {
            _storageVm = new StorageManagerViewModel(
                script => _webView?.InvokeScript(script) ?? Task.FromResult<string?>(null),
                key => LocalizationManager.Instance[key],
                new SaveBackupService(App.Services.Config),   // {gameRoot}/save_backup (v2.37)
                // v2.49/v2.50: Ruffle SharedObject 内存缓存 — 删除/恢复只写 localStorage，
                // 且页面卸载（pagehide）时 Ruffle 会把缓存旧档 flush 写回；操作后自动
                // 重载页面（清缓存 + 卸载写回被 __blockSaves 拦截），并关闭本窗口。
                () =>
                {
                    (DataContext as PlayerViewModel)?.RestartGame();
                    _storageWindow?.Close();
                });
            _storageWindow = new StorageManagerWindow { DataContext = _storageVm };
            _storageWindow.Closed += (_, _) => _storageWindow = null;
            // R46: 存档修改工具入口（预载选中的存档）
            _storageWindow.EditSaveRequested += OpenSaveEditor;
            // v2.42: refresh BOTH tabs on open — the VM is recreated per window instance,
            // so the backups list would otherwise show as empty (looks like backups got
            // deleted while they only weren't loaded).
            _storageWindow.Opened += (_, _) =>
            {
                _storageVm.RefreshCommand.Execute(null);
                _storageVm.RefreshBackupsCommand.Execute(null);
            };
        }

        _storageWindow.Show(this);
    }

    /// <summary>
    /// Standalone data-browser dialog (docs/42 v2.14): a normal bordered window centered on
    /// the player — movable/resizable so data can be compared side-by-side with the game.
    /// </summary>
    private void ToggleDataBrowser()
    {
        if (DataContext is not PlayerViewModel vm) return;

        if (_dataBrowser is null)
        {
            _dataBrowser = new DataBrowserWindow { DataContext = vm.DataBrowser };
            _dataBrowser.Closed += (_, _) => _dataBrowser = null;
        }

        if (_dataBrowser.IsVisible)
        {
            _dataBrowser.Hide();
            Activate();
        }
        else
        {
            vm.DataBrowser.Refresh();
            _dataBrowser.Show(this);
        }
    }
}
