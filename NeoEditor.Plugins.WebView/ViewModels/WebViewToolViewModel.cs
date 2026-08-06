using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Services;
using NeoEditor.Data.Messages;
using NeoEditor.Infra.Services;
using NeoEditor.Player.Core.Services;
using Serilog;

namespace NeoEditor.Plugins.WebView.ViewModels;

/// <summary>
/// Shared state + commands for the WebView tool panel (Docs/42 P1/P2). The view's
/// code-behind owns the <c>NativeWebView</c> control and subscribes to the navigation
/// events raised here; the toolbar's "内置预览" button reaches this VM through
/// <see cref="SwfPreviewRequestedMessage"/> (Docs/42 §3.7).
/// </summary>
public sealed partial class WebViewToolViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly IConfigService _config;
    private readonly INotificationService _notifications;
    private readonly ILocalizationService _loc;
    private readonly GameContentServer _server;
    private readonly SwfLogBridge _logBridge;

    public event Action<Uri>? NavigateRequested;
    public event Action? BackRequested;
    public event Action? ForwardRequested;
    public event Action? RefreshRequested;
    public event Action? OpenFileDialogRequested;

    private Uri? _currentUri;

    [ObservableProperty] private string _addressText = "";
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoForward;

    public WebViewToolViewModel(
        IMessenger messenger,
        IConfigService config,
        INotificationService notifications,
        ILocalizationService loc,
        GameContentServer server,
        SwfLogBridge logBridge)
    {
        _messenger = messenger;
        _config = config;
        _notifications = notifications;
        _loc = loc;
        _server = server;
        _logBridge = logBridge;

        _messenger.Register<SwfPreviewRequestedMessage>(this,
            (_, message) => _ = HandleSwfPreviewAsync(message.SwfPath));

        // Game-side Flash events (Docs/42 v2.5) → editor toast.
        _logBridge.GameEventDetected += OnGameEventDetected;
    }

    private void OnGameEventDetected(object? sender, GameEventDetectedEventArgs e)
    {
        // Fired from the log server thread — marshal to the UI thread for notifications.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            switch (e.Type)
            {
                case PlayerGameEventType.GameExit:
                    _notifications.ShowInfo("游戏请求退出。", _loc["SwfPreviewButton"]);
                    break;
                case PlayerGameEventType.NavigationBlocked:
                    _notifications.ShowInfo("游戏尝试打开外部链接（已拦截）。", _loc["SwfPreviewButton"]);
                    break;
            }
        });
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    [RelayCommand]
    private void GoForward() => ForwardRequested?.Invoke();

    [RelayCommand]
    private void RefreshPage() => RefreshRequested?.Invoke();

    [RelayCommand]
    private void NavigateToAddress() => NavigateTo(AddressText);

    [RelayCommand]
    private void OpenFile() => OpenFileDialogRequested?.Invoke();

    [RelayCommand]
    private void PreviewSwf() => _ = HandleSwfPreviewAsync(null);

    /// <summary>Start the loopback server and load the Ruffle host page for the game SWF.</summary>
    public async Task HandleSwfPreviewAsync(string? swfPath)
    {
        if (!RuffleWebAssets.IsAvailable())
        {
            _notifications.ShowWarning(_loc["SwfPreviewRuffleMissing"], _loc["SwfPreviewButton"]);
            return;
        }

        if (!_server.Start())
        {
            _notifications.ShowError(_loc["SwfPreviewServerFailed"], _loc["SwfPreviewButton"]);
            return;
        }

        var gameRoot = _config.Config.GameRootDir;
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            _notifications.ShowWarning(_loc["SwfPreviewNoGameRoot"], _loc["SwfPreviewButton"]);
            return;
        }

        var swf = swfPath ?? RuffleOptionsBuilder.FindSwfPath(gameRoot);
        Uri uri;
        if (swf is null)
        {
            // Host page shows "SWF not found" guidance.
            _notifications.ShowWarning(_loc["SwfPreviewSwfNotFound"], _loc["SwfPreviewButton"]);
            uri = new Uri(_server.BaseUrl!);
        }
        else
        {
            var relative = Path.GetRelativePath(gameRoot, swf).Replace('\\', '/');
            uri = new Uri(_server.BaseUrl! + "?swf=" + Uri.EscapeDataString(relative));
        }

        AddressText = uri.ToString();
        RequestNavigate(uri);
        Log.Logger.ForContext("Source", "WebViewPreview")
            .Information("[WebView] preview requested → {Uri} (swf: {Swf})", uri, swf ?? "<none>");
    }

    /// <summary>
    /// Navigate-or-reload: a same-URL Navigate may be ignored by the webview, so when the
    /// target equals the current URL we refresh the page instead — the old Ruffle game is
    /// torn down with the document and restarts from the same query.
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

    /// <summary>Navigate the panel to an arbitrary URL / local file (generic WebView use).</summary>
    public void NavigateTo(string text)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
            NavigateTo(uri);
    }

    public void NavigateTo(Uri uri)
    {
        AddressText = uri.ToString();
        RequestNavigate(uri);
    }

    /// <summary>Called by the view when native navigation history changes.</summary>
    public void SetHistory(bool canGoBack, bool canGoForward)
    {
        CanGoBack = canGoBack;
        CanGoForward = canGoForward;
    }

    /// <summary>Update the address bar from native navigation without re-navigating.</summary>
    public void NavigateToSilently(Uri uri) => AddressText = uri.ToString();

    public void Dispose()
    {
        _messenger.UnregisterAll(this);
        _server.Dispose();
    }
}
