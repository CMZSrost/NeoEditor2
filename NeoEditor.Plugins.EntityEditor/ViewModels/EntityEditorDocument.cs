using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Command;
using ContextActionProvider = NeoEditor.Core.Abstractions.IEntityContextActionProvider;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Infra.Services;
using NeoEditor.Services;
using Serilog;

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
        }
    }

    [ObservableProperty] public partial int ActiveTab { get; set; }

    [ObservableProperty] public partial TextDocument XmlContent { get; set; } = new("");

    [ObservableProperty] public partial bool IsReadOnly { get; set; }

    [ObservableProperty] public partial bool IsVisualDirty { get; set; }

    /// <summary>Context action providers from DI (e.g. "Generate Image").</summary>
    public IEnumerable<ContextActionProvider> ContextActionProviders { get; }
    public bool HasContextActions => ContextActionProviders.Any();

    /// <summary>R11: true when entity has unsaved edits. Cleared on successful save.</summary>
    [ObservableProperty] public partial bool IsDirty { get; set; }

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
            await using var db = await _dbFactory.CreateDbContextAsync();
            var entity = Entity;
            var type = entity.GetType();

            var method = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(type);
            var dbSet = (System.Collections.IList)method.Invoke(db, null)!;

            IEntity? existing = null;
            foreach (var item in dbSet)
            {
                if (item is IEntity e && e.EntityId == entity.EntityId)
                {
                    existing = e;
                    break;
                }
            }

            if (existing != null)
            {
                foreach (var prop in type.GetProperties()
                    .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null && p.CanWrite))
                {
                    var newValue = prop.GetValue(entity);
                    prop.SetValue(existing, newValue);
                }
                db.Update(existing);
            }
            else
            {
                db.Add(entity);
            }

            await db.SaveChangesAsync();
            MarkClean();

            var entityId = Entity.EntityId;
            _dataTable.EditedCells.RemoveWhere(c => c.EntityId == entityId);

            WeakReferenceMessenger.Default.Send(new SaveCompletedMessage());
            // Exclude only game base (ModId=-1); ModId=0 is a valid mod id and its WAL snapshot
            // must advance too, or its commands replay (and re-dirty) on restart.
            if (entity.ModId >= 0)
                WeakReferenceMessenger.Default.Send(new EntityDbSavedMessage(entity.ModId));

            _notification.ShowInfo(
                $"Saved: {Entity.GetType().Name} — {Entity.Subject ?? Entity.EntityId}",
                "Entity Saved");
        }
        catch (Exception ex)
        {
            _notification.ShowInfo($"Save failed: {ex.Message}", "Save Error");
        }
    }

    private readonly IWorkspaceSession _session;
    private readonly IDbContextFactory<GameDbContext> _dbFactory;
    private readonly IEntityLookupService _dataTable;
    private readonly INotificationService _notification;

    public EntityEditorDocument(
        IEntity entity,
        IWorkspaceSession session,
        IDbContextFactory<GameDbContext> dbFactory,
        IEntityLookupService dataTable,
        ILocalizationService loc,
        INotificationService notification,
        IEnumerable<ContextActionProvider>? contextActionProviders = null,
        bool isReadOnly = false)
        : base(loc)
    {
        ContextActionProviders = contextActionProviders ?? [];
        _session = session;
        _dbFactory = dbFactory;
        _dataTable = dataTable;
        _notification = notification;
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

    public void RefreshXml()
    {
        if (IsXmlFocused) return;
        if (Entity != null)
            XmlContent.Text = EntityXmlHelper.GenerateXmlFragment(Entity);
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

                if (prop == null || !prop.CanWrite) continue;

                try
                {
                    var converted = ValueConverter.Convert(val, prop.PropertyType);
                    xmlValues[name] = (prop, converted);
                }
                catch { /* skip unparseable values */ }
            }

            foreach (var (colName, (prop, newValue)) in xmlValues)
            {
                var oldValue = prop.GetValue(Entity);
                if (!Equals(oldValue, newValue))
                {
                    edits.Add(new EditRecord(Entity, prop, colName, oldValue, newValue));
                }
            }

            Log.Information("[XML-Apply] Phase2 done: {Count} diffs for entity {Eid}", edits.Count, Entity.EntityId);

            foreach (var (_, (prop, newValue)) in xmlValues)
            {
                prop.SetValue(Entity, newValue);
            }

            if (edits.Count > 0)
            {
                Log.Information("[XML-Apply] Phase4: sending {Count} edits to WAL for entity {Eid}", edits.Count, Entity.EntityId);
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
        catch { /* XML parse error */ }
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
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
            .OrderBy(p => IsKeyProperty(p) ? 0 : 1)
            .ThenBy(p => p.Name);

        foreach (var prop in props)
        {
            var colName = prop.GetCustomAttribute<ColumnAttribute>()!.Name;
            var value = prop.GetValue(entity);
            var escapedValue = System.Security.SecurityElement.Escape(value?.ToString() ?? "");
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
