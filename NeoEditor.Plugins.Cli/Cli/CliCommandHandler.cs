using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data;
using NeoEditor.Data.Command;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Plugins.Cli.Cli;

/// <summary>
/// Dispatches parsed CLI commands to handlers that interact with IHostService.
/// </summary>
public class CliCommandHandler
{
    private readonly IHostService _hostService;
    private readonly IReferenceResolver _referenceResolver;
    private readonly CliOutputFormatter _formatter;

    public CliCommandHandler(IHostService hostService, IReferenceResolver referenceResolver,
        CliOutputFormatter formatter)
    {
        _hostService = hostService;
        _referenceResolver = referenceResolver;
        _formatter = formatter;
    }

    public async Task<string> ExecuteAsync(CliParsedCommand cmd)
    {
        // Ensure a CLI scope exists for command execution
        using var session = new CliSession(_hostService);

        if (cmd.HasError)
            return _formatter.Format(new { error = true, message = cmd.ErrorMessage }, cmd.Format);

        object? result = cmd.Command switch
        {
            CliCommandType.Help => null, // handled below
            CliCommandType.GetEntity => await GetEntityAsync(cmd),
            CliCommandType.EditEntity => await EditEntityAsync(cmd),
            CliCommandType.AddEntity => await AddEntityAsync(cmd),
            CliCommandType.DeleteEntity => await DeleteEntityAsync(cmd),
            CliCommandType.ListEntities => await ListEntitiesAsync(cmd),
            CliCommandType.Save => await SaveAsync(cmd),
            CliCommandType.Diff => await DiffAsync(cmd),
            CliCommandType.QueryReferences => await ResolveReferencesAsync(cmd),
            _ => new { error = true, message = $"Unknown command: {cmd.Command}" }
        };

        if (cmd.Command == CliCommandType.Help)
            return _formatter.FormatHelp();

        return _formatter.Format(result, cmd.Format);
    }

    // ── Command Handlers ──

    private async Task<object> GetEntityAsync(CliParsedCommand cmd)
    {
        var entity = await GetEntityByTypeAsync(cmd.EntityType!, cmd.EntityId!);
        if (entity is null)
            return new { error = $"Entity not found: {cmd.EntityType}/{cmd.EntityId}" };

        return SerializeEntity(entity);
    }

    private async Task<object> EditEntityAsync(CliParsedCommand cmd)
    {
        var entity = await GetEntityByTypeAsync(cmd.EntityType!, cmd.EntityId!);
        if (entity is null)
            return new { error = $"Entity not found: {cmd.EntityType}/{cmd.EntityId}" };

        var prop = FindProperty(entity, cmd.PropertyName!);
        if (prop is null)
            return new { error = $"Property '{cmd.PropertyName}' not found on {cmd.EntityType}" };

        var oldValue = prop.GetValue(entity);
        var converted = TryConvertValue(cmd.PropertyValue!, prop.PropertyType);

        var editCmd = new EditCellCommand(entity, prop, cmd.PropertyName!, oldValue, converted, () => { });
        var result = await _hostService.ExecuteAsync(editCmd, "cli");

        return new
        {
            success = result.Success,
            description = editCmd.Description,
            error = result.Error
        };
    }

    private async Task<object> AddEntityAsync(CliParsedCommand cmd)
    {
        if (!Constants.GameTypes.TryGetValue(cmd.EntityType!, out var type))
            return new { error = $"Unknown entity type: {cmd.EntityType}" };

        var entity = (IEntity)Activator.CreateInstance(type)!;
        entity.EntityId = cmd.EntityId!;

        // Set initial property if both --property and --value were provided
        if (!string.IsNullOrWhiteSpace(cmd.PropertyName) && cmd.PropertyValue is not null)
        {
            var prop = FindProperty(entity, cmd.PropertyName);
            if (prop is not null && prop.CanWrite)
            {
                var converted = TryConvertValue(cmd.PropertyValue, prop.PropertyType);
                prop.SetValue(entity, converted);
            }
        }

        var addCmd = new AddEntityCommand(cmd.EntityType!, entity,
            e => _hostService.AddEntityToCache(e),
            e => _hostService.RemoveEntityFromCache(e.EntityId));
        var result = await _hostService.ExecuteAsync(addCmd, "cli");

        return new
        {
            success = result.Success,
            entityId = entity.EntityId,
            entityType = cmd.EntityType,
            error = result.Error
        };
    }

    private async Task<object> DeleteEntityAsync(CliParsedCommand cmd)
    {
        var entity = await GetEntityByTypeAsync(cmd.EntityType!, cmd.EntityId!);
        if (entity is null)
            return new { error = $"Entity not found: {cmd.EntityType}/{cmd.EntityId}" };

        var delCmd = new DeleteEntityCommand(cmd.EntityType!, entity,
            e => _hostService.RemoveEntityFromCache(e.EntityId),
            e => _hostService.AddEntityToCache(e));
        var result = await _hostService.ExecuteAsync(delCmd, "cli");

        return new
        {
            success = result.Success,
            entityId = cmd.EntityId,
            entityType = cmd.EntityType,
            error = result.Error
        };
    }

    private async Task<object> ListEntitiesAsync(CliParsedCommand cmd)
    {
        var entities = await GetAllByTypeAsync(cmd.EntityType!);
        var limit = cmd.Limit ?? 100;

        var filtered = entities.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(cmd.Filter))
        {
            filtered = filtered.Where(e =>
                (e.Subject ?? e.EntityId ?? "")
                .Contains(cmd.Filter, StringComparison.OrdinalIgnoreCase));
        }

        var items = filtered.Take(limit)
            .Select(e => new { entityType = cmd.EntityType, entityId = e.EntityId, subject = e.Subject ?? e.EntityId ?? "" })
            .ToList();

        return new { count = items.Count, total = entities.Count, items };
    }

    private async Task<object> SaveAsync(CliParsedCommand cmd)
    {
        if (!string.IsNullOrWhiteSpace(cmd.EntityId))
            await _hostService.SaveAsync(cmd.EntityId);
        else
            await _hostService.SaveAllAsync();

        return new { saved = true, entityId = cmd.EntityId ?? "(all)" };
    }

    private async Task<object> DiffAsync(CliParsedCommand cmd)
    {
        var diffs = await _hostService.GetDiffAsync(
            string.IsNullOrWhiteSpace(cmd.EntityId) ? null : cmd.EntityId);

        var items = diffs.Select(d => new
        {
            d.PropertyName,
            d.OldValue,
            d.NewValue,
            Kind = d.Kind.ToString()
        }).ToList();

        return new { count = items.Count, diffs = items };
    }

    private async Task<object> ResolveReferencesAsync(CliParsedCommand cmd)
    {
        var entity = await GetEntityByTypeAsync(cmd.EntityType!, cmd.EntityId!);
        if (entity is null)
            return new { error = $"Entity not found: {cmd.EntityType}/{cmd.EntityId}" };

        var prop = FindProperty(entity, cmd.PropertyName!);
        if (prop is null)
            return new { error = $"Property '{cmd.PropertyName}' not found on {cmd.EntityType}" };

        var rawValue = prop.GetValue(entity)?.ToString() ?? "";
        var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();

        var separator = refAttr?.Separator;
        var segments = string.IsNullOrWhiteSpace(rawValue)
            ? Array.Empty<string>()
            : (separator is not null
                ? rawValue.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                : new[] { rawValue });

        var targetType = refAttr?.TargetEntityType ?? typeof(IEntity);
        var resolved = segments.Select(seg =>
        {
            var trimmed = seg.Trim();
            var subject = _referenceResolver.LookupSubject(entity.EntityId, cmd.PropertyName!, targetType, trimmed);
            return new { rawId = trimmed, subject = subject ?? "(unresolved)", resolved = subject is not null };
        }).ToList();

        return new { rawValue, segmentCount = segments.Length, resolvedCount = resolved.Count(r => r.resolved), targets = resolved };
    }

    // ── Shared Helpers ──

    private async Task<IEntity?> GetEntityByTypeAsync(string entityType, string entityId)
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

    private async Task<IReadOnlyList<IEntity>> GetAllByTypeAsync(string entityType)
    {
        if (!Constants.GameTypes.TryGetValue(entityType, out var type))
            return Array.Empty<IEntity>();
        var repo = GetRepository(type);
        var method = repo?.GetType().GetMethod("GetAllAsync");
        var task = (Task?)method?.Invoke(repo, null);
        if (task is null) return Array.Empty<IEntity>();
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")?.GetValue(task)
            as System.Collections.IEnumerable;
        return result?.Cast<IEntity>().ToList() ?? (IReadOnlyList<IEntity>)Array.Empty<IEntity>();
    }

    private object? GetRepository(Type entityType)
    {
        var method = typeof(IHostService).GetMethod(nameof(IHostService.Repository))
            ?.MakeGenericMethod(entityType);
        return method?.Invoke(_hostService, null);
    }

    private static PropertyInfo? FindProperty(IEntity entity, string propertyName)
    {
        return entity.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    }

    private static object? TryConvertValue(string raw, Type targetType)
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

    private static object SerializeEntity(IEntity entity)
    {
        var props = entity.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name, p => p.GetValue(entity)?.ToString() ?? "");

        return new
        {
            entityType = entity.GetType().Name,
            entityId = entity.EntityId,
            subject = entity.Subject ?? entity.EntityId,
            modId = entity.ModId,
            properties = props
        };
    }
}
