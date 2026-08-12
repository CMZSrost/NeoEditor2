using System;
using Avalonia.Controls;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.JsVisualization.Services;
using Serilog;

namespace NeoEditor.Plugins.JsVisualization;

/// <summary>
/// D09 P0.8 v4: WebView2 共享（环境层）——**不做控件 reparent**。
/// 实测：控件在文档间移动（移入/移出停靠容器）会残留输入/渲染状态（"一会行一会不行"）。
/// 因此：Detach（文档失活）即**销毁** NativeWebView，Attach（激活）时重建。
/// 共享收敛在环境层：UserDataFolder/ProfileName 统一（单一浏览器进程 + 缓存）
/// + ExperimentalOffscreen（离屏合成：滚轮/输入走 Avalonia 转发）。
/// 重建成本 ~百 ms 级（环境已就绪），快关快开场景不受影响。
/// </summary>
public sealed class SharedJsVizWebView
{
    private readonly VizContentServer _server;
    private readonly VizActionHandler _actions;
    private NativeWebView? _webView;
    private bool _loadFailed;
    private IEntity? _current;

    public SharedJsVizWebView(VizContentServer server, VizActionHandler actions)
    {
        _server = server;
        _actions = actions;
    }

    /// <summary>切换渲染实体（文档打开/实体变化）：记录 + 若已就绪则导航。</summary>
    public void LoadEntity(IEntity entity)
    {
        _current = entity;
        if (_webView is not null && !_loadFailed)
            Navigate();
    }

    /// <summary>文档 tab 激活：确保 WebView 存在（无则新建挂入宿主）并导航；false = 平台不可用。</summary>
    public bool Attach(Grid host)
    {
        if (!EnsureWebView(host)) return false;
        if (_current is not null)
            Navigate();
        return true;
    }

    /// <summary>文档 tab 失活：销毁 WebView（释放资源；下次 Attach 重建——避免 reparent 状态残留）。</summary>
    public void Detach(Grid host)
    {
        if (_webView is null) return;
        host.Children.Remove(_webView);
        _webView = null;   // 引用释放 → GC 回收 WebView2 资源
    }

    private bool EnsureWebView(Grid attachHost)
    {
        if (_webView is not null) return true;
        if (_loadFailed) return false;
        if (!_server.Start())
        {
            _loadFailed = true;
            return false;
        }

        try
        {
            var webView = new NativeWebView();
            VizWebViewEnvironment.Attach(webView);   // 共享环境 + 离屏合成（滚轮/输入转发）
            webView.NavigationCompleted += OnNavigationCompleted;
            webView.WebMessageReceived += OnWebMessageReceived;   // P2: postMessage 增强通道（§五）
            attachHost.Children.Add(webView);
            _webView = webView;
            return true;
        }
        catch (Exception)
        {
            _loadFailed = true;
            return false;
        }
    }

    /// <summary>
    /// P2 (D09 §五): 页面 `chrome.webview.postMessage` 桥 —— 与 /viz/action POST 同一协议、
    /// 同一 VizActionHandler（"双向可选、协议唯一"）。页面以 POST 为主，桥作为 WebView2
    /// 内 fetch 失败时的兜底通道；浏览器环境无 chrome.webview 自然回退 HTTP。
    /// </summary>
    private void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Body)) return;
        if (!_actions.TryParse(e.Body, out var action)) return;
        var error = _actions.Handle(action);
        if (error is not null)
            Log.Logger.ForContext("Source", "JsVisualization")
                .Warning("[JsViz] postMessage action rejected: {Error}", error);
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
    }

    private void Navigate()
    {
        if (_current is null || _webView is null || _server.BaseUrl is null) return;
        var url = $"{_server.BaseUrl}viz/index.html?type={_current.GetType().Name}&id={Uri.EscapeDataString(_current.EntityId)}";
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_webView is not null)
                _webView.Navigate(new Uri(url));
        });
    }
}

/// <summary>
/// WebView2 环境配置（挂在 EnvironmentRequested 事件上，Windows 子类
/// WindowsWebView2EnvironmentRequestedEventArgs 的属性，反射设置，非 Windows 静默回退）：
/// ① UserDataFolder/ProfileName 统一 → 全部实例共用单一浏览器进程与缓存；
/// ② ExperimentalOffscreen（离屏合成）→ 输入（点击/滚轮/键盘）走 Avalonia 转发
/// （源码确认 OnPointerWheelChanged 仅离屏适配器转发滚轮；SendMouseInput 完整实现），
/// 这是共享控件 reparent 安全的前提，顺带消除 airspace。
/// </summary>
public static class VizWebViewEnvironment
{
    private static readonly string UserDataFolder =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NeoEditor", "WebView2Viz");

    private const string ProfileName = "neoviz";

    public static void Attach(NativeWebView webView)
        => webView.EnvironmentRequested += OnEnvironmentRequested;

    private static void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
        try
        {
            var t = e.GetType();
            var folder = t.GetProperty("UserDataFolder");
            if (folder?.SetMethod is { IsPublic: true } fset)
                fset.Invoke(e, [UserDataFolder]);
            var profile = t.GetProperty("ProfileName");
            if (profile?.SetMethod is { IsPublic: true } pset)
                pset.Invoke(e, [ProfileName]);
            var offscreen = t.GetProperty("ExperimentalOffscreen");
            if (offscreen?.SetMethod is { IsPublic: true } oset)
                oset.Invoke(e, [true]);
        }
        catch (Exception)
        {
            // 非 Windows 平台无这些属性 → 默认环境
        }
    }
}
