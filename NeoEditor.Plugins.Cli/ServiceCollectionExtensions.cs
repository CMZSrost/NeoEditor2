using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.Cli.Cli;

namespace NeoEditor.Plugins.Cli;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCliPlugin(this IServiceCollection services)
    {
        services.AddSingleton<IServicePlugin, CliPlugin>();
        services.AddSingleton<CliCommandParser>();
        services.AddSingleton<CliCommandHandler>();
        services.AddSingleton<CliOutputFormatter>();
        services.AddTransient<CliSession>();
        return services;
    }
}
