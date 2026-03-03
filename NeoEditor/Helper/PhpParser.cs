using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NeoEditor.Data.DTO;

namespace NeoEditor.Helper;

public class PhpParser
{
    /// <summary>
    ///     解析 getmods.php 内容（URL 编码键值对格式）
    /// </summary>
    public List<ModEntry> Parse(string filePath) => ParseContent(File.ReadAllText(filePath).ReplaceLineEndings(""));

    public List<ModEntry> ParseContent(string content)
    {
        var dict = new Dictionary<string, string>();
        var pairs = content.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var eq = pair.IndexOf('=');
            if (eq > 0)
            {
                var key = pair.Substring(0, eq);
                var value = Uri.UnescapeDataString(pair.Substring(eq + 1));
                dict[key] = value;
            }
        }

        if (!dict.TryGetValue("nRows", out var nRowsStr) || !int.TryParse(nRowsStr, out var nRows))
            throw new InvalidDataException("Invalid getmods.php: missing or invalid nRows");

        var result = new List<ModEntry>();
        for (var i = 0; i <= nRows; i++) // nRows 是最大索引
        {
            var nameKey = $"strModName{i}";
            var urlKey = $"strModURL{i}";
            if (dict.TryGetValue(nameKey, out var name) && dict.TryGetValue(urlKey, out var url))
                result.Add(new ModEntry { Name = name, Path = url });
        }

        return result;
    }

    /// <summary>
    ///     生成 getmods.php 内容
    /// </summary>
    public string GenerateModsPhp(List<ModEntry> mods)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"nRows={mods.Count}");
        for (var i = 0; i < mods.Count; i++)
        {
            sb.AppendLine($"&strModName{i}={Uri.EscapeDataString(mods[i].Name)}");
            sb.AppendLine($"&strModURL{i}={Uri.EscapeDataString(mods[i].Path)}");
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
            sb.AppendLine($"&strImageURL{i++}={Uri.EscapeDataString(imagePair.Key)}");
            sb.AppendLine($"&strImageURL{i++}={Uri.EscapeDataString(imagePair.Value)}");
        }

        return sb.ToString();
    }
}