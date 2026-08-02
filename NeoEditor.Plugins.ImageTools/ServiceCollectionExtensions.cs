using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools;

/// <summary>
/// DI registration for the ImageTools plugin.
/// Call <c>services.AddImageToolsPlugin()</c> in the App Composition Root.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all ImageTools plugin services and the plugin itself.
    /// </summary>
    public static IServiceCollection AddImageToolsPlugin(this IServiceCollection services)
    {
        // Split tool plugins (D02: one IToolPlugin per Tool, 1:1).
        services.AddSingleton<IToolPlugin, ImageAssetManagerPlugin>();
        services.AddSingleton<IToolPlugin, ImageOrchestrationPlugin>();
        services.AddSingleton<IProfileModSourceProvider, ProfileModSourceProvider>();
        services.AddSingleton<IModImagesDocumentFactory, ModImagesDocumentFactory>();
        services.AddSingleton<PixelArtConversionService>();
        services.AddSingleton<ImageEditorProcessingService>();
        services.AddSingleton<IImageEditorProcessingService>(sp =>
            sp.GetRequiredService<ImageEditorProcessingService>());
        services.AddSingleton<IImageSearchService, ImageSearchService>();
        services.AddSingleton<EntityToPromptConverter>();
        services.AddSingleton<IImageGenerationService, ImageGenerationService>();
        services.AddSingleton<ViewModels.ImagePreviewContent>();
        services.AddSingleton<ViewModels.ImageAssetManagerViewModel>();
        services.AddSingleton<ViewModels.ImageOrchestrationViewModel>();
        return services;
    }
}