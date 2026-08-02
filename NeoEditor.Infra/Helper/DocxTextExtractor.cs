using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace NeoEditor.Helper;

public static class DocxTextExtractor
{
    /// <summary>Extract all text from a .docx file, preserving paragraphs as lines.</summary>
    public static string ExtractText(string docxPath)
    {
        using var archive = ZipFile.OpenRead(docxPath);
        var docEntry = archive.GetEntry("word/document.xml")
                       ?? archive.GetEntry("word/document2.xml");
        if (docEntry is null) return string.Empty;

        using var stream = docEntry.Open();
        var doc = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var sb = new StringBuilder();
        foreach (var para in doc.Descendants(w + "p"))
        {
            var lineBuilder = new StringBuilder();
            foreach (var text in para.Descendants(w + "t"))
            {
                lineBuilder.Append(text.Value);
            }
            var line = lineBuilder.ToString();
            if (!string.IsNullOrWhiteSpace(line))
                sb.AppendLine(line);
        }
        return sb.ToString();
    }

    /// <summary>Parse the field descriptions .docx into a dictionary of {TableName}.{ColumnName} -> description.</summary>
    public static Dictionary<string, string> ParseFieldDescriptions(string text)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string? currentTable = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Detect table header: "第X部分 tablename 中文名" or just a pattern like "attackmodes".
            // R30: capture only the ASCII table name — the Chinese suffix used to leak into the
            // key ("attackmodes攻击模式.strname") and break lookups against TableAttribute names.
            var partMatch = System.Text.RegularExpressions.Regex.Match(trimmed,
                @"第[^部分]+部分\s*([a-zA-Z_][a-zA-Z0-9_]*)");
            if (partMatch.Success)
            {
                currentTable = partMatch.Groups[1].Value.ToLowerInvariant();
                continue;
            }

            // Detect: <column name="colName">value</column> description
            var colMatch = System.Text.RegularExpressions.Regex.Match(trimmed,
                @"<column\s+name=""(\w+)""[^>]*>([^<]*)</column>\s*(.*)");
            if (colMatch.Success && currentTable is not null)
            {
                var colName = colMatch.Groups[1].Value;
                var desc = colMatch.Groups[3].Value.Trim();

                if (!string.IsNullOrWhiteSpace(desc))
                {
                    var key = $"{currentTable}.{colName}".ToLowerInvariant();
                    if (!result.ContainsKey(key))
                        result[key] = desc;
                    else
                        result[key] += "; " + desc;
                }
            }
        }

        return result;
    }
}

