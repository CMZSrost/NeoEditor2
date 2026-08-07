using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Platform;

namespace NeoEditor.Player.Services;

/// <summary>
/// Best-effort F12 DevTools opener (R38). Avalonia.Controls.WebView's public surface only
/// exposes the raw WebView2 COM pointer (<see cref="IWindowsWebView2PlatformHandle.CoreWebView2"/>
/// is an IntPtr; the managed ICoreWebView2 interop wrapper is internal with no public factory),
/// so we bridge it with a minimal [ComImport] subset of the SDK's ICoreWebView2 and call
/// <c>OpenDevToolsWindow()</c> (vtable slot 48). Slots 0..47 are declared but never invoked —
/// declaration order is what pins the vtable alignment. Signature drift against a future
/// WebView2 runtime would only break this call, and every failure degrades to a status-bar
/// hint. Note: DevTools are enabled by default in WebView2 (the package never disables them),
/// so F12/Ctrl+Shift+I already work natively while the game webview itself has focus — this
/// covers the menu item / Avalonia-chrome-focus cases.
/// </summary>
public static class WebView2DevTools
{
    [SupportedOSPlatform("windows")]
    public static bool TryOpen(NativeWebView webView)
    {
        try
        {
            if (webView.TryGetPlatformHandle() is not IWindowsWebView2PlatformHandle wv2)
                return false;
            if (wv2.CoreWebView2 == IntPtr.Zero)
                return false;

            var core = Marshal.GetTypedObjectForIUnknown(wv2.CoreWebView2, typeof(ICoreWebView2Com));
            if (core is not ICoreWebView2Com icw) return false;
            try
            {
                icw.OpenDevToolsWindow();
                return true;
            }
            finally
            {
                Marshal.FinalReleaseComObject(icw);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ICoreWebView2 (GUID 76eceacb-0462-4d94-ac83-423a6793775e) — method order follows the
    // official IDL / the package's own interop binding; only OpenDevToolsWindow is called.
    [ComImport,
     Guid("76eceacb-0462-4d94-ac83-423a6793775e"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICoreWebView2Com
    {
        [PreserveSig] void GetSettings();
        [PreserveSig] void GetSource();
        [PreserveSig] void Navigate(string uri);
        [PreserveSig] void NavigateToString(string html);
        [PreserveSig] void add_NavigationStarting(object handler, out long token);
        [PreserveSig] void remove_NavigationStarting(long token);
        [PreserveSig] void add_ContentLoading(object handler, out long token);
        [PreserveSig] void remove_ContentLoading(long token);
        [PreserveSig] void add_SourceChanged(object handler, out long token);
        [PreserveSig] void remove_SourceChanged(long token);
        [PreserveSig] void add_HistoryChanged(object handler, out long token);
        [PreserveSig] void remove_HistoryChanged(long token);
        [PreserveSig] void add_NavigationCompleted(object handler, out long token);
        [PreserveSig] void remove_NavigationCompleted(long token);
        [PreserveSig] void add_FrameNavigationStarting(object handler, out long token);
        [PreserveSig] void remove_FrameNavigationStarting(long token);
        [PreserveSig] void add_FrameNavigationCompleted(object handler, out long token);
        [PreserveSig] void remove_FrameNavigationCompleted(long token);
        [PreserveSig] void add_ScriptDialogOpening(object handler, out long token);
        [PreserveSig] void remove_ScriptDialogOpening(long token);
        [PreserveSig] void add_PermissionRequested(object handler, out long token);
        [PreserveSig] void remove_PermissionRequested(long token);
        [PreserveSig] void add_ProcessFailed(object handler, out long token);
        [PreserveSig] void remove_ProcessFailed(long token);
        [PreserveSig] void AddScriptToExecuteOnDocumentCreated(string id, object handler);
        [PreserveSig] void RemoveScriptToExecuteOnDocumentCreated(string id);
        [PreserveSig] void ExecuteScript(string script, object handler);
        [PreserveSig] void CapturePreview(int format, IntPtr stream, object handler);
        [PreserveSig] void Reload();
        [PreserveSig] void PostWebMessageAsJson(string msg);
        [PreserveSig] void PostWebMessageAsString(string msg);
        [PreserveSig] void add_WebMessageReceived(object handler, out long token);
        [PreserveSig] void remove_WebMessageReceived(long token);
        [PreserveSig] void CallDevToolsProtocolMethod(string id, string args, object handler);
        [PreserveSig] void GetBrowserProcessId(out uint id);
        [PreserveSig] void GetCanGoBack(out int canGoBack);
        [PreserveSig] void GetCanGoForward(out int canGoForward);
        [PreserveSig] void GoBack();
        [PreserveSig] void GoForward();
        [PreserveSig] void GetDevToolsProtocolEventReceiver(string name, out IntPtr receiver);
        [PreserveSig] void Stop();
        [PreserveSig] void add_NewWindowRequested(object handler, out long token);
        [PreserveSig] void remove_NewWindowRequested(long token);
        [PreserveSig] void add_DocumentTitleChanged(object handler, out long token);
        [PreserveSig] void remove_DocumentTitleChanged(long token);
        [PreserveSig] void GetDocumentTitle();
        [PreserveSig] void AddHostObjectToScript(string name, IntPtr value);
        [PreserveSig] void RemoveHostObjectFromScript(string name);
        [PreserveSig] void OpenDevToolsWindow();
    }
}
