using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Player.Core.Logging;
using NeoEditor.Player.Core.Services;

namespace NeoEditor.Player.Core;

/// <summary>
/// DI registration for the shared player core (Docs/42 §3.8). Hosts (editor preview plugin,
/// standalone player) call <c>services.AddPlayerCore()</c> and register their own
/// <c>IGameDataExportService</c> + <c>IGamePhpGenerator</c> implementations:
///  - editor: live reverse proxy (IHostService) + App PhpParser
///  - player: disk mode (ProxyEnabled=false) + GamePhpGenerator
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlayerCore(this IServiceCollection services)
    {
        services.AddSingleton<RunLogStore>();
        services.AddSingleton<SwfLogBridge>();
        services.AddSingleton<ProxyHttpModule>();
        services.AddSingleton<GameContentServer>();
        return services;
    }
}
