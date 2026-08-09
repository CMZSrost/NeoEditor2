using System;
using Microsoft.Win32;

namespace NeoEditor.Player.Services;

/// <summary>
/// Startup WebView2 Runtime presence check (v2.68). The player's only rendering path is
/// the WebView2-based Ruffle preview, so a missing runtime must surface at startup as an
/// alert with the official installer link — not as a bare error text after the user
/// already dragged a SWF. Detection mirrors what the WebView2 SDK does internally: the
/// runtime registers a "pv" version value under EdgeUpdate's Clients key. No new package
/// needed — Avalonia.Controls.WebView does its own interop, so we query the registry.
/// </summary>
public static class WebView2RuntimeCheck
{
    /// <summary>Official evergreen bootstrapper link (WebView2 docs → 下载 WebView2 Runtime).</summary>
    public const string InstallUrl = "https://go.microsoft.com/fwlink/?linkid=2124701";

    // Client GUID of "Microsoft Edge WebView2 Runtime" (documented deployment key).
    private const string ClientsPath =
        @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    /// <summary>True when a WebView2 Runtime is registered. Any non-registry failure is
    /// reported as available — the lazy WebView creation surfaces real errors with the
    /// exact message instead of a misleading "please install" alert.</summary>
    public static bool IsInstalled()
    {
        foreach (var (hive, view) in new (RegistryHive, RegistryView)[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Registry64),
                     (RegistryHive.LocalMachine, RegistryView.Registry32),
                     (RegistryHive.CurrentUser, RegistryView.Registry64),
                     (RegistryHive.CurrentUser, RegistryView.Registry32),
                 })
        {
            try
            {
                using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(ClientsPath);
                if (key?.GetValue("pv") is string version && version.Length > 0)
                    return true;
            }
            catch (Exception)
            {
                // hive/view unavailable — try the next one
            }
        }
        return false;
    }
}
