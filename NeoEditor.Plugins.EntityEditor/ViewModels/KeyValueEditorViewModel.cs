using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Command;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Services;
using Serilog;

// Alias reference-entry types to avoid IWorkspaceSession/IHostService ambiguity
// with NeoEditor.Services (same pattern as ReferenceResolver).
using IReferenceEntry = NeoEditor.Core.Abstractions.IReferenceEntry;
using IReferenceListSerializer = NeoEditor.Core.Abstractions.IReferenceListSerializer;

namespace NeoEditor.Plugins.EntityEditor.ViewModels;

public partial class KeyValueEditorViewModel : ObservableObject
{
    private readonly IWorkspaceSession _session;
    private readonly IReferenceListSerializer _serializer;

    [ObservableProperty] public partial IEntity? CurrentEntity { get; set; }

    partial void OnCurrentEntityChanged(IEntity? value)
    {
        if (value is not null)
            IsCurrentEntityDirty = _session.DirtyEntities.Contains(value.EntityId);
        else
            IsCurrentEntityDirty = false;
    }

    [ObservableProperty] public partial ObservableCollection<FieldSection> Sections { get; set; } = [];

    [ObservableProperty] public partial bool IsCurrentEntityDirty { get; set; }

    public KeyValueEditorViewModel(IWorkspaceSession session, IReferenceListSerializer serializer)
    {
        _session = session;
        _serializer = serializer;
        _session.DirtyStateChanged += (_, _) =>
        {
            if (CurrentEntity is not null)
                IsCurrentEntityDirty = _session.DirtyEntities.Contains(CurrentEntity.EntityId);
            else
                IsCurrentEntityDirty = false;
        };

        // Docs/41 需求1: after Save & Export, EditStore was cleared — re-derive the
        // per-field "edited (not yet exported)" markers.
        WeakReferenceMessenger.Default.Register<SaveCompletedMessage>(this, (_, _) =>
        {
            if (CurrentEntity is null) return;
            foreach (var section in Sections)
                foreach (var field in section.Fields)
                    field.IsEdited = IsEditedField(CurrentEntity, field.PropertyName);
        });
    }

    /// <summary>Docs/41 需求2: editor-internal metadata columns (EntityId/ModId/FilePath).</summary>
    private static bool IsMetaProperty(PropertyInfo p) => p.DeclaringType == typeof(IEntity);

    /// <summary>Docs/41 需求1: edited this session, not yet exported. Matches the DataGrid
    /// cell-highlight source (EditStore.EditedCells), including the "*" wildcard used by
    /// KV/XML edit paths.</summary>
    private bool IsEditedField(IEntity entity, string colName)
    {
        var editedCells = _session.ActiveEditStore?.EditedCells;
        if (editedCells is null) return false;
        return editedCells.Contains((entity.EntityId, colName))
               || editedCells.Contains((entity.EntityId, "*"));
    }

    private static readonly Dictionary<Type, List<(PropertyInfo Prop, string Section, bool IsKey, bool IsRef,
        string ColName, EditControlType CtrlType)>> PropCache = new();

    private Type? _lastEntityType;

    [RelayCommand]
    private void LoadEntity(IEntity? entity)
    {
        if (CurrentEntity != null && CurrentEntity != entity)
            ApplyChanges();

        CurrentEntity = entity;

        if (entity != null)
            IsCurrentEntityDirty = _session.DirtyEntities.Contains(entity.EntityId);
        else
            IsCurrentEntityDirty = false;

        if (entity == null)
        {
            Sections.Clear();
            _lastEntityType = null;
            return;
        }

        var type = entity.GetType();
        if (!PropCache.TryGetValue(type, out var cached))
        {
            cached = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
                .OrderBy(p => IsKeyProperty(p) ? 0 : 1).ThenBy(p => p.Name)
                .Select(p =>
                {
                    var ca = p.GetCustomAttribute<ColumnAttribute>()!;
                    // Docs/41: metadata fields render as a read-only TextBlock only —
                    // forcing EditControlType.ReadOnly prevents the editable control from
                    // also rendering (ghost/重影 fix).
                    var isReadOnly = IsKeyProperty(p) || IsMetaProperty(p);
                    return (Prop: p, Section: FieldGroupMetadata.GetSection(type, p.Name),
                        IsKey: IsKeyProperty(p), IsRef: p.GetCustomAttribute<ReferenceFieldAttribute>() != null,
                        ColName: ca.Name ?? p.Name,
                        CtrlType: isReadOnly
                            ? EditControlType.ReadOnly
                            : DetermineControlType(p, p.GetCustomAttribute<ReferenceFieldAttribute>() != null,
                                isKey: false));
                }).ToList();
            PropCache[type] = cached;
        }

        if (type == _lastEntityType && Sections.Count > 0)
        {
            var flatFields = Sections.SelectMany(s => s.Fields).ToList();
            var idx = 0;
            foreach (var group in cached.GroupBy(x => x.Section).OrderBy(g => g.Key))
            {
                foreach (var (prop, _, _, _, _, _) in group)
                {
                    if (idx >= flatFields.Count) goto fullRebuild;
                    var val = prop.GetValue(entity);
                    var str = ReferenceText.GetRawString(val, prop.GetCustomAttribute<ReferenceFieldAttribute>());
                    var field = flatFields[idx];
                    field.ReInit();
                    field.OriginalValue = str;
                    field.CurrentValue = str;
                    field.Property = prop;
                    // Docs/41: metadata + edited-state per field (survives auto-save).
                    field.IsMeta = IsMetaProperty(prop);
                    field.IsEdited = IsEditedField(entity, field.PropertyName);
                    idx++;
                }
            }

            return;
        }

        fullRebuild:
        _lastEntityType = type;
        var newSections = new ObservableCollection<FieldSection>();
        foreach (var group in cached.GroupBy(x => x.Section).OrderBy(g => g.Key))
        {
            var section = new FieldSection { Header = group.Key, IsExpanded = true };
            foreach (var (prop, _, isKey, isRef, colName, ctrlType) in group)
            {
                var val = prop.GetValue(entity);
                var str = ReferenceText.GetRawString(val, prop.GetCustomAttribute<ReferenceFieldAttribute>());
                var enumVals = prop.PropertyType.IsEnum
                    ? prop.PropertyType.GetEnumNames().ToList()
                    : new List<string>();
                var row = new FieldRow
                {
                    PropertyName = colName, DisplayName = colName,
                    CurrentValue = str, OriginalValue = str,
                    PropertyType = prop.PropertyType, Property = prop,
                    IsReference = isRef, IsKey = isKey, ControlType = ctrlType,
                    EnumValues = enumVals,
                    Description = BuildFieldDescription(type, prop),
                    // Docs/41: metadata read-only + edited-state per field.
                    IsMeta = IsMetaProperty(prop),
                    IsEdited = IsEditedField(entity, colName),
                };
                row.CompleteInit();
                section.Fields.Add(row);
            }

            newSections.Add(section);
        }

        Sections = newSections;
    }

    /// <summary>
    /// R30: field explanation for the Value Editor — authoritative meaning from
    /// <see cref="FieldDescriptions"/> (embedded from Docs/38), plus the reference
    /// format summary for [ReferenceField] columns.
    /// </summary>
    private static string? BuildFieldDescription(Type entityType, PropertyInfo prop)
    {
        var desc = FieldDescriptions.GetDescription(entityType, prop.Name);
        var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();
        if (refAttr is null) return desc;

        var detail = new List<string>();
        if (!string.IsNullOrWhiteSpace(refAttr.Pattern))
            detail.Add($"Pattern={refAttr.Pattern}");
        if (refAttr.Separator is not null)
            detail.Add($"分隔符=\"{refAttr.Separator}\"");
        if (!string.IsNullOrWhiteSpace(refAttr.OrSeparator))
            detail.Add($"或(OR)=\"{refAttr.OrSeparator}\"");
        if (!string.IsNullOrWhiteSpace(refAttr.TargetKey) && refAttr.TargetKey != "{Id}")
            detail.Add($"目标键={refAttr.TargetKey}");
        var refInfo = $"🔗 引用 → {refAttr.TargetEntityType.Name}"
                      + (detail.Count > 0 ? $"（{string.Join("，", detail)}）" : "");
        return desc is null ? refInfo : $"{desc}\n{refInfo}";
    }

    [RelayCommand]
    private void ToggleSection(FieldSection? section)
    {
        if (section != null) section.IsExpanded = !section.IsExpanded;
    }

    [RelayCommand]
    private void EditField(FieldRow? field)
    {
        if (field == null || CurrentEntity == null) return;
        field.IsDirty = true;
    }

    [RelayCommand]
    private void ApplyChanges()
    {
        if (CurrentEntity == null) return;
        Log.Information("[KV-Apply] scanning for dirty fields on entity {Eid}", CurrentEntity.EntityId);
        var edits = new List<EditRecord>();
        foreach (var section in Sections)
        {
            foreach (var field in section.Fields)
            {
                if (!field.IsDirty || field.Property == null) continue;
                try
                {
                    var refAttr = field.Property.GetCustomAttribute<ReferenceFieldAttribute>();
                    var isRefList = field.IsReference
                                    && field.Property.PropertyType == typeof(ReferenceList<IReferenceEntry>);

                    object? oldValue, newValue;
                    bool changed;
                    if (isRefList)
                    {
                        // Reference fields: ValueConverter.ChangeType throws on ReferenceList,
                        // so edits never persisted. Deserialize via the serializer instead.
                        // ReferenceList has no value-equality → detect no-op by raw text.
                        changed = field.OriginalValue != field.CurrentValue;
                        oldValue = _serializer.Deserialize(field.OriginalValue, refAttr!);
                        newValue = _serializer.Deserialize(field.CurrentValue, refAttr!);
                    }
                    else
                    {
                        oldValue = ValueConverter.Convert(field.OriginalValue, field.PropertyType);
                        newValue = ValueConverter.Convert(field.CurrentValue, field.PropertyType);
                        changed = !Equals(oldValue, newValue);
                    }

                    if (changed)
                    {
                        Log.Information("[KV-Apply] diff: col={Col} old={Old} new={New}", field.PropertyName, oldValue,
                            newValue);
                        edits.Add(new EditRecord(
                            CurrentEntity, field.Property, field.PropertyName,
                            oldValue, newValue));
                        // Docs/41 追修: reflect "edited, not yet exported" immediately — the
                        // EditStore entry is added when the EntityFieldEditsMessage is handled.
                        field.IsEdited = true;
                    }
                    else
                    {
                        Log.Information("[KV-Apply] no diff (already synced by keystrokes): col={Col} val={Val}",
                            field.PropertyName, oldValue);
                    }

                    var currentEntityValue = field.Property.GetValue(CurrentEntity);
                    if (!Equals(currentEntityValue, newValue))
                        field.Property.SetValue(CurrentEntity, newValue);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[KV-Apply] error on col={Col}", field.PropertyName);
                }

                field.IsDirty = false;
                field.OriginalValue = field.CurrentValue;
                field.IsJustApplied = true;
            }
        }

        if (edits.Count > 0)
        {
            Log.Information("[KV-Apply] sending {Count} edits to WAL for entity {Eid}", edits.Count,
                CurrentEntity.EntityId);
            WeakReferenceMessenger.Default.Send(new EntityFieldEditsMessage(CurrentEntity, edits));
            WeakReferenceMessenger.Default.Send(new RefreshEntityEditorMessage(CurrentEntity));
            _ = System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    foreach (var s in Sections)
                    foreach (var f in s.Fields)
                        f.IsJustApplied = false;
                }));
        }
        else
        {
            Log.Information("[KV-Apply] no dirty fields found for entity {Eid}", CurrentEntity.EntityId);
        }

        IsCurrentEntityDirty = false;
    }

    [RelayCommand]
    private void RevertChanges()
    {
        if (CurrentEntity == null) return;
        foreach (var section in Sections)
        {
            foreach (var field in section.Fields)
            {
                if (field.IsDirty)
                {
                    field.CurrentValue = field.OriginalValue;
                    field.IsDirty = false;
                }
            }
        }
    }

    private static EditControlType DetermineControlType(PropertyInfo prop, bool isRef, bool isKey)
    {
        if (isKey) return EditControlType.ReadOnly;
        if (isRef) return EditControlType.ReferencePicker;        if (prop.PropertyType == typeof(bool)) return EditControlType.ToggleSwitch;
        if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(long)
                                             || prop.PropertyType == typeof(float) ||
                                             prop.PropertyType == typeof(double))
            return EditControlType.Numeric;
        if (prop.PropertyType.IsEnum) return EditControlType.ComboBox;
        return EditControlType.TextBox;
    }

    private static bool IsKeyProperty(PropertyInfo prop)
    {
        var indexAttr = prop.DeclaringType?.GetCustomAttribute<IndexAttribute>();
        if (indexAttr?.PropertyNames != null)
            return indexAttr.PropertyNames.Contains(prop.Name) && prop.Name != nameof(IEntity.EntityId);
        return false;
    }
}

public partial class FieldSection : ObservableObject
{
    public string Header { get; set; } = "";
    [ObservableProperty] public partial bool IsExpanded { get; set; } = true;
    public List<FieldRow> Fields { get; set; } = [];
}

public partial class FieldRow : ObservableObject
{
    public string PropertyName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    [ObservableProperty] public partial string CurrentValue { get; set; } = "";

    partial void OnCurrentValueChanged(string value)
    {
        if (!_initializing && value != OriginalValue)
            IsDirty = true;
    }

    internal bool _initializing = true;

    public void CompleteInit()
    {
        _initializing = false;
    }

    public void ReInit()
    {
        _initializing = true;
        IsDirty = false;
        IsJustApplied = false;
    }

    public string OriginalValue { get; set; } = "";
    public Type PropertyType { get; set; } = typeof(string);
    public PropertyInfo? Property { get; set; }
    public bool IsReference { get; set; }
    public bool IsKey { get; set; }

    /// <summary>Docs/41 需求2: editor-internal metadata (EntityId/ModId/FilePath) — read-only.</summary>
    public bool IsMeta { get; set; }

    /// <summary>Read-only display: primary key OR editor metadata.</summary>
    public bool IsReadOnly => IsKey || IsMeta;

    /// <summary>Docs/41 需求1: edited this session and not yet exported (EditStore-driven).
    /// Auto-save does NOT clear it — only Save & Export does.</summary>
    [ObservableProperty] public partial bool IsEdited { get; set; }

    public EditControlType ControlType { get; set; }

    /// <summary>R30: field explanation tooltip (embedded Docs/38 meaning + reference format).</summary>
    public string Description { get; set; } = "";

    /// <summary>Docs/41 P4: whether the '?' hint glyph is shown for this field.</summary>
    public bool HasDescription => !string.IsNullOrEmpty(Description);
    [ObservableProperty] public partial bool IsDirty { get; set; }
    [ObservableProperty] public partial bool IsJustApplied { get; set; }
    [ObservableProperty] public partial List<string>? Suggestions { get; set; }
    [ObservableProperty] public partial List<string> EnumValues { get; set; } = [];

    public FieldRow()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PropertyType))
            {
                if (PropertyType.IsEnum)
                    EnumValues = PropertyType.GetEnumNames().ToList();
            }
        };
    }
}

public enum EditControlType
{
    TextBox,
    Numeric,
    ToggleSwitch,
    ComboBox,
    ReferencePicker,
    ReadOnly
}

public class ControlTypeVisibilityConverter : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not EditControlType ct || parameter is not string mode) return false;
        return mode switch
        {
            "toggle" => ct == EditControlType.ToggleSwitch,
            "combo" => ct == EditControlType.ComboBox,
            "textbox" => ct is EditControlType.TextBox or EditControlType.Numeric,
            "refpicker" => ct == EditControlType.ReferencePicker,
            _ => false,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter,
        System.Globalization.CultureInfo culture) => throw new System.NotImplementedException();
}

public class BoolStringConverter : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string s && bool.TryParse(s, out var b)) return b;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter,
        System.Globalization.CultureInfo culture)
    {
        if (value is bool b) return b.ToString();
        return "False";
    }
}

/// <summary>
/// R30: ToolTip.Tip shows nothing when the description is empty (avoid empty tooltip popups).
/// </summary>
public class EmptyStringToNullConverter : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? null : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter,
        System.Globalization.CultureInfo culture) => throw new System.NotImplementedException();
}