using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
/// Round 22: IHostService.SearchEntitiesAsync — the cross-type search now owned by
/// HostService so MCP's SearchAllTypes (and CLI/AI consumers) share one implementation.
/// Uses in-memory SQLite contexts that share one open connection per test.
/// </summary>
public class HostServiceSearchTests
{
    private static (HostService Host, IDbContextFactory<GameDbContext> GameFactory) CreateHost()
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
        using (var edb = new EditorDbContext(editorOptions)) edb.Database.EnsureCreated();
        var editorFactory = new RepositoryTestHelpers.TestDbFactory<EditorDbContext>(editorOptions);

        var modManager = new ModManager(editorFactory, gameFactory,
            new RepositoryTestHelpers.StubConfigService(Path.GetTempPath()),
            new RepositoryTestHelpers.StubXmlParser(),
            new StubBrowserIndexService(),
            new StubNotificationService());

        var host = new HostService(new StubWorkspaceSession(), gameFactory,
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

    [Fact]
    public async Task SearchEntitiesAsync_MatchesSubject_CaseInsensitive()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "12口径 独头弹"));
            db.Add(Entity(2, "4毫米高斯步枪 穿甲弹"));
            db.Add(Entity(3, "格挡"));
            await db.SaveChangesAsync();
        }

        var results = await host.SearchEntitiesAsync("口径");

        var entity = Assert.Single(results);
        Assert.Contains("独头弹", entity.Subject);
    }

    [Fact]
    public async Task SearchEntitiesAsync_RespectsLimit()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "Alpha"));
            db.Add(Entity(2, "Alpine"));
            db.Add(Entity(3, "Almond"));
            await db.SaveChangesAsync();
        }

        var results = await host.SearchEntitiesAsync("al", limit: 2);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchEntitiesAsync_BlankQuery_ReturnsEmpty()
    {
        var (host, _) = CreateHost();

        var results = await host.SearchEntitiesAsync("   ");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchEntitiesAsync_NoMatch_ReturnsEmpty()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "Slam"));
            await db.SaveChangesAsync();
        }

        var results = await host.SearchEntitiesAsync("zzzz");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchEntitiesAsync_FiltersByEntityType()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "Blade"));
            db.Add(Entity(2, "Blade2"));
            await db.SaveChangesAsync();
        }

        // Only AttackMode rows were seeded — filtering to ItemType must return nothing.
        var results = await host.SearchEntitiesAsync("Blade", entityType: "ItemType");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchEntitiesAsync_FiltersByModId()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "Blade")); // Entity() factory hard-codes ModId = 5
            db.Add(new AttackMode
            {
                Id = 2,
                Name = "Blade2",
                ModId = 7,
                FilePath = "neogame.xml",
                EntityId = "am#7#2"
            });
            await db.SaveChangesAsync();
        }

        var results = await host.SearchEntitiesAsync("Blade", modId: 7);

        var entity = Assert.Single(results);
        Assert.Equal(7, entity.ModId);
    }
}