using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Command;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Repository;
using NeoEditor.Infra.Tests.Data.Repository;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>
/// Round 31 regression: HostService.ExecuteAsync must run a command exactly ONCE.
/// Previously CommandHistory.Execute re-ran command.Execute() after HostService had
/// already executed it, so collection callbacks fired twice.
/// </summary>
[Collection("GameDbReferenceSerializer")]
public class HostServiceCommandTests
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

    [Fact]
    public async Task ExecuteAsync_RunsCollectionCallbackOnce_UndoRedoConsistent()
    {
        var (host, _) = CreateHost();
        var collection = new ObservableCollection<object>();
        var entity = new AttackMode
        {
            Id = 99, Name = "Test", ModId = 5,
            FilePath = "neogame.xml", EntityId = "am#5#99"
        };
        var cmd = new AddEntityCommand("AttackMode", entity,
            e => collection.Add(e),
            e => collection.Remove(e));

        // Execute: the add callback must fire exactly once (double-execution bug regression).
        await host.ExecuteAsync(cmd, "t");
        Assert.Single(collection);

        // Redo re-adds — each exactly once.
        await host.RedoAsync("t");
        Assert.Single(collection);

        // Undo marks the entity dirty again (the revert still needs saving).
        await host.UndoAsync("t");
        Assert.Contains("am#5#99", host.DirtyEntities);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutScope_ExecutesOnce()
    {
        var (host, _) = CreateHost();
        var collection = new ObservableCollection<object>();
        var entity = new AttackMode
        {
            Id = 98, Name = "Test", ModId = 5,
            FilePath = "neogame.xml", EntityId = "am#5#98"
        };
        var cmd = new AddEntityCommand("AttackMode", entity,
            e => collection.Add(e),
            e => collection.Remove(e));

        // No scope registered for this id → HostService executes manually, still once.
        await host.ExecuteAsync(cmd, "no-such-scope");
        Assert.Single(collection);
    }
}
