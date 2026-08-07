using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using NeoEditor.Player.Services;
using NeoEditor.Player.ViewModels;

namespace NeoEditor.Player.Views;

/// <summary>
/// 日志窗口（R50 起由全屏覆盖层改为普通弹窗：可拖动/调整大小/标题栏关闭）。
/// Toggled by the log button or the page's F10 bridge (host.html →
/// chrome.webview.postMessage). F10 仍可关闭本窗口。
/// </summary>
public partial class LogOverlayWindow : Window
{
    public LogOverlayWindow()
    {
        InitializeComponent();

        // F10 closes the log window. While it's open the focus lives in THIS Avalonia
        // window (the page bridge can't hear the key), so we must intercept it here —
        // tunnel-phase KeyDown beats Avalonia's default key handling.
        AddHandler(InputElement.KeyDownEvent, OnOverlayKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnOverlayKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F10)
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

    // R38 调试工具：打开日志目录 / 导出日志（导出完成后宿主在 Explorer 定位文件）。
    private void OnOpenLogFolderClick(object? sender, RoutedEventArgs e)
        => PlayerWindow.OpenLogFolder((DataContext as PlayerViewModel)?.FileLogDirectory);

    private void OnExportLogsClick(object? sender, RoutedEventArgs e)
        => (DataContext as PlayerViewModel)?.ExportLogsCommand.Execute(null);

    /// <summary>v2.59: 剪贴板诊断——直接读系统剪贴板（C# 侧，不经过 JS 拦截链），
    /// 一锤定音确认游戏是否仍在写真实剪贴板、内容是什么。</summary>
    private async void OnClipboardCheckClick(object? sender, RoutedEventArgs e)
    {
        string shown;
        try
        {
            var text = await ReadClipboardTextAsync();
            shown = string.IsNullOrEmpty(text)
                ? LocalizationManager.Instance["Log.ClipboardEmpty"]
                : text.Length > 3000 ? text[..3000] + "…" : text;
        }
        catch (Exception ex)
        {
            shown = "读取失败: " + ex.Message;
        }
        var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ContentTitle = LocalizationManager.Instance["Log.ClipboardTitle"],
            ContentMessage = shown,
            MinWidth = 520,
            MaxWidth = 900,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        });
        await box.ShowWindowDialogAsync(this);
    }

    /// <summary>Avalonia 12 剪贴板协议：TryGetDataAsync → IAsyncDataTransfer（调用方负责 Dispose）。</summary>
    private async Task<string?> ReadClipboardTextAsync()
    {
        var transfer = await Clipboard.TryGetDataAsync();
        if (transfer is null) return null;
        try
        {
            return await transfer.TryGetTextAsync();
        }
        finally
        {
            transfer.Dispose();
        }
    }
}
