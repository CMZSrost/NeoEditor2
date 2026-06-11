using System;
using System.Collections.Generic;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Services;

/// <summary>
/// Per-tab merge state — replaces the static dictionaries on GenericDataGridHelper.
/// One instance per ModGameDataTabsView, scoped to a single merge/single-mod view.
/// </summary>
public class EntityMergeStore
{
    /// <summary>Cross-table entity lookups keyed by entity type (populated during load).</summary>
    public Dictionary<Type, List<object>> ReferenceLookups { get; } = new();

    /// <summary>Entity EntityId → source mod directory name.</summary>
    public Dictionary<string, string> EntityModNames { get; } = new();

    /// <summary>Entity EntityId → strModName (namespace) for reference resolution.
    /// Base game = "0", mods = their strModName. Used by ReferenceIndex for same-namespace grouping.</summary>
    public Dictionary<string, string> EntityNamespaces { get; } = new();

    /// <summary>Entity EntityId → merged auto-increment ID (populated during merge).</summary>
    public Dictionary<string, int> EntityMergedIds { get; } = new();

    /// <summary>Entities overridden by a higher-priority mod in the merge chain.</summary>
    public HashSet<string> OverriddenEntityIds { get; } = new();

    /// <summary>Entity EntityId → overlay chain entries (mod name, id, entity type for navigation).</summary>
    public Dictionary<string, List<OverlayChainEntry>> OverlayChainDisplay { get; } = new();

    /// <summary>(EntityId, ColumnName) → source mod name. Per-field origin tracking.</summary>
    public Dictionary<(string, string), string> FieldSources { get; } = new();

    /// <summary>(EntityId, ColumnName) flagged when two different merge mods modify the same field.</summary>
    public HashSet<(string, string)> FieldConflicts { get; } = new();

    /// <summary>strModName namespace → mod directory name mapping.</summary>
    public Dictionary<string, string> NamespaceToModName { get; } = new();

    /// <summary>Subject lookup cache: (entityType, rawId) → resolved subject string.</summary>
    [Obsolete("Use ReferenceIndex.LookupDisplay instead. Kept for backward compat.")]
    public Dictionary<(Type EntityType, string RawId), string?> SubjectCache { get; } = new();

    /// <summary>ModIds that participate in key-based overriding (Game base + strModName=0 mods).</summary>
    public HashSet<int> MergeSpaceModIds { get; } = new();

    // ── Reference index ────────────────────────────────────────────────────
    private ReferenceIndex? _index;
    /// <summary>Per-store reference index. Lazily initialized. Replaces FindBestMatch scan.</summary>
    public ReferenceIndex Index => _index ??= new ReferenceIndex(this);

    public void Clear()
    {
        ReferenceLookups.Clear();
        EntityModNames.Clear();
        EntityNamespaces.Clear();
        EntityMergedIds.Clear();
        OverriddenEntityIds.Clear();
        OverlayChainDisplay.Clear();
        FieldSources.Clear();
        FieldConflicts.Clear();
        NamespaceToModName.Clear();
        SubjectCache.Clear();
        MergeSpaceModIds.Clear();
        _index?.Clear();
    }
}
