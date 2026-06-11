using System;
using System.Collections.Generic;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Services;

/// <summary>
/// Per-entity-type merge result. Used by the View to create tabs.
/// </summary>
public record TypeMergeData(
    Type EntityType,
    IReadOnlyList<IEntity> AllEntities,
    IReadOnlyList<IEntity> VisibleEntities,
    int OverriddenCount);

/// <summary>
/// Immutable result of a merge computation across all entity types.
/// The View copies these into EntityMergeStore and creates DataGrid tabs.
/// </summary>
public record MergeResult(
    IReadOnlyList<TypeMergeData> Types,
    IReadOnlyDictionary<string, string> EntityModNames,
    IReadOnlyDictionary<string, string> EntityNamespaces,
    IReadOnlyDictionary<string, List<OverlayChainEntry>> OverlayChains,
    IReadOnlyDictionary<(string EntityId, string ColumnName), string> FieldSources,
    IReadOnlySet<(string EntityId, string ColumnName)> FieldConflicts,
    IReadOnlyDictionary<string, int> EntityMergedIds,
    IReadOnlySet<string> OverriddenEntityIds,
    IReadOnlyDictionary<string, string> NamespaceToModName,
    IReadOnlySet<int> MergeSpaceModIds,
    IReadOnlyDictionary<Type, List<object>> ReferenceLookups);
