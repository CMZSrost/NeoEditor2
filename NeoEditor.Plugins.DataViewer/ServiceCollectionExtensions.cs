using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Plugins.DataViewer.ViewModels;

namespace NeoEditor.Plugins.DataViewer;

/// <summary>
/// DI registration for the DataViewer plugin.
/// Call <c>services.AddDataViewerPlugin()</c> in the App Composition Root.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all DataViewer plugin services and the plugin itself.
    /// </summary>
    public static IServiceCollection AddDataViewerPlugin(this IServiceCollection services)
    {
        // Split tool plugins (D02: one IToolPlugin per Tool, 1:1).
        // DataTable's Context is swapped at runtime by the App shell (placeholder ↔ ModDataToolViewModel).
        services.AddSingleton<IToolPlugin, DataTablePlugin>();
        services.AddSingleton<IToolPlugin, ForwardIndexPlugin>();
        services.AddSingleton<IToolPlugin, ReverseIndexPlugin>();
        services.AddSingleton<IToolPlugin, SearchPlugin>();
        services.AddSingleton<IToolPlugin, PeekPlugin>();

        // Shared tool ViewModels — singletons so plugin views and the App shell
        // reference the same instances (D02 §五, R01).
        services.AddSingleton<ModDataToolViewModel>();
        services.AddSingleton<SearchResultViewModel>();
        services.AddSingleton<PeekPanelViewModel>();
        services.AddSingleton<IIndexTableFactory, IndexTableFactory>();

        // Core navigation services
        services.AddSingleton<DataGridInteractionState>();
        services.AddSingleton<INavigationRouter, NavigationRouter>();
        services.AddSingleton<IDataGridNavigationService, DataGridNavigationService>();
        services.AddSingleton<IDataGridCellInteractionService, DataGridCellInteractionService>();

        // DataTable services
        services.AddSingleton<DataTableService>();
        services.AddSingleton<ColumnTemplateFactory>();
        services.AddSingleton<InteractionHandler>();

        // Data loading
        services.AddSingleton<DataLoaderService>();

        return services;
    }
}
