using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NeoEditor.Data.DTO;

namespace NeoEditor.Services;

public class GetModsParser
{
    /// <summary>
    ///     解析 getmods.php 内容（URL 编码键值对格式）
    /// </summary>
    public List<ModEntry> Parse(string content)
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
    public string Generate(List<ModEntry> mods)
    {
        var sb = new StringBuilder();
        sb.Append($"nRows={mods.Count}");
        for (var i = 0; i < mods.Count; i++)
        {
            sb.Append($"&strModName{i}={Uri.EscapeDataString(mods[i].Name)}");
            sb.Append($"&strModURL{i}={Uri.EscapeDataString(mods[i].Path)}");
        }

        return sb.ToString();
    }
}