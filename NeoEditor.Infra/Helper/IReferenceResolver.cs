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

    /// <summary>
    /// Resolve an already-extracted raw reference ID to an entity of the given type.
    /// Unlike <see cref="LookupRef{T}"/>, this does NOT re-extract the raw ID via pattern
    /// matching — the caller has already done that. The source entity provides namespace context.
    /// Returns null if the reference cannot be resolved.
    /// </summary>
    IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, System.Type targetType);

    /// <summary>
    /// Build reverse reference index by scanning all entities' [ReferenceField] properties.
    /// Must be called AFTER the main reference_index is built.
    /// </summary>
    System.Threading.Tasks.Task BuildReverseIndexAsync(ReferenceIndexService indexService, EntityMergeStore store);

    /// <summary>
    /// Convenience: reverse-lookup + resolve source entity display info.
    /// </summary>
    System.Collections.Generic.List<(System.Type SourceType, string SourceSubject, string SourceEntityId, string PropName)>
        ResolveReverseRefs(EntityMergeStore store, string targetEntityId);

    /// <summary>
    /// Clear the reference lookup cache. Call after session/store/index changes.
    /// </summary>
    void ClearLookupCache();

    /// <summary>
    /// Unified entity lookup via SQLite IndexService. Handles 4 reference forms.
    /// </summary>
    string? LookupEntityId(ReferenceIndexService indexService, string entityType, string rawId, string? sourceNs);
}
