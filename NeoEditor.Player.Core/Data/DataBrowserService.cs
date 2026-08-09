using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Player.Core.Services;

namespace NeoEditor.Player.Core.Data;

/// <summary>One parsed row of game data (pma_xml_export: each row = a <c>&lt;table&gt;</c> block).</summary>
public sealed record GameDataRow(string TableName, IReadOnlyList<GameDataField> Fields)
{
    /// <summary>
    /// Optional per-column label resolver (v2.72, localization) — set by the catalog when
    /// built with a column labeler; not part of the record's structural equality.
    /// </summary>
    public Func<string, string?>? ColumnLabel { get; set; }

    /// <summary>First few non-empty fields joined for compact display (no dynamic columns needed).</summary>
    public string Summary
        => string.Join(" | ", Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).Take(4)
            .Select(f => $"{ColumnLabel?.Invoke(f.Column) ?? f.Column}:{Truncate(f.Value, 40)}"));

    /// <summary>Merge key: nID → id → first field value (later source wins in the catalog).</summary>
    public string RowKey
    {
        get
        {
            foreach (var column in new[] { "nID", "id" })
            {
                var value = Fields.FirstOrDefault(f =>
                    string.Equals(f.Column, column, StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            return Fields.Count > 0 ? $"{Fields[0].Column}={Fields[0].Value}" : Summary;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

public sealed record GameDataField(string Column, string Value);

/// <summary>
/// The merged game data catalog (Docs/42 v2.15): base <c>data/*.xml</c> tables overlaid by
/// every <c>Mods/*/*/neogame.xml</c> — the same merge semantics the game applies at load
/// (later source wins for the same row key). Grouped by entity table (the 24 data classes).
/// </summary>
public sealed class GameDataCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<GameDataRow>> _tables;

    public GameDataCatalog(IReadOnlyDictionary<string, IReadOnlyList<GameDataRow>> tables)
    {
        _tables = tables;
        TableNames = tables.Keys.ToList();
        TotalRows = tables.Values.Sum(rows => rows.Count);
    }

    /// <summary>Table names: known entity tables (GameTableMap) first, then any extras, alphabetical.</summary>
    public IReadOnlyList<string> TableNames { get; }

    public int TotalRows { get; }

    public IReadOnlyList<GameDataRow> GetRows(string tableName)
        => _tables.GetValueOrDefault(tableName) ?? [];

    /// <summary>
    /// Find a row by its merge key, any exact field value, or a dotted composite key
    /// ("GroupId.SubgroupId" style, e.g. treasuretable loot ids) — resolves reference
    /// links to their target row.
    /// </summary>
    public GameDataRow? FindRow(string tableName, string key)
    {
        if (!_tables.TryGetValue(tableName, out var rows)) return null;

        var match = rows.FirstOrDefault(r => string.Equals(r.RowKey, key, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        match = rows.FirstOrDefault(r =>
            r.Fields.Any(f => string.Equals(f.Value, key, StringComparison.OrdinalIgnoreCase)));
        if (match is not null) return match;

        if (key.Contains('.'))
        {
            var parts = key.Split('.', 2);
            match = rows.FirstOrDefault(r =>
                r.Fields.Any(f1 => string.Equals(f1.Value, parts[0], StringComparison.OrdinalIgnoreCase) &&
                                   r.Fields.Any(f2 => string.Equals(f2.Value, parts[1], StringComparison.OrdinalIgnoreCase))));
        }
        return match;
    }
}

/// <summary>
/// Read-only game data browser source (Docs/42 v2.12→v2.15): builds the merged catalog of
/// the game's data files (base data/*.xml + Mods/*/*/neogame.xml), keyed per row by the
/// table's primary key (nID, else id, else first field). Pure disk reads — never writes.
/// </summary>
public sealed class DataBrowserService
{
    private readonly IConfigService _config;

    public DataBrowserService(IConfigService config)
    {
        _config = config;
    }

    /// <summary>Game root dir — img/*.png resolve under it (wiki image gallery).</summary>
    public string? GameRootDir => _config.Config.GameRootDir;

    /// <summary>
    /// Scan + merge ALL data files into the per-table catalog (base first, mods in order).
    /// <paramref name="columnLabel"/> optionally localizes the row-summary column prefixes
    /// (table, column) → label; null keeps the raw XML column names (v2.72).
    /// </summary>
    public GameDataCatalog BuildCatalog(Func<string, string, string?>? columnLabel = null)
    {
        var merged = new Dictionary<string, Dictionary<string, GameDataRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in EnumerateDataFiles())
        {
            foreach (var row in ParseFile(file))
            {
                if (!merged.TryGetValue(row.TableName, out var rows))
                {
                    rows = new Dictionary<string, GameDataRow>(StringComparer.OrdinalIgnoreCase);
                    merged[row.TableName] = rows;
                }

                if (columnLabel is not null)
                {
                    // 行摘要里的列名前缀本地化（本地化查找在取值时进行 → 语言切换即时生效）。
                    var table = row.TableName;
                    row.ColumnLabel = column => columnLabel(table, column);
                }
                rows[row.RowKey] = row;   // later source wins: mods overlay base
            }
        }

        var known = GameTableMap.KnownTableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tables = merged.ToDictionary(
            static kv => kv.Key,
            static kv => (IReadOnlyList<GameDataRow>)kv.Value.Values.ToList(),
            StringComparer.OrdinalIgnoreCase);
        var orderedNames = tables.Keys
            .OrderBy(name => known.Contains(name) ? 0 : 1)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase);

        return new GameDataCatalog(tables);
    }

    /// <summary>
    /// data/*.xml first, then mods in the game's own load order (getmods.php strModURL
    /// sequence, v2.20), then any unlisted mod directories alphabetically.
    /// </summary>
    private IEnumerable<string> EnumerateDataFiles()
    {
        var gameRoot = _config.Config.GameRootDir;
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot)) yield break;

        var dataDir = Path.Combine(gameRoot, "data");
        if (Directory.Exists(dataDir))
            foreach (var file in Directory.EnumerateFiles(dataDir, "*.xml")
                         .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                yield return file;

        var modsDir = Path.Combine(gameRoot, "Mods");
        if (!Directory.Exists(modsDir)) yield break;

        var ordered = new List<string>();
        foreach (var url in ParseGetModsOrder(gameRoot))
        {
            var modXml = Path.Combine(gameRoot,
                url.Replace('/', Path.DirectorySeparatorChar), "neogame.xml");
            if (File.Exists(modXml)) ordered.Add(modXml);
        }

        // Unlisted mod directories (e.g. created by the editor after the php was generated).
        foreach (var subDir in Directory.EnumerateDirectories(modsDir)
                     .SelectMany(static modDir => Directory.EnumerateDirectories(modDir))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var modXml = Path.Combine(subDir, "neogame.xml");
            if (File.Exists(modXml) && !ordered.Contains(modXml, StringComparer.OrdinalIgnoreCase))
                ordered.Add(modXml);
        }

        foreach (var file in ordered)
            yield return file;
    }

    /// <summary>
    /// Parse the mod load order from the game's getmods.php: the &amp;-joined
    /// <c>strModURL{i}=Mods/...</c> pairs, ordered by index — the exact sequence the SWF
    /// loads mods in (later mods override earlier ones). Empty when the file is missing.
    /// </summary>
    internal static IReadOnlyList<string> ParseGetModsOrder(string gameRoot)
    {
        var php = Path.Combine(gameRoot, "getmods.php");
        if (!File.Exists(php)) return [];

        try
        {
            var text = File.ReadAllText(php);
            return System.Text.RegularExpressions.Regex.Matches(
                    text, @"strModURL(\d+)=([^&\r\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .Select(m => (
                    Index: int.Parse(m.Groups[1].Value),
                    Url: Uri.UnescapeDataString(m.Groups[2].Value.Trim())))
                .OrderBy(static x => x.Index)
                .Select(static x => x.Url)
                .Where(static url => url.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static IEnumerable<GameDataRow> ParseFile(string fullPath)
    {
        try
        {
            // File.ReadAllText + XDocument.Parse, NOT XDocument.Load: the game's XML files
            // declare encoding='utf8' (non-standard spelling) which makes XDocument.Load
            // throw "System does not support 'utf8' encoding" — parsing the already-decoded
            // string ignores the declaration entirely.
            var doc = XDocument.Parse(File.ReadAllText(fullPath));
            return doc.Descendants("table")
                .Select(table =>
                {
                    var tableName = (string?)table.Attribute("name") ?? "";
                    var fields = table.Elements("column")
                        .Select(col => new GameDataField(
                            (string?)col.Attribute("name") ?? "",
                            col.Value.Trim()))
                        .Where(f => f.Column.Length > 0)
                        .ToList();
                    return new GameDataRow(tableName, fields);
                })
                .ToList();
        }
        catch (Exception)
        {
            return [];   // malformed file — skip it, the rest stays browsable
        }
    }
}
