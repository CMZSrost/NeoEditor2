using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NeoEditor.Player.Core.Services;
using NeoEditor.Player.Core.ViewModels;
using NeoEditor.Player.Services;
using NeoEditor.Player.ViewModels;

namespace NeoEditor.Player.Views;

public partial class PlayerWindow : Window
{
    private NativeWebView? _webView;
    private LogOverlayWindow? _logOverlay;
    private DataBrowserWindow? _dataBrowser;
    private StorageManagerWindow? _storageWindow;
    private bool _attached;
    private bool _loadFailed;
    private bool _backgroundMode;

    /// <summary>SWF dragged onto the exe (command-line arg) — started once the view is loaded.</summary>
    public string? StartupSwfPath { get; set; }

    public PlayerWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        // F11 toggles fullscreen; ESC exits (works while the Avalonia chrome has focus —
        // the WebView2 child window may swallow keys while the game itself is focused).
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.F11) ToggleFullScreen();
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
    }

    /// <summary>Invoke Ruffle's public destroy() to stop audio/AVM immediately (best effort).</summary>
    private void TryDestroyPlayer() => TryInvoke(
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
            // bridge hears the next Shift+Tab and re-opens the overlay.
            Activate();
        }
        else
        {
            // Fullscreen player → fullscreen overlay; windowed player → overlay matches
            // the window's position/size (never covers the whole screen).
            if (WindowState == WindowState.FullScreen)
            {
                _logOverlay.WindowState = WindowState.FullScreen;
            }
            else
            {
                _logOverlay.WindowState = WindowState.Normal;
                _logOverlay.Position = Position;
                _logOverlay.Width = Width;
                _logOverlay.Height = Height;
            }

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
            var storageVm = new StorageManagerViewModel(
                script => _webView?.InvokeScript(script) ?? Task.FromResult<string?>(null),
                key => LocalizationManager.Instance[key],
                new SaveBackupService(App.Services.Config));   // {gameRoot}/save_backup (v2.37)
            _storageWindow = new StorageManagerWindow { DataContext = storageVm };
            _storageWindow.Closed += (_, _) => _storageWindow = null;
            // v2.42: refresh BOTH tabs on open — the VM is recreated per window instance,
            // so the backups list would otherwise show as empty (looks like backups got
            // deleted while they only weren't loaded).
            _storageWindow.Opened += (_, _) =>
            {
                storageVm.RefreshCommand.Execute(null);
                storageVm.RefreshBackupsCommand.Execute(null);
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
