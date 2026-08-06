using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data;
using NeoEditor.Data.Command;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using Newtonsoft.Json;

namespace NeoEditor.Plugins.Mcp.Tools;

/// <summary>
/// MCP tool definitions for NeoEditor. All tools interact with game data
/// exclusively through <see cref="IHostService"/> (R24).
/// Methods are decorated with <see cref="McpServerToolAttribute"/> for
/// automatic JSON Schema generation by the ModelContextProtocol SDK.
/// </summary>
[McpServerToolType]
public sealed class EditorTools
{
    private readonly IHostService _hostService;
    private readonly IReferenceResolver _referenceResolver;
    private readonly IServiceProvider _serviceProvider;

    public EditorTools(IHostService hostService, IReferenceResolver referenceResolver,
        IServiceProvider serviceProvider)
    {
        _hostService = hostService;
        _referenceResolver = referenceResolver;
        _serviceProvider = serviceProvider;
    }

    // ── Existing tools (descriptions enhanced for LLM guidance) ──

    [McpServerTool, Description(
         "Fetch a single entity by exact type and ID. Use ListEntities FIRST to discover available " +
         "entity IDs, then use this to get the full entity as JSON with all properties and references.")]
    public async Task<string> GetEntity(
        [Description("Entity type name (e.g., ItemType, Creature, Recipe, Encounter)")]
        string entityType,
        [Description("Entity ID string (e.g., item_weapon_sword)")]
        string entityId)
    {
        var entity = await GetEntityByTypeAsync(entityType, entityId);
        if (entity is null)
            return JsonConvert.SerializeObject(new { error = $"Entity not found: {entityType}/{entityId}" });

        return SerializeEntity(entity);
    }

    [McpServerTool, Description(
         "Edit a single property value on an entity. Changes are STAGED in memory, NOT saved to disk. " +
         "After making all desired edits, call the Save tool to persist. " +
         "Use GetEntitySchema to see available properties for a type.")]
    public async Task<string> EditEntity(
        [Description("Entity type name")] string entityType,
        [Description("Entity ID string")] string entityId,
        [Description("Name of the property to edit (use GetEntitySchema to list properties)")]
        string propertyName,
        [Description("New value to set (numbers parsed automatically, booleans: true/false)")]
        string newValue)
    {
        var entity = await GetEntityByTypeAsync(entityType, entityId);
        if (entity is null)
            return JsonConvert.SerializeObject(new { error = $"Entity not found: {entityType}/{entityId}" });

        var prop = FindProperty(entity, propertyName);
        if (prop is null)
            return JsonConvert.SerializeObject(new { error = $"Property '{propertyName}' not found on {entityType}" });

        var oldValue = prop.GetValue(entity);
        var converted = TryConvertValue(newValue, prop.PropertyType);
        var cmd = new EditCellCommand(entity, prop, propertyName, oldValue, converted, () => { });
        var result = await _hostService.ExecuteAsync(cmd, "mcp");

        return JsonConvert.SerializeObject(new
        {
            success = result.Success,
            property = propertyName,
            oldValue = oldValue?.ToString(),
            newValue = converted?.ToString(),
            error = result.Error
        });
    }

    [McpServerTool, Description(
         "Edit MULTIPLE properties on one entity in a single call (batch edit — the efficient " +
         "way to fill a new entity or mass-update fields). Changes are STAGED in memory; call Save to persist. " +
         "fieldsJson: JSON array of {\"name\": \"PropertyName\", \"value\": \"new value\"} — see GetEntitySchema " +
         "for property names. All edits apply atomically (one undo step).")]
    public async Task<string> BatchEditEntity(
        [Description("Entity type name")] string entityType,
        [Description("Entity ID string")] string entityId,
        [Description("JSON array of field edits, e.g. [{\"name\":\"StrName\",\"value\":\"Bandage\"},{\"name\":\"Weight\",\"value\":\"0.1\"}]")]
        string fieldsJson)
    {
        // Validate inputs FIRST (feedback: fail fast with clear messages before touching data).
        List<FieldEditSpec>? fields;
        try
        {
            fields = JsonConvert.DeserializeObject<List<FieldEditSpec>>(fieldsJson);
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new { error = $"Invalid fieldsJson: {ex.Message}" });
        }

        if (fields is not { Count: > 0 })
            return JsonConvert.SerializeObject(new { error = "fieldsJson must contain at least one {name, value} entry" });

        if (!Constants.GameTypes.TryGetValue(entityType, out var type))
            return JsonConvert.SerializeObject(new { error = $"Unknown entity type: {entityType}" });

        var entity = await GetEntityByTypeAsync(entityType, entityId);
        if (entity is null)
            return JsonConvert.SerializeObject(new { error = $"Entity not found: {entityType}/{entityId}" });

        var edits = new List<EditRecord>();
        var applied = new List<object>();
        foreach (var f in fields)
        {
            var prop = FindProperty(entity, f.Name);
            if (prop is null)
                return JsonConvert.SerializeObject(new { error = $"Property '{f.Name}' not found on {entityType}" });
            var oldValue = prop.GetValue(entity);
            var converted = TryConvertValue(f.Value ?? "", prop.PropertyType);
            edits.Add(new EditRecord(entity, prop, f.Name, oldValue, converted));
            applied.Add(new { name = f.Name, oldValue = oldValue?.ToString(), newValue = converted?.ToString() });
        }

        var cmd = new BatchEditCommand(edits, () => { });
        var result = await _hostService.ExecuteAsync(cmd, "mcp");

        return JsonConvert.SerializeObject(new
        {
            success = result.Success,
            entityId,
            entityType,
            appliedFields = applied.Count,
            applied,
            error = result.Error
        });
    }

    private sealed class FieldEditSpec
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }

    [McpServerTool, Description(
         "Create a new entity with the given type and ID. The entity is created empty — " +
         "use EditEntity to set its properties. Changes are staged; call Save to persist. " +
         "Use ListEntities to check if an entity ID is already taken.")]
    public async Task<string> AddEntity(
        [Description("Entity type name (from available types list)")]
        string entityType,
        [Description("Unique entity ID string — must not already exist")]
        string entityId)
    {
        if (!Constants.GameTypes.TryGetValue(entityType, out var type))
            return JsonConvert.SerializeObject(new { error = $"Unknown entity type: {entityType}" });

        var entity = (IEntity)Activator.CreateInstance(type)!;
        entity.EntityId = entityId;

        var cmd = new AddEntityCommand(entityType, entity,
            e => _hostService.AddEntityToCache(e),
            e => _hostService.RemoveEntityFromCache(e.EntityId));
        var result = await _hostService.ExecuteAsync(cmd, "mcp");

        return JsonConvert.SerializeObject(new
        {
            success = result.Success,
            entityId,
            entityType,
            error = result.Error
        });
    }

    [McpServerTool, Description(
         "Delete an entity permanently. WARNING: this is irreversible once saved. " +
         "Always confirm with the user before calling this. Changes are staged; call Save to persist.")]
    public async Task<string> DeleteEntity(
        [Description("Entity type name")] string entityType,
        [Description("Entity ID string")] string entityId)
    {
        var entity = await GetEntityByTypeAsync(entityType, entityId);
        if (entity is null)
            return JsonConvert.SerializeObject(new { error = $"Entity not found: {entityType}/{entityId}" });

        var cmd = new DeleteEntityCommand(entityType, entity,
            e => _hostService.RemoveEntityFromCache(e.EntityId),
            e => _hostService.AddEntityToCache(e));
        var result = await _hostService.ExecuteAsync(cmd, "mcp");

        return JsonConvert.SerializeObject(new
        {
            success = result.Success,
            entityId,
            entityType,
            error = result.Error
        });
    }

    [McpServerTool, Description(
         "Find which entities REFERENCE the given entity (reverse references). " +
         "Critical before deleting: e.g. deleting an ImageAsset without knowing which ItemType/Creature " +
         "uses it would break those entities. Uses the active session's reverse index; returns source " +
         "entity type/ID and the referencing property for each hit.")]
    public async Task<string> FindReferencingEntities(
        [Description("Entity type name of the target (e.g. ImageAsset, ItemType)")]
        string entityType,
        [Description("Entity ID string of the target (e.g. image_bandage)")]
        string entityId)
    {
        if (!Constants.GameTypes.TryGetValue(entityType, out _))
            return JsonConvert.SerializeObject(new { error = $"Unknown entity type: {entityType}" });

        var session = _serviceProvider.GetRequiredService<NeoEditor.Services.IWorkspaceSession>();
        var index = session.ReverseIndex;
        if (index is null)
            return JsonConvert.SerializeObject(new
            {
                error = "Reverse index is not built yet — open a mod/profile in the editor first"
            });

        var hits = index.ReverseLookup(entityId);
        var items = new List<object>();
        foreach (var (srcEid, propName, rawId) in hits)
        {
            var src = _hostService.GetCachedEntity(srcEid);
            items.Add(new
            {
                sourceEntityId = srcEid,
                sourceType = src?.GetType().Name,
                sourceSubject = src?.Subject ?? srcEid,
                property = propName,
                rawValue = rawId
            });
        }

        return JsonConvert.SerializeObject(new
        {
            targetEntityId = entityId,
            referencingCount = items.Count,
            referencing = items
        });
    }

    [McpServerTool, Description(
         "Discard the STAGED (unsaved) changes for one entity — removes it from the dirty set so " +
         "a subsequent Save will NOT write it. Use when a batch of edits went wrong and Undo is " +
         "insufficient (e.g. after the edits were already interleaved). The in-memory values remain; " +
         "to revert values to their last-saved state, use Undo before this, or re-edit explicitly.")]
    public async Task<string> DiscardChanges(
        [Description("Entity type name")] string entityType,
        [Description("Entity ID string whose staged changes should be discarded")]
        string entityId)
    {
        if (!Constants.GameTypes.TryGetValue(entityType, out _))
            return JsonConvert.SerializeObject(new { error = $"Unknown entity type: {entityType}" });

        await _hostService.DiscardAsync(entityId);
        return JsonConvert.SerializeObject(new
        {
            success = true,
            entityId,
            entityType,
            staged = "cleared"
        });
    }

    [McpServerTool, Description(
         "List entities of a given EXACT type. Use only when the entity type is already known " +
         "(e.g. ItemType, Creature); prefer SearchAllTypes when the type is unknown or the user " +
         "describes the entity by name. Supports optional substring filtering on entity subject/ID. " +
         "Results are truncated; use a higher 'limit' to see more entries.")]
    public async Task<string> ListEntities(
        [Description("Entity type name to list")]
        string entityType,
        [Description("Optional substring filter on entity subject or ID")]
        string? filter = null,
        [Description("Maximum number of results (default 100)")]
        int limit = 100)
    {
        var entities = await GetAllByTypeAsync(entityType);

        var filtered = entities.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filtered = filtered.Where(e =>
                (e.Subject ?? e.EntityId ?? "")
                .Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var allItems = filtered.Select(e => new
        {
            entityType,
            entityId = e.EntityId ?? "",
            subject = e.Subject ?? e.EntityId ?? ""
        }).ToList();

        var limited = allItems.Take(limit).ToList();

        var result = new
        {
            count = limited.Count,
            total = entities.Count,
            filteredTotal = allItems.Count,
            truncated = allItems.Count > limit,
            items = limited
        };

        return SerializeWithTruncation(result, 800);
    }

    [McpServerTool, Description(
         "Persist all staged changes to disk. After making edits with EditEntity/AddEntity/DeleteEntity, " +
         "you MUST call this to write changes back to the database/XML files. " +
         "Pass entityId to save only one entity, or omit to save everything.")]
    public async Task<string> Save(
        [Description("Optional: save only this entity ID instead of all dirty entities")]
        string? entityId = null)
    {
        var dirtyBefore = _hostService.DirtyEntities.Count;

        SaveResult result;
        if (!string.IsNullOrWhiteSpace(entityId))
            result = await _hostService.SaveAsync(entityId);
        else
            result = await _hostService.SaveAllAsync();

        return JsonConvert.SerializeObject(new
        {
            saved = result.SavedEntityIds.Count > 0,
            savedCount = result.SavedEntityIds.Count,
            savedEntityIds = result.SavedEntityIds,
            dirtyBefore,
            remainingDirty = _hostService.DirtyEntities.Count,
            note = result.SavedEntityIds.Count == 0 && dirtyBefore > 0
                ? "No entities were saved — dirty entities are missing from the working cache. Re-edit them to re-stage."
                : null
        });
    }

    [McpServerTool, Description(
         "Show field-level differences between the in-memory (edited) version and the stored " +
         "(on-disk) version. Use BEFORE saving to verify your changes are correct. " +
         "Omit entityId to diff all dirty entities.")]
    public async Task<string> GetDiff(
        [Description("Optional: diff for a specific entity; omit to diff all dirty entities")]
        string? entityId = null)
    {
        var diffs = await _hostService.GetDiffAsync(
            string.IsNullOrWhiteSpace(entityId) ? null : entityId);

        var items = diffs.Select(d => new
        {
            d.PropertyName,
            d.OldValue,
            d.NewValue,
            Kind = d.Kind.ToString()
        }).ToList();

        return JsonConvert.SerializeObject(new { count = items.Count, diffs = items });
    }

    [McpServerTool, Description(
         "Resolve reference values on an entity property. References are IDs that point to other entities. " +
         "Use this to find out what entity a reference points to, e.g., what weapon an ItemType's " +
         "weaponRef points to. Returns both the raw ID and the resolved entity subject.")]
    public async Task<string> ResolveReferences(
        [Description("Entity type name")] string entityType,
        [Description("Entity ID string")] string entityId,
        [Description("Name of the reference property to resolve")]
        string propertyName)
    {
        var entity = await GetEntityByTypeAsync(entityType, entityId);
        if (entity is null)
            return JsonConvert.SerializeObject(new { error = $"Entity not found: {entityType}/{entityId}" });

        var prop = FindProperty(entity, propertyName);
        if (prop is null)
            return JsonConvert.SerializeObject(new { error = $"Property '{propertyName}' not found on {entityType}" });

        var rawValue = ReferenceText.GetRawString(prop.GetValue(entity),
            prop.GetCustomAttribute<ReferenceFieldAttribute>());
        var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();

        var separator = refAttr?.Separator;
        var segments = string.IsNullOrWhiteSpace(rawValue)
            ? Array.Empty<string>()
            : (separator is not null
                ? rawValue.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                : new[] { rawValue });

        var targetType = refAttr?.TargetEntityType ?? typeof(IEntity);
        var resolved = new List<object>();

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            var subject = _referenceResolver.LookupSubject(entity.EntityId, propertyName, targetType, trimmed);
            resolved.Add(new
            {
                rawId = trimmed,
                subject = subject ?? "(unresolved)",
                resolved = subject is not null
            });
        }

        return JsonConvert.SerializeObject(new
        {
            rawValue,
            segmentCount = segments.Length,
            resolvedCount = resolved.Count(r => ((dynamic)r).resolved),
            targets = resolved
        });
    }

    // ── New tools (A3.2) ──

    [McpServerTool, Description(
         "Get the schema (all properties, their types, and reference field metadata) for a given entity type. " +
         "Use this when you need to know what properties exist before calling EditEntity, " +
         "or when the user asks about an entity type's structure. " +
         "Call GetModInfo to see all available entity types.")]
    public async Task<string> GetEntitySchema(
        [Description("Entity type name (e.g., ItemType, Creature, Recipe)")]
        string entityType)
    {
        if (!Constants.GameTypes.TryGetValue(entityType, out var type))
        {
            var allTypes = string.Join(", ", Constants.GameTypes.Keys.OrderBy(k => k));
            return JsonConvert.SerializeObject(new
            {
                error = $"Unknown entity type: {entityType}",
                availableTypes = allTypes
            });
        }

        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

        var schemaProps = new List<object>();
        foreach (var p in props)
        {
            var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
            schemaProps.Add(new
            {
                name = p.Name,
                type = p.PropertyType.Name.ToLowerInvariant(),
                isReference = refAttr is not null,
                targetEntityType = refAttr?.TargetEntityType?.Name,
                separator = refAttr?.Separator,
                description = GetPropertyDescription(p.Name, refAttr)
            });
        }

        return JsonConvert.SerializeObject(new
        {
            entityType,
            tableName = tableAttr?.Name ?? type.Name,
            propertyCount = schemaProps.Count,
            properties = schemaProps.OrderBy(p => ((dynamic)p).name).ToList()
        });
    }

    [McpServerTool, Description(
         "Search across ALL entity types for a matching subject, ID, or any string property. " +
         "PREFERRED tool for finding entities by name/keyword/content when the user does not name " +
         "an exact type (e.g. \"find something about stone\", \"which entity is 独头弹?\"). " +
         "Narrow with entityType (e.g. AttackMode, ItemType) or modId (the numeric namespace shown " +
         "in search results / GetEntity) to reduce noise. Only use ListEntities when the entity " +
         "type is already known. For typed field filters pass filtersJson, e.g. " +
         "[{\"field\":\"Weight\",\"op\":\">=\",\"value\":\"1.5\"}] — ops: contains, =, ==, !=, <>, " +
         "startsWith, endsWith, >, >=, <, <=. Pass entityTypesJson to search several types at once.")]
    public async Task<string> SearchAllTypes(
        [Description("Substring to search for in entity subject, ID, or any string property. " +
                     "OPTIONAL — may be empty when searching purely by filtersJson or modId")]
        string? query = "",
        [Description("Optional: restrict to one entity type (e.g. AttackMode, ItemType, Recipe)")]
        string? entityType = null,
        [Description("Optional: restrict to a mod's entities (its numeric modId, as shown in search results)")]
        int? modId = null,
        [Description("Maximum total results across all types (default 100)")]
        int limit = 100,
        [Description("Optional: JSON array of entity type names, e.g. [\"ItemType\",\"Creature\"] (overrides entityType)")]
        string? entityTypesJson = null,
        [Description("Optional: JSON array of typed field filters, e.g. [{\"field\":\"Weight\",\"op\":\">=\",\"value\":\"1.5\"}], AND semantics")]
        string? filtersJson = null,
        [Description("Optional: page offset for pagination (default 0)")]
        int offset = 0)
    {
        // entityTypesJson → multi-type selection (overrides the single entityType)
        List<string>? entityTypes = null;
        if (!string.IsNullOrWhiteSpace(entityTypesJson))
        {
            try
            {
                entityTypes = JsonConvert.DeserializeObject<List<string>>(entityTypesJson);
                if (entityTypes is { Count: 0 }) entityTypes = null;
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = $"Invalid entityTypesJson: {ex.Message}" });
            }
        }
        else if (!string.IsNullOrWhiteSpace(entityType))
        {
            entityTypes = new List<string> { entityType };
        }

        // filtersJson → typed field filters
        List<EntityFilter>? filters = null;
        if (!string.IsNullOrWhiteSpace(filtersJson))
        {
            try
            {
                filters = JsonConvert.DeserializeObject<List<FilterSpec>>(filtersJson)?
                    .Select(f => new EntityFilter(f.Field, ParseFilterOperator(f.Op), f.Value ?? ""))
                    .ToList();
                if (filters is { Count: 0 }) filters = null;
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = $"Invalid filtersJson: {ex.Message}" });
            }
        }

        var result = await _hostService.SearchEntitiesAsync(new EntitySearchRequest(
            query ?? "", entityTypes, modId, filters, limit, offset));

        var items = result.Items.Select(e => new
        {
            entityType = e.GetType().Name,
            entityId = e.EntityId ?? "",
            subject = e.Subject ?? e.EntityId ?? "",
            modId = e.ModId
        });

        return JsonConvert.SerializeObject(new
        {
            query,
            totalMatches = result.Total,
            offset,
            returned = result.Items.Count,
            truncated = result.Truncated,
            items
        });
    }

    [McpServerTool, Description(
         "Get information about the current workspace: loaded mods, game directory, " +
         "available entity types, dirty status. Use this when the user asks about " +
         "the editor's current state or to orient yourself at the start of a session.")]
    public async Task<string> GetModInfo()
    {
        var entityTypes = Constants.GameTypes.Keys.OrderBy(k => k).ToList();
        var dirtyList = _hostService.DirtyEntities.Take(20).ToList();

        return JsonConvert.SerializeObject(new
        {
            entityTypeCount = entityTypes.Count,
            entityTypes,
            hasUnsavedChanges = _hostService.HasUnsavedChanges,
            dirtyEntityCount = _hostService.DirtyEntities.Count,
            dirtyEntitiesSample = dirtyList
        });
    }

    // ── Undo / Redo (MCP scope undo — R31) ──

    [McpServerTool, Description(
         "Undo the last MCP edit (EditEntity/AddEntity/DeleteEntity). Restores the entity's " +
         "previous in-memory value and re-marks it dirty. Scoped to MCP commands only — UI edits " +
         "are not affected. Call Save afterwards to persist the undone state.")]
    public async Task<string> Undo()
    {
        await _hostService.UndoAsync("mcp");
        return JsonConvert.SerializeObject(new
        {
            success = true,
            dirtyEntityCount = _hostService.DirtyEntities.Count
        });
    }

    [McpServerTool, Description(
         "Redo the last undone MCP edit. Re-applies the command that was reverted by Undo. " +
         "Call Save afterwards to persist.")]
    public async Task<string> Redo()
    {
        await _hostService.RedoAsync("mcp");
        return JsonConvert.SerializeObject(new
        {
            success = true,
            dirtyEntityCount = _hostService.DirtyEntities.Count
        });
    }

    // ── Publish / Export (R26 pipeline: memory → DB → XML) ──

    [McpServerTool, Description(
         "Run the full publish pipeline: save all staged changes to the DB, then export the " +
         "affected mods back to their XML files. Set commit=true to write the exported XML files " +
         "to disk immediately; otherwise a preview (file paths + change kinds) is returned and " +
         "nothing is written to disk.")]
    public async Task<string> Publish(
        [Description("Optional: write the exported XML files to disk immediately (default false = preview only)")]
        bool commit = false)
    {
        var result = await _hostService.PublishAsync();

        var exports = new List<object>();
        foreach (var export in result.Exports)
        {
            if (commit && export.Files.Count > 0)
                await _hostService.CommitExportAsync(export.Files);
            exports.Add(new
            {
                modId = export.ModId,
                fileCount = export.Files.Count,
                files = export.Files.Select(f => new
                {
                    targetId = f.TargetId,
                    kind = f.Kind.ToString(),
                    oldContentPreview = Truncate(f.OldContent, 200),
                    newContentPreview = Truncate(f.NewContent, 200)
                })
            });
        }

        return JsonConvert.SerializeObject(new
        {
            success = true,
            savedCount = result.Save.SavedEntityIds.Count,
            savedEntityIds = result.Save.SavedEntityIds,
            committed = commit,
            exports
        });
    }

    [McpServerTool, Description(
         "Preview (or commit) the XML export of a single mod: converts the mod's DB entities back " +
         "to their XML files and reports what would change. Use GetModInfo to list mods, or read " +
         "modId from search results / GetEntity. Set commit=true to write the files to disk " +
         "immediately — the final write step after you have reviewed the preview.")]
    public async Task<string> ExportMod(
        [Description("Numeric mod ID (shown as modId in GetModInfo / search results / GetEntity)")]
        int modId,
        [Description("Optional: write the exported XML files to disk immediately (default false = preview only)")]
        bool commit = false)
    {
        var results = await _hostService.ExportModAsync(modId);

        var files = new List<object>();
        foreach (var result in results)
        {
            if (commit && result.Files.Count > 0)
                await _hostService.CommitExportAsync(result.Files);
            files.AddRange(result.Files.Select(f => new
            {
                targetId = f.TargetId,
                kind = f.Kind.ToString(),
                oldContentPreview = Truncate(f.OldContent, 200),
                newContentPreview = Truncate(f.NewContent, 200)
            }));
        }

        return JsonConvert.SerializeObject(new
        {
            success = true,
            modId,
            committed = commit,
            fileCount = files.Count,
            files
        });
    }

    // ── Helpers ──

    /// <summary>Wire format for a typed filter in filtersJson.</summary>
    private class FilterSpec
    {
        public string? Field { get; set; }
        public string? Op { get; set; }
        public string? Value { get; set; }
    }

    private static FilterOperator ParseFilterOperator(string op)
    {
        return op.Trim().ToLowerInvariant() switch
        {
            "contains" => FilterOperator.Contains,
            "=" or "==" or "equals" => FilterOperator.Equals,
            "!=" or "<>" or "notequals" => FilterOperator.NotEquals,
            "startswith" or "prefix" => FilterOperator.StartsWith,
            "endswith" or "suffix" => FilterOperator.EndsWith,
            ">" => FilterOperator.GreaterThan,
            ">=" => FilterOperator.GreaterThanOrEqual,
            "<" => FilterOperator.LessThan,
            "<=" => FilterOperator.LessThanOrEqual,
            _ => FilterOperator.Contains
        };
    }

    private static string? Truncate(string? s, int max)
        => s is null || s.Length <= max ? s : s[..max] + "…";

    /// <summary>
    /// Serialize an object to JSON and truncate if it exceeds the character limit.
    /// Appends a truncation notice with the full count.
    /// </summary>
    private static string SerializeWithTruncation(object obj, int maxChars)
    {
        var json = JsonConvert.SerializeObject(obj);
        if (json.Length <= maxChars) return json;

        // Truncate the items array and add notice
        var truncated = json.Substring(0, maxChars);
        var lastComma = truncated.LastIndexOf(',');
        if (lastComma > maxChars - 100) // find clean cut point
            truncated = truncated.Substring(0, lastComma);

        return truncated + $", \"_truncated\":true, \"_notice\":\"Response truncated at {maxChars} chars.\"}}";
    }

    private static string SerializeEntity(IEntity entity)
    {
        var props = entity.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name, p =>
            {
                var val = p.GetValue(entity);
                return val?.ToString() ?? "";
            });

        return JsonConvert.SerializeObject(new
        {
            entityType = entity.GetType().Name,
            entityId = entity.EntityId,
            subject = entity.Subject ?? entity.EntityId,
            modId = entity.ModId,
            properties = props
        });
    }

    private static string GetPropertyDescription(string propName, ReferenceFieldAttribute? refAttr)
    {
        if (refAttr is not null)
        {
            var target = refAttr.TargetEntityType?.Name ?? "IEntity";
            return refAttr.Separator is not null
                ? $"Reference to {target} (multi-value, separator: '{refAttr.Separator}')"
                : $"Reference to {target}";
        }

        return propName switch
        {
            "EntityId" => "Primary identifier string",
            "Subject" => "Display name shown in the game",
            "ModId" => "Owning mod namespace",
            "SortKey" => "Sort order within lists",
            _ => "Data property"
        };
    }

    internal async Task<IEntity?> GetEntityByTypeAsync(string entityType, string entityId)
    {
        if (!Constants.GameTypes.TryGetValue(entityType, out var type))
            return null;
        var repo = GetRepository(type);
        var method = repo?.GetType().GetMethod("GetByIdAsync");
        var task = (Task?)method?.Invoke(repo, new object[] { entityId });
        if (task is null) return null;
        await task.ConfigureAwait(false);
        var baseline = task.GetType().GetProperty("Result")?.GetValue(task) as IEntity;
        // 追修(C): the tables are the baseline — merge the current profile's overlay so
        // reads see this profile's edits (and IsDeleted entities resolve to null).
        var merged = baseline is null
            ? _hostService.MergeProfileOverlay([])
            : _hostService.MergeProfileOverlay([baseline]);
        return merged.FirstOrDefault(e => e.EntityId == entityId);
    }

    internal async Task<IReadOnlyList<IEntity>> GetAllByTypeAsync(string entityType)
    {
        if (!Constants.GameTypes.TryGetValue(entityType, out var type))
            return Array.Empty<IEntity>();
        var repo = GetRepository(type);
        var method = repo?.GetType().GetMethod("GetAllAsync");
        var task = (Task?)method?.Invoke(repo, null);
        if (task is null) return Array.Empty<IEntity>();
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")?.GetValue(task) as System.Collections.IEnumerable;
        // 追修(C): merge the current profile's overlay (see GetEntityByTypeAsync).
        return result?.Cast<IEntity>() is { } baseline
            ? _hostService.MergeProfileOverlay(baseline)
            : (IReadOnlyList<IEntity>)Array.Empty<IEntity>();
    }

    internal object? GetRepository(Type entityType)
    {
        var method = typeof(IHostService).GetMethod(nameof(IHostService.Repository))
            ?.MakeGenericMethod(entityType);
        return method?.Invoke(_hostService, null);
    }

    internal static PropertyInfo? FindProperty(IEntity entity, string propertyName)
    {
        return entity.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    }

    internal static object? TryConvertValue(string raw, Type targetType)
    {
        try
        {
            if (targetType == typeof(string)) return raw;
            if (targetType == typeof(int)) return int.Parse(raw);
            if (targetType == typeof(double)) return double.Parse(raw);
            if (targetType == typeof(float)) return float.Parse(raw);
            if (targetType == typeof(bool)) return bool.Parse(raw);
            if (targetType == typeof(long)) return long.Parse(raw);
            return raw;
        }
        catch
        {
            return raw;
        }
    }

    // ── Image Generation (G2) ──

    [McpServerTool, Description(
         "Generate a pixel art image for a game entity using AI image generation (DALL·E or compatible). " +
         "Uses the entity's properties to build a prompt and returns the generated PNG image bytes. " +
         "Use GetEntity first to review the entity data before generating. " +
         "Requires OPENAI_API_KEY environment variable to be set.")]
    public async Task<string> GenerateImage(
        [Description("Entity type name (e.g., ItemType, Creature)")]
        string entityType,
        [Description("Entity ID string (e.g., item_weapon_sword)")]
        string entityId,
        [Description("Optional: target width in pixels (default 64)")]
        int? width = null,
        [Description("Optional: target height in pixels (default 64)")]
        int? height = null,
        [Description("Optional: style hint ('pixel-art', 'realistic', 'sketch')")]
        string? style = null)
    {
        // Resolve IImageGenerationService from DI at call time (R17-compliant)
        var imageService = _serviceProvider?.GetService(
            typeof(IImageGenerationService)) as IImageGenerationService;

        if (imageService is null || !imageService.IsAvailable)
        {
            return JsonConvert.SerializeObject(new
            {
                error = "Image generation is not available. " +
                        "Set OPENAI_API_KEY in environment variables to enable."
            });
        }

        try
        {
            var options = new ImageGenerationOptions(
                width ?? 64, height ?? 64, style ?? "pixel-art");

            var result = await imageService.GenerateForEntityAsync(entityType, entityId, options);

            // Convert image bytes to base64 for JSON transport
            var b64 = Convert.ToBase64String(result.ImageBytes);

            return JsonConvert.SerializeObject(new
            {
                success = true,
                entityType,
                entityId,
                width = result.Width,
                height = result.Height,
                format = result.Format,
                revisedPrompt = result.RevisedPrompt,
                imageBase64 = b64,
                imageSizeBytes = result.ImageBytes.Length
            });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                error = $"Image generation failed: {ex.Message}"
            });
        }
    }
}