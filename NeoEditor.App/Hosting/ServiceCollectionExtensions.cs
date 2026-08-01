using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;

namespace NeoEditor.Hosting;

/// <summary>
/// DI extension methods for registering plugins and infrastructure modules.
/// All DI registrations converge here (R20: App Composition Root).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Register a plugin type with the DI container.</summary>
    public static IServiceCollection AddPlugin<T>(this IServiceCollection services) where T : class, IPlugin
    {
        services.AddSingleton<T>();
        services.AddSingleton<IPlugin>(sp => sp.GetRequiredService<T>());
        return services;
    }
}
