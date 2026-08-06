using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NeoEditor.Plugins.Paratranz.Conversion;

namespace NeoEditor.Plugins.Paratranz.Views;

/// <summary>
/// 译文应用前的 diff 预览弹窗（D03 §6.2）：NativeWebView + NavigateToString 渲染
/// <see cref="DiffHtmlRenderer"/> 的离线 HTML；确认回调由调用方注入（R24 命令执行）。
/// </summary>
public class DiffPreviewWindow : Window
{
    private readonly Action _onConfirm;
    private NativeWebView? _webView;

    public DiffPreviewWindow(string html, string title, string confirmText, string cancelText,
        string noteText, Action onConfirm)
    {
        _onConfirm = onConfirm;
        Title = title;
        Width = 920;
        Height = 640;

        var statsLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            Text = noteText,
        };

        var confirmButton = new Button
        {
            Content = confirmText,
            IsDefault = true,
            Margin = new Avalonia.Thickness(4),
        };
        confirmButton.Click += (_, _) => Confirm();

        var cancelButton = new Button
        {
            Content = cancelText,
            IsCancel = true,
            Margin = new Avalonia.Thickness(4),
        };
        cancelButton.Click += (_, _) => Close();

        var bottomBar = new DockPanel { Margin = new Avalonia.Thickness(8, 6) };
        DockPanel.SetDock(bottomBar, Dock.Bottom);
        bottomBar.Children.Add(confirmButton);
        bottomBar.Children.Add(cancelButton);
        bottomBar.Children.Add(statsLabel);

        var host = new Panel();
        Content = new DockPanel
        {
            Children = { bottomBar, host },
        };

        Loaded += (_, _) => EnsureWebView(host, html);
    }

    private void EnsureWebView(Panel host, string html)
    {
        if (_webView is not null) return;
        try
        {
            var webView = new NativeWebView();
            host.Children.Add(webView);
            _webView = webView;
            webView.NavigateToString(html);
        }
        catch (Exception)
        {
            // WebView 不可用（如非 Windows 平台）时退化为文本提示
            host.Children.Add(new TextBlock
            {
                Text = "WebView 不可用，无法预览 diff。",
                Margin = new Avalonia.Thickness(8),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
        }
    }

    private void Confirm()
    {
        _onConfirm();
        Close();
    }
}
