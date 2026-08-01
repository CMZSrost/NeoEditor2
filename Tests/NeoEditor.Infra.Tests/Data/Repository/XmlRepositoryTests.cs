using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Repository;
using Xunit;

namespace NeoEditor.Infra.Tests.Data.Repository;

public class XmlRepositoryTests
{
    private readonly string _gameRoot = Path.Combine(Path.GetTempPath(), $"neoeditor_xmltest_{Guid.NewGuid():N}");

    private XmlRepository<AttackMode> CreateRepo(out string modRelPath)
    {
        modRelPath = $"Mods/TestMod_{Guid.NewGuid():N}";

        var conn = RepositoryTestHelpers.OpenSqlite();
        conn.Open();
        var options = new DbContextOptionsBuilder<EditorDbContext>().UseSqlite(conn).Options;
        using (var db = new EditorDbContext(options))
        {
            db.Database.EnsureCreated();
            db.ModInfos.Add(new ModInfo { ModId = 5, Name = "TestMod", Path = modRelPath });
            db.SaveChanges();
        }

        return new XmlRepository<AttackMode>(
            new StubHostService(),
            5,
            new RepositoryTestHelpers.StubXmlParser(),
            new RepositoryTestHelpers.StubConfigService(_gameRoot),
            new RepositoryTestHelpers.TestDbFactory<EditorDbContext>(options));
    }

    private static AttackMode Entity() => new()
    {
        Id = 1,
        Name = "Slam",
        ModId = 5,
        FilePath = null,
        EntityId = "abc",
    };

    [Fact]
    public async Task LoadAsync_Imports_Entities_From_Bound_Mod()
    {
        var repo = CreateRepo(out var modRelPath);
        var xmlPath = Path.Combine(_gameRoot, modRelPath, "neogame.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
        await File.WriteAllTextAsync(xmlPath,
            "<pma_xml_export><database name=\"neogame\"><table id=\"abc\"></table></database></pma_xml_export>");

        var entities = await repo.LoadAsync();

        var entity = Assert.Single(entities);
        Assert.Equal("abc", entity.EntityId);
        Assert.Equal(5, entity.ModId);
        Assert.Equal(Path.GetFullPath(xmlPath), entity.FilePath);
    }

    [Fact]
    public async Task GetDiffAsync_RowLevel_Returns_NewFile_Plan()
    {
        var repo = CreateRepo(out var modRelPath);
        var entity = Entity();

        var diffs = await repo.GetDiffAsync([entity]);

        var diff = Assert.Single(diffs);
        Assert.Equal(DiffKind.Added, diff.Kind);
        Assert.Equal(Path.GetFullPath(Path.Combine(_gameRoot, modRelPath, "neogame.xml")), diff.TargetId);
        Assert.Equal("<!-- new file -->", diff.OldContent);
        Assert.Contains("abc", diff.NewContent);
    }

    [Fact]
    public async Task SaveAsync_Writes_Xml_File()
    {
        var repo = CreateRepo(out var modRelPath);
        var entity = Entity();

        await repo.SaveAsync([entity]);

        var fullPath = Path.Combine(_gameRoot, modRelPath, "neogame.xml");
        Assert.True(File.Exists(fullPath));
        var content = await File.ReadAllTextAsync(fullPath);
        Assert.Contains("abc", content);
    }

    [Fact]
    public async Task GetDiffAsync_RowLevel_Returns_Empty_When_Unchanged()
    {
        var repo = CreateRepo(out _);
        var entity = Entity();

        await repo.SaveAsync([entity]);
        var diffs = await repo.GetDiffAsync([entity]);

        Assert.Empty(diffs);
    }
}