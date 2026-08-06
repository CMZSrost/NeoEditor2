using System;
using System.IO;
using System.Linq;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

public class GameTableMapTests
{
    [Theory]
    [InlineData("itemtypes")]
    [InlineData("ingredients")]
    [InlineData("conditions")]
    [InlineData("treasuretable")]
    public void FindsKnownTables(string tableName)
    {
        var type = NeoEditor.Player.Core.Services.GameTableMap.FindType(tableName);
        Assert.NotNull(type);
        Assert.True(typeof(NeoEditor.Data.Model.Game.IEntity).IsAssignableFrom(type));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no_such_table")]
    [InlineData(null)]
    public void UnknownOrEmptyTableReturnsNull(string? tableName)
        => Assert.Null(NeoEditor.Player.Core.Services.GameTableMap.FindType(tableName!));
}

public class ModListScannerTests
{
    [Fact]
    public void EmptyOrMissingDirReturnsEmpty()
    {
        Assert.Empty(NeoEditor.Player.Core.Services.ModListScanner.Scan(TestFs.NewTempDir()));
        Assert.Empty(NeoEditor.Player.Core.Services.ModListScanner.Scan(Path.Combine(TestFs.NewTempDir(), "missing")));
    }

    [Fact]
    public void ScansSubModsWithNeogameXmlOrDataFolder()
    {
        var root = TestFs.NewTempDir();
        var mods = Path.Combine(root, "Mods");
        Directory.CreateDirectory(Path.Combine(mods, "NeoScavExtended", "NSExtended"));
        File.WriteAllText(Path.Combine(mods, "NeoScavExtended", "NSExtended", "neogame.xml"), "<xml/>");
        Directory.CreateDirectory(Path.Combine(mods, "Fishing", "Mod", "data"));
        Directory.CreateDirectory(Path.Combine(mods, "Fishing", "Empty")); // no data → excluded

        var entries = NeoEditor.Player.Core.Services.ModListScanner.Scan(mods);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Name == "NSExtended" && e.Path == "Mods/NeoScavExtended/NSExtended");
        Assert.Contains(entries, e => e.Name == "Mod" && e.Path == "Mods/Fishing/Mod");
    }
}

public class ImageListScannerTests
{
    [Fact]
    public void MissingDirReturnsEmpty()
        => Assert.Empty(NeoEditor.Player.Core.Services.ImageListScanner.ScanPairs(Path.Combine(TestFs.NewTempDir(), "no-img")));

    [Fact]
    public void PairsNormalWithX2Variant()
    {
        var imgDir = Path.Combine(TestFs.NewTempDir(), "img");
        Directory.CreateDirectory(imgDir);
        File.WriteAllBytes(Path.Combine(imgDir, "a.png"), [1]);
        File.WriteAllBytes(Path.Combine(imgDir, "b.png"), [1]);
        File.WriteAllBytes(Path.Combine(imgDir, "x2_a.png"), [1]);

        var pairs = NeoEditor.Player.Core.Services.ImageListScanner.ScanPairs(imgDir);

        Assert.Equal(2, pairs.Count);
        Assert.Contains(pairs, p => p.NormalImage == "a.png" && p.X2Image == "x2_a.png");
        Assert.Contains(pairs, p => p.NormalImage == "b.png" && p.X2Image == "");
    }

    [Fact]
    public void X2OnlyImageIsKeptAsItsOwnEntry()
    {
        var imgDir = Path.Combine(TestFs.NewTempDir(), "img");
        Directory.CreateDirectory(imgDir);
        File.WriteAllBytes(Path.Combine(imgDir, "x2_solo.png"), [1]);

        var pairs = NeoEditor.Player.Core.Services.ImageListScanner.ScanPairs(imgDir);

        Assert.Contains(pairs, p => p.NormalImage == "x2_solo.png" && p.X2Image == "");
    }
}
