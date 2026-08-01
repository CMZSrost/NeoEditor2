using System;
using System.Collections.Generic;
using System.IO;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Image search directory resolution.
/// Implements the same logic as App's ImageService.GetImageSearchDirsForEntity,
/// but without the IImageService dependency.
/// </summary>
public sealed class ImageSearchService : IImageSearchService
{
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
}
