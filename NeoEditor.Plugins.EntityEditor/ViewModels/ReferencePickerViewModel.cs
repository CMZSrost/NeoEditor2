using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.EntityEditor.ViewModels;

/// <summary>
/// Result returned by <see cref="ReferencePickerDialog"/> after user confirms selection.
/// </summary>
public record ReferencePickerResult(
    string RawText,
    ReferenceList<IReferenceEntry> EntryList
);

/// <summary>
/// ViewModel for the Reference Picker dialog. Manages entity search/filter,
/// single/multi selection, and decoration editing (multiplier, negation, assignment).
/// </summary>
public partial class ReferencePickerViewModel : ObservableObject
{
    private readonly Type _targetEntityType;
    private readonly Type? _secondaryTargetEntityType;
    private readonly string? _separator;
    private readonly string? _pattern;
    private readonly IEntityLookupService _lookup;
    private readonly IReferenceListSerializer _serializer;
    private readonly ReferenceFieldAttribute _metadata;

    // ── Observable state ────────────────────────────────────────────────

    [ObservableProperty] public partial string SearchText { get; set; } = "";

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>All available entities of the target type (unfiltered master list).</summary>
    public List<EntityViewModel> AllEntities { get; } = [];

    /// <summary>Filtered subset of <see cref="AllEntities"/> matching <see cref="SearchText"/>.</summary>
    [ObservableProperty]
    public partial ObservableCollection<EntityViewModel> FilteredEntities { get; set; } = [];

    /// <summary>Currently highlighted entity in the list.</summary>
    [ObservableProperty] public partial EntityViewModel? SelectedEntity { get; set; }

    /// <summary>Already-selected references with decorations.</summary>
    [ObservableProperty]
    public partial ObservableCollection<ReferenceEntryViewModel> SelectedEntries { get; set; } = [];

    /// <summary>Live preview of the serialized output text.</summary>
    [ObservableProperty] public partial string PreviewRawText { get; set; } = "";

    // ── Mode flags ──────────────────────────────────────────────────────

    public bool IsMultiValue { get; }
    public bool SupportsMultiplier { get; }
    public bool SupportsNegation => true; // Negation wraps any format
    public bool SupportsAssign { get; }

    // ── Output ───────────────────────────────────────────────────────────

    public string? ResultRawText { get; private set; }
    public ReferenceList<IReferenceEntry>? ResultReferenceList { get; private set; }

    // ── Construction ─────────────────────────────────────────────────────

    public ReferencePickerViewModel(
        Type targetEntityType,
        Type? secondaryTargetEntityType,
        string? separator,
        string? pattern,
        string? targetKey,
        string currentRawValue,
        IEntityLookupService lookup,
        IReferenceListSerializer serializer)
    {
        _targetEntityType = targetEntityType;
        _secondaryTargetEntityType = secondaryTargetEntityType;
        _separator = separator;
        _pattern = pattern;
        _lookup = lookup;
        _serializer = serializer;

        IsMultiValue = separator is not null;

        // Pattern analysis for decoration support
        var p = pattern ?? "{id}";
        SupportsMultiplier = p.Contains("{mult}");
        SupportsAssign = p.Contains("={value}") || p.Contains("{value}=");

        // Build a synthetic ReferenceFieldAttribute for the serializer
        _metadata = new ReferenceFieldAttribute(targetEntityType)
        {
            Separator = separator,
            Pattern = pattern,
            TargetKey = targetKey,
            SecondaryTargetEntityType = secondaryTargetEntityType,
        };

        LoadEntities();
        DeserializeCurrentValue(currentRawValue);
        UpdatePreview();
    }

    // ── Entity loading ───────────────────────────────────────────────────

    private void LoadEntities()
    {
        var raw = _lookup.ReferenceLookups.TryGetValue(_targetEntityType, out var list)
            ? list.Cast<IEntity>().ToList()
            : [];

        // Deduplicate: highest MergedId wins (same logic as GetDedupedEntities)
        var deduped = raw
            .GroupBy(e => e.EntityId)
            .Select(g => g.MaxBy(e => e.MergedId)!)
            .OrderBy(e => e.Subject)
            .ToList();

        // Also load secondary type entities if specified
        List<IEntity> secondaryEntities = [];
        if (_secondaryTargetEntityType is not null)
        {
            var raw2 = _lookup.ReferenceLookups.TryGetValue(_secondaryTargetEntityType, out var list2)
                ? list2.Cast<IEntity>().ToList()
                : [];
            secondaryEntities = raw2
                .GroupBy(e => e.EntityId)
                .Select(g => g.MaxBy(e => e.MergedId)!)
                .OrderBy(e => e.Subject)
                .ToList();
        }

        foreach (var entity in deduped)
        {
            var modName = _lookup.EntityModNames.TryGetValue(entity.EntityId, out var mn) ? mn : "?";
            AllEntities.Add(new EntityViewModel(entity, modName, _targetEntityType));
        }

        foreach (var entity in secondaryEntities)
        {
            var modName = _lookup.EntityModNames.TryGetValue(entity.EntityId, out var mn) ? mn : "?";
            // Avoid duplicates across primary/secondary
            if (!AllEntities.Any(e => e.EntityId == entity.EntityId))
                AllEntities.Add(new EntityViewModel(entity, modName, _secondaryTargetEntityType!));
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var search = SearchText?.Trim() ?? "";
        FilteredEntities = new ObservableCollection<EntityViewModel>(
            string.IsNullOrEmpty(search)
                ? AllEntities
                : AllEntities.Where(e =>
                    e.Subject.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    e.EntityId.Contains(search, StringComparison.OrdinalIgnoreCase)));
    }

    // ── Deserialize current value ────────────────────────────────────────

    private void DeserializeCurrentValue(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            SelectedEntries = [];
            return;
        }

        try
        {
            var refList = _serializer.Deserialize(rawValue, _metadata);
            var entries = new List<ReferenceEntryViewModel>();
            foreach (var entry in refList)
            {
                var vm = ReferenceEntryViewModel.FromEntry(entry, _lookup, _metadata);
                entries.Add(vm);
            }
            SelectedEntries = new ObservableCollection<ReferenceEntryViewModel>(entries);
        }
        catch
        {
            // If deserialization fails, show empty and let user rebuild
            SelectedEntries = [];
        }
    }

    // ── Commands ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void AddSelectedEntity()
    {
        if (SelectedEntity is null) return;

        var entityRef = BuildEntityRef(SelectedEntity.Entity);
        IReferenceEntry newEntry = new PureRefFormat { Entity = entityRef };

        // Apply default decorations based on pattern
        if (SupportsMultiplier)
        {
            newEntry = _pattern?.Contains("{mult}x") == true
                ? new MultXIdFormat { Entity = entityRef, Multiplier = 1.0 }
                : new IdXMultFormat { Entity = entityRef, Multiplier = 1.0 };
        }

        if (!IsMultiValue)
        {
            // Single-value: replace
            SelectedEntries = new ObservableCollection<ReferenceEntryViewModel>
            {
                new(newEntry, _lookup, _metadata)
            };
        }
        else
        {
            // Multi-value: append if not already present
            var rawId = entityRef.ToRawString();
            if (!SelectedEntries.Any(e => e.RawId == rawId))
            {
                SelectedEntries.Add(new ReferenceEntryViewModel(newEntry, _lookup, _metadata));
            }
        }

        UpdatePreview();
    }

    [RelayCommand]
    private void RemoveEntry(ReferenceEntryViewModel? entry)
    {
        if (entry is not null)
        {
            SelectedEntries.Remove(entry);
            UpdatePreview();
        }
    }

    [RelayCommand]
    private void ToggleNegation(ReferenceEntryViewModel? entry)
    {
        if (entry is null) return;
        entry.IsNegated = !entry.IsNegated;
        RebuildEntry(entry);
        UpdatePreview();
    }

    [RelayCommand]
    private void SetMultiplier(ReferenceEntryViewModel? entry)
    {
        if (entry is null) return;
        RebuildEntry(entry);
        UpdatePreview();
    }

    [RelayCommand]
    private void SetAssignedValue(ReferenceEntryViewModel? entry)
    {
        if (entry is null) return;
        RebuildEntry(entry);
        UpdatePreview();
    }

    [RelayCommand]
    private void Confirm()
    {
        var entries = SelectedEntries.Select(e => e.ToReferenceEntry()).ToList();
        var refList = new ReferenceList<IReferenceEntry>();
        foreach (var entry in entries)
            refList.Add(entry);

        ResultRawText = _serializer.Serialize(refList, _metadata);
        ResultReferenceList = refList;
    }

    [RelayCommand]
    private void Cancel()
    {
        ResultRawText = null;
        ResultReferenceList = null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static EntityRef BuildEntityRef(IEntity entity)
    {
        // Check if the target uses composite keys (GroupId.SubgroupId)
        // We infer this from the entity type's key annotation
        var ns = entity.ModId == 0 ? null : entity.EntityId.Contains(":") ? entity.EntityId.Split(':')[0] : null;
        var idStr = entity.EntityId.Contains(':') ? entity.EntityId.Split(':')[1] : entity.EntityId;

        // Simple case: single integer or string ID
        if (int.TryParse(idStr, out var numId))
            return new EntityRef { Id = idStr, Namespace = ns };

        // Composite key case: "86.6"
        if (idStr.Contains('.'))
        {
            var parts = idStr.Split('.');
            if (parts.Length == 2 && int.TryParse(parts[0], out var gid) && int.TryParse(parts[1], out var sid))
                return new EntityRef { GroupId = gid, SubgroupId = sid, Namespace = ns };
        }

        return new EntityRef { Id = idStr, Namespace = ns };
    }

    private void RebuildEntry(ReferenceEntryViewModel vm)
    {
        vm.RebuildFromDecorations(_pattern);
    }

    private void UpdatePreview()
    {
        try
        {
            var entries = SelectedEntries.Select(e => e.ToReferenceEntry()).ToList();
            var refList = new ReferenceList<IReferenceEntry>();
            foreach (var entry in entries)
                refList.Add(entry);
            PreviewRawText = _serializer.Serialize(refList, _metadata);
        }
        catch
        {
            PreviewRawText = "(invalid)";
        }
    }
}

// ── Inner types ────────────────────────────────────────────────────────

/// <summary>
/// Wraps an <see cref="IEntity"/> for display in the picker list.
/// </summary>
public partial class EntityViewModel : ObservableObject
{
    public string EntityId { get; }
    public string Subject { get; }
    public string ModName { get; }
    public Type EntityType { get; }
    public IEntity Entity { get; }

    /// <summary>Display text for the picker list.</summary>
    public string DisplayText => $"{Subject}  [{EntityId}]";

    public EntityViewModel(IEntity entity, string modName, Type entityType)
    {
        Entity = entity;
        EntityId = entity.EntityId;
        Subject = entity.Subject ?? entity.EntityId;
        ModName = modName;
        EntityType = entityType;
    }

    public override string ToString() => DisplayText;
}

/// <summary>
/// Wraps a single <see cref="IReferenceEntry"/> with editable decorations.
/// </summary>
public partial class ReferenceEntryViewModel : ObservableObject
{
    private IReferenceEntry _innerEntry;

    /// <summary>Display text for the selected entity.</summary>
    public string DisplayText { get; set; } = "";

    /// <summary>Raw ID for dedup comparison.</summary>
    public string RawId { get; set; } = "";

    [ObservableProperty] public partial double Multiplier { get; set; } = 1.0;
    [ObservableProperty] public partial bool IsNegated { get; set; }
    [ObservableProperty] public partial double AssignedValue { get; set; }
    [ObservableProperty] public partial bool ValueFirst { get; set; }

    /// <summary>A human-readable summary of the current decoration state.</summary>
    public string DecorationSummary
    {
        get
        {
            var parts = new List<string>();
            if (IsNegated) parts.Add("-");
            if (Math.Abs(Multiplier - 1.0) > 0.001) parts.Add($"×{Multiplier}");
            if (AssignedValue != 0) parts.Add(ValueFirst ? $"{AssignedValue}=" : $"={AssignedValue}");
            return parts.Count > 0 ? string.Join(" ", parts) : "";
        }
    }

    public ReferenceEntryViewModel(IReferenceEntry entry, IEntityLookupService lookup, ReferenceFieldAttribute metadata)
    {
        _innerEntry = entry;
        ExtractFromEntry(entry, lookup, metadata);
    }

    /// <summary>
    /// Create from an existing IReferenceEntry, resolving display names.
    /// </summary>
    public static ReferenceEntryViewModel FromEntry(
        IReferenceEntry entry, IEntityLookupService lookup, ReferenceFieldAttribute metadata)
    {
        return new ReferenceEntryViewModel(entry, lookup, metadata);
    }

    private void ExtractFromEntry(IReferenceEntry entry, IEntityLookupService lookup, ReferenceFieldAttribute metadata)
    {
        // Drill through NegatedRefFormat wrapper
        var inner = entry;
        if (entry is NegatedRefFormat neg)
        {
            IsNegated = true;
            inner = neg.Inner;
        }

        // Drill through format wrappers to get the base EntityRef
        switch (inner)
        {
            case IdXMultFormat idX:
                Multiplier = idX.Multiplier;
                ResolveEntityRef(idX.Entity, lookup, metadata);
                break;
            case MultXIdFormat multX:
                Multiplier = multX.Multiplier;
                ResolveEntityRef(multX.Entity, lookup, metadata);
                break;
            case AssignFormat assign:
                AssignedValue = assign.Value;
                ValueFirst = assign.ValueFirst;
                ResolveEntityRef(assign.Entity, lookup, metadata);
                break;
            case PureRefFormat pure:
                ResolveEntityRef(pure.Entity, lookup, metadata);
                break;
            case BracketFormat bracket:
                ResolveEntityRef(bracket.Entity, lookup, metadata);
                break;
            default:
                RawId = entry.ToRawString();
                DisplayText = RawId;
                break;
        }

        _innerEntry = entry;
    }

    private void ResolveEntityRef(EntityRef entityRef, IEntityLookupService lookup, ReferenceFieldAttribute metadata)
    {
        RawId = entityRef.ToRawString();
        var targetType = metadata.TargetEntityType;

        // Try to resolve the entity to get its display name
        var rawKey = entityRef.IsComposite
            ? $"{entityRef.GroupId}.{entityRef.SubgroupId}"
            : entityRef.Id;

        var resolved = lookup.FindBestMatch(targetType, rawKey, metadata.TargetKey);
        if (resolved is not null)
        {
            DisplayText = resolved.Subject ?? rawKey;
        }
        else
        {
            DisplayText = rawKey;
        }
    }

    /// <summary>Rebuild the inner IReferenceEntry from current decoration state.</summary>
    public void RebuildFromDecorations(string? pattern)
    {
        // First, get the base EntityRef from the current entry
        var baseRef = GetBaseEntityRef();
        if (baseRef is null) return;

        // Build entry from decorations
        IReferenceEntry built;

        if (Math.Abs(AssignedValue) > 0.001)
        {
            built = new AssignFormat { Entity = baseRef, Value = AssignedValue, ValueFirst = ValueFirst };
        }
        else if (Math.Abs(Multiplier - 1.0) > 0.001)
        {
            built = (pattern?.Contains("{mult}x") == true)
                ? new MultXIdFormat { Entity = baseRef, Multiplier = Multiplier }
                : new IdXMultFormat { Entity = baseRef, Multiplier = Multiplier };
        }
        else
        {
            built = new PureRefFormat { Entity = baseRef };
        }

        if (IsNegated)
            built = new NegatedRefFormat { Inner = built };

        _innerEntry = built;
    }

    /// <summary>Convert back to a typed IReferenceEntry.</summary>
    public IReferenceEntry ToReferenceEntry() => _innerEntry;

    private EntityRef? GetBaseEntityRef()
    {
        // Drill through to find the EntityRef
        var current = _innerEntry;
        if (current is NegatedRefFormat neg)
            current = neg.Inner;

        return current switch
        {
            PureRefFormat p => p.Entity,
            IdXMultFormat i => i.Entity,
            MultXIdFormat m => m.Entity,
            AssignFormat a => a.Entity,
            BracketFormat b => b.Entity,
            _ => null,
        };
    }
}
