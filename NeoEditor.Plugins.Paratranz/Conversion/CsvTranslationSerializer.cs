using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using NeoEditor.Plugins.Paratranz.Models;

namespace NeoEditor.Plugins.Paratranz.Conversion;

/// <summary>
/// 翻译 CSV（ParaTranz 文件）与 <see cref="TranslationUnit"/> 的互转（D03 §3.4）。
/// 格式对齐 NeoParatranz：无表头、每行 3 列 (key, original, translation)（读取兼容 2 列
/// (key, translation)）、行终止 \n、UTF-8 无 BOM；字段含逗号/引号/换行时按 RFC 4180 引号转义。
/// </summary>
public interface ICsvTranslationSerializer
{
    /// <summary>序列化为 CSV 文本（3 列无头，\n 行终止，UTF-8 无 BOM）。</summary>
    string Serialize(IEnumerable<TranslationUnit> units);

    /// <summary>解析 CSV 文本为翻译单元（兼容 2/3 列、首尾引号与多余 // 前缀清洗、
    /// 字面 \n 还原为换行）；坏行跳过。</summary>
    IReadOnlyList<TranslationUnit> Deserialize(string csv);
}

public sealed class CsvTranslationSerializer : ICsvTranslationSerializer
{
    private static readonly CsvConfiguration WriteConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
        NewLine = "\n",
    };

    private static readonly CsvConfiguration ReadConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
        // 行终止由解析器自动识别（\n / \r\n 均支持）；坏行（如字段内未转义引号）跳过而非抛出
        BadDataFound = null,
    };

    public string Serialize(IEnumerable<TranslationUnit> units)
    {
        using var writer = new StringWriter();
        using (var csv = new CsvWriter(writer, WriteConfig))
        {
            foreach (var unit in units)
            {
                csv.WriteField(unit.Key);
                csv.WriteField(unit.Original);
                csv.WriteField(unit.Translation ?? string.Empty);
                csv.NextRecord();
            }
        }
        return writer.ToString();
    }

    public IReadOnlyList<TranslationUnit> Deserialize(string csv)
    {
        var result = new List<TranslationUnit>();
        if (string.IsNullOrEmpty(csv))
            return result;

        // 用 CsvParser 而非 CsvReader：无表头文件中行间列数可变（2/3 列兼容），
        // CsvReader 会按首行列数做一致性检查并对异列数行抛 BadDataException。
        using var reader = new StringReader(csv);
        using var parser = new CsvParser(reader, ReadConfig);
        while (parser.Read())
        {
            var fields = parser.Record;
            if (fields.Length < 2)
                continue; // 空行 / 坏行
            var key = CleanKey(fields[0]);
            if (string.IsNullOrEmpty(key))
                continue;
            var original = fields.Length >= 3 ? fields[1] ?? "" : "";
            var translation = fields.Length >= 3 ? fields[2] ?? "" : fields[1] ?? "";
            // 对齐 deconvert_xml：CSV 中的字面 \n 还原为真实换行
            result.Add(new TranslationUnit(key, original, translation.Replace("\\n", "\n")));
        }
        return result;
    }

    /// <summary>清洗 key（对齐旧脚本 clean_xpath）：去空白、"" 折叠、截取 // 起始、去首尾引号。</summary>
    private static string CleanKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;
        var cleaned = key.Trim().Replace("\"\"", "\"");
        var slash = cleaned.IndexOf("//", StringComparison.Ordinal);
        if (slash >= 0)
            cleaned = cleaned[slash..];
        return cleaned.Trim('"');
    }
}
