using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using Newtonsoft.Json;

namespace NeoEditor.Plugins.Mcp.Resources;

/// <summary>
/// Implements <see cref="IMcpResourceProvider"/> by exposing entity data
/// as entity://{type}/{id} URIs.
/// </summary>
public class EntityResourceProvider : IMcpResourceProvider
{
    private readonly IHostService _hostService;

    public EntityResourceProvider(IHostService hostService)
    {
        _hostService = hostService;
    }

    public IReadOnlyList<string> GetResourceUris()
    {
        // We don't enumerate all entities eagerly — expensive.
        // Return the URI scheme pattern; individual reads resolve on demand.
        return new[] { "entity://{type}/{id}" };
    }

    public async Task<string?> ReadResourceAsync(string uri, CancellationToken ct = default)
    {
        // Parse entity://{type}/{id}
        if (!uri.StartsWith("entity://", StringComparison.OrdinalIgnoreCase))
            return null;

        var path = uri["entity://".Length..];
        var slashIndex = path.IndexOf('/');
        if (slashIndex < 0) return null;

        var entityType = path[..slashIndex];
        var entityId = path[(slashIndex + 1)..];

        if (!Constants.GameTypes.TryGetValue(entityType, out var type))
            return null;

        // Use reflection to call Repository<T>().GetByIdAsync()
        var repoMethod = typeof(IHostService).GetMethod(nameof(IHostService.Repository))
            ?.MakeGenericMethod(type);
        var repo = repoMethod?.Invoke(_hostService, null);
        var getById = repo?.GetType().GetMethod("GetByIdAsync");
        var task = (Task?)getById?.Invoke(repo, new object[] { entityId });
        if (task is null) return null;

        await task.ConfigureAwait(false);
        var resultProp = task.GetType().GetProperty("Result");
        var entity = resultProp?.GetValue(task) as IEntity;
        if (entity is null) return null;

        // Serialize to JSON. R30: reference columns must serialize as their raw text
        // ("16,46"), not the damaged "[16, 46]" ReferenceList.ToString() format.
        var dict = entity.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name, p => ReferenceText.GetRawString(p.GetValue(entity),
                p.GetCustomAttribute<ReferenceFieldAttribute>()));

        return JsonConvert.SerializeObject(new
        {
            entityType,
            entityId = entity.EntityId,
            properties = dict
        }, Formatting.Indented);
    }
}
