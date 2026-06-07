using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.DTO;
using NeoEditor.Services;
using Newtonsoft.Json;

namespace NeoEditor.Helper;

public class PhpParser
{
    private IImageService? _imageService;

    private IImageService ImageService =>
        _imageService ??= App.ServiceProvider!.GetRequiredService<IImageService>();
    public List<ModEntry> ParseMods(string filePath) => ParseModsContent(File.ReadAllText(filePath));

    public List<ModEntry> ParseModsContent(string content)
    {
        var dict = new Dictionary<string, string>();
        var parsed = System.Web.HttpUtility.ParseQueryString(content);
        var nRow = int.Parse(parsed["nRows"] ?? throw new InvalidDataException("Invalid getmods.php: missing nRows"));
        for (var i = 0; i < nRow; i++)
        {
            var nameKey = $"strModName{i}";
            var urlKey = $"strModURL{i}";
            if (parsed[nameKey] != null && parsed[urlKey] != null)
                dict[parsed[nameKey]!.ReplaceLineEndings("")] = parsed[urlKey]!.ReplaceLineEndings("");
        }

        var result = dict.Select(kv => new ModEntry { Name = kv.Key, Path = kv.Value }).ToList();
        Serilog.Log.Logger.Information("[PhpParser] parsed {Count} mods from getmods.php", result.Count);
        return result;
    }

    public List<string> ParseImages(string filePath) => ParseImagesContent(File.ReadAllText(filePath));

    public List<string> ParseImagesContent(string content)
    {
        List<string> result = [];
        var parsed = System.Web.HttpUtility.ParseQueryString(content);
        var nRow = int.Parse(parsed["nRows"] ?? throw new InvalidDataException("Invalid getmods.php: missing nRows"));
        for (var i = 0; i < nRow * 2; i++)
        {
            var nameKey = $"strImageURL{i}";
            if (parsed[nameKey] != null)
                result.Add(parsed[nameKey]!.ReplaceLineEndings(""));
        }

        Serilog.Log.Logger.Information("[PhpParser] parsed {Count} images from getimages.php", result.Count);
        return result;
    }

    public List<(string NormalImage, string X2Image)> ParseImagePairs(string filePath)
    {
        return PairImages(ParseImages(filePath));
    }

    public List<(string NormalImage, string X2Image)> PairImages(IReadOnlyList<string> imagePaths)
        => ImageService.PairImages(imagePaths);

    public string GenerateModsPhp(List<ModEntry> mods)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"nRows={mods.Count}");
        for (var i = 0; i < mods.Count; i++)
        {
            sb.Append($"&strModName{i}={mods[i].Name}");
            sb.AppendLine($"&strModURL{i}={mods[i].Path}");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     生成 getimages.php 内容
    ///     注意 图片Url里，不带x2_要排前面，然后带x2的同名Url紧跟其后，接着就是下一个Url，这样形成一个 n*2 的表格
    /// </summary>
    public string GenerateImagePhp(IReadOnlyList<(string NormalImage, string X2Image)> imagePairs)
    {
        var flattenedImages = imagePairs
            .SelectMany(pair => new[] { pair.NormalImage, pair.X2Image })
            .Where(static image => !string.IsNullOrWhiteSpace(image))
            .Select(static image => image.Trim())
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"nRows={flattenedImages.Count}&nCols=2");
        // if (flattenedImages.Count > 0)
        // {
        //     sb.AppendLine($"&strImageURL0={flattenedImages[0]}");
        // }
        // else
        // {
        //     sb.AppendLine();
        // }

        for (var i = 0; i < flattenedImages.Count; i++)
        {
            sb.AppendLine($"&strImageURL{i}={flattenedImages[i]}");
        }

        return sb.ToString();
    }
}