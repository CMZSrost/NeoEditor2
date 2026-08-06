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
[Collection("GameDbReferenceSerializer")]
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
            modManager,
            null!);
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

    // ── Round 31: structured search (EntitySearchRequest) ──

    [Fact]
    public async Task SearchEntitiesAsync_Request_MultipleEntityTypes()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "Blade"));
            db.Add(new ItemType
            {
                Id = 100, GroupId = 1, SubgroupId = 1,
                Name = "Blade Sword",
                ModId = 5, FilePath = "mod.xml", EntityId = "item-1-1"
            });
            await db.SaveChangesAsync();
        }

        var result = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "Blade", EntityTypes: new[] { "ItemType", "AttackMode" }));

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, e => e is AttackMode);
        Assert.Contains(result.Items, e => e is ItemType);
    }

    [Fact]
    public async Task SearchEntitiesAsync_Request_StringFilter_WithoutQuery()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "Blade"));
            db.Add(Entity(2, "Sword"));
            await db.SaveChangesAsync();
        }

        // Empty query = pure filter mode (old overload returns empty for blank queries).
        var result = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "", Filters: new[] { new EntityFilter("Name", FilterOperator.StartsWith, "sw") }));

        var entity = Assert.Single(result.Items);
        Assert.Equal("Sword", ((AttackMode)entity).Name);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task SearchEntitiesAsync_Request_NumericFilter()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            var e1 = Entity(1, "Blade"); e1.Range = 1;
            var e2 = Entity(2, "Blade2"); e2.Range = 3;
            var e3 = Entity(3, "Blade3"); e3.Range = 7;
            db.AddRange(e1, e2, e3);
            await db.SaveChangesAsync();
        }

        var result = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "Blade",
            Filters: new[] { new EntityFilter("Range", FilterOperator.GreaterThanOrEqual, "3") }));

        Assert.Equal(2, result.Total);
        Assert.Contains(result.Items, e => ((AttackMode)e).Range == 3);
        Assert.Contains(result.Items, e => ((AttackMode)e).Range == 7);
    }

    [Fact]
    public async Task SearchEntitiesAsync_Request_BoolFilter()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            var e1 = Entity(1, "Blade"); e1.Transfer = true;
            var e2 = Entity(2, "Blade2"); e2.Transfer = false;
            db.AddRange(e1, e2);
            await db.SaveChangesAsync();
        }

        var result = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "Blade",
            Filters: new[] { new EntityFilter("Transfer", FilterOperator.Equals, "true") }));

        var entity = Assert.Single(result.Items);
        Assert.True(((AttackMode)entity).Transfer);
    }

    [Fact]
    public async Task SearchEntitiesAsync_Request_EnumFilter_ByName()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            var e1 = Entity(1, "Blade"); e1.Type = AttackType.Melee;
            var e2 = Entity(2, "Blade2"); e2.Type = AttackType.Ranged;
            db.AddRange(e1, e2);
            await db.SaveChangesAsync();
        }

        var result = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "Blade",
            Filters: new[] { new EntityFilter("Type", FilterOperator.Equals, "Ranged") }));

        var entity = Assert.Single(result.Items);
        Assert.Equal(AttackType.Ranged, ((AttackMode)entity).Type);
    }

    [Fact]
    public async Task SearchEntitiesAsync_Request_AndFilters_Combine()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            var e1 = Entity(1, "Blade"); e1.Range = 5; e1.Transfer = true;
            var e2 = Entity(2, "Blade2"); e2.Range = 1; e2.Transfer = true;
            var e3 = Entity(3, "Blade3"); e3.Range = 5; e3.Transfer = false;
            db.AddRange(e1, e2, e3);
            await db.SaveChangesAsync();
        }

        var result = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "Blade",
            Filters: new[]
            {
                new EntityFilter("Range", FilterOperator.GreaterThanOrEqual, "5"),
                new EntityFilter("Transfer", FilterOperator.Equals, "true")
            }));

        var entity = Assert.Single(result.Items);
        Assert.Equal("Blade", ((AttackMode)entity).Name);
    }

    [Fact]
    public async Task SearchEntitiesAsync_Request_UnknownFilterField_ReturnsEmpty()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "Blade"));
            await db.SaveChangesAsync();
        }

        var result = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "Blade",
            Filters: new[] { new EntityFilter("NoSuchColumn", FilterOperator.Equals, "x") }));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task SearchEntitiesAsync_Request_Pagination_TracksTotalAndTruncation()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.AddRange(Entity(1, "Alpha"), Entity(2, "Alpine"), Entity(3, "Almond"));
            await db.SaveChangesAsync();
        }

        var page1 = await host.SearchEntitiesAsync(new EntitySearchRequest("al", Limit: 2, Offset: 0));
        Assert.Equal(3, page1.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.Truncated);

        var page2 = await host.SearchEntitiesAsync(new EntitySearchRequest("al", Limit: 2, Offset: 2));
        Assert.Equal(3, page2.Total);
        Assert.Single(page2.Items);
        Assert.False(page2.Truncated);
    }

    [Fact]
    public async Task SearchEntitiesAsync_Request_SortBy_NumericColumn()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            var e1 = Entity(1, "Blade"); e1.Range = 3;
            var e2 = Entity(2, "Blade2"); e2.Range = 1;
            var e3 = Entity(3, "Blade3"); e3.Range = 2;
            db.AddRange(e1, e2, e3);
            await db.SaveChangesAsync();
        }

        var asc = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "Blade", SortBy: "Range"));
        Assert.Equal(1, ((AttackMode)asc.Items[0]).Range);
        Assert.Equal(3, ((AttackMode)asc.Items[2]).Range);

        var desc = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "Blade", SortBy: "Range", SortDescending: true));
        Assert.Equal(3, ((AttackMode)desc.Items[0]).Range);
        Assert.Equal(1, ((AttackMode)desc.Items[2]).Range);
    }

    [Fact]
    public async Task SearchEntitiesAsync_Request_SortBy_BaseProperty_Subject()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.AddRange(Entity(1, "Zulu"), Entity(2, "Alpha"));
            await db.SaveChangesAsync();
        }

        // "Subject" lives on IEntity — the base-property fallback must resolve it.
        var result = await host.SearchEntitiesAsync(new EntitySearchRequest(
            Query: "", SortBy: "Subject"));

        Assert.Equal("Alpha", result.Items[0].Subject);
        Assert.Equal("Zulu", result.Items[1].Subject);
    }
}