using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
        [Description("Entity type name (e.g., ItemType, Creature, Recipe, Encounter)")] string entityType,
        [Description("Entity ID string (e.g., item_weapon_sword)")] string entityId)
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
        [Description("Name of the property to edit (use GetEntitySchema to list properties)")] string propertyName,
        [Description("New value to set (numbers parsed automatically, booleans: true/false)")] string newValue)
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
        "Create a new entity with the given type and ID. The entity is created empty — " +
        "use EditEntity to set its properties. Changes are staged; call Save to persist. " +
        "Use ListEntities to check if an entity ID is already taken.")]
    public async Task<string> AddEntity(
        [Description("Entity type name (from available types list)")] string entityType,
        [Description("Unique entity ID string — must not already exist")] string entityId)
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
        "List entities of a given type. Always use this BEFORE GetEntity to explore what exists. " +
        "Supports optional substring filtering on entity subject/ID. Results are truncated at 500 chars; " +
        "use a higher 'limit' to see more entries.")]
    public async Task<string> ListEntities(
        [Description("Entity type name to list")] string entityType,
        [Description("Optional substring filter on entity subject or ID")] string? filter = null,
        [Description("Maximum number of results (default 100)")] int limit = 100)
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
        [Description("Optional: save only this entity ID instead of all dirty entities")] string? entityId = null)
    {
        if (!string.IsNullOrWhiteSpace(entityId))
            await _hostService.SaveAsync(entityId);
        else
            await _hostService.SaveAllAsync();

        return JsonConvert.SerializeObject(new { saved = true, entityId = entityId ?? "(all)" });
    }

    [McpServerTool, Description(
        "Show field-level differences between the in-memory (edited) version and the stored " +
        "(on-disk) version. Use BEFORE saving to verify your changes are correct. " +
        "Omit entityId to diff all dirty entities.")]
    public async Task<string> GetDiff(
        [Description("Optional: diff for a specific entity; omit to diff all dirty entities")] string? entityId = null)
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
        [Description("Name of the reference property to resolve")] string propertyName)
    {
        var entity = await GetEntityByTypeAsync(entityType, entityId);
        if (entity is null)
            return JsonConvert.SerializeObject(new { error = $"Entity not found: {entityType}/{entityId}" });

        var prop = FindProperty(entity, propertyName);
        if (prop is null)
            return JsonConvert.SerializeObject(new { error = $"Property '{propertyName}' not found on {entityType}" });

        var rawValue = prop.GetValue(entity)?.ToString() ?? "";
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
        "Use ListEntityTypes first to see all available types.")]
    public async Task<string> GetEntitySchema(
        [Description("Entity type name (e.g., ItemType, Creature, Recipe)")] string entityType)
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
        "Search across ALL entity types for a matching subject or ID. " +
        "Use this when you don't know which type an entity belongs to, " +
        "or when the user asks a general question about what entities exist. " +
        "Much faster than calling ListEntities on every type individually.")]
    public async Task<string> SearchAllTypes(
        [Description("Substring to search for in entity subject or ID")] string query,
        [Description("Maximum total results across all types (default 30)")] int limit = 30)
    {
        var allResults = new List<object>();

        foreach (var kvp in Constants.GameTypes.OrderBy(k => k.Key))
        {
            if (allResults.Count >= limit) break;

            var entities = await GetAllByTypeAsync(kvp.Key);
            var matches = entities
                .Where(e =>
                    (e.Subject ?? e.EntityId ?? "")
                    .Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit - allResults.Count)
                .Select(e => new
                {
                    entityType = kvp.Key,
                    entityId = e.EntityId ?? "",
                    subject = e.Subject ?? e.EntityId ?? ""
                });

            allResults.AddRange(matches);
        }

        return JsonConvert.SerializeObject(new
        {
            query,
            totalMatches = allResults.Count,
            items = allResults
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

    // ── Helpers ──

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
        return task.GetType().GetProperty("Result")?.GetValue(task) as IEntity;
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
        return result?.Cast<IEntity>().ToList() ?? (IReadOnlyList<IEntity>)Array.Empty<IEntity>();
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
        catch { return raw; }
    }

    // ── Image Generation (G2) ──

    [McpServerTool, Description(
        "Generate a pixel art image for a game entity using AI image generation (DALL·E or compatible). " +
        "Uses the entity's properties to build a prompt and returns the generated PNG image bytes. " +
        "Use GetEntity first to review the entity data before generating. " +
        "Requires OPENAI_API_KEY environment variable to be set.")]
    public async Task<string> GenerateImage(
        [Description("Entity type name (e.g., ItemType, Creature)")] string entityType,
        [Description("Entity ID string (e.g., item_weapon_sword)")] string entityId,
        [Description("Optional: target width in pixels (default 64)")] int? width = null,
        [Description("Optional: target height in pixels (default 64)")] int? height = null,
        [Description("Optional: style hint ('pixel-art', 'realistic', 'sketch')")] string? style = null)
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
