using System.Collections.Generic;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// A plugin that provides document editing for specific entity types.
/// The returned document view is cast to an Avalonia Control by the App shell.
/// </summary>
public interface IDocumentPlugin : IPlugin
{
    /// <summary>Entity type names this plugin can edit (e.g. "ItemType", "Recipe").</summary>
    IReadOnlyList<string> SupportedEntityTypes { get; }

    /// <summary>Create a document view for the given entity. Returns object to avoid Core depending on Avalonia.</summary>
    object CreateDocument(Data.Model.Game.IEntity entity, IPluginContext ctx);
}
