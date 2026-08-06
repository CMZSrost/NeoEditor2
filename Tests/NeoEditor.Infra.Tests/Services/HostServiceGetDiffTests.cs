using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Command;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Repository;
using NeoEditor.Infra.Tests.Data.Repository;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>
/// Round 31: HostService.GetDiffAsync produces REAL field-level diffs (Modified / Added /
/// Removed per [Column] property) instead of the former single EntityState placeholder row.
/// </summary>
[Collection("GameDbReferenceSerializer")]
public class HostServiceGetDiffTests
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
    public async Task GetDiffAsync_ModifiedEntity_ReturnsFieldLevelDiffs()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "Blade"));
            await db.SaveChangesAsync();
        }

        var repo = host.Repository<AttackMode>();
        var entity = await repo.GetByIdAsync("am#5#1");
        Assert.NotNull(entity);

        var prop = typeof(AttackMode).GetProperty("Range")!;
        var cmd = new EditCellCommand(entity!, prop, "Range", entity!.Range, 99, () => { });
        await host.ExecuteAsync(cmd, "t");

        var diffs = await host.GetDiffAsync("am#5#1");

        Assert.NotEmpty(diffs);
        // No placeholder EntityState rows anymore.
        Assert.DoesNotContain(diffs, d => d.PropertyName == "EntityState");
        var range = Assert.Single(diffs, d => d.PropertyName == "Range");
        Assert.Equal("1", range.OldValue);
        Assert.Equal("99", range.NewValue);
        Assert.Equal(DiffKind.Modified, range.Kind);
    }

    [Fact]
    public async Task GetDiffAsync_NewEntity_ReturnsAddedDiffs()
    {
        var (host, _) = CreateHost();

        var entity = Entity(99, "BrandNew");
        var cmd = new AddEntityCommand("AttackMode", entity);
        await host.ExecuteAsync(cmd, "t");

        var diffs = await host.GetDiffAsync(entity.EntityId);

        Assert.NotEmpty(diffs);
        Assert.All(diffs, d => Assert.Equal(DiffKind.Added, d.Kind));
        Assert.Contains(diffs, d => d.PropertyName == "Name" && d.NewValue == "BrandNew");
    }

    [Fact]
    public async Task GetDiffAsync_DeletedEntity_ReturnsRemovedDiffs()
    {
        var (host, gameFactory) = CreateHost();
        await using (var db = await gameFactory.CreateDbContextAsync())
        {
            db.Add(Entity(1, "Blade"));
            await db.SaveChangesAsync();
        }

        var repo = host.Repository<AttackMode>();
        var entity = await repo.GetByIdAsync("am#5#1");
        Assert.NotNull(entity);

        var cmd = new DeleteEntityCommand("AttackMode", entity!);
        await host.ExecuteAsync(cmd, "t");

        var diffs = await host.GetDiffAsync("am#5#1");

        Assert.NotEmpty(diffs);
        Assert.All(diffs, d => Assert.Equal(DiffKind.Removed, d.Kind));
        Assert.Contains(diffs, d => d.PropertyName == "Name" && d.OldValue == "Blade");
    }
}
