using System;
using System.IO;
using System.Linq;
using NeoEditor.Player.Core.Data;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

public class DataBrowserServiceTests : IDisposable
{
    private readonly string _gameRoot;
    private readonly FakeConfigService _config = new();
    private readonly DataBrowserService _service;

    public DataBrowserServiceTests()
    {
        _gameRoot = Path.Combine(Path.GetTempPath(), "wv-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_gameRoot);
        _config.Config.GameRootDir = _gameRoot;
        _service = new DataBrowserService(_config);
    }

    public void Dispose()
    {
        try { Directory.Delete(_gameRoot, recursive: true); } catch (IOException) { }
    }

    private void WriteData(string relative, string content)
    {
        var path = Path.Combine(_gameRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private const string Pma = """
        <?xml version="1.0" encoding="utf-8"?>
        <pma_xml_export version="1.0"><database name="neogame">
        {0}
        </database></pma_xml_export>
        """;

    private static string Row(string table, string id, string name)
        => $"<table name=\"{table}\"><column name=\"nID\">{id}</column><column name=\"strName\">{name}</column></table>";

    [Fact]
    public void MergesBaseThenModOverridesByNid()
    {
        // base data table
        WriteData("data/ingredients.xml", string.Format(Pma,
            Row("ingredients", "1", "Water") + Row("ingredients", "2", "Berries")));
        // mod overrides id=1, adds id=3
        WriteData("Mods/NSE/NSExtended/neogame.xml", string.Format(Pma,
            Row("ingredients", "1", "Clean Water") + Row("ingredients", "3", "Mushroom")));

        var catalog = _service.BuildCatalog();

        Assert.Contains("ingredients", catalog.TableNames);
        var rows = catalog.GetRows("ingredients");
        Assert.Equal(3, rows.Count);                       // 2 base + 1 new mod row
        Assert.Contains(rows, r => r.Summary.Contains("Clean Water"));   // mod won
        Assert.Contains(rows, r => r.Summary.Contains("Mushroom"));      // mod added
        Assert.DoesNotContain(rows, r => r.Summary.Contains("Water |")); // base overwritten
    }

    [Fact]
    public void EmptyOrMissingRootReturnsEmptyCatalog()
    {
        _config.Config.GameRootDir = Path.Combine(_gameRoot, "nope");
        var catalog = _service.BuildCatalog();
        Assert.Empty(catalog.TableNames);
        Assert.Equal(0, catalog.TotalRows);
    }

    [Fact]
    public void MalformedFilesAreSkipped()
    {
        WriteData("data/broken.xml", "<<<not xml");
        WriteData("data/ok.xml", string.Format(Pma, Row("gamevars", "1", "seed")));

        var catalog = _service.BuildCatalog();

        Assert.Contains("gamevars", catalog.TableNames);
        Assert.Equal(1, catalog.GetRows("gamevars").Count);
    }

    [Fact]
    public void KnownEntityTablesSortFirst()
    {
        WriteData("data/zzz_extra.xml", string.Format(Pma, Row("zzz_extra", "1", "x")));
        WriteData("data/itemtypes.xml", string.Format(Pma, Row("itemtypes", "5", "Knife")));

        var catalog = _service.BuildCatalog();

        // itemtypes is a known entity table → sorts before the extra table
        Assert.Equal("itemtypes", catalog.TableNames[0]);
        Assert.Equal("zzz_extra", catalog.TableNames[^1]);
    }

    [Fact]
    public void RowWithoutNidFallsBackToFirstField()
    {
        WriteData("data/gamevars.xml", string.Format(Pma,
            "<table name=\"gamevars\"><column name=\"strName\">foo</column><column name=\"fValue\">1</column></table>" +
            "<table name=\"gamevars\"><column name=\"strName\">foo</column><column name=\"fValue\">2</column></table>"));

        var catalog = _service.BuildCatalog();

        var rows = catalog.GetRows("gamevars");
        Assert.Single(rows);                       // same first-field key → later wins
        Assert.Contains("fValue:2", rows[0].Summary);
    }

    [Fact]
    public void LoadsFilesWithNonStandardUtf8Declaration()
    {
        // The game's real files declare encoding='utf8' (non-standard) — XDocument.Load
        // throws on that, so parsing must go through the decoded string (v2.18 regression).
        WriteData("data/ingredients.xml", """
            <?xml version='1.0' encoding='utf8'?>
            <pma_xml_export version="1.0"><database name="neogame">
            <table name="ingredients"><column name="nID">1</column><column name="strName">火源</column></table>
            <table name="ingredients"><column name="nID">2</column><column name="strName">引火物</column></table>
            </database></pma_xml_export>
            """);

        var catalog = _service.BuildCatalog();

        var rows = catalog.GetRows("ingredients");
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Summary.Contains("火源"));
        Assert.Contains(rows, r => r.Summary.Contains("引火物"));
    }

    [Fact]
    public void ModOverlayFollowsGetModsPhpOrder()
    {
        // getmods.php lists mod B BEFORE mod A → B loads first, A loads later → A wins.
        File.WriteAllText(Path.Combine(_gameRoot, "getmods.php"),
            "nRows=2\n&strModName0=B&strModURL0=Mods/X/B\n&strModName1=A&strModURL1=Mods/X/A");
        WriteData("data/ingredients.xml", string.Format(Pma, Row("ingredients", "1", "base")));
        WriteData("Mods/X/B/neogame.xml", string.Format(Pma, Row("ingredients", "1", "from B")));
        WriteData("Mods/X/A/neogame.xml", string.Format(Pma, Row("ingredients", "1", "from A")));

        var catalog = _service.BuildCatalog();

        var rows = catalog.GetRows("ingredients");
        Assert.Single(rows);
        Assert.Contains("from A", rows[0].Summary);   // later in php order wins
    }

    [Fact]
    public void UnlistedModDirsLoadAfterPhpOrder()
    {
        File.WriteAllText(Path.Combine(_gameRoot, "getmods.php"),
            "nRows=1\n&strModName0=A&strModURL0=Mods/X/A");
        WriteData("data/ingredients.xml", string.Format(Pma, Row("ingredients", "1", "base")));
        WriteData("Mods/X/A/neogame.xml", string.Format(Pma, Row("ingredients", "1", "from A")));
        // Not in getmods.php — must still load (appended after the php order).
        WriteData("Mods/EditorNew/Mod/neogame.xml", string.Format(Pma, Row("ingredients", "1", "new mod")));

        var catalog = _service.BuildCatalog();

        var rows = catalog.GetRows("ingredients");
        Assert.Single(rows);
        Assert.Contains("new mod", rows[0].Summary);   // unlisted loads last → wins
    }
}
