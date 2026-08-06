using System;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.Paratranz.Services;

namespace NeoEditor.Plugins.Paratranz;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ParaTranz API client and plugin. The client is a singleton
    /// holding one <see cref="System.Net.Http.HttpClient"/> (BaseAddress =
    /// <see cref="ParatranzApiClient.DefaultBaseUrl"/>); the token is provided
    /// later by the settings UI (spec D03).
    /// </summary>
    public static IServiceCollection AddParatranzPlugin(this IServiceCollection services)
    {
        services.AddSingleton<IParatranzApiClient>(_ =>
        {
            var http = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri(ParatranzApiClient.DefaultBaseUrl),
                Timeout = TimeSpan.FromSeconds(100),
            };
            return new ParatranzApiClient(http);
        });
        // M2 数据转换层（D03 §3）
        services.AddSingleton<Conversion.ITranslationKeyParser, Conversion.TranslationKeyParser>();
        services.AddSingleton<Conversion.ITranslationExtractor, Conversion.TranslationExtractor>();
        services.AddSingleton<Conversion.ICsvTranslationSerializer, Conversion.CsvTranslationSerializer>();
        services.AddSingleton<Conversion.ITranslationApplier, Conversion.TranslationApplier>();
        // M4 同步编排 + Dock 工具面板（D03 §4.2/§6.2）
        services.AddSingleton<IParatranzSyncService, ParatranzSyncService>();
        services.AddTransient<ViewModels.ParatranzPaneViewModel>();
        services.AddSingleton<IToolPlugin, ParatranzPlugin>();
        return services;
    }
}
