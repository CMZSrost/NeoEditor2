using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace NeoEditor.Services;

public interface IImageService
{
    string? FindImage(string name);
    List<string> GetImageSearchDirs();
    /// <summary>Get image search dirs, including entity-specific mod img directory (Q9=A).</summary>
    List<string> GetImageSearchDirsForEntity(string gameRoot, string? entityFilePath);
    List<(string NormalImage, string X2Image)> PairImages(IReadOnlyList<string> imagePaths);
}

/// <summary>
/// Image file operations — search, pairing heuristics, path resolution.
/// Extracted from PhpParser, EditorHelper, and ItemTypeEditor to eliminate duplication.
/// </summary>
public class ImageService : IImageService
{
    private readonly IConfigService _config;

    public ImageService(IConfigService config) { _config = config; }

    // ── Image search ───────────────────────────────────────────────────────

    /// <summary>Search all known image directories under the game root for a file by name.</summary>
    public string? FindImage(string name)
    {
        var dirs = GetImageSearchDirs();
        foreach (var d in dirs)
        {
            try
            {
                var f = Directory.GetFiles(d, name, SearchOption.AllDirectories).FirstOrDefault();
                if (f is not null) return f;
            }
            catch { }
        }
        return null;
    }

    /// <summary>Build the list of directories to search for images.</summary>
    public List<string> GetImageSearchDirs()
    {
        var gameRoot = _config.Config.GameRootDir;
        return GetImageSearchDirsForEntity(gameRoot, null);
    }

    /// <summary>Get image search dirs, including entity-specific mod img directory (Q9=A).</summary>
    public List<string> GetImageSearchDirsForEntity(string gameRoot, string? entityFilePath)
    {
        var dirs = new List<string>();
        if (string.IsNullOrWhiteSpace(gameRoot)) return dirs;
        dirs.Add(Path.Combine(gameRoot, "img"));
        try
        {
            // Add entity's own mod img directory if available
            if (!string.IsNullOrWhiteSpace(entityFilePath))
            {
                var entityDir = Path.GetDirectoryName(entityFilePath);
                if (entityDir is not null)
                {
                    var entityImgDir = Path.Combine(entityDir, "img");
                    if (Directory.Exists(entityImgDir)) dirs.Add(entityImgDir);
                }
            }

            var modsDir = Path.Combine(gameRoot, "Mods");
            if (Directory.Exists(modsDir))
                foreach (var d in Directory.GetDirectories(modsDir))
                {
                    dirs.Add(d);
                    var imgDir = Path.Combine(d, "img");
                    if (Directory.Exists(imgDir)) dirs.Add(imgDir);
                }
        }
        catch { }
        return dirs;
    }

    // ── Image pairing ──────────────────────────────────────────────────────

    public List<(string NormalImage, string X2Image)> PairImages(IReadOnlyList<string> imagePaths)
    {
        var images = imagePaths
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Select(static s => s.Trim())
            .ToList();

        if (images.Count == 0) return [];

        if (LooksLikeSplitHalfPairs(images))
        {
            var half = images.Count / 2;
            var normalImages = images.Take(half).ToList();
            var x2Images = images.Skip(half)
                .Where(static image => image.StartsWith("x2_", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(static image => image[3..], static image => image, StringComparer.OrdinalIgnoreCase);

            return normalImages
                .Select(normalImage =>
                {
                    var normalFileName = Path.GetFileName(normalImage);
                    return (normalImage, x2Images.GetValueOrDefault(normalFileName, string.Empty));
                })
                .ToList();
        }

        var sequentialPairs = new List<(string NormalImage, string X2Image)>();
        for (var i = 0; i < images.Count; i += 2)
            sequentialPairs.Add((images[i], i + 1 < images.Count ? images[i + 1] : string.Empty));

        return sequentialPairs;
    }

    private static bool LooksLikeSplitHalfPairs(IReadOnlyList<string> images)
    {
        if (images.Count < 2 || images.Count % 2 != 0) return false;
        var half = images.Count / 2;
        var firstHalfX2Count = images.Take(half).Count(IsX2Image);
        var secondHalfX2Count = images.Skip(half).Count(IsX2Image);
        return firstHalfX2Count <= half / 10 && secondHalfX2Count >= (int)Math.Ceiling(half * 0.6);
    }

    public static bool IsX2Variant(string normalImage, string x2Image)
    {
        var normalFileName = Path.GetFileName(normalImage.Trim());
        var x2FileName = Path.GetFileName(x2Image.Trim());
        return x2FileName.Equals($"x2_{normalFileName}", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsX2Image(string imagePath)
        => Path.GetFileName(imagePath.Trim()).StartsWith("x2_", StringComparison.OrdinalIgnoreCase);
}
