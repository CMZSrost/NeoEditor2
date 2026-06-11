using NeoEditor.Data.Model.Game;
using NeoEditor.Services;

namespace NeoEditor.Helper;

/// <summary>
/// Canonical reference resolution interface.
/// All resolution paths MUST go through the store's ReferenceIndex.
/// No call-site should iterate ReferenceLookups or build its own lookup dictionaries for entity resolution.
/// </summary>
public interface IReferenceResolver
{
    /// <summary>
    /// Resolve a raw reference segment to an entity.
    /// Uses the store's ReferenceIndex (context-aware), falls back to same-mod matching.
    /// This is the single canonical resolution entry point.
    /// </summary>
    T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : IEntity;

    /// <summary>
    /// Resolve a raw reference segment to a display Subject string.
    /// For DataGrid cell rendering and other non-generic contexts.
    /// </summary>
    string? LookupSubject(string sourceEntityId, string propertyName, System.Type targetType, string rawId,
        System.Type? secondaryTargetType = null);

    /// <summary>
    /// Reverse lookup: find all entities that reference the target entity.
    /// Uses the store's pre-built ReferenceIndex.ReverseLookup.
    /// </summary>
    System.Collections.Generic.IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)>
        ReverseLookup(EntityMergeStore store, string targetEntityId);

    /// <summary>Navigate to an entity by type and entity ID.</summary>
    void NavigateTo(System.Type entityType, string entityId);

    /// <summary>Navigate to an entity by type and int business key.</summary>
    void NavigateToByKey<T>(int key) where T : IEntity;

    /// <summary>Navigate to an entity by type and int business key, with same-mod priority.</summary>
    void NavigateToByKeyFor<T>(int key, IEntity sourceEntity) where T : IEntity;

}
