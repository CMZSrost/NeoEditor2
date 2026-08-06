using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;

namespace NeoEditor.Player.Core.Services;

/// <summary>A proxy response body: status code + content type + bytes. Null = "not proxied".</summary>
public sealed record ProxyResponse(int StatusCode, string ContentType, byte[] Body)
{
    public static ProxyResponse Ok(string contentType, string text) => new(200, contentType, Encoding.UTF8.GetBytes(text));
    public static ProxyResponse NotFound => new(404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not Found"));
}

/// <summary>
/// Data-layer reverse proxy for the preview (Docs/42 §3.6): the game's requests for
/// getmods.php / getimages.php / data XML are transparently served from the editor's live
/// state instead of raw disk files. The SWF keeps issuing plain HTTP requests — no JS or
/// ruffle changes needed.
/// </summary>
public sealed class ProxyHttpModule
{
    private readonly IConfigService _config;
    private readonly IGamePhpGenerator _phpGen;
    private readonly IGameDataExportService _dataExport;

    public ProxyHttpModule(IConfigService config, IGamePhpGenerator phpGen, IGameDataExportService dataExport)
    {
        _config = config;
        _phpGen = phpGen;
        _dataExport = dataExport;
    }

    /// <summary>
    /// When false, the proxy is fully disabled and every request falls through to the disk
    /// file — the standalone player's mode (Docs/42 §3.8: no editor session → pure static
    /// serving). The editor preview keeps it true (live reverse proxy).
    /// </summary>
    public bool ProxyEnabled { get; set; } = true;

    /// <summary>
    /// Try to serve <paramref name="relativeUrl"/> (no leading slash, e.g. "data/itemtypes.xml").
    /// Returns null when the request should fall through to the disk file.
    /// </summary>
    public async Task<ProxyResponse?> TryServeAsync(string relativeUrl)
    {
        if (!ProxyEnabled) return null;

        var gameRoot = _config.Config.GameRootDir;

        // ── data/<table>.xml → LIVE export (the core preview value: unsaved edits visible) ──
        if (relativeUrl.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
            && relativeUrl.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            var tableName = relativeUrl["data/".Length..^".xml".Length];
            var xml = await _dataExport.ExportTableXmlAsync(tableName).ConfigureAwait(false);
            if (xml is null) return null;                       // unknown table → disk fallback
            return ProxyResponse.Ok("text/xml; charset=utf-8", xml);
        }

        // ── root neogame.xml → deliberately 404: the game then loads data/*.xml per table,
        //    which is exactly the live-proxied path (tested-success layout, Docs/42 §2.5) ──
        if (relativeUrl.Equals("neogame.xml", StringComparison.OrdinalIgnoreCase))
            return ProxyResponse.NotFound;

        // ── getmods.php → disk-first; generate from the Mods/ scan only when the file is
        //    missing (so editor-created mods still appear after they get data on disk) ──
        if (relativeUrl.Equals("getmods.php", StringComparison.OrdinalIgnoreCase))
        {
            var diskFile = Path.Combine(gameRoot, relativeUrl);
            if (File.Exists(diskFile)) return null;
            var mods = ModListScanner.Scan(Path.Combine(gameRoot, "Mods"));
            if (mods.Count == 0) return null;
            return ProxyResponse.Ok("text/plain; charset=utf-8", _phpGen.GenerateModsPhp(mods));
        }

        // ── <mod>/getimages.php → disk-first; generate from that mod's img/ scan when missing ──
        if (relativeUrl.EndsWith("getimages.php", StringComparison.OrdinalIgnoreCase))
        {
            var diskFile = Path.Combine(gameRoot, relativeUrl);
            if (File.Exists(diskFile)) return null;
            var modDir = Path.GetDirectoryName(relativeUrl) ?? "";
            var pairs = ImageListScanner.ScanPairs(Path.Combine(gameRoot, modDir, "img"));
            if (pairs.Count == 0) return null;
            return ProxyResponse.Ok("text/plain; charset=utf-8", _phpGen.GenerateImagePhp(pairs));
        }

        // Mods/<mod>/neogame.xml and anything else → disk fallback.
        return null;
    }
}

/// <summary>
/// Scans <c>Mods/*/*</c> for mods that carry game data (neogame.xml or a data/ folder),
/// mirroring the format of the game's own getmods.php (Docs/42 §2.5). Pure disk logic.
/// </summary>
internal static class ModListScanner
{
    public static List<(string Name, string Path)> Scan(string modsDir)
    {
        var result = new List<(string Name, string Path)>();
        if (!Directory.Exists(modsDir)) return result;

        foreach (var modDir in Directory.EnumerateDirectories(modsDir))
        {
            foreach (var subDir in Directory.EnumerateDirectories(modDir))
            {
                if (File.Exists(Path.Combine(subDir, "neogame.xml"))
                    || Directory.Exists(Path.Combine(subDir, "data")))
                {
                    var rel = Path.GetRelativePath(modsDir, subDir).Replace('\\', '/');
                    result.Add((Path.GetFileName(subDir), $"Mods/{rel}"));
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Scans a mod's <c>img/</c> folder and pairs normal images with their x2 variants
/// (x2_ prefix convention, matching ImageService.PairImages), in the n*2 table layout
/// the game's getimages.php uses. Pure disk logic.
/// </summary>
internal static class ImageListScanner
{
    private const string X2Prefix = "x2_";

    public static List<(string NormalImage, string X2Image)> ScanPairs(string imgDir)
    {
        var result = new List<(string NormalImage, string X2Image)>();
        if (!Directory.Exists(imgDir)) return result;

        var files = Directory.EnumerateFiles(imgDir)
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var x2Map = files
            .Where(static name => name.StartsWith(X2Prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                static name => name[X2Prefix.Length..],
                static name => name,
                StringComparer.OrdinalIgnoreCase);

        foreach (var normal in files.Where(static name => !name.StartsWith(X2Prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var x2 = x2Map.GetValueOrDefault(normal, "");
            if (x2.Length > 0) x2Map.Remove(normal);
            result.Add((normal, x2));
        }

        // x2-only images (no normal variant) — emit as (x2, "") so they are not lost.
        foreach (var orphan in x2Map.Values.OrderBy(static v => v, StringComparer.OrdinalIgnoreCase))
            result.Add((orphan, ""));

        return result;
    }
}
