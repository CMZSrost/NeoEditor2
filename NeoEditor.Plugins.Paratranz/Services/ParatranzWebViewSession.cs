using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Platform;

namespace NeoEditor.Plugins.Paratranz.Services;

/// <summary>
/// WebView2 持久化会话配置（D03 §6.3 PT1 spike 结论，2026-08-05）：
/// <c>WindowsWebView2EnvironmentRequestedEventArgs.UserDataFolder</c> 为公开属性，会直接流入
/// 原生 <c>CreateCoreWebView2EnvironmentWithOptions</c>（源码 AvaloniaUI/Avalonia.Controls.WebView
/// CoreWebView2Environment.CreateAsync）——设置后 cookie（登录态）持久化到指定目录，跨重启保持。
/// 纯托管，无需 Microsoft.Web.WebView2 互操作包。
/// </summary>
public static class ParatranzWebViewSession
{
    /// <summary>持久化数据目录：%LOCALAPPDATA%/NeoEditor/paratranz-webview（登录态在此）。</summary>
    public static string GetUserDataFolder()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "NeoEditor", "paratranz-webview");
    }

    /// <summary>
    /// 订阅 NativeWebView 的 <c>EnvironmentRequested</c>（环境创建前同步触发）：
    /// 设置持久目录 + 界面语言。Windows 上事件参数实际类型为
    /// <see cref="WindowsWebView2EnvironmentRequestedEventArgs"/>；其他平台无操作。
    /// </summary>
    public static void ApplyPersistentSession(WebViewEnvironmentRequestedEventArgs args)
    {
        if (args is WindowsWebView2EnvironmentRequestedEventArgs win)
        {
            win.UserDataFolder = GetUserDataFolder();
            win.Language = "zh-CN"; // paratranz.cn 中文界面
        }
    }
}
