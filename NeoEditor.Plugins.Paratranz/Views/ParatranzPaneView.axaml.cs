using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NeoEditor.Plugins.Paratranz.Conversion;
using NeoEditor.Plugins.Paratranz.Services;
using NeoEditor.Plugins.Paratranz.ViewModels;

namespace NeoEditor.Plugins.Paratranz.Views;

public partial class ParatranzPaneView : UserControl
{
    private NativeWebView? _workbenchWebView;
    private bool _attached;

    public ParatranzPaneView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        EnsureWorkbenchWebView();
        AttachViewModel();
        if (DataContext is ParatranzPaneViewModel vm)
            _ = vm.RefreshCommand.ExecuteAsync(null);
    }

    /// <summary>Tab 2 工作台 WebView：懒创建 + 持久化会话（PT1 方案）+ 项目页导航。</summary>
    private void EnsureWorkbenchWebView()
    {
        if (_workbenchWebView is not null) return;
        try
        {
            var webView = new NativeWebView();
            webView.EnvironmentRequested += (_, args) => ParatranzWebViewSession.ApplyPersistentSession(args);
            WorkbenchHost.Children.Insert(0, webView);
            _workbenchWebView = webView;
            NavigateWorkbench();
        }
        catch (Exception ex)
        {
            WorkbenchPlaceholder.Text = $"WebView 不可用：{ex.Message}";
        }
    }

    private void NavigateWorkbench()
    {
        if (_workbenchWebView is null || DataContext is not ParatranzPaneViewModel vm)
            return;
        var url = vm.WorkbenchUrl;
        WorkbenchAddress.Text = url.Length > 0 ? url : "未配置项目（设置 → ParaTranz）";
        if (url.Length == 0)
        {
            WorkbenchPlaceholder.IsVisible = true;
            return;
        }
        WorkbenchPlaceholder.IsVisible = false;
        // 地址锁死 paratranz.cn 项目页域内
        if (_workbenchWebView.Source?.ToString() != url)
            _workbenchWebView.Navigate(new Uri(url));
    }

    private void AttachViewModel()
    {
        if (_attached || DataContext is not ParatranzPaneViewModel vm) return;
        _attached = true;

        // diff 预览：准备完成 → 弹窗（确认后执行命令）
        vm.DiffPreviewRequested += (build, row) =>
        {
            var html = DiffHtmlRenderer.Render(build.Rows);
            var window = new DiffPreviewWindow(
                html,
                vm.Loc["Paratranz.DiffTitle"],
                vm.Loc["Paratranz.ConfirmApply"],
                vm.Loc["Paratranz.Cancel"],
                vm.Loc["Paratranz.DiffNote"],
                () => _ = vm.ExecuteBuildAsync(build, row));
            var top = TopLevel.GetTopLevel(this);
            if (top is Window owner)
                window.ShowDialog(owner);
            else
                window.Show();
        };

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ParatranzPaneViewModel.WorkbenchUrl))
                NavigateWorkbench();
        };
    }

    private void OnApplyFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ParatranzFileRow row } &&
            DataContext is ParatranzPaneViewModel vm)
        {
            _ = vm.ApplyFileCommand.ExecuteAsync(row);
        }
    }

    private void OnWorkbenchRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (_workbenchWebView is not null)
            _workbenchWebView.Refresh();
        else
            NavigateWorkbench();
    }
}
