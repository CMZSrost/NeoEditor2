using System;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.Services;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using NeoEditor.UI.Common.Services;

namespace NeoEditor.Plugins.EntityEditor;

/// <summary>
/// DI registration for the EntityEditor plugin.
/// Call <c>services.AddEntityEditorPlugin()</c> in the App Composition Root.
/// Note: VisHelperService and Services.RefNode are registered separately in App.axaml.cs because
/// they need IImageService.FindImage (App → Plugin boundary, R18).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all EntityEditor plugin services and the plugin itself.
    /// </summary>
    public static IServiceCollection AddEntityEditorPlugin(this IServiceCollection services)
    {
        // Document plugin (entity editing / XML / visualizers).
        services.AddSingleton<EntityEditorPlugin>();
        services.AddSingleton<IDocumentPlugin>(sp => sp.GetRequiredService<EntityEditorPlugin>());

        // Split tool plugins (D02: one IToolPlugin per Tool, 1:1).
        services.AddSingleton<IToolPlugin, KeyValueEditorPlugin>();
        services.AddSingleton<IToolPlugin, OverlayChainPlugin>();

        // Shared tool ViewModels — singletons so plugin views and the App shell
        // reference the same instances (D02 §五, R01).
        services.AddSingleton<KeyValueEditorViewModel>();
        services.AddSingleton<OverlayChainToolContent>();

        services.AddSingleton<IEntityEditorDocumentFactory, EntityEditorDocumentFactory>();
        return services;
    }

    /// <summary>
    /// Register all 25 entity visualizers from this plugin into the global EntityVisualizerRegistry.
    /// Call after all DI services are built, from the App Composition Root.
    /// </summary>
    public static void RegisterEntityEditorVisualizers(this IServiceProvider services)
    {
        var registry = services.GetRequiredService<EntityVisualizerRegistry>();
        var vis = services.GetRequiredService<VisHelperService>();
        var refNode = services.GetRequiredService<Services.RefNode>();
        var dataTable = services.GetRequiredService<IEntityLookupService>();

        // Default visualizer (fallback for entities without a custom visualizer)
        registry.SetDefault(new Visualizers.DefaultEntityVisualizer(typeof(IEntity), vis));

        // === Simple (no DataTableService) ===
        registry.Register(new Visualizers.DataFileEntityVisualizer(vis));
        registry.Register(new Visualizers.GameVarEntityVisualizer(vis));
        registry.Register(new Visualizers.HeadlineEntityVisualizer(vis));
        registry.Register(new Visualizers.ForbiddenHexEntityVisualizer(vis));
        registry.Register(new Visualizers.ItemPropEntityVisualizer(vis));

        // === Medium (RefNode only) ===
        registry.Register(new Visualizers.AttackModeEntityVisualizer(vis, refNode));
        registry.Register(new Visualizers.BattleMoveEntityVisualizer(vis, refNode));
        registry.Register(new Visualizers.ChargeProfileEntityVisualizer(vis, refNode));
        registry.Register(new Visualizers.ConditionEntityVisualizer(vis, refNode));
        registry.Register(new Visualizers.DmcPlaceEntityVisualizer(vis, refNode));
        registry.Register(new Visualizers.EncounterTriggerEntityVisualizer(vis, refNode));
        registry.Register(new Visualizers.HexTypeEntityVisualizer(vis, refNode));
        registry.Register(new Visualizers.IngredientEntityVisualizer(vis, refNode));
        registry.Register(new Visualizers.MapEntityVisualizer(vis, refNode));

        // === Complex (RefNode + DataTableService) ===
        registry.Register(new Visualizers.BarterHexEntityVisualizer(vis, refNode, dataTable));
        registry.Register(new Visualizers.CampTypeEntityVisualizer(vis, refNode, dataTable));
        registry.Register(new Visualizers.ContainerTypeEntityVisualizer(vis, refNode, dataTable));
        registry.Register(new Visualizers.CreatureSourceEntityVisualizer(vis, refNode, dataTable));
        registry.Register(new Visualizers.CreatureEntityVisualizer(vis, refNode, dataTable));
        registry.Register(new Visualizers.EncounterEntityVisualizer(vis, refNode, dataTable));
        registry.Register(new Visualizers.FactionEntityVisualizer(vis, refNode, dataTable));
        registry.Register(new Visualizers.ItemTypeEntityVisualizer(vis, refNode, dataTable));
        registry.Register(new Visualizers.RecipeEntityVisualizer(vis, refNode, dataTable));
        registry.Register(new Visualizers.TreasureTableEntityVisualizer(vis, refNode, dataTable));
    }
}
