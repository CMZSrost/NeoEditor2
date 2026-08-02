using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.Mcp.Tools;
using NeoEditor.Services;
using Newtonsoft.Json;
using Xunit;

namespace NeoEditor.Plugins.Mcp.Tests;

public class McpToolExecutorTests
{
    private static EditorTools CreateTools()
    {
        var hostService = new StubHostService();
        var refResolver = new StubReferenceResolver();
        return new EditorTools(hostService, refResolver, null!);
    }

    [Fact]
    public void GetTools_Returns_AllTwelveTools()
    {
        var executor = new McpToolExecutor(CreateTools());
        var tools = executor.GetTools();

        Assert.Equal(12, tools.Count);

        var names = tools.Select(t => t.Name).ToHashSet();
        // Original 8 tools
        Assert.Contains("GetEntity", names);
        Assert.Contains("EditEntity", names);
        Assert.Contains("AddEntity", names);
        Assert.Contains("DeleteEntity", names);
        Assert.Contains("ListEntities", names);
        Assert.Contains("Save", names);
        Assert.Contains("GetDiff", names);
        Assert.Contains("ResolveReferences", names);
        // New A3 tools
        Assert.Contains("GetEntitySchema", names);
        Assert.Contains("SearchAllTypes", names);
        Assert.Contains("GetModInfo", names);
        // New G2 tool
        Assert.Contains("GenerateImage", names);
    }

    [Fact]
    public void GetTools_Each_HasName_Description_And_Schema()
    {
        var executor = new McpToolExecutor(CreateTools());
        var tools = executor.GetTools();

        foreach (var tool in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Name));
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));
            Assert.False(string.IsNullOrWhiteSpace(tool.InputSchemaJson));

            // Schema must be valid JSON with type=object
            var schema = JsonConvert.DeserializeObject<dynamic>(tool.InputSchemaJson);
            Assert.NotNull(schema);
            Assert.Equal("object", (string?)schema!.type);
        }
    }

    [Fact]
    public void GetTools_RequiredParams_HaveNoDefault()
    {
        var executor = new McpToolExecutor(CreateTools());
        var tools = executor.GetTools();

        var getEntity = tools.First(t => t.Name == "GetEntity");
        var schema = JsonConvert.DeserializeObject<dynamic>(getEntity.InputSchemaJson);
        Assert.NotNull(schema);
        Assert.Contains("entityType", (IEnumerable<dynamic>?)schema!.required ?? Array.Empty<dynamic>());
        Assert.Contains("entityId", (IEnumerable<dynamic>?)schema.required ?? Array.Empty<dynamic>());
    }

    [Fact]
    public async Task ExecuteTool_UnknownTool_ReturnsError()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("NonExistentTool", "{}");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.NotNull((string?)parsed!.error);
        Assert.Contains("Unknown tool", (string)parsed.error!);
    }

    [Fact]
    public async Task ExecuteTool_MissingRequiredParam_ReturnsError()
    {
        var executor = new McpToolExecutor(CreateTools());
        // GetEntity requires entityType + entityId; send neither
        var result = await executor.ExecuteToolAsync("GetEntity", "{}");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.NotNull((string?)parsed!.error);
    }

    [Fact]
    public async Task ExecuteTool_GetEntity_NotFound_ReturnsErrorJson()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("GetEntity",
            """{"entityType": "ItemType", "entityId": "nonexistent"}""");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.Contains("not found", ((string?)parsed!.error ?? "").ToLowerInvariant());
    }

    [Fact]
    public async Task ExecuteTool_Save_ReturnsSuccess()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("Save", """{"entityId": null}""");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.True((bool?)parsed!.saved);
    }

    [Fact]
    public async Task ExecuteTool_ListEntities_ReturnsCountAndItems()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("ListEntities",
            """{"entityType": "ItemType", "filter": null, "limit": 10}""");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.NotNull((int?)parsed!.count);
        Assert.NotNull((int?)parsed.total);
    }

    [Fact]
    public async Task ExecuteTool_AddEntity_UnknownType_ReturnsError()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("AddEntity",
            """{"entityType": "UnknownType", "entityId": "test123"}""");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.Contains("Unknown entity type", (string?)parsed!.error ?? "");
    }

    // ── New A3 tools ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteTool_GetEntitySchema_UnknownType_ReturnsAvailableTypes()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("GetEntitySchema",
            """{"entityType": "NonExistentType"}""");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.NotNull((string?)parsed!.error);
        Assert.Contains("Unknown entity type", (string?)parsed.error!);
        Assert.NotNull((string?)parsed.availableTypes);
    }

    [Fact]
    public async Task ExecuteTool_GetEntitySchema_ReturnsPropertyList()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("GetEntitySchema",
            """{"entityType": "ItemType"}""");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.Equal("ItemType", (string?)parsed!.entityType);
        Assert.NotNull((int?)parsed.propertyCount);
        Assert.True((int?)parsed.propertyCount > 0);
        Assert.NotNull(parsed.properties);
    }

    [Fact]
    public async Task ExecuteTool_SearchAllTypes_ReturnsMatchingEntities()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("SearchAllTypes",
            """{"query": "sword", "limit": 10}""");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.NotNull((string?)parsed!.query);
        Assert.NotNull((int?)parsed.totalMatches);
        Assert.NotNull(parsed.items);
    }

    [Fact]
    public async Task ExecuteTool_GetModInfo_ReturnsWorkspaceInfo()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("GetModInfo", "{}");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.NotNull((int?)parsed!.entityTypeCount);
        Assert.True((int?)parsed.entityTypeCount > 0);
        Assert.NotNull(parsed.entityTypes);
        Assert.NotNull((bool?)parsed.hasUnsavedChanges);
    }

    // ── Stubs ────────────────────────────────────────────────────────────

    private sealed class StubHostService : IHostService
    {
        public int ActiveProfileId => -1;

        public void SetActiveProfile(int profileId)
        {
        }

        public ISet<string> DirtyEntities => new HashSet<string>();
        public bool HasUnsavedChanges => false;
        public event EventHandler? DirtyStateChanged;

        public Task<CommandResult> ExecuteAsync(IEditorCommand command, string? scopeId = null)
            => Task.FromResult(new CommandResult(true, null, Array.Empty<string>()));

        public Task<CommandResult> ExecuteBatchAsync(IEnumerable<IEditorCommand> commands, string? scopeId = null)
            => Task.FromResult(new CommandResult(true, null, Array.Empty<string>()));

        public Task UndoAsync(string? scopeId = null) => Task.CompletedTask;
        public Task RedoAsync(string? scopeId = null) => Task.CompletedTask;

        public void MarkEntityDirty(string entityId)
        {
        }

        public void MarkEntitiesDirty(IEnumerable<string> entityIds)
        {
        }

        public void ClearDirtyEntities()
        {
        }

        public void RemoveDirtyEntities(IEnumerable<string> entityIds)
        {
        }

        public Task<SaveResult> SaveAsync(string? entityId = null)
            => Task.FromResult(new SaveResult([], []));

        public Task<SaveResult> SaveAllAsync()
            => Task.FromResult(new SaveResult([], []));

        public Task DiscardAsync(string? entityId = null) => Task.CompletedTask;

        public Task<IReadOnlyList<ExportResult>> ExportModAsync(int modId)
            => Task.FromResult<IReadOnlyList<ExportResult>>([]);

        public Task<IReadOnlyList<ExportResult>> ExportProfileAsync()
            => Task.FromResult<IReadOnlyList<ExportResult>>([]);

        public Task<PublishResult> PublishAsync()
            => Task.FromResult(new PublishResult(new SaveResult([], []), []));

        public Task<IReadOnlyList<DiffEntry>> GetDiffAsync(string? entityId = null)
            => Task.FromResult<IReadOnlyList<DiffEntry>>(Array.Empty<DiffEntry>());

        public IObservable<EntityChangedEvent> Changes => null!;

        public IEntityRepository<T> Repository<T>() where T : IEntity
            => new StubRepository<T>();

        public List<IEntity> SearchResults { get; } = new();

        public Task<IReadOnlyList<IEntity>> SearchEntitiesAsync(string query, int limit = 50,
            string? entityType = null, int? modId = null)
            => Task.FromResult<IReadOnlyList<IEntity>>(SearchResults);

        public void RegisterCommandScope(string scopeId, ICommandHistory history)
        {
        }

        public void UnregisterCommandScope(string scopeId)
        {
        }

        public void SetActiveScope(string? scopeId)
        {
        }

        public void RegisterPreSaveHook(IExtensionPoint<PreSaveContext> hook)
        {
        }

        public void RegisterPostLoadHook(IExtensionPoint<PostLoadContext> hook)
        {
        }

        public void RegisterPreExecuteHook(IExtensionPoint<PreExecuteContext> hook)
        {
        }

        public void RegisterPreExportHook(IExtensionPoint<PreExportContext> hook)
        {
        }

        public void RegisterEntityCollection(string scopeId, string entityType, System.Collections.IList collection)
        {
        }

        public void UnregisterEntityCollections(string scopeId)
        {
        }

        public IEntity? GetCachedEntity(string entityId) => null;
        public IReadOnlyList<IEntity> GetCachedEntitiesByType(string entityType) => Array.Empty<IEntity>();

        public void AddEntityToCache(IEntity entity)
        {
        }

        public void RemoveEntityFromCache(string entityId)
        {
        }
    }

    private sealed class StubEntity : IEntity
    {
        public StubEntity(string entityId, string subject)
        {
            EntityId = entityId;
            _subject = subject;
        }

        private readonly string _subject;

        public override string Subject => _subject;
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

        public void MarkDirty(IEnumerable<string> ids)
        {
        }

        public void ClearDirty(IEnumerable<string> ids)
        {
        }

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

        public IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, Type targetType)
            => null;

        public Task BuildReverseIndexAsync(ReferenceIndexService indexService, EntityMergeStore store)
            => Task.CompletedTask;

        public List<(Type SourceType, string SourceSubject, string SourceEntityId, string PropName)>
            ResolveReverseRefs(EntityMergeStore store, string targetEntityId)
            => new();

        public void ClearLookupCache()
        {
        }

        public string? LookupEntityId(ReferenceIndexService indexService, string entityType,
            string rawId, string? sourceNs) => null;
    }

    // ── SearchAllTypes routes through IHostService (round22) ──

    [Fact]
    public async Task SearchAllTypes_DelegatesToHostServiceSearch()
    {
        var hostService = new StubHostService();
        hostService.SearchResults.Add(new StubEntity("1", "Stone") { ModId = 0 });
        hostService.SearchResults.Add(new StubEntity("2", "Flint") { ModId = 0 });
        var executor = new McpToolExecutor(new EditorTools(hostService, new StubReferenceResolver(), null!));

        var result = await executor.ExecuteToolAsync("SearchAllTypes", """{"query": "st"}""");

        var parsed = JsonConvert.DeserializeObject<dynamic>(result);
        Assert.NotNull(parsed);
        Assert.Equal(2, (int)parsed!.totalMatches);
        Assert.Equal("Stone", (string)parsed.items[0].subject);
    }

    // ── G2: GenerateImage tool ──

    [Fact]
    public async Task ExecuteTool_GenerateImage_NoServiceRegistered_ReturnsError()
    {
        var executor = new McpToolExecutor(CreateTools());
        var result = await executor.ExecuteToolAsync("GenerateImage",
            """{"entityType": "ItemType", "entityId": "item_weapon_sword"}""");

        Assert.NotNull(result);
        Assert.Contains("error", result, StringComparison.OrdinalIgnoreCase);
    }
}