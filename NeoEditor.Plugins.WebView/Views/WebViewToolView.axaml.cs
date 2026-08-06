using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NeoEditor.Plugins.WebView.ViewModels;

namespace NeoEditor.Plugins.WebView.Views;

public partial class WebViewToolView : UserControl
{
    private NativeWebView? _webView;
    private bool _attached;
    private bool _loadFailed;

    public WebViewToolView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        EnsureWebView();
        AttachViewModel();
    }

    /// <summary>Create the native webview once (Windows: WebView2; see Docs/42 §2.1).</summary>
    private void EnsureWebView()
    {
        if (_webView is not null || _loadFailed) return;
        try
        {
            var webView = new NativeWebView();
            webView.NavigationCompleted += OnNavigationCompleted;
            webView.WebMessageReceived += OnWebMessageReceived;
            WebViewHost.Children.Add(webView);
            _webView = webView;
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            WebViewHost.Children.Add(new TextBlock
            {
                Text = $"WebView unavailable on this platform: {ex.Message}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(8),
            });
        }
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (DataContext is WebViewToolViewModel vm && _webView is not null)
        {
            vm.SetHistory(_webView.CanGoBack, _webView.CanGoForward);
            if (_webView.Source is { } source)
                vm.NavigateToSilently(source);
        }
    }

    private void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        // Reserved for host-side log bridging (Docs/42 §3.4 channel A) — v1 uses the
        // page-internal POST /__log path, so nothing to do here yet.
    }

    /// <summary>
    /// Wire the shared VM to the native control. The tool view lives as long as the VM
    /// (dock tool lifecycle), so this is a one-way attach with no detach.
    /// </summary>
    private void AttachViewModel()
    {
        if (_attached || DataContext is not WebViewToolViewModel vm) return;
        _attached = true;

        vm.NavigateRequested += uri => _webView?.Navigate(uri);
        vm.BackRequested += () => _webView?.GoBack();
        vm.ForwardRequested += () => _webView?.GoForward();
        vm.RefreshRequested += () => _webView?.Refresh();
        vm.OpenFileDialogRequested += () => _ = PickAndOpenFileAsync(vm);
    }

    private async Task PickAndOpenFileAsync(WebViewToolViewModel vm)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open HTML file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("HTML") { Patterns = new[] { "*.html", "*.htm" } },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count > 0)
            vm.NavigateTo(new Uri("file://" + files[0].Path.LocalPath.Replace('\\', '/')));
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => _webView?.GoBack();

    private void OnForwardClick(object? sender, RoutedEventArgs e) => _webView?.GoForward();

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => _webView?.Refresh();

    private void OnGoClick(object? sender, RoutedEventArgs e) => GoFromAddressBox();

    private void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) GoFromAddressBox();
    }

    private void GoFromAddressBox()
    {
        if (DataContext is WebViewToolViewModel vm)
            vm.NavigateTo(AddressBox.Text ?? "");
    }

    private void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WebViewToolViewModel vm)
            vm.OpenFileCommand.Execute(null);
    }

    private void OnPreviewSwfClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WebViewToolViewModel vm)
            vm.PreviewSwfCommand.Execute(null);
    }
}
