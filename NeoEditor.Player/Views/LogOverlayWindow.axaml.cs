using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NeoEditor.Player.ViewModels;

namespace NeoEditor.Player.Views;

/// <summary>
/// Steam-style overlay (Docs/42 v2.3): a borderless, topmost, full-screen window floating
/// ABOVE the native WebView2 surface (Avalonia controls cannot paint over a native child
/// window, so the overlay must be its own HWND). Toggled by the log button or the page's
/// Shift+Tab bridge (host.html → chrome.webview.postMessage).
/// </summary>
public partial class LogOverlayWindow : Window
{
    public LogOverlayWindow()
    {
        InitializeComponent();

        // Shift+Tab closes the overlay. While the overlay is open the focus lives in THIS
        // Avalonia window (the page bridge can't hear the key), so we must intercept it
        // here — tunnel-phase KeyDown beats the Avalonia Tab focus navigation.
        AddHandler(InputElement.KeyDownEvent, OnOverlayKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnOverlayKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            Hide();
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Hide();

    private void OnClearLogsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerViewModel vm)
            vm.ClearLogsCommand.Execute(null);
    }
}
