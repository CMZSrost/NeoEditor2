using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.Mcp.Server;
using NeoEditor.Plugins.Mcp.Tools;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Plugins.Mcp.Tests;

/// <summary>
/// Regression tests for <see cref="McpServerHost"/>.
/// The core regression: building <see cref="ModelContextProtocol.Server.McpServerOptions"/>
/// directly leaves <c>ToolCollection</c> null (SDK preview.3 only initializes it through the
/// DI builder path), which made <c>--mcp</c> startup throw NullReferenceException on the
/// first <c>options.ToolCollection.Add(tool)</c>.
/// </summary>
public class McpServerHostTests
{
    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostService>(new StubHostService());
        services.AddSingleton<IReferenceResolver>(new StubReferenceResolver());
        services.AddSingleton<EditorTools>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void BuildOptions_InitializesToolCollection_AndRegistersAllTools()
    {
        var host = new McpServerHost(CreateServiceProvider(), NullLoggerFactory.Instance);

        var options = host.BuildOptions();

        // Without the fix this throws NRE before the assertion is reached.
        Assert.NotNull(options.ToolCollection);
        Assert.Equal(19, options.ToolCollection!.Count);

        var names = new HashSet<string>(options.ToolCollection.Select(t => t.ProtocolTool.Name));
        Assert.Contains("GetEntity", names);
        Assert.Contains("EditEntity", names);
        Assert.Contains("AddEntity", names);
        Assert.Contains("DeleteEntity", names);
        Assert.Contains("ListEntities", names);
        Assert.Contains("Save", names);
        Assert.Contains("GetDiff", names);
        Assert.Contains("ResolveReferences", names);
        Assert.Contains("GetEntitySchema", names);
        Assert.Contains("SearchAllTypes", names);
        Assert.Contains("GetModInfo", names);
        Assert.Contains("GenerateImage", names);
        Assert.Contains("Undo", names);
        Assert.Contains("Redo", names);
        Assert.Contains("Publish", names);
        Assert.Contains("ExportMod", names);
        // Docs/41 MCP feedback tools (AI review)
        Assert.Contains("BatchEditEntity", names);
        Assert.Contains("FindReferencingEntities", names);
        Assert.Contains("DiscardChanges", names);
    }

    [Fact]
    public void BuildOptions_EachTool_HasNameAndDescription()
    {
        var host = new McpServerHost(CreateServiceProvider(), NullLoggerFactory.Instance);

        var options = host.BuildOptions();

        foreach (var tool in options.ToolCollection!)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.ProtocolTool.Name));
            Assert.False(string.IsNullOrWhiteSpace(tool.ProtocolTool.Description));
        }
    }

    // ── Stubs (only used to construct EditorTools; BuildOptions never invokes them) ──

    private sealed class StubHostService : IHostService
    {
        public int ActiveProfileId => -1;
        public void SetActiveProfile(int profileId) { }
        public ISet<string> DirtyEntities => new HashSet<string>();
        public bool HasUnsavedChanges => false;
        public event EventHandler? DirtyStateChanged;
        public Task<CommandResult> ExecuteAsync(IEditorCommand command, string? scopeId = null)
            => Task.FromResult(new CommandResult(true, null, Array.Empty<string>()));
        public Task<CommandResult> ExecuteBatchAsync(IEnumerable<IEditorCommand> commands, string? scopeId = null)
            => Task.FromResult(new CommandResult(true, null, Array.Empty<string>()));
        public Task UndoAsync(string? scopeId = null) => Task.CompletedTask;
        public Task RedoAsync(string? scopeId = null) => Task.CompletedTask;
        public void MarkEntityDirty(string entityId) { }
        public void MarkEntitiesDirty(IEnumerable<string> entityIds) { }
        public void ClearDirtyEntities() { }
        public void RemoveDirtyEntities(IEnumerable<string> entityIds) { }
        public Task<SaveResult> SaveAsync(string? entityId = null)
            => Task.FromResult(new SaveResult([], []));
        public Task<SaveResult> SaveAllAsync()
            => Task.FromResult(new SaveResult([], []));
        public Task DiscardAsync(string? entityId = null) => Task.CompletedTask;
        public Task<IReadOnlyList<ExportResult>> ExportModAsync(int modId)
            => Task.FromResult<IReadOnlyList<ExportResult>>([]);
        public Task<IReadOnlyList<ExportResult>> ExportProfileAsync()
            => Task.FromResult<IReadOnlyList<ExportResult>>([]);
        public Task CommitExportAsync(IEnumerable<RowDiff> diffs) => Task.CompletedTask;
    public Task AdvanceBaselineAsync(IReadOnlyList<string> entityIds) => Task.CompletedTask;
    public IReadOnlyList<IEntity> MergeProfileOverlay(IEnumerable<IEntity> baselineEntities) => baselineEntities.ToList();
        public Task<PublishResult> PublishAsync()
            => Task.FromResult(new PublishResult(new SaveResult([], []), []));
        public Task<IReadOnlyList<DiffEntry>> GetDiffAsync(string? entityId = null)
            => Task.FromResult<IReadOnlyList<DiffEntry>>(Array.Empty<DiffEntry>());
        public IObservable<EntityChangedEvent> Changes => null!;
        public IEntityRepository<T> Repository<T>() where T : IEntity
            => new StubRepository<T>();
        public Task<IReadOnlyList<IEntity>> SearchEntitiesAsync(string query, int limit = 50,
            string? entityType = null, int? modId = null)
            => Task.FromResult<IReadOnlyList<IEntity>>(Array.Empty<IEntity>());
        public void RegisterCommandScope(string scopeId, ICommandHistory history) { }
        public void UnregisterCommandScope(string scopeId) { }
        public void SetActiveScope(string? scopeId) { }
        public void RegisterPreSaveHook(IExtensionPoint<PreSaveContext> hook) { }
        public void RegisterPostLoadHook(IExtensionPoint<PostLoadContext> hook) { }
        public void RegisterPreExecuteHook(IExtensionPoint<PreExecuteContext> hook) { }
        public void RegisterPreExportHook(IExtensionPoint<PreExportContext> hook) { }
        public void RegisterEntityCollection(string scopeId, string entityType, System.Collections.IList collection) { }
        public void UnregisterEntityCollections(string scopeId) { }
        public IEntity? GetCachedEntity(string entityId) => null;
        public IReadOnlyList<IEntity> GetCachedEntitiesByType(string entityType) => Array.Empty<IEntity>();
        public void AddEntityToCache(IEntity entity) { }
        public void RemoveEntityFromCache(string entityId) { }
    }

    private sealed class StubRepository<T> : IEntityRepository<T> where T : IEntity
    {
        public Task<T?> GetByIdAsync(string entityId) => Task.FromResult<T?>(default);
        public Task<IReadOnlyList<T>> GetAllAsync()
            => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
        public Task AddAsync(T entity) => Task.CompletedTask;
        public Task UpdateAsync(T entity) => Task.CompletedTask;
        public Task DeleteAsync(string entityId) => Task.CompletedTask;
        public Task<IReadOnlyList<RowDiff>> GetDiffAsync(IReadOnlyList<T> candidates)
            => Task.FromResult<IReadOnlyList<RowDiff>>(Array.Empty<RowDiff>());
        public Task<IReadOnlyList<DiffEntry>> GetFieldDiffAsync(T before, T after)
            => Task.FromResult<IReadOnlyList<DiffEntry>>(Array.Empty<DiffEntry>());
        public IReadOnlyCollection<string> DirtyIds => Array.Empty<string>();
        public void MarkDirty(IEnumerable<string> ids) { }
        public void ClearDirty(IEnumerable<string> ids) { }
        public Task SaveAsync(IEnumerable<T> entities) => Task.CompletedTask;
        public Task<IReadOnlyList<T>> LoadAsync()
            => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
    }

    private sealed class StubReferenceResolver : IReferenceResolver
    {
        public T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : IEntity
            => default;
        public string? LookupSubject(string sourceEntityId, string propertyName, Type targetType,
            string rawId, Type? secondaryTargetType = null) => null;
        public IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)>
            ReverseLookup(EntityMergeStore store, string targetEntityId)
            => Array.Empty<(string, string, string)>();
        public IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, Type targetType, EntityMergeStore? storeOverride = null)
            => null;
        public Task BuildReverseIndexAsync(ReferenceIndexService indexService, EntityMergeStore store)
            => Task.CompletedTask;
        public List<(Type SourceType, string SourceSubject, string SourceEntityId, string PropName)>
            ResolveReverseRefs(EntityMergeStore store, string targetEntityId)
            => new();
        public void ClearLookupCache() { }
        public string? LookupEntityId(ReferenceIndexService indexService, string entityType,
            string rawId, string? sourceNs) => null;
    }
}
