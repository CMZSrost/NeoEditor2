using System;
using System.Linq;
using System.Text.Json;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.Plugins.JsVisualization.Services;

/// <summary>
/// D09 §五: the JS→C# interaction bridge (/viz/action POST or postMessage — same
/// protocol, one handler). navigate/peek follow the global editor reference
/// navigation (INavigationRouter); select syncs the Center selection (R12).
/// </summary>
public sealed class VizActionHandler
{
    private readonly INavigationRouter _router;
    private readonly ISelectionService _selection;
    private readonly IEntityLookupService _dataTable;

    public VizActionHandler(INavigationRouter router, ISelectionService selection, IEntityLookupService dataTable)
    {
        _router = router;
        _selection = selection;
        _dataTable = dataTable;
    }

    public sealed record VizAction(string Kind, string? EntityType, string? EntityId, string? Modifier);

    public bool TryParse(string json, out VizAction action)
    {
        action = new VizAction("", null, null, null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            action = new VizAction(
                root.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "",
                root.TryGetProperty("entityType", out var t) ? t.GetString() : null,
                root.TryGetProperty("entityId", out var i) ? i.GetString() : null,
                root.TryGetProperty("modifier", out var m) ? m.GetString() : null);
            return action.Kind.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Handle one action; returns a human-readable error or null on success.</summary>
    public string? Handle(VizAction action)
    {
        if (string.IsNullOrWhiteSpace(action.EntityType) || string.IsNullOrWhiteSpace(action.EntityId))
            return $"action '{action.Kind}' requires entityType+entityId";

        var type = ResolveType(action.EntityType);
        if (type is null) return $"unknown entity type '{action.EntityType}'";
        if (!typeof(IEntity).IsAssignableFrom(type)) return $"'{action.EntityType}' is not an entity type";

        switch (action.Kind)
        {
            case "navigate":
                _router.NavigateToEntity(type, action.EntityId);
                return null;
            case "peek":
                _router.RequestPeek(type, action.EntityId, null);
                return null;
            case "select":
            {
                var entity = FindEntity(type, action.EntityId);
                if (entity is not null) _selection.SetCurrentEntity(entity);
                return null;
            }
            default:
                return $"unknown action kind '{action.Kind}'";
        }
    }

    private Type? ResolveType(string name)
        => _dataTable.ReferenceLookups.Keys.FirstOrDefault(t => t.Name == name)
           ?? typeof(IEntity).Assembly.GetTypes().FirstOrDefault(t => t.Name == name);

    private IEntity? FindEntity(Type type, string entityId)
    {
        if (!_dataTable.ReferenceLookups.TryGetValue(type, out var list)) return null;
        return list.OfType<IEntity>().FirstOrDefault(e => e.EntityId == entityId);
    }
}
