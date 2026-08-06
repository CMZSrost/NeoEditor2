using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Command;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Services;
using Serilog;

// Aliases to avoid IWorkspaceSession/IHostService ambiguity with NeoEditor.Services.
using IConfigService = NeoEditor.Core.Abstractions.IConfigService;
using IHostService = NeoEditor.Core.Abstractions.IHostService;
using IXmlParser = NeoEditor.Core.Abstractions.IXmlParser;
using IReferenceEntry = NeoEditor.Core.Abstractions.IReferenceEntry;
using IReferenceListSerializer = NeoEditor.Core.Abstractions.IReferenceListSerializer;

namespace NeoEditor.Plugins.EntityEditor.ViewModels;

/// <summary>
/// Entity editor document shown in the Center DocumentDock.
/// Two tabs: Visualization (read-only, refreshable) and XML Edit.
/// Migrated from NeoEditor.App to Plugin during M10 Phase 5.
/// Now inherits PluginDocumentBase with DI-injected ILocalizationService
/// instead of ViewServices.Loc.
/// </summary>
public partial class EntityEditorDocument : PluginDocumentBase
{
    [ObservableProperty] public partial IEntity? Entity { get; set; }

    partial void OnEntityChanged(IEntity? value)
    {
        if (value != null)
        {
            var subject = value.Subject ?? $"{value.GetType().Name}#{value.EntityId}";
            SetStaticTitle($"{value.GetType().Name}: {subject}");
            XmlContent.Text = EntityXmlHelper.GenerateXmlFragment(value);
            _originalXml = ResolveOriginalXml(value);
            if (IsDiffView) RefreshDiff();
        }
    }

    [ObservableProperty] public partial int ActiveTab { get; set; }

    [ObservableProperty] public partial TextDocument XmlContent { get; set; } = new("");

    [ObservableProperty] public partial bool IsReadOnly { get; set; }

    [ObservableProperty] public partial bool IsVisualDirty { get; set; }

    /// <summary>R11: true when entity has unsaved edits. Cleared on successful save.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    /// <summary>XML diff view: left = original (disk) snapshot, right = current edits.</summary>
    [ObservableProperty] public partial bool IsDiffView { get; set; }

    [ObservableProperty] public partial TextDocument? DiffOldDocument { get; set; }
    [ObservableProperty] public partial TextDocument? DiffNewDocument { get; set; }

    partial void OnIsDiffViewChanged(bool value)
    {
        if (value) RefreshDiff();
    }

    /// <summary>Set by the View when XmlEditor has focus. When true, RefreshXml skips
    /// updating the text so the user's undo stack is preserved.</summary>
    public bool IsXmlFocused { get; set; }

    /// <summary>R11: Mark entity as having unsaved edits. Syncs with IWorkspaceSession.DirtyEntities.</summary>
    public void MarkDirty()
    {
        if (Entity == null || IsDirty) return;
        IsDirty = true;
        _session.MarkEntityDirty(Entity.EntityId);
        var subject = Entity.Subject ?? $"{Entity.GetType().Name}#{Entity.EntityId}";
        SetStaticTitle($"* {subject}");
    }

    /// <summary>R11: Clear dirty state after successful save.</summary>
    public void MarkClean()
    {
        if (!IsDirty || Entity == null) return;
        IsDirty = false;
        _session.RemoveDirtyEntities([Entity.EntityId]);
        var subject = Entity.Subject ?? $"{Entity.GetType().Name}#{Entity.EntityId}";
        SetStaticTitle(subject);
    }

    /// <summary>R11: Save single entity to game.db. Called from toolbar button.</summary>
    [RelayCommand]
    private async Task SaveDocument()
    {
        if (Entity == null || !IsDirty) return;
        try
        {
            // R24: all entity persistence flows through the host pipeline — the editor
            // never touches GameDbContext directly from the document.
            var entity = Entity;
            _hostService.AddEntityToCache(entity);
            var saved = await _hostService.SaveAsync(entity.EntityId);
            if (!saved.SavedEntityIds.Contains(entity.EntityId))
            {
                _notification.ShowInfo(
                    "Save skipped: '" + entity.EntityId + "' was not in the dirty set — discard and re-edit, or save again.",
                    "Entity Not Saved");
                return;
            }

            MarkClean();
            WeakReferenceMessenger.Default.Send(new SaveCompletedMessage());
            // Exclude only game base (ModId=-1); ModId=0 is a valid mod id and its WAL snapshot
            // must advance too, or its commands replay (and re-dirty) on restart.
            if (entity.ModId >= 0)
                WeakReferenceMessenger.Default.Send(new EntityDbSavedMessage(entity.ModId));

            _notification.ShowInfo(
                $"Saved: {entity.GetType().Name} — {entity.Subject ?? entity.EntityId}",
                "Entity Saved");
        }
        catch (Exception ex)
        {
            _notification.ShowInfo($"Save failed: {ex.Message}", "Save Error");
        }
    }

    private readonly IWorkspaceSession _session;
    private readonly IHostService _hostService;
    private readonly IEntityLookupService _dataTable;
    private readonly INotificationService _notification;
    private readonly IReferenceListSerializer _serializer;
    private readonly IXmlParser _xmlParser;
    private readonly IConfigService _configService;
    private string _originalXml = "";

    public EntityEditorDocument(
        IEntity entity,
        IWorkspaceSession session,
        IHostService hostService,
        IEntityLookupService dataTable,
        ILocalizationService loc,
        INotificationService notification,
        IReferenceListSerializer serializer,
        IXmlParser xmlParser,
        IConfigService configService,
        bool isReadOnly = false)
        : base(loc)
    {
        _session = session;
        _hostService = hostService;
        _dataTable = dataTable;
        _notification = notification;
        _serializer = serializer;
        _xmlParser = xmlParser;
        _configService = configService;
        Entity = entity;
        IsReadOnly = isReadOnly;

        if (_session.DirtyEntities.Contains(entity.EntityId))
        {
            IsDirty = true;
            _session.MarkEntityDirty(entity.EntityId);
        }

        var subject = IsDirty
            ? $"* {ResolveSubject(entity)}"
            : ResolveSubject(entity);
        SetStaticTitle(subject);
        XmlContent = new TextDocument(EntityXmlHelper.GenerateXmlFragment(entity));
    }

    private static string ResolveSubject(IEntity entity)
    {
        var s = entity.Subject;
        if (!string.IsNullOrWhiteSpace(s)) return s;

        var type = entity.GetType();
        foreach (var name in new[] { "Id", "nID" })
        {
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop?.GetValue(entity) is { } val)
                return $"{type.Name} #{val}";
        }

        return type.Name;
    }

    private static bool IsPrimaryKeyColumn(PropertyInfo prop)
    {
        var column = prop.GetCustomAttribute<ColumnAttribute>()?.Name;
        return column is "id" or "nID";
    }

    public void RefreshXml()
    {
        if (IsXmlFocused) return;
        if (Entity != null)
        {
            XmlContent.Text = EntityXmlHelper.GenerateXmlFragment(Entity);
            if (IsDiffView) RefreshDiff();
        }
    }

    /// <summary>Rebuild the diff documents (original disk snapshot vs current edits).</summary>
    public void RefreshDiff()
    {
        DiffOldDocument = new TextDocument(_originalXml);
        DiffNewDocument = new TextDocument(XmlContent.Text);
    }

    /// <summary>XML diff: load the entity's original (disk) snapshot for side-by-side compare.</summary>
    private string ResolveOriginalXml(IEntity entity)
    {
        try
        {
            var path = ResolveXmlPath(entity.FilePath);
            if (path is null || !File.Exists(path))
                return EntityXmlHelper.GenerateXmlFragment(entity);

            var original = FindOriginalEntity(entity, path);
            if (original is null)
                return EntityXmlHelper.GenerateXmlFragment(entity);

            var originalXml = EntityXmlHelper.GenerateXmlFragment(original);
            var currentXml = EntityXmlHelper.GenerateXmlFragment(entity);
            return string.Equals(originalXml, currentXml, StringComparison.Ordinal) ? currentXml : originalXml;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[XML-Diff] failed to load original for {Eid} — falling back to current snapshot", entity.EntityId);
            return EntityXmlHelper.GenerateXmlFragment(entity);
        }
    }

    private string? ResolveXmlPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        if (Path.IsPathRooted(filePath)) return filePath;

        var gameRoot = _configService.Config.GameRootDir;
        return string.IsNullOrWhiteSpace(gameRoot) ? null : Path.GetFullPath(Path.Combine(gameRoot, filePath));
    }

    /// <summary>
    /// Re-import the disk XML through the shared parser pipeline to find the original
    /// entity (same EntityId) — the current snapshot may have unexported edits.
    /// </summary>
    private IEntity? FindOriginalEntity(IEntity current, string fullPath)
    {
        var text = File.ReadAllText(fullPath);
        // Same utf8-declaration tolerance as the player's data browser (Docs/42 v2.18).
        if (text.Contains("encoding=\"utf8\"", StringComparison.OrdinalIgnoreCase))
            text = text.Replace("encoding=\"utf8\"", "encoding=\"utf-8\"", StringComparison.OrdinalIgnoreCase);
        var doc = XDocument.Parse(text);

        var method = typeof(IXmlParser).GetMethod(nameof(IXmlParser.ImportEntities))!
            .MakeGenericMethod(current.GetType());
        var imported = (System.Collections.IList)method.Invoke(_xmlParser, new object[] { doc, current.ModId, fullPath })!;
        foreach (var item in imported)
        {
            if (item is IEntity e && e.EntityId == current.EntityId)
                return e;
        }
        return null;
    }

    [RelayCommand]
    private void RefreshVisualization()
    {
        if (Entity == null) return;
        IsVisualDirty = false;
        OnPropertyChanged(nameof(Entity));
    }

    [RelayCommand]
    private void ApplyXmlToEntity()
    {
        if (Entity == null) return;
        try
        {
            var xml = XmlContent.Text;
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            var tableEl = doc.Element("table");
            if (tableEl == null) return;

            var type = Entity.GetType();
            var edits = new List<EditRecord>();

            var xmlValues = new Dictionary<string, (PropertyInfo Prop, object? NewValue)>();
            var primaryKeyChanges = new List<string>();
            foreach (var colEl in tableEl.Elements("column"))
            {
                var name = colEl.Attribute("name")?.Value;
                var val = colEl.Value;
                if (string.IsNullOrEmpty(name)) continue;

                var prop = type.GetProperties().FirstOrDefault(p =>
                {
                    var ca = p.GetCustomAttribute<ColumnAttribute>();
                    return ca?.Name == name;
                }) ?? type.GetProperty(name);

                if (prop == null || !prop.CanWrite || prop.DeclaringType == typeof(IEntity)) continue;

                if (IsPrimaryKeyColumn(prop))
                {
                    // R30: primary key columns are identity anchors — XML edits to them are
                    // rejected (original value kept) instead of silently corrupting the row key.
                    var rawCurrent = ReferenceText.GetRawString(prop.GetValue(Entity),
                        prop.GetCustomAttribute<ReferenceFieldAttribute>());
                    if (rawCurrent != val) primaryKeyChanges.Add(name);
                    continue;
                }

                try
                {
                    // R30 (A1): reference columns must deserialize through the serializer —
                    // ValueConverter.ChangeType throws on ReferenceList and the catch below
                    // silently swallowed XML edits to reference fields.
                    var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();
                    object? converted;
                    if (refAttr is not null
                        && prop.PropertyType == typeof(ReferenceList<IReferenceEntry>))
                    {
                        converted = _serializer.Deserialize(val, refAttr);
                    }
                    else
                    {
                        converted = ValueConverter.Convert(val, prop.PropertyType);
                    }

                    xmlValues[name] = (prop, converted);
                }
                catch
                {
                    /* skip unparseable values */
                }
            }

            if (primaryKeyChanges.Count > 0)
                _notification.ShowWarning(
                    "Primary key cannot be changed (original value kept): " + string.Join(", ", primaryKeyChanges),
                    "XML Apply");

            foreach (var (colName, (prop, newValue)) in xmlValues)
            {
                var oldValue = prop.GetValue(Entity);
                // R30 (追修 7): ReferenceList has no value equality — Equals(old, new) was
                // always false, so EVERY XML apply (incl. the auto-apply on document open)
                // produced spurious edits → WAL rows → dirty-on-open. Compare reference
                // fields by raw text; skip unchanged properties entirely (no instance churn).
                // Strings: DB NULL round-trips through XML as "" — normalize so a null column
                // doesn't count as a diff.
                var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();
                var changed = refAttr is not null
                    && prop.PropertyType == typeof(ReferenceList<IReferenceEntry>)
                    ? ReferenceText.GetRawString(oldValue, refAttr) != ReferenceText.GetRawString(newValue, refAttr)
                    : !(Equals(oldValue, newValue)
                        || oldValue is null && newValue is string { Length: 0 }
                        || newValue is null && oldValue is string { Length: 0 });
                if (!changed) continue;

                edits.Add(new EditRecord(Entity, prop, colName, oldValue, newValue));
                prop.SetValue(Entity, newValue);
            }

            Log.Information("[XML-Apply] Phase2 done: {Count} diffs for entity {Eid}", edits.Count, Entity.EntityId);

            if (edits.Count > 0)
            {
                Log.Information("[XML-Apply] Phase4: sending {Count} edits to WAL for entity {Eid}", edits.Count,
                    Entity.EntityId);
                WeakReferenceMessenger.Default.Send(new EntityFieldEditsMessage(Entity, edits));
                RefreshVisualizationCommand.Execute(null);
                MarkDirty();
            }
            else
            {
                Log.Information("[XML-Apply] Phase4: no diffs — nothing to persist");
                RefreshVisualizationCommand.Execute(null);
            }

            WeakReferenceMessenger.Default.Send(new ActiveEntityChangedMessage(Entity));
        }
        catch
        {
            /* XML parse error */
        }
    }
}

/// <summary>
/// Generates XML fragments for single entities, matching the game's pma_xml_export format.
/// </summary>
public static class EntityXmlHelper
{
    public static string GenerateXmlFragment(IEntity entity)
    {
        var type = entity.GetType();
        var tableName = type.GetCustomAttribute<TableAttribute>()?.Name ?? type.Name.ToLower();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version='1.0' encoding='utf8'?>");
        sb.AppendLine($"<table name=\"{tableName}\">");

        var props = type.GetProperties()
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null
                        && p.DeclaringType != typeof(IEntity))
            .OrderBy(p => IsKeyProperty(p) ? 0 : 1)
            .ThenBy(p => p.Name);

        foreach (var prop in props)
        {
            var colName = prop.GetCustomAttribute<ColumnAttribute>()!.Name;
            var value = prop.GetValue(entity);
            // R30: reference columns serialize as raw text ("3,14"), not "[3, 14]".
            var rawValue = ReferenceText.GetRawString(value,
                prop.GetCustomAttribute<ReferenceFieldAttribute>());
            var escapedValue = System.Security.SecurityElement.Escape(rawValue);
            sb.AppendLine($"  <column name=\"{colName}\">{escapedValue}</column>");
        }

        sb.AppendLine("</table>");
        return sb.ToString();
    }

    private static bool IsKeyProperty(PropertyInfo prop)
    {
        var indexAttr = prop.DeclaringType?.GetCustomAttribute<IndexAttribute>();
        if (indexAttr?.PropertyNames != null)
        {
            return indexAttr.PropertyNames.Contains(prop.Name)
                   && prop.Name != nameof(IEntity.EntityId);
        }

        return false;
    }
}