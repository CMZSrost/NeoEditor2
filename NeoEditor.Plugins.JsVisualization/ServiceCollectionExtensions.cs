using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Plugins.JsVisualization.Services;
using NeoEditor.UI.Common.Visualizers;

namespace NeoEditor.Plugins.JsVisualization;

/// <summary>
/// D09: composition-root helper (called from App.axaml.cs, R20). The
/// EncounterSemanticsExtractor is registered by the App root itself — its
/// findImage delegate comes from the App's IImageService (R18: plugins never
/// reference App).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJsVisualizationPlugin(this IServiceCollection services)
    {
        services.AddSingleton<VizSnapshotService>();
        services.AddSingleton<VizActionHandler>();
        services.AddSingleton<VizContentServer>();
        // 单 WebView2 共享（P0.8 v3）：离屏合成下 reparent 安全，全部文档共用一个实例
        services.AddSingleton<SharedJsVizWebView>();
        services.AddSingleton<IEntityJsVisualizationHost, JsVisualizationHost>();
        return services;
    }
}
