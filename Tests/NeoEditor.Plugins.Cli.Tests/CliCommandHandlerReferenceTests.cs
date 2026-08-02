using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.Cli.Cli;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Plugins.Cli.Tests;

/// <summary>
/// R30: CLI output must serialize reference columns as their raw XML text ("3,14"),
/// not the damaged "[3, 14]" ReferenceList.ToString() format.
/// </summary>
public class CliCommandHandlerReferenceTests
{
    [Fact]
    public async Task ShowEntity_ReferenceColumns_SerializeRawText_NotBrokenBrackets()
    {
        var creature = new Creature { EntityId = "c1" };
        creature.EncounterIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
            new PureRefFormat { Entity = new EntityRef { Id = "14" } },
        };

        var handler = new CliCommandHandler(new StubHostService(creature), null!, new CliOutputFormatter());
        var cmd = new CliParsedCommand
        {
            Command = CliCommandType.GetEntity,
            EntityType = "Creature", // Constants.GameTypes is keyed by class name
            EntityId = "c1",
            Format = "json",
        };

        var result = await handler.ExecuteAsync(cmd);

        Assert.Contains("\"3,14\"", result);
        Assert.DoesNotContain("[3, 14]", result);
    }

    [Fact]
    public async Task ResolveReferences_UsesRawValue_NotBrokenBrackets()
    {
        // The rawValue echoed back by `refs` must be "3,14" — a bracketed value would
        // also break per-segment extraction downstream.
        var creature = new Creature { EntityId = "c1" };
        creature.EncounterIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
            new PureRefFormat { Entity = new EntityRef { Id = "14" } },
        };

        var handler = new CliCommandHandler(new StubHostService(creature), new StubReferenceResolver(), new CliOutputFormatter());
        var cmd = new CliParsedCommand
        {
            Command = CliCommandType.QueryReferences,
            EntityType = "Creature",
            EntityId = "c1",
            PropertyName = "EncounterIds",
            Format = "json",
        };

        var result = await handler.ExecuteAsync(cmd);

        Assert.Contains("\"3,14\"", result);
        Assert.DoesNotContain("[3, 14]", result);
    }

    // ── Stubs ──────────────────────────────────────────────────────────────

    private sealed class StubReferenceResolver : IReferenceResolver
    {
        public T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : IEntity
            => null;

        public string? LookupSubject(string sourceEntityId, string propertyName, Type targetType, string rawId,
            Type? secondaryTargetType = null)
            => null;

        public IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)>
            ReverseLookup(EntityMergeStore store, string targetEntityId) => [];

        public IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, Type targetType) => null;

        public Task BuildReverseIndexAsync(ReferenceIndexService indexService, EntityMergeStore store)
            => Task.CompletedTask;

        public List<(Type SourceType, string SourceSubject, string SourceEntityId, string PropName)>
            ResolveReverseRefs(EntityMergeStore store, string targetEntityId) => [];

        public void ClearLookupCache()
        {
        }

        public string? LookupEntityId(ReferenceIndexService indexService, string entityType,
            string rawId, string? sourceNs = null)
            => null;
    }

    private sealed class StubHostService : IHostService
    {
        private readonly IEntity _entity;

        public StubHostService(IEntity entity) => _entity = entity;

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
            => new StubRepository<T>(_entity);

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

    private sealed class StubRepository<T> : IEntityRepository<T> where T : IEntity
    {
        private readonly IEntity _entity;

        public StubRepository(IEntity entity) => _entity = entity;

        public IReadOnlyCollection<string> DirtyIds => [];
        public Task AddAsync(T entity) => Task.CompletedTask;
        public Task UpdateAsync(T entity) => Task.CompletedTask;
        public Task DeleteAsync(string entityId) => Task.CompletedTask;
        public Task<IReadOnlyList<RowDiff>> GetDiffAsync(IReadOnlyList<T> candidates)
            => Task.FromResult<IReadOnlyList<RowDiff>>([]);
        public Task<IReadOnlyList<DiffEntry>> GetFieldDiffAsync(T before, T after)
            => Task.FromResult<IReadOnlyList<DiffEntry>>([]);
        public void MarkDirty(IEnumerable<string> ids)
        {
        }

        public void ClearDirty(IEnumerable<string> ids)
        {
        }

        public Task SaveAsync(IEnumerable<T> entities) => Task.CompletedTask;
        public Task<IReadOnlyList<T>> LoadAsync()
            => Task.FromResult<IReadOnlyList<T>>([(T)_entity]);
        public Task<T?> GetByIdAsync(string entityId) => Task.FromResult((T)_entity);
        public Task<IReadOnlyList<T>> GetAllAsync()
            => Task.FromResult<IReadOnlyList<T>>([(T)_entity]);
    }
}
