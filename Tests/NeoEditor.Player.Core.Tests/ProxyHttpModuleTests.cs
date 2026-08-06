using System;
using System.IO;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

/// <summary>Minimal IGamePhpGenerator fake mirroring the real query-string format.</summary>
internal sealed class FakePhpGenerator : IGamePhpGenerator
{
    public string GenerateModsPhp(System.Collections.Generic.IReadOnlyList<(string Name, string Path)> mods)
    {
        var sb = new System.Text.StringBuilder($"nRows={mods.Count}");
        for (var i = 0; i < mods.Count; i++)
            sb.Append($"&strModName{i}={mods[i].Name}&strModURL{i}={mods[i].Path}");
        return sb.ToString();
    }

    public string GenerateImagePhp(System.Collections.Generic.IReadOnlyList<(string NormalImage, string X2Image)> imagePairs)
    {
        // Mirrors PhpParser.GenerateImagePhp: nRows = flattened non-empty names (normal then x2).
        var flat = new System.Collections.Generic.List<string>();
        foreach (var (normal, x2) in imagePairs)
        {
            if (!string.IsNullOrWhiteSpace(normal)) flat.Add(normal.Trim());
            if (!string.IsNullOrWhiteSpace(x2)) flat.Add(x2.Trim());
        }

        var sb = new System.Text.StringBuilder($"nRows={flat.Count}&nCols=2");
        for (var i = 0; i < flat.Count; i++)
            sb.Append($"&strImageURL{i}={flat[i]}");
        return sb.ToString();
    }
}

public class ProxyHttpModuleTests
{
    private static (NeoEditor.Player.Core.Services.ProxyHttpModule Module, FakeConfigService Config, FakeGameDataExportService Data)
        CreateModule(string gameRoot)
    {
        var config = new FakeConfigService { };
        config.Config.GameRootDir = gameRoot;
        var data = new FakeGameDataExportService();
        var module = new NeoEditor.Player.Core.Services.ProxyHttpModule(config, new FakePhpGenerator(), data);
        return (module, config, data);
    }

    [Fact]
    public async Task DataXmlServedFromLiveExport()
    {
        var root = TestFs.NewTempDir();
        var (module, _, data) = CreateModule(root);
        data.Add("itemtypes", "<database name=\"neogame\"><table name=\"itemtypes\"/></database>");

        var response = await module.TryServeAsync("data/itemtypes.xml");

        Assert.NotNull(response);
        Assert.Equal(200, response.StatusCode);
        Assert.StartsWith("text/xml", response.ContentType);
        Assert.Contains("table name=\"itemtypes\"",
            System.Text.Encoding.UTF8.GetString(response.Body));
    }

    [Fact]
    public async Task UnknownTableFallsThroughToDisk()
    {
        var root = TestFs.NewTempDir();
        var (module, _, _) = CreateModule(root);

        Assert.Null(await module.TryServeAsync("data/no_such_table.xml"));
    }

    [Fact]
    public async Task RootNeogameXmlIsDeliberate404()
    {
        var root = TestFs.NewTempDir();
        var (module, _, _) = CreateModule(root);

        var response = await module.TryServeAsync("neogame.xml");

        Assert.NotNull(response);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task ModNeogameXmlFallsThroughToDisk()
    {
        var root = TestFs.NewTempDir();
        var (module, _, _) = CreateModule(root);

        Assert.Null(await module.TryServeAsync("Mods/A/B/neogame.xml"));
    }

    [Fact]
    public async Task GetModsGeneratedWhenDiskFileMissing()
    {
        var root = TestFs.NewTempDir();
        var mods = Path.Combine(root, "Mods", "NeoScavExtended", "NSExtended");
        Directory.CreateDirectory(mods);
        File.WriteAllText(Path.Combine(mods, "neogame.xml"), "<xml/>");
        var (module, _, _) = CreateModule(root);

        var response = await module.TryServeAsync("getmods.php");

        Assert.NotNull(response);
        Assert.Equal(200, response.StatusCode);
        var body = System.Text.Encoding.UTF8.GetString(response.Body);
        Assert.Contains("nRows=1", body);
        Assert.Contains("strModURL0=Mods/NeoScavExtended/NSExtended", body);
    }

    [Fact]
    public async Task GetModsDiskFileWins()
    {
        var root = TestFs.NewTempDir();
        File.WriteAllText(Path.Combine(root, "getmods.php"), "nRows=0");
        var (module, _, _) = CreateModule(root);

        Assert.Null(await module.TryServeAsync("getmods.php"));
    }

    [Fact]
    public async Task GetImagesDiskFileWins()
    {
        var root = TestFs.NewTempDir();
        Directory.CreateDirectory(Path.Combine(root, "Mods", "A", "B"));
        File.WriteAllText(Path.Combine(root, "Mods", "A", "B", "getimages.php"), "nRows=0&nCols=2");
        var (module, _, _) = CreateModule(root);

        Assert.Null(await module.TryServeAsync("Mods/A/B/getimages.php"));
    }

    [Fact]
    public async Task GetImagesGeneratedFromImgScanWhenMissing()
    {
        var root = TestFs.NewTempDir();
        var img = Path.Combine(root, "Mods", "A", "B", "img");
        Directory.CreateDirectory(img);
        File.WriteAllBytes(Path.Combine(img, "item.png"), [1]);
        File.WriteAllBytes(Path.Combine(img, "x2_item.png"), [1]);
        var (module, _, _) = CreateModule(root);

        var response = await module.TryServeAsync("Mods/A/B/getimages.php");

        Assert.NotNull(response);
        var body = System.Text.Encoding.UTF8.GetString(response.Body);
        Assert.Contains("nRows=2&nCols=2", body);
        Assert.Contains("strImageURL0=item.png", body);
        Assert.Contains("strImageURL1=x2_item.png", body);
    }
}
