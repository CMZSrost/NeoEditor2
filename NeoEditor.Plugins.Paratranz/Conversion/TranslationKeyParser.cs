using System;
using System.Text.RegularExpressions;
using NeoEditor.Plugins.Paratranz.Models;

namespace NeoEditor.Plugins.Paratranz.Conversion;

/// <summary>解析后的翻译键：定位 (表, id列, id值, 字段列)。</summary>
public sealed record TranslationKey(string Table, string IdField, string Id, string Column);

/// <summary>
/// xpath 翻译键的构造与解析（D03 §3.2/§3.5）。
/// 格式对齐 NeoParatranz：<c>//table[@name="T"]/column[@name="K"][text()=id]/../column[@name="C"]</c>。
/// </summary>
public interface ITranslationKeyParser
{
    /// <summary>构造翻译键（id 值按原样拼接，与旧脚本一致）。</summary>
    string BuildKey(string tableName, string idField, string idValue, string columnName);

    /// <summary>解析翻译键；非本格式返回 false。</summary>
    bool TryParseKey(string key, out TranslationKey? parsed);
}

public sealed class TranslationKeyParser : ITranslationKeyParser
{
    // 放宽旧脚本的 (\w+) 限制：table/idField/column 允许任意非引号字符，id 值到 ] 为止
    private static readonly Regex KeyRegex = new(
        @"^//table\[@name=""([^""]+)""\]/column\[@name=""([^""]+)""\]\[text\(\)=([^\]]*)\]/../column\[@name=""([^""]+)""\]$",
        RegexOptions.Compiled);

    public string BuildKey(string tableName, string idField, string idValue, string columnName)
        => $"//table[@name=\"{tableName}\"]/column[@name=\"{idField}\"][text()={idValue}]/../column[@name=\"{columnName}\"]";

    public bool TryParseKey(string key, out TranslationKey? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(key))
            return false;
        var match = KeyRegex.Match(key.Trim());
        if (!match.Success)
            return false;
        parsed = new TranslationKey(match.Groups[1].Value, match.Groups[2].Value,
            match.Groups[3].Value, match.Groups[4].Value);
        return true;
    }
}
