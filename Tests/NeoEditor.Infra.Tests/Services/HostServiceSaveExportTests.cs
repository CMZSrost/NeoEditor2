using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Command;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Core.Model;
using NeoEditor.Data.Repository;
using NeoEditor.Infra.Tests.Data.Repository;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>
/// B2: IHostService three actions (Save / Export / Publish) + R25 hooks (PreSave / PreExport).
/// Uses in-memory SQLite contexts that share one open connection per test.
/// </summary>
[Collection("GameDbReferenceSerializer")]
public class HostServiceSaveExportTests
{
    private sealed class Hook(Queue<string> calls)
        : IExtensionPoint<PreSaveContext>, IExtensionPoint<PreExportContext>
    {
        public string Name => "test";
        public int Order => 0;

        public Task ExecuteAsync(PreSaveContext ctx)
        {
            calls.Enqueue($"save:{string.Join(",", ctx.EntityIds)}");
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(PreExportContext ctx)
        {
            calls.Enqueue($"export:{string.Join(",", ctx.EntityIds)}");
            return Task.CompletedTask;
        }
    }

    private static (HostService Host, IDbContextFactory<GameDbContext> GameFactory, IDbContextFactory<EditorDbContext> EditorFactory) CreateHost(
        StubWorkspaceSession session)
    {
        GameDbContext.ReferenceSerializer = new RepositoryTestHelpers.StubReferenceSerializer();

        var gameConn = RepositoryTestHelpers.OpenSqlite();
        gameConn.Open();
        var gameOptions = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(gameConn).Options;
        using (var db = new GameDbContext(gameOptions)) db.Database.EnsureCreated();
        var gameFactory = new RepositoryTestHelpers.TestDbFactory<GameDbContext>(gameOptions);

        var editorConn = RepositoryTestHelpers.OpenSqlite();
        editorConn.Open();
        var editorOptions = new DbContextOptionsBuilder<EditorDbContext>().UseSqlite(editorConn).Options;
        using (var edb = new EditorDbContext(editorOptions))
        {
            edb.Database.EnsureCreated();
            edb.ModInfos.Add(new ModInfo { ModId = 5, Name = "TestMod", Path = "Mods/TestMod" });
            edb.SaveChanges();
        }

        var editorFactory = new RepositoryTestHelpers.TestDbFactory<EditorDbContext>(editorOptions);

        // B5: HostService now takes the Infra ModManager it delegates to for mod management.
        var modManager = new ModManager(editorFactory, gameFactory,
            new RepositoryTestHelpers.StubConfigService(Path.GetTempPath()),
            new RepositoryTestHelpers.StubXmlParser(),
            new StubBrowserIndexService(),
            new StubNotificationService());

        // 追修(C): the overlay persistence is exercised by the save tests below.
        var persistence = new WorkspacePersistenceService(editorFactory, gameFactory);

        var host = new HostService(session, gameFactory,
            new RepositoryTestHelpers.StubXmlParser(),
            new RepositoryTestHelpers.StubConfigService(Path.GetTempPath()),
            editorFactory,
            modManager,
            persistence);
        return (host, gameFactory, editorFactory);
    }

    private static AttackMode Entity(int id, string name) => new()
    {
        Id = id,
        Name = name,
        ModId = 5,
        FilePath = "neogame.xml",
        EntityId = $"am#5#{id}",
    };

    // ── Save 动作 ──

    [Fact]
    public async Task SaveAsync_Persists_Entity_And_Clears_Dirty()
    {
        var session = new StubWorkspaceSession();
        var (host, _, _) = CreateHost(session);
        var entity = Entity(1, "Slam");
        host.AddEntityToCache(entity);
        session.MarkEntityDirty(entity.EntityId);

        var result = await host.SaveAsync(entity.EntityId);

        Assert.Contains(entity.EntityId, result.SavedEntityIds);
        Assert.DoesNotContain(entity.EntityId, session.DirtyEntities);
    }

    [Fact]
    public async Task SaveAllAsync_Persists_All_Dirty_And_Clears_Dirty()
    {
        var session = new StubWorkspaceSession();
        var (host, _, _) = CreateHost(session);
        var e1 = Entity(1, "A");
        var e2 = Entity(2, "B");
        host.AddEntityToCache(e1);
        host.AddEntityToCache(e2);
        session.MarkEntitiesDirty([e1.EntityId, e2.EntityId]);

        var result = await host.SaveAllAsync();

        Assert.Equal(2, result.SavedEntityIds.Count);
        Assert.NotEmpty(result.PartialDiff);
        Assert.Empty(session.DirtyEntities);
    }

    [Fact]
    public async Task SaveAsync_NoOp_When_Not_Dirty()
    {
        var (host, _, _) = CreateHost(new StubWorkspaceSession());
        var result = await host.SaveAsync("nonexistent");
        Assert.Empty(result.SavedEntityIds);
    }

    // ── R30 (追修 6): edit commands must seed the working-set cache ──

    [Fact]
    public async Task Execute_BatchEditCommand_UpsertsEntityIntoCache_And_SaveAll_Persists()
    {
        // Regression: BatchEditCommand carried no cache delta → the edited entity never
        // entered _entityCache → SaveAllAsync dropped it (empty save, "No mod entities to
        // save") → WAL never cleared → replay on every restart (dirty-on-open).
        var session = new StubWorkspaceSession();
        var (host, _, _) = CreateHost(session);
        var entity = Entity(1, "Slam");
        var prop = typeof(AttackMode).GetProperty(nameof(AttackMode.Name))!;
        var cmd = new BatchEditCommand(
            [new EditRecord(entity, prop, "strName", "Slam", "Slam2")], () => { });

        await host.ExecuteAsync(cmd);

        Assert.Same(entity, host.GetCachedEntity(entity.EntityId));

        var result = await host.SaveAllAsync();
        Assert.Contains(entity.EntityId, result.SavedEntityIds);
        Assert.Empty(session.DirtyEntities);
    }

    [Fact]
    public async Task Execute_EditCellCommand_UpsertsEntityIntoCache_And_SaveAll_Persists()
    {
        var session = new StubWorkspaceSession();
        var (host, _, _) = CreateHost(session);
        var entity = Entity(2, "Bash");
        var prop = typeof(AttackMode).GetProperty(nameof(AttackMode.Name))!;
        var cmd = new EditCellCommand(entity, prop, "strName", "Bash", "Bash2", () => { });

        await host.ExecuteAsync(cmd);

        Assert.Same(entity, host.GetCachedEntity(entity.EntityId));

        var result = await host.SaveAllAsync();
        Assert.Contains(entity.EntityId, result.SavedEntityIds);
    }

    [Fact]
    public async Task SaveAll_Drops_DirtyEntity_Missing_From_Cache_Without_Saving()
    {
        // Documents the guard behavior: a dirty id absent from the cache is dropped
        // (with a warning) instead of throwing — SaveAllAsync must not crash.
        var session = new StubWorkspaceSession();
        var (host, _, _) = CreateHost(session);
        session.MarkEntityDirty("ghost-id");

        var result = await host.SaveAllAsync();

        Assert.Empty(result.SavedEntityIds);
        Assert.Empty(session.DirtyEntities);
    }

    // ── 追修(C): save writes the per-profile EDIT OVERLAY, never the shared entity tables ──

    [Fact]
    public async Task SaveAll_NewEntity_WritesIsNewOverlay_NotEntityTable()
    {
        var session = new StubWorkspaceSession();
        var (host, gameFactory, editorFactory) = CreateHost(session);
        session.CurrentProfileId = 5; // HostService ctor restores the config value — override after.
        var entity = Entity(1, "Slam");
        host.AddEntityToCache(entity);
        session.MarkEntityDirty(entity.EntityId);

        var result = await host.SaveAllAsync();

        Assert.Contains(entity.EntityId, result.SavedEntityIds);

        // The entity table stays untouched (baseline)…
        await using var gameDb = await gameFactory.CreateDbContextAsync();
        Assert.Null(await gameDb.AttackModes.FirstOrDefaultAsync(a => a.EntityId == entity.EntityId));

        // …while the overlay holds an IsNew marker + full column values.
        await using var edb = await editorFactory.CreateDbContextAsync();
        var edits = await edb.ProfileEdits.Where(p => p.ProfileId == 5).ToListAsync();
        Assert.Contains(edits, e => e.EntityId == entity.EntityId && e.IsNew);
        Assert.Contains(edits, e => e.EntityId == entity.EntityId && e.ColumnName == "strName"
                                     && e.RawValue == "Slam");
    }

    [Fact]
    public async Task SaveAll_ExistingEntity_WritesOnlyChangedColumns()
    {
        var session = new StubWorkspaceSession();
        var (host, gameFactory, editorFactory) = CreateHost(session);
        session.CurrentProfileId = 5; // HostService ctor restores the config value — override after.

        // Baseline row already in the entity table (import/export state).
        await using (var seed = await gameFactory.CreateDbContextAsync())
        {
            seed.AttackModes.Add(Entity(1, "Slam"));
            await seed.SaveChangesAsync();
        }

        var entity = Entity(1, "Slam2"); // only strName changed
        entity.DamageBlunt = 1.5;        // second changed column
        host.AddEntityToCache(entity);
        session.MarkEntityDirty(entity.EntityId);

        await host.SaveAllAsync();

        await using var edb = await editorFactory.CreateDbContextAsync();
        var edits = await edb.ProfileEdits.Where(p => p.ProfileId == 5 && p.EntityId == entity.EntityId)
            .ToListAsync();
        var cols = edits.Where(e => e.ColumnName is not null).Select(e => e.ColumnName).ToHashSet();
        Assert.Contains("strName", cols);
        Assert.Contains("fDamageBlunt", cols);
        Assert.DoesNotContain("strNotes", cols); // unchanged → no overlay row
    }

    [Fact]
    public async Task SaveAll_DeletedEntity_WritesIsDeletedOverlay()
    {
        var session = new StubWorkspaceSession();
        var (host, _, editorFactory) = CreateHost(session);
        session.CurrentProfileId = 5; // HostService ctor restores the config value — override after.
        var entity = Entity(1, "Slam");

        var delCmd = new DeleteEntityCommand("AttackMode", entity,
            e => host.RemoveEntityFromCache(e.EntityId), e => { });
        await host.ExecuteAsync(delCmd);

        await host.SaveAllAsync();

        await using var edb = await editorFactory.CreateDbContextAsync();
        var edits = await edb.ProfileEdits.Where(p => p.ProfileId == 5).ToListAsync();
        Assert.Contains(edits, e => e.EntityId == entity.EntityId && e.IsDeleted
                                     && e.EntityType == "AttackMode");
    }

    [Fact]
    public async Task DiscardAsync_ClearsOverlay_And_Dirty()
    {
        var session = new StubWorkspaceSession();
        var (host, _, editorFactory) = CreateHost(session);
        session.CurrentProfileId = 5; // HostService ctor restores the config value — override after.
        var entity = Entity(1, "Slam");
        host.AddEntityToCache(entity);
        session.MarkEntityDirty(entity.EntityId);
        await host.SaveAllAsync(); // overlay now holds the edit

        await host.DiscardAsync(entity.EntityId);

        Assert.Empty(session.DirtyEntities);
        await using var edb = await editorFactory.CreateDbContextAsync();
        Assert.Empty(await edb.ProfileEdits.Where(p => p.ProfileId == 5).ToListAsync());
    }

    [Fact]
    public async Task PreSaveHook_Fires_Before_Save()
    {
        var session = new StubWorkspaceSession();
        var (host, _, _) = CreateHost(session);
        var calls = new Queue<string>();
        host.RegisterPreSaveHook(new Hook(calls));
        var entity = Entity(1, "Slam");
        host.AddEntityToCache(entity);
        session.MarkEntityDirty(entity.EntityId);

        await host.SaveAsync(entity.EntityId);

        Assert.Contains(calls, c => c.StartsWith("save:"));
    }

    // ── Export 动作 ──

    [Fact]
    public async Task ExportModAsync_Returns_ExportResult_With_Diff()
    {
        var (host, gameFactory, _) = CreateHost(new StubWorkspaceSession());
        var repo = new DbRepository<AttackMode>(host, gameFactory);
        await repo.SaveAsync([Entity(1, "Slam")]);

        var results = await host.ExportModAsync(5);

        var export = Assert.Single(results);
        Assert.Equal(5, export.ModId);
        Assert.NotEmpty(export.Files);
        Assert.Contains(export.Files, f => f.TargetId.EndsWith("neogame.xml"));
    }

    [Fact]
    public async Task ExportModAsync_NoEntities_Returns_Empty()
    {
        var (host, _, _) = CreateHost(new StubWorkspaceSession());
        var results = await host.ExportModAsync(99);
        Assert.Empty(results);
    }

    [Fact]
    public async Task PreExportHook_Fires_Before_Export()
    {
        var (host, gameFactory, _) = CreateHost(new StubWorkspaceSession());
        var calls = new Queue<string>();
        host.RegisterPreExportHook(new Hook(calls));
        var repo = new DbRepository<AttackMode>(host, gameFactory);
        await repo.SaveAsync([Entity(1, "Slam")]);

        await host.ExportModAsync(5);

        Assert.Contains(calls, c => c.StartsWith("export:"));
    }

    [Fact]
    public async Task CommitExportAsync_Writes_Confirmed_Xml_Files()
    {
        var (host, _, _) = CreateHost(new StubWorkspaceSession());
        var filePath = Path.Combine(Path.GetTempPath(), $"neoeditor-export-{Guid.NewGuid():N}.xml");
        try
        {
            await host.CommitExportAsync([new RowDiff(filePath, DiffKind.Modified, "old", "<neogame/>")]);

            Assert.True(File.Exists(filePath));
            Assert.Equal("<neogame/>", await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    // ── Publish 动作 ──

    [Fact]
    public async Task PublishAsync_Returns_Save_And_Exports()
    {
        var session = new StubWorkspaceSession();
        var (host, _, _) = CreateHost(session);
        var entity = Entity(1, "Slam");
        host.AddEntityToCache(entity);
        session.MarkEntityDirty(entity.EntityId);

        var result = await host.PublishAsync();

        Assert.Contains(entity.EntityId, result.Save.SavedEntityIds);
    }
}
