using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Tests.Data.Repository;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>
/// B5: HostService implements IModManager (R24 — mod writes flow through the unified pipeline).
/// Exercises CreateModAsync / ImportModAsync / DeleteMod / ExportModToZipAsync with in-memory
/// SQLite contexts + a temp game root.
/// </summary>
public class HostServiceModManagerTests : IDisposable
{
    private readonly string _gameRoot = Path.Combine(Path.GetTempPath(), $"neoeditor_modtest_{Guid.NewGuid():N}");
    private readonly SqliteConnection _gameConn = RepositoryTestHelpers.OpenSqlite();
    private readonly SqliteConnection _editorConn = RepositoryTestHelpers.OpenSqlite();

    private readonly IDbContextFactory<GameDbContext> _gameFactory;
    private readonly IDbContextFactory<EditorDbContext> _editorFactory;
    private readonly HostService _host;

    public HostServiceModManagerTests()
    {
        GameDbContext.ReferenceSerializer = new RepositoryTestHelpers.StubReferenceSerializer();

        Directory.CreateDirectory(_gameRoot);

        _gameConn.Open();
        var gameOptions = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(_gameConn).Options;
        using (var db = new GameDbContext(gameOptions)) db.Database.EnsureCreated();
        _gameFactory = new RepositoryTestHelpers.TestDbFactory<GameDbContext>(gameOptions);

        _editorConn.Open();
        var editorOptions = new DbContextOptionsBuilder<EditorDbContext>().UseSqlite(_editorConn).Options;
        using (var edb = new EditorDbContext(editorOptions)) edb.Database.EnsureCreated();
        _editorFactory = new RepositoryTestHelpers.TestDbFactory<EditorDbContext>(editorOptions);

        var config = new RepositoryTestHelpers.StubConfigService(_gameRoot);
        var modManager = new ModManager(_editorFactory, _gameFactory, config,
            new RepositoryTestHelpers.StubXmlParser(),
            new StubBrowserIndexService(),
            new StubNotificationService());

        _host = new HostService(new StubWorkspaceSession(), _gameFactory,
            new RepositoryTestHelpers.StubXmlParser(), config, _editorFactory, modManager);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_gameRoot)) Directory.Delete(_gameRoot, true); } catch { }
        _gameConn.Dispose();
        _editorConn.Dispose();
    }

    [Fact]
    public async Task CreateModAsync_Creates_Dir_And_Db_Entry()
    {
        await _host.CreateModAsync("MyMod", "TestAuthor");

        var modDir = Path.Combine(_gameRoot, "Mods", "TestAuthor", "MyMod");
        Assert.True(Directory.Exists(modDir), "mod directory should be created");
        Assert.True(File.Exists(Path.Combine(modDir, "neogame.xml")));
        Assert.True(File.Exists(Path.Combine(modDir, "getimages.php")));

        await using var edb = await _editorFactory.CreateDbContextAsync();
        var mod = await edb.ModInfos.FirstOrDefaultAsync(m => m.Name == "MyMod");
        Assert.NotNull(mod);
        Assert.True(mod.ModId >= 0);
    }

    [Fact]
    public async Task ImportModAsync_Loads_Entities_From_Xml_And_Registers_Mod()
    {
        var modDir = Path.Combine(_gameRoot, "Mods", "TestMod");
        Directory.CreateDirectory(modDir);
        await File.WriteAllTextAsync(Path.Combine(modDir, "neogame.xml"),
            "<pma_xml_export><database name=\"neogame\"><table id=\"abc\"></table></database></pma_xml_export>");

        var mod = await _host.ImportModAsync(modDir);

        Assert.NotNull(mod);
        Assert.True(mod.ModId >= 0);

        await using var gdb = await _gameFactory.CreateDbContextAsync();
        var count = await gdb.AttackModes.CountAsync(e => e.ModId == mod.ModId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DeleteMod_Removes_Dir_And_Db_Entry()
    {
        await _host.CreateModAsync("MyMod", "Author");

        ModInfo mod;
        await using (var edb = await _editorFactory.CreateDbContextAsync())
            mod = (await edb.ModInfos.FirstOrDefaultAsync(m => m.Name == "MyMod"))!;
        Assert.NotNull(mod);

        await _host.DeleteMod(mod);

        Assert.False(Directory.Exists(Path.Combine(_gameRoot, "Mods", "Author", "MyMod")));
        await using var edb2 = await _editorFactory.CreateDbContextAsync();
        Assert.Null(await edb2.ModInfos.FirstOrDefaultAsync(m => m.Name == "MyMod"));
    }

    [Fact]
    public async Task ExportModToZipAsync_Creates_Zip_File()
    {
        await _host.CreateModAsync("MyMod", "Author");

        ModInfo mod;
        await using (var edb = await _editorFactory.CreateDbContextAsync())
            mod = (await edb.ModInfos.FirstOrDefaultAsync(m => m.Name == "MyMod"))!;

        var zipPath = Path.Combine(_gameRoot, "MyMod.zip");
        await _host.ExportModToZipAsync(mod, zipPath);

        Assert.True(File.Exists(zipPath));
        Assert.True(new FileInfo(zipPath).Length > 0);
    }
}
