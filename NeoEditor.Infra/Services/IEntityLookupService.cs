using System;
using System.Collections.Generic;
using NeoEditor.Data.Model.Game;
using NeoEditor.Services;

namespace NeoEditor.Infra.Services;

/// <summary>
/// Data access interface for entity lookup and merge-store properties.
/// Replaces direct DataTableService dependency for Plugin consumers (R17: Plugin → Plugin 0 reference).
/// Implemented by DataTableService in DataViewer Plugin; consumed via DI by EntityEditor Plugin.
/// </summary>
public interface IEntityLookupService
{
    // ── Store access ─────────────────────────────────────────────────────
    EntityMergeStore? ActiveMergeStore { get; }
    EntityMergeStore? BrowserStore { get; }
    HashSet<(string EntityId, string ColumnName)> EditedCells { get; }

    // ── Lookup dictionaries ──────────────────────────────────────────────
    Dictionary<Type, List<object>> ReferenceLookups { get; }
    Dictionary<string, string> EntityModNames { get; }
    Dictionary<string, string> EntityNamespaces { get; }
    Dictionary<string, int> EntityMergedIds { get; }

    // ── Entity queries ───────────────────────────────────────────────────
    Dictionary<int, T> GetEntities<T>() where T : IEntity;
    Dictionary<string, T> GetCompositeEntities<T>(Func<T, string> keySelector, int sourceModId = int.MaxValue)
        where T : IEntity;
    List<T> GetDedupedEntities<T>() where T : IEntity;

    // ── Reference resolution ─────────────────────────────────────────────
    IEntity? FindBestMatch(Type entityType, string rawId, string? targetKey,
        string sourceEntityId = "", string propertyName = "");
}
