using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Repository;
using NeoEditor.Infra.Tests.Data.Repository;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>
/// B2: IHostService three actions (Save / Export / Publish) + R25 hooks (PreSave / PreExport).
/// Uses in-memory SQLite contexts that share one open connection per test.
/// </summary>
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

    private static (HostService Host, IDbContextFactory<GameDbContext> GameFactory) CreateHost(
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

        var host = new HostService(session, gameFactory,
            new RepositoryTestHelpers.StubXmlParser(),
            new RepositoryTestHelpers.StubConfigService(Path.GetTempPath()),
            editorFactory,
            modManager);
        return (host, gameFactory);
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
        var (host, _) = CreateHost(session);
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
        var (host, _) = CreateHost(session);
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
        var (host, _) = CreateHost(new StubWorkspaceSession());
        var result = await host.SaveAsync("nonexistent");
        Assert.Empty(result.SavedEntityIds);
    }

    [Fact]
    public async Task PreSaveHook_Fires_Before_Save()
    {
        var session = new StubWorkspaceSession();
        var (host, _) = CreateHost(session);
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
        var (host, gameFactory) = CreateHost(new StubWorkspaceSession());
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
        var (host, _) = CreateHost(new StubWorkspaceSession());
        var results = await host.ExportModAsync(99);
        Assert.Empty(results);
    }

    [Fact]
    public async Task PreExportHook_Fires_Before_Export()
    {
        var (host, gameFactory) = CreateHost(new StubWorkspaceSession());
        var calls = new Queue<string>();
        host.RegisterPreExportHook(new Hook(calls));
        var repo = new DbRepository<AttackMode>(host, gameFactory);
        await repo.SaveAsync([Entity(1, "Slam")]);

        await host.ExportModAsync(5);

        Assert.Contains(calls, c => c.StartsWith("export:"));
    }

    // ── Publish 动作 ──

    [Fact]
    public async Task PublishAsync_Returns_Save_And_Exports()
    {
        var session = new StubWorkspaceSession();
        var (host, _) = CreateHost(session);
        var entity = Entity(1, "Slam");
        host.AddEntityToCache(entity);
        session.MarkEntityDirty(entity.EntityId);

        var result = await host.PublishAsync();

        Assert.Contains(entity.EntityId, result.Save.SavedEntityIds);
        Assert.Empty(session.DirtyEntities);
    }
}