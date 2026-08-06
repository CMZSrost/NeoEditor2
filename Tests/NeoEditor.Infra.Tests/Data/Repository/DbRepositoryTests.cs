using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Command;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Repository;
using Xunit;

namespace NeoEditor.Infra.Tests.Data.Repository;

[Collection("GameDbReferenceSerializer")]
public class DbRepositoryTests
{
    private static IDbContextFactory<GameDbContext> CreateGameDb()
    {
        // Static serializer is required for GameDbContext model building (ReferenceList converters).
        GameDbContext.ReferenceSerializer = new RepositoryTestHelpers.StubReferenceSerializer();

        var conn = RepositoryTestHelpers.OpenSqlite();
        conn.Open();
        var options = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(conn).Options;
        using (var db = new GameDbContext(options))
        {
            db.Database.EnsureCreated();
        }

        return new RepositoryTestHelpers.TestDbFactory<GameDbContext>(options);
    }

    private static AttackMode Entity(int id, string name) => new()
    {
        Id = id,
        Name = name,
        ModId = 1,
        FilePath = "neogame.xml",
        EntityId = $"attackmode#1#{id}",
    };

    [Fact]
    public async Task Save_Then_GetById_Returns_Entity()
    {
        var factory = CreateGameDb();
        var repo = new DbRepository<AttackMode>(new StubHostService(), factory);
        var entity = Entity(7, "Slam");

        await repo.SaveAsync([entity]);

        var loaded = await repo.GetByIdAsync(entity.EntityId);
        Assert.NotNull(loaded);
        Assert.Equal(7, loaded!.Id);
        Assert.Equal("Slam", loaded.Name);
        Assert.Equal(1, loaded.ModId);
    }

    [Fact]
    public async Task GetAll_Returns_Persisted_Entities()
    {
        var factory = CreateGameDb();
        var repo = new DbRepository<AttackMode>(new StubHostService(), factory);

        await repo.SaveAsync([Entity(1, "A"), Entity(2, "B")]);

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, e => e.Name == "A");
        Assert.Contains(all, e => e.Name == "B");
    }

    [Fact]
    public async Task GetFieldDiffAsync_Detects_Field_Changes()
    {
        var factory = CreateGameDb();
        var repo = new DbRepository<AttackMode>(new StubHostService(), factory);
        var before = Entity(1, "Old");
        var after = Entity(1, "New");

        var diff = await repo.GetFieldDiffAsync(before, after);

        Assert.Contains(diff, d => d.PropertyName == nameof(AttackMode.Name) && d.NewValue == "New");
    }

    [Fact]
    public async Task GetDiffAsync_RowLevel_Detects_Added_And_Modified()
    {
        var factory = CreateGameDb();
        var repo = new DbRepository<AttackMode>(new StubHostService(), factory);
        var existing = Entity(1, "A");
        await repo.SaveAsync([existing]);

        var diffs = await repo.GetDiffAsync([existing, Entity(2, "B")]);

        Assert.Equal(2, diffs.Count);
        Assert.Contains(diffs, d => d.TargetId == existing.EntityId && d.Kind == DiffKind.Modified);
        Assert.Contains(diffs, d => d.TargetId == "attackmode#1#2" && d.Kind == DiffKind.Added);
    }

    [Fact]
    public async Task LoadAsync_Reads_All_Rows()
    {
        var factory = CreateGameDb();
        var repo = new DbRepository<AttackMode>(new StubHostService(), factory);
        await repo.SaveAsync([Entity(1, "A"), Entity(2, "B")]);

        var all = await repo.LoadAsync();

        Assert.Equal(2, all.Count);
    }

    // ── CRUD 命令门面（R26 v2）──

    [Fact]
    public async Task AddAsync_Dispatches_AddEntityCommand_And_Updates_Cache()
    {
        var host = new StubHostService();
        var repo = new DbRepository<AttackMode>(host, CreateGameDb());
        var entity = Entity(3, "C");

        await repo.AddAsync(entity);

        Assert.IsType<AddEntityCommand>(host.LastCommand);
        Assert.True(host.Cache.ContainsKey(entity.EntityId));
        Assert.Contains(entity.EntityId, host.Dirty);
    }

    [Fact]
    public async Task DeleteAsync_Dispatches_DeleteEntityCommand_And_Removes_Cache()
    {
        var host = new StubHostService();
        host.AddEntityToCache(Entity(1, "A"));
        var repo = new DbRepository<AttackMode>(host, CreateGameDb());

        await repo.DeleteAsync("attackmode#1#1");

        Assert.IsType<DeleteEntityCommand>(host.LastCommand);
        Assert.False(host.Cache.ContainsKey("attackmode#1#1"));
    }
}