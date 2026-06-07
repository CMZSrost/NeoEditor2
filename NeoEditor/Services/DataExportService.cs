using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Services;

public class DataExportService
{
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;
    private readonly ILogger<DataExportService> _logger;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly IModManager _modManager;

    public DataExportService(IDbContextFactory<GameDbContext> gameDbFactory, ILogger<DataExportService> logger,
        IDbContextFactory<EditorDbContext> editorDbFactory, IModManager modManager)
    {
        _gameDbFactory = gameDbFactory;
        _logger = logger;
        _editorDbFactory = editorDbFactory;
        _modManager = modManager;
    }

    /// <summary>Ensure game base data (ModId=-1) is loaded into the DB before exporting.</summary>
    private async Task EnsureGameDataLoadedAsync()
    {
        try
        {
            await using var edb = await _editorDbFactory.CreateDbContextAsync();
            var gameMod = await edb.ModInfos.FindAsync(-1);
            if (gameMod is not null && gameMod.LastImport <= DateTime.MinValue.AddDays(1))
                await _modManager.LoadModAsync(gameMod);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure game data is loaded for export");
        }
    }

    private static Dictionary<TKey, T> ToDedupedDict<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector)
        where T : IEntity where TKey : notnull
    {
        return source.GroupBy(keySelector)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.ModId).First());
    }

    /// <summary>
    /// Export crafting table as CSV or XLSX (based on file extension).
    /// </summary>
    public async Task ExportCraftingTableAsync(string outputPath)
    {
        await EnsureGameDataLoadedAsync();
        await using var db = await _gameDbFactory.CreateDbContextAsync();
        var recipes = db.Set<Recipe>().ToList();
        var ingredients = ToDedupedDict<Ingredient, int>(db.Set<Ingredient>().ToList(), i => i.Id);
        var treasureTables = ToDedupedDict<TreasureTable, int>(db.Set<TreasureTable>().ToList(), t => t.Id);
        var itemTypes = db.Set<ItemType>().ToList();

        var rows = new List<string[]>();
        rows.Add(["RecipeID", "Name", "Type", "Tools", "Consumed", "Destroyed", "Product", "Hours"]);

        foreach (var recipe in recipes)
        {
            var tools = FormatIngredientList(recipe.Tools, ingredients);
            var consumed = FormatIngredientList(recipe.Consumed, ingredients);
            var destroyed = FormatIngredientList(recipe.Destroyed, ingredients);
            var product = ResolveTreasureProduct(recipe.TreasureId, treasureTables, itemTypes);

            rows.Add([
                recipe.Id.ToString(),
                recipe.Name,
                recipe.Type,
                tools,
                consumed,
                destroyed,
                product,
                recipe.Hours.ToString("F2")
            ]);
        }

        await WriteOutput(outputPath, "Crafting", rows, r => string.Join(",", r.Select(CsvEscape)));
        _logger.LogInformation("Exported crafting table to {Path}", outputPath);
    }

    /// <summary>
    /// Export item encyclopedia as Markdown.
    /// </summary>
    public async Task ExportItemEncyclopediaMdAsync(string outputPath)
    {
        await EnsureGameDataLoadedAsync();
        await using var db = await _gameDbFactory.CreateDbContextAsync();
        var itemTypes = db.Set<ItemType>().ToList();
        var itemProps = ToDedupedDict<ItemProp, int>(db.Set<ItemProp>().ToList(), p => p.Id);
        var treasureTables = ToDedupedDict<TreasureTable, int>(db.Set<TreasureTable>().ToList(), t => t.Id);
        var conditions = ToDedupedDict<Condition, int>(db.Set<Condition>().ToList(), c => c.Id);
        var attackModes = ToDedupedDict<AttackMode, int>(db.Set<AttackMode>().ToList(), a => a.Id);
        var containerTypes = ToDedupedDict<ContainerType, int>(db.Set<ContainerType>().ToList(), c => c.Id);
        var chargeProfiles = ToDedupedDict<ChargeProfile, int>(db.Set<ChargeProfile>().ToList(), c => c.Id);

        var sb = new StringBuilder();
        sb.AppendLine("# Neo Scavenger Item Encyclopedia");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        foreach (var item in itemTypes.OrderBy(i => i.GroupId).ThenBy(i => i.SubgroupId))
        {
            sb.AppendLine($"## {item.GroupId}.{item.SubgroupId} — {item.Name}");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(item.Description))
                sb.AppendLine($"- **Description**: {UnescapeUnicode(item.Description)}");
            if (!string.IsNullOrWhiteSpace(item.DescriptionAlt))
                sb.AppendLine($"- **Alt Description**: {UnescapeUnicode(item.DescriptionAlt)}");
            sb.AppendLine($"- **Weight**: {item.Weight:F2}");
            sb.AppendLine($"- **Value**: {item.MonetaryValue} (identified: {item.MonetaryValueAlt})");
            sb.AppendLine($"- **Durability**: {item.Durability}");
            sb.AppendLine($"- **Stack Limit**: {item.StackLimit}");

            if (!string.IsNullOrWhiteSpace(item.Properties))
                sb.AppendLine($"- **Properties**: {ResolveNames(item.Properties, itemProps, ",")}");
            if (!string.IsNullOrWhiteSpace(item.EquipConditions))
                sb.AppendLine($"- **Equip Conditions**: {ResolveNames(item.EquipConditions, conditions, ",", "{id}x{mult}")}");
            if (!string.IsNullOrWhiteSpace(item.PossessConditions))
                sb.AppendLine($"- **Possess Conditions**: {ResolveNames(item.PossessConditions, conditions, ",", "{id}x{mult}")}");
            if (!string.IsNullOrWhiteSpace(item.UseConditions))
                sb.AppendLine($"- **Use Conditions**: {ResolveNames(item.UseConditions, conditions, ",", "{id}x{mult}")}");
            if (!string.IsNullOrWhiteSpace(item.TreasureId) && treasureTables.TryGetValue(ParseIntId(item.TreasureId), out var tt))
                sb.AppendLine($"- **Treasure Table**: {tt.Name} (id={item.TreasureId})");
            if (!string.IsNullOrWhiteSpace(item.AttackModes))
                sb.AppendLine($"- **Attack Modes**: {ResolveNames(item.AttackModes, attackModes, ",")}");
            if (!string.IsNullOrWhiteSpace(item.FormatId) && containerTypes.TryGetValue(ParseIntId(item.FormatId), out var ct))
                sb.AppendLine($"- **Container**: {ct.Name} (id={item.FormatId})");
            if (!string.IsNullOrWhiteSpace(item.ChargeProfiles))
                sb.AppendLine($"- **Charge Profiles**: {ResolveNames(item.ChargeProfiles, chargeProfiles, ",")}");

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
        _logger.LogInformation("Exported item encyclopedia to {Path}", outputPath);
    }

    /// <summary>
    /// Export loot tables as JSON.
    /// </summary>
    public async Task ExportLootTableJsonAsync(string outputPath)
    {
        await EnsureGameDataLoadedAsync();
        await using var db = await _gameDbFactory.CreateDbContextAsync();
        var treasureTables = ToDedupedDict<TreasureTable, int>(db.Set<TreasureTable>().ToList(), t => t.Id);
        var itemTypes = db.Set<ItemType>().ToList();
        var itemTypeMap = new Dictionary<string, ItemType>();
        foreach (var it in itemTypes)
        {
            var key = $"{it.GroupId}.{it.SubgroupId}";
            if (!itemTypeMap.ContainsKey(key) || it.ModId > itemTypeMap[key].ModId)
                itemTypeMap[key] = it;
        }

        var result = new List<JsonTreasureTable>();
        var visited = new HashSet<int>();

        foreach (var (id, table) in treasureTables)
        {
            visited.Clear();
            result.Add(BuildJsonTreasureTable(table, treasureTables, itemTypeMap, visited, 0));
        }

        var json = JsonSerializer.Serialize(new { TreasureTables = result }, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(outputPath, json, Encoding.UTF8);
        _logger.LogInformation("Exported loot table JSON to {Path}", outputPath);
    }

    /// <summary>
    /// Export all entity types as XLSX with separate sheets per type.
    /// </summary>
    public async Task ExportAllToXlsxAsync(string outputPath)
    {
        await EnsureGameDataLoadedAsync();
        await using var db = await _gameDbFactory.CreateDbContextAsync();

        var tables = new Dictionary<Type, List<IEntity>>();
        var refLookups = new Dictionary<Type, System.Collections.IList>();
        foreach (var (typeName, type) in Constants.GameTypes.OrderBy(kv => kv.Key))
        {
            var method = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(type);
            var dbSet = (System.Collections.IEnumerable)method.Invoke(db, null)!;
            var list = dbSet.Cast<IEntity>().ToList();
            tables[type] = list;
            refLookups[type] = list;
        }

        using var workbook = new XlsxWriter(outputPath);
        foreach (var (entityType, entityList) in tables.OrderBy(kv => kv.Key.Name))
        {
            if (entityList.Count == 0) continue;
            var typeName = entityType.GetCustomAttribute<TableAttribute>()?.Name ?? entityType.Name;
            var props = entityType.GetProperties()
                .Where(p => p.GetCustomAttribute<ColumnAttribute>() is not null
                            && p.DeclaringType != typeof(IEntity))
                .OrderBy(p => p.MetadataToken)
                .ToList();

            // Prepend MergeId header
            var headers = new List<string> { "→Id" };
            headers.AddRange(props.Select(p => p.GetCustomAttribute<ColumnAttribute>()!.Name!));

            // Build rows with MergeId + resolved Subject for reference fields
            var rows = entityList.Select(entity =>
            {
                var row = new List<string> { GenericDataGridHelper.GetEntityMergedId(entity).ToString() };
                foreach (var p in props)
                {
                    var val = p.GetValue(entity);
                    var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
                    if (refAttr is not null && val is string refStr && !string.IsNullOrWhiteSpace(refStr))
                    {
                        // Show both raw value and resolved names
                        var resolved = ResolveReferenceDisplay(refStr, refAttr, refLookups);
                        row.Add(string.IsNullOrEmpty(resolved) ? (val?.ToString() ?? "") : resolved);
                    }
                    else
                    {
                        var strVal = val is bool b ? (b ? "1" : "0") : val?.ToString() ?? "";
                        row.Add(UnescapeUnicode(strVal));
                    }
                }
                return row.ToArray();
            }).ToList();

            workbook.AddSheet(typeName, headers.ToArray(), rows);
        }
        workbook.Save();

        _logger.LogInformation("Exported XLSX to {Path}", outputPath);
    }

    private static async Task WriteOutput(string outputPath, string sheetName,
        List<string[]> rows, Func<string[], string> formatRow)
    {
        if (outputPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            using var workbook = new XlsxWriter(outputPath);
            workbook.AddSheet(sheetName, rows[0], rows.Skip(1).ToList());
            workbook.Save();
        }
        else
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
                sb.AppendLine(formatRow(row));
            await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
        }
    }

    private static string FormatIngredientList(string raw, Dictionary<int, Ingredient> ingredients)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return string.Join("; ", raw.Split('+').Select(part =>
        {
            var parts = part.Trim().Split('x');
            if (parts.Length < 2) return part;
            var qty = parts[0].Trim();
            var id = int.TryParse(parts[1].Trim(), out var ingId) ? ingId : 0;
            var name = ingredients.TryGetValue(id, out var ing) ? ing.Name : $"Ingredient#{id}";
            return $"{name} x{qty}";
        }));
    }

    private static string ResolveTreasureProduct(string treasureId, Dictionary<int, TreasureTable> treasureTables, List<ItemType> itemTypes)
    {
        if (!int.TryParse(treasureId, out var ttId) || !treasureTables.TryGetValue(ttId, out var table))
            return $"TreasureTable#{treasureId}";
        if (string.IsNullOrWhiteSpace(table.Treasures)) return table.Name;
        return string.Join("; ", table.Treasures.Split(',').Take(3).Select(seg =>
        {
            var parts = seg.Trim().Split('x');
            if (parts.Length < 2) return seg;
            var itemId = parts[0];
            var decomposer = ReferenceHelper.ParseTargetKey("{GroupId}.{SubgroupId}");
            var item = itemTypes.FirstOrDefault(it =>
            {
                var decomposed = ReferenceHelper.DecomposeId(itemId, decomposer);
                return decomposed.TryGetValue("GroupId", out var gid) && it.GroupId == gid
                    && decomposed.TryGetValue("SubgroupId", out var sid) && it.SubgroupId == sid;
            });
            return item?.Name ?? $"ItemType({itemId})";
        }));
    }

    private static string ResolveNames<T>(string raw, Dictionary<int, T> lookup, string separator,
        string? pattern = null) where T : IEntity
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return string.Join(", ", raw.Split(separator).Select(seg =>
        {
            // Extract the actual ID using the pattern (e.g., "{id}x{mult}" → extract part before first x)
            var actualId = ReferenceHelper.ExtractRawId(seg.Trim(), pattern);
            var val = CsvImportExportService.ConvertValue(actualId, typeof(int));
            var id = val is int i ? i : 0;
            if (id > 0 && lookup.TryGetValue(id, out var entity))
                return entity.Subject;
            return seg.Trim();
        }));
    }

    /// <summary>Unescape \uXXXX sequences in strings from game XML data.</summary>
    private static string UnescapeUnicode(string s)
    {
        if (string.IsNullOrEmpty(s) || !s.Contains("\\u"))
            return s;
        try { return Regex.Unescape(s); }
        catch { return s; }
    }

    private static int ParseIntId(string raw)
    {
        return int.TryParse(raw, out var i) ? i : 0;
    }

    private static JsonTreasureTable BuildJsonTreasureTable(TreasureTable table,
        Dictionary<int, TreasureTable> allTables,
        Dictionary<string, ItemType> itemTypeMap,
        HashSet<int> visited,
        int depth)
    {
        var result = new JsonTreasureTable
        {
            Id = table.Id,
            Name = table.Name,
            Nested = table.Nested,
            Suppress = table.Suppress,
            OrGroups = new List<JsonOrGroup>()
        };

        if (depth >= 5 || string.IsNullOrWhiteSpace(table.Treasures)) return result;

        var orSegments = table.Treasures.Split('|');
        foreach (var orSeg in orSegments)
        {
            var orGroup = new JsonOrGroup { Items = new List<JsonTreasureItem>() };
            var andSegments = orSeg.Split(',');
            foreach (var seg in andSegments)
            {
                var parts = seg.Trim().Split('x');
                if (parts.Length < 3) continue;

                var itemId = parts[0].Trim();
                var probStr = parts[1].Trim();
                var qtyRange = parts[2].Trim();

                var prob = double.TryParse(probStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 1.0;
                var qtyParts = qtyRange.Split('-');
                var qtyMin = int.TryParse(qtyParts[0], out var qm) ? qm : 1;
                var qtyMax = qtyParts.Length > 1 && int.TryParse(qtyParts[1], out var qmx) ? qmx : qtyMin;

                var item = new JsonTreasureItem
                {
                    ItemId = itemId,
                    Probability = prob,
                    QuantityMin = qtyMin,
                    QuantityMax = qtyMax
                };

                if (itemTypeMap.TryGetValue(itemId, out var resolvedItem))
                {
                    item.ItemName = resolvedItem.Name;
                    if (!string.IsNullOrWhiteSpace(resolvedItem.TreasureId)
                        && int.TryParse(resolvedItem.TreasureId, out var nestedTtId)
                        && allTables.TryGetValue(nestedTtId, out var nestedTable)
                        && !visited.Contains(nestedTtId)
                        && depth < 5)
                    {
                        visited.Add(nestedTtId);
                        item.NestedTreasure = BuildJsonTreasureTable(nestedTable, allTables, itemTypeMap, visited, depth + 1);
                    }
                }

                orGroup.Items.Add(item);
            }
            result.OrGroups.Add(orGroup);
        }

        return result;
    }

    private static string ResolveReferenceDisplay(string raw, ReferenceFieldAttribute refAttr,
        Dictionary<Type, System.Collections.IList> lookups)
    {
        var targetType = refAttr.TargetEntityType;
        if (!lookups.TryGetValue(targetType, out var list) || list is null)
            return raw;

        var separator = refAttr.Separator;
        if (separator is null) return ResolveSingleRef(raw, list, refAttr.TargetKey, refAttr.Pattern);

        var parts = raw.Split(separator);
        return string.Join(separator == "|" ? " | " : separator == "&" ? " & " : ", ",
            parts.Select(p => ResolveSingleRef(p.Trim(), list, refAttr.TargetKey, refAttr.Pattern)));
    }

    private static string ResolveSingleRef(string rawId, System.Collections.IList list, string? targetKey, string? pattern)
    {
        // Extract the actual ID from the segment using the pattern
        var actualId = ReferenceHelper.ExtractRawId(rawId, pattern);
        var keyInfo = ReferenceHelper.ParseTargetKey(targetKey);
        var decomposed = ReferenceHelper.DecomposeId(actualId, keyInfo);
        foreach (var obj in list)
        {
            if (obj is not IEntity entity) continue;
            var match = true;
            foreach (var (key, val) in decomposed)
            {
                var prop = entity.GetType().GetProperty(key, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
                if (prop is null) { match = false; break; }
                var propVal = prop.GetValue(entity);
                if (propVal is int pi && pi != val) { match = false; break; }
                if (propVal is string ps && ps != val.ToString()) { match = false; break; }
            }
            if (match && decomposed.Count > 0) return entity.Subject;
        }
        return rawId;
    }

    private static string CsvEscape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}

/// <summary>
/// Lightweight XLSX writer using raw XML (no external dependency).
/// Produces valid .xlsx files readable by Excel, LibreOffice, etc.
/// </summary>
public class XlsxWriter : IDisposable
{
    private readonly string _outputPath;
    private readonly List<SheetData> _sheets = [];
    private bool _disposed;

    private record SheetData(string Name, string[] Headers, List<string[]> Rows);

    public XlsxWriter(string outputPath)
    {
        _outputPath = outputPath;
    }

    public void AddSheet(string name, string[] headers, List<string[]> rows)
    {
        // Sanitize sheet name (max 31 chars, no special chars)
        name = new string(name.Where(c => !@"\/*?:[]".Contains(c)).ToArray());
        if (name.Length > 31) name = name[..31];
        if (_sheets.Any(s => s.Name == name))
            name = $"{name}_{_sheets.Count}";
        _sheets.Add(new SheetData(name, headers, rows));
    }

    public void Save()
    {
        if (_disposed) return;
        var tempDir = Path.Combine(Path.GetTempPath(), $"xlsx_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // [Content_Types].xml
            var contentTypes = new StringBuilder();
            contentTypes.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            contentTypes.AppendLine("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            contentTypes.AppendLine("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            contentTypes.AppendLine("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            contentTypes.AppendLine("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            for (int i = 0; i < _sheets.Count; i++)
                contentTypes.AppendLine($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            contentTypes.AppendLine("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
            contentTypes.AppendLine("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            contentTypes.AppendLine("</Types>");
            File.WriteAllText(Path.Combine(tempDir, "[Content_Types].xml"), contentTypes.ToString(), Encoding.UTF8);

            // _rels/.rels
            Directory.CreateDirectory(Path.Combine(tempDir, "_rels"));
            File.WriteAllText(Path.Combine(tempDir, "_rels", ".rels"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>", Encoding.UTF8);

            // xl/workbook.xml
            Directory.CreateDirectory(Path.Combine(tempDir, "xl"));
            var wbXml = new StringBuilder();
            wbXml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            wbXml.AppendLine("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            wbXml.AppendLine("<sheets>");
            for (int i = 0; i < _sheets.Count; i++)
                wbXml.AppendLine($"<sheet name=\"{EscapeXml(_sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            wbXml.AppendLine("</sheets>");
            wbXml.AppendLine("</workbook>");
            File.WriteAllText(Path.Combine(tempDir, "xl", "workbook.xml"), wbXml.ToString(), Encoding.UTF8);

            // xl/_rels/workbook.xml.rels
            Directory.CreateDirectory(Path.Combine(tempDir, "xl", "_rels"));
            var relsXml = new StringBuilder();
            relsXml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            relsXml.AppendLine("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 0; i < _sheets.Count; i++)
                relsXml.AppendLine($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
            relsXml.AppendLine("<Relationship Id=\"rIdSharedStrings\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");
            relsXml.AppendLine("<Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            relsXml.AppendLine("</Relationships>");
            File.WriteAllText(Path.Combine(tempDir, "xl", "_rels", "workbook.xml.rels"), relsXml.ToString(), Encoding.UTF8);

            // xl/styles.xml (minimal)
            File.WriteAllText(Path.Combine(tempDir, "xl", "styles.xml"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
                "</styleSheet>", Encoding.UTF8);

            // Build shared strings and sheet data
            var sharedStrings = new Dictionary<string, int>();
            var stringId = 0;

            int GetOrAddString(string s)
            {
                if (sharedStrings.TryGetValue(s, out var id)) return id;
                sharedStrings[s] = stringId;
                return stringId++;
            }

            // xl/worksheets/sheetN.xml
            Directory.CreateDirectory(Path.Combine(tempDir, "xl", "worksheets"));
            for (int si = 0; si < _sheets.Count; si++)
            {
                var sheet = _sheets[si];
                var sheetXml = new StringBuilder();
                sheetXml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sheetXml.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                sheetXml.AppendLine("<sheetData>");

                // Header row
                sheetXml.AppendLine("<row r=\"1\">");
                for (int c = 0; c < sheet.Headers.Length; c++)
                {
                    var col = GetColumnLetter(c);
                    var ssId = GetOrAddString(sheet.Headers[c]);
                    sheetXml.AppendLine($"<c r=\"{col}1\" t=\"s\"><v>{ssId}</v></c>");
                }
                sheetXml.AppendLine("</row>");

                // Data rows
                for (int r = 0; r < sheet.Rows.Count; r++)
                {
                    sheetXml.AppendLine($"<row r=\"{r + 2}\">");
                    for (int c = 0; c < Math.Min(sheet.Rows[r].Length, sheet.Headers.Length); c++)
                    {
                        var col = GetColumnLetter(c);
                        var val = sheet.Rows[r][c] ?? "";
                        var ssId = GetOrAddString(val);
                        sheetXml.AppendLine($"<c r=\"{col}{r + 2}\" t=\"s\"><v>{ssId}</v></c>");
                    }
                    sheetXml.AppendLine("</row>");
                }

                sheetXml.AppendLine("</sheetData>");
                sheetXml.AppendLine("</worksheet>");
                File.WriteAllText(Path.Combine(tempDir, "xl", "worksheets", $"sheet{si + 1}.xml"), sheetXml.ToString(), Encoding.UTF8);
            }

            // xl/sharedStrings.xml
            var ssXml = new StringBuilder();
            ssXml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            ssXml.AppendLine($"<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"{sharedStrings.Count}\" uniqueCount=\"{sharedStrings.Count}\">");
            foreach (var (str, _) in sharedStrings.OrderBy(kv => kv.Value))
                ssXml.AppendLine($"<si><t>{EscapeXml(str)}</t></si>");
            ssXml.AppendLine("</sst>");
            File.WriteAllText(Path.Combine(tempDir, "xl", "sharedStrings.xml"), ssXml.ToString(), Encoding.UTF8);

            // Create ZIP
            if (File.Exists(_outputPath)) File.Delete(_outputPath);
            System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, _outputPath, System.IO.Compression.CompressionLevel.Optimal, false);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static string GetColumnLetter(int index)
    {
        if (index < 26) return ((char)('A' + index)).ToString();
        return $"{(char)('A' + index / 26 - 1)}{(char)('A' + index % 26)}";
    }

    private static string EscapeXml(string s)
    {
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

public class JsonTreasureTable
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Nested { get; set; }
    public bool Suppress { get; set; }
    public List<JsonOrGroup> OrGroups { get; set; } = [];
}

public class JsonOrGroup
{
    public List<JsonTreasureItem> Items { get; set; } = [];
}

public class JsonTreasureItem
{
    public string ItemId { get; set; } = "";
    public string? ItemName { get; set; }
    public double Probability { get; set; }
    public int QuantityMin { get; set; }
    public int QuantityMax { get; set; }
    public JsonTreasureTable? NestedTreasure { get; set; }
}
