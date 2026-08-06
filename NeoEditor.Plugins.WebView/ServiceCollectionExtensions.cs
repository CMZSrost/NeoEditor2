using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Player.Core;
using NeoEditor.Player.Core.Services;
using NeoEditor.Plugins.WebView.Services;
using NeoEditor.Plugins.WebView.ViewModels;

namespace NeoEditor.Plugins.WebView;

/// <summary>
/// DI registration for the WebView plugin — the editor host of the shared player core
/// (Docs/42 §3.8). Registers the editor's LIVE reverse-proxy data source (IHostService)
/// and the dock tool UI; the player services themselves come from AddPlayerCore().
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebViewPlugin(this IServiceCollection services)
    {
        services.AddPlayerCore();

        // Editor-only data source: live export (DB + active profile overlay) — the
        // "debug 加载" of the reverse proxy (Docs/42 §3.6/§3.8).
        services.AddSingleton<IGameDataExportService, LiveGameDataExportService>();

        services.AddSingleton<IToolPlugin, WebViewPlugin>();
        services.AddSingleton<WebViewToolViewModel>();

        return services;
    }
}
