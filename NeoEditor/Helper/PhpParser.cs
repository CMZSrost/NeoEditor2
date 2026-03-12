using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NeoEditor.Data.DTO;
using Newtonsoft.Json;

namespace NeoEditor.Helper;

public class PhpParser
{
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
        Console.WriteLine(
            $"parsed {result.Count} mods from getmods.php\n");
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

        Console.WriteLine($"parsed {result.Count} images from getimages.php");
        return result;
    }

    public List<(string NormalImage, string X2Image)> ParseImagePairs(string filePath)
    {
        return PairImages(ParseImages(filePath));
    }

    public List<(string NormalImage, string X2Image)> PairImages(IReadOnlyList<string> imagePaths)
    {
        var images = imagePaths
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Select(static s => s.Trim())
            .ToList();

        if (images.Count == 0)
        {
            return [];
        }

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
        {
            sequentialPairs.Add((images[i], i + 1 < images.Count ? images[i + 1] : string.Empty));
        }

        return sequentialPairs;
    }

    private static bool LooksLikeSplitHalfPairs(IReadOnlyList<string> images)
    {
        if (images.Count < 2 || images.Count % 2 != 0)
        {
            return false;
        }

        var half = images.Count / 2;
        var firstHalfX2Count = images.Take(half).Count(IsX2Image);
        var secondHalfX2Count = images.Skip(half).Count(IsX2Image);

        return firstHalfX2Count <= half / 10 && secondHalfX2Count >= (int)Math.Ceiling(half * 0.6);
    }

    private static bool IsX2Variant(string normalImage, string x2Image)
    {
        var normalFileName = Path.GetFileName(normalImage.Trim());
        var x2FileName = Path.GetFileName(x2Image.Trim());
        return x2FileName.Equals($"x2_{normalFileName}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsX2Image(string imagePath)
    {
        return Path.GetFileName(imagePath.Trim()).StartsWith("x2_", StringComparison.OrdinalIgnoreCase);
    }

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
    public string GenerateImagePhp(IList<string> modImages)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"nRows={modImages.Count}&nCols=2");
        // 首先进行排序，确保 非x2_ 的图片在前面，然后是同名的 x2_ 图片，并且保持交替
        var imageMap = modImages.Where((s => !s.Contains("x2_"))).Order().ToDictionary((s => s), s => $"x2_{s}");

        int i = 0;
        foreach (var imagePair in imageMap)
        {
            sb.AppendLine($"&strImageURL{i++}={imagePair.Key}");
            sb.AppendLine($"&strImageURL{i++}={imagePair.Value}");
        }

        return sb.ToString();
    }
}