using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Player.Core.Services;

namespace NeoEditor.Plugins.WebView.Services;

/// <summary>
/// IHostService-backed implementation of <see cref="IGameDataExportService"/>.
/// Reads via <c>IHostService.Repository&lt;T&gt;()</c> (the R24 funnel — never GameDbContext
/// directly), merges the active profile overlay, then exports with IXmlParser (the same
/// pma_xml_export serializer the editor writes to disk).
/// </summary>
internal sealed class LiveGameDataExportService : IGameDataExportService
{
    private static readonly MethodInfo RepositoryMethod =
        typeof(IHostService).GetMethod(nameof(IHostService.Repository))!;

    private readonly IHostService _host;
    private readonly IXmlParser _xmlParser;

    public LiveGameDataExportService(IHostService host, IXmlParser xmlParser)
    {
        _host = host;
        _xmlParser = xmlParser;
    }

    public async Task<string?> ExportTableXmlAsync(string tableName)
    {
        var entityType = GameTableMap.FindType(tableName);
        if (entityType is null) return null;

        var entities = await GetAllAsync(_host, entityType).ConfigureAwait(false);
        var merged = _host.MergeProfileOverlay(entities);
        var doc = _xmlParser.Export(merged, "neogame");
        return doc.ToString(SaveOptions.None);
    }

    private static async Task<IReadOnlyList<IEntity>> GetAllAsync(IHostService host, Type entityType)
    {
        // IEntityRepository<T> repo = host.Repository<T>();  (T resolved at runtime)
        var repo = RepositoryMethod.MakeGenericMethod(entityType).Invoke(host, null)!;
        var getAll = typeof(IDataRepository<IEntity>).GetMethod(nameof(IDataRepository<IEntity>.GetAllAsync))!;
        var task = (Task)getAll.Invoke(repo, null)!;
        await task.ConfigureAwait(false);
        // Task<IReadOnlyList<T>>.Result — IReadOnlyList<T> is covariant, so the cast to
        // IReadOnlyList<IEntity> is safe.
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return (IReadOnlyList<IEntity>)result;
    }
}
