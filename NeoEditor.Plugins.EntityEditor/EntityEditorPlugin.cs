using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using NeoEditor.Services;

namespace NeoEditor.Plugins.EntityEditor;

/// <summary>
/// EntityEditor document plugin — provides the XML editor, KV editor, and 25 entity
/// visualizers. Document-based: opens in the Center DocumentDock when an entity is
/// double-clicked. The KV / OverlayChain Tools are separate IToolPlugin classes
/// (KeyValueEditorPlugin / OverlayChainPlugin). Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class EntityEditorPlugin : IDocumentPlugin
{
    public string Name => "EntityEditor";
    public Version Version => new(1, 0, 0);

    public IReadOnlyList<string> SupportedEntityTypes { get; } =
    [
        "AttackMode", "BarterHex", "BattleMove", "CampType", "ChargeProfile",
        "Condition", "ContainerType", "Creature", "CreatureSource", "DataFile",
        "DmcPlace", "Encounter", "EncounterTrigger", "Faction", "ForbiddenHex",
        "GameVar", "Headline", "HexType", "Ingredient", "ItemProp",
        "ItemType", "Map", "Recipe", "TreasureTable"
    ];

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateDocument(IEntity entity, IPluginContext ctx)
    {
        var services = ctx.Services;
        return new EntityEditorDocument(
            entity,
            services.GetRequiredService<NeoEditor.Services.IWorkspaceSession>(),
            services.GetRequiredService<IHostService>(),
            services.GetRequiredService<IEntityLookupService>(),
            services.GetRequiredService<ILocalizationService>(),
            services.GetRequiredService<INotificationService>(),
            services.GetRequiredService<IReferenceListSerializer>(),
            services.GetRequiredService<IXmlParser>(),
            services.GetRequiredService<IConfigService>());
    }
}