using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace NeoEditor.Player.Core.Data;

/// <summary>
/// Wiki-style detail page generator (Docs/42 v2.22 → v2.24): renders one merged game-data
/// row as Markdown for LiveMarkdown.Avalonia. Generic tables get a field table with
/// cross-table reference links (db://table/key); recipes get a crafting card and
/// treasuretable a loot probability tree. v2.24 adds: an image gallery (image reference
/// columns → img/*.png grid) and an incoming-references section ("who references this row").
/// Pure data transformation — no UI dependencies.
/// </summary>
public sealed class WikiDetailBuilder
{
    private const int MaxTreasureDepth = 5;
    private const int MaxValueLength = 300;
    private const int GalleryColumns = 3;

    private readonly GameDataCatalog _catalog;
    private readonly Dictionary<string, List<RefColumn>> _refColumns;
    private readonly ReferenceAnalyzer _analyzer;
    private readonly List<string> _imageDirs = [];

    /// <param name="imageRoot">Game root dir — used to check img/*.png existence for the gallery.</param>
    public WikiDetailBuilder(GameDataCatalog catalog, string? imageRoot = null)
    {
        _catalog = catalog;
        _refColumns = ReferenceMetadata.Build();
        _analyzer = new ReferenceAnalyzer(catalog);
        // R54-R56: 图片来源 = 主 {gameRoot}/img + 各 mod 的根目录/img 子目录。
        // mod 路径**来自游戏自带 getmods.php**（strModURL{i}，如 Mods/<分组>/<mod>，
        // 见 ProxyHttpModule/ModListScanner）——不存在的固定约定；getmods.php 缺失时
        // 兜底扫 Mods/*/*（两层，与 ModListScanner 一致）。启动时扫一次缓存。
        if (imageRoot is { } root)
        {
            _imageDirs.AddRange(CollectImageDirs(root));
        }
    }

    private static List<string> CollectImageDirs(string root)
    {
        var dirs = new List<string>();
        dirs.Add(Path.Combine(root, "img"));
        try
        {
            // mod 路径来自游戏 getmods*.php（strModURL{i}）。注意：磁盘上的 getmods.php
            // 可能是空壳（nRows=0，如用户目录），真正生效的是 getmods2.php——两个都读，
            // 任一解析出路径即用；都空才走 Mods/*/* 两层扫描兜底（ModListScanner 同款）。
            var modPaths = new List<string>();
            foreach (var file in new[] { "getmods.php", "getmods2.php" })
            {
                var php = Path.Combine(root, file);
                if (File.Exists(php))
                    modPaths.AddRange(ParseModUrls(File.ReadAllText(php)));
            }

            if (modPaths.Count > 0)
            {
                foreach (var modPath in modPaths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var dir = Path.Combine(root, modPath);
                    dirs.Add(dir);                                  // mod 根目录
                    var img = Path.Combine(dir, "img");
                    if (Directory.Exists(img)) dirs.Add(img);
                }
                return dirs;
            }

            // 兜底：Mods/<分组>/<mod> 两层扫描
            var modsRoot = Path.Combine(root, "Mods");
            if (Directory.Exists(modsRoot))
            {
                foreach (var category in Directory.EnumerateDirectories(modsRoot))
                {
                    foreach (var modDir in Directory.EnumerateDirectories(category))
                    {
                        dirs.Add(modDir);
                        var img = Path.Combine(modDir, "img");
                        if (Directory.Exists(img)) dirs.Add(img);
                    }
                }
            }
        }
        catch (Exception)
        {
            // 只读扫描失败不影响详情页
        }
        return dirs;
    }

    /// <summary>getmods.php → strModURL{i} 路径列表（query-string 格式；文件可能多行，
    /// 值末尾带换行——必须 Trim，否则拼出的路径带 \n 导致目录查找失败）。</summary>
    private static List<string> ParseModUrls(string php)
    {
        var result = new List<string>();
        foreach (var pair in php.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            if (pair[..eq].StartsWith("strModURL", StringComparison.OrdinalIgnoreCase))
                result.Add(Uri.UnescapeDataString(pair[(eq + 1)..]).Trim());
        }
        return result;
    }

    /// <summary>Render the row as a Markdown wiki page (detail + field table + gallery + refs).</summary>
    public string Build(GameDataRow row)
    {
        var sb = new StringBuilder();
        sb.Append(BuildDetail(row));
        AppendFieldTable(sb, row);
        AppendImageGallery(sb, row);
        sb.Append(BuildReferences(row));
        return sb.ToString();
    }

    /// <summary>Detail page body only (recipe card / loot tree / header) — the field table,
    /// gallery and references are rendered as UI controls (v2.34).</summary>
    public string BuildDetail(GameDataRow row) => row.TableName switch
    {
        "recipes" => BuildRecipe(row),
        "treasuretable" => BuildTreasureTable(row),
        _ => BuildGeneric(row),
    };

    /// <summary>
    /// Field table for the UI grid (v2.34): RAW values with multi-line content preserved
    /// (markdown tables cannot hold line breaks). Image columns are excluded (the carousel
    /// shows them) and recipes exclude fields already shown by the crafting card.
    /// Reference columns resolve to clickable links (raw ids kept when unresolvable).
    /// </summary>
    public IReadOnlyList<FieldItem> GetFields(GameDataRow row)
    {
        var excluded = row.TableName == "recipes" ? RecipeExcludedColumns : null;
        var refs = ReferenceColumns(row.TableName);
        return row.Fields
            .Where(f => !IsImageColumn(row.TableName, f.Column)
                        && (excluded is null || !excluded.Contains(f.Column)))
            .Select(f => BuildFieldItem(f, refs))
            .ToList();
    }

    private FieldItem BuildFieldItem(GameDataField field, IReadOnlyList<RefColumn>? refs)
    {
        var refColumn = refs?.FirstOrDefault(c =>
            string.Equals(c.Column, field.Column, StringComparison.OrdinalIgnoreCase));
        if (refColumn is not null)
        {
            var links = ReferenceMetadata.ParseSegments(field.Value, refColumn)
                .Select(seg => ResolveLink(refColumn.TableName, seg.Id))
                .ToList();
            if (links.Count > 0)
                return new FieldItem(field.Column, field.Value) { Links = links };
        }
        return new FieldItem(field.Column, field.Value);
    }

    /// <summary>Resolve one reference id to a display name + db:// target (raw id when
    /// unresolvable — shown as plain text, not a link).</summary>
    private FieldLink ResolveLink(string tableName, string id)
    {
        var row = _catalog.FindRow(tableName, id);
        return row is null
            ? new FieldLink(id, null)
            : new FieldLink(DisplayName(row), $"db://{tableName}/{Uri.EscapeDataString(id)}");
    }

    private bool IsImageColumn(string tableName, string column)
        => _refColumns.TryGetValue(tableName, out var list)
           && list.Any(c => c.IsImage && string.Equals(c.Column, column, StringComparison.OrdinalIgnoreCase));

    /// <summary>Legacy markdown field table for the full-page <see cref="Build"/> output —
    /// line breaks are flattened (UI uses <see cref="GetFields"/> instead).</summary>
    private void AppendFieldTable(StringBuilder sb, GameDataRow row)
    {
        var fields = GetFields(row);
        if (fields.Count == 0) return;

        sb.AppendLine("## 字段");
        sb.AppendLine("| 列 | 值 |");
        sb.AppendLine("|---|---|");
        foreach (var field in fields)
        {
            string rendered;
            if (field.Links is { Count: > 0 })
            {
                rendered = string.Join(" · ", field.Links.Select(l =>
                    l.Target is null ? $"`{Escape(l.Display)}`" : $"[{Escape(l.Display)}]({l.Target})"));
            }
            else
            {
                var value = field.Value.Trim().Replace("\r", " ").Replace("\n", " ");
                if (value.Length > MaxValueLength) value = value[..MaxValueLength] + "…";
                rendered = Escape(value);
            }
            sb.AppendLine($"| `{Escape(field.Column)}` | {rendered} |");
        }
    }

    /// <summary>"Who references this row" markdown section ("" when none).</summary>
    public string BuildReferences(GameDataRow row)
    {
        var groups = BuildReferenceGroups(row);
        if (groups.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine($"## 被引用（{groups.Sum(g => g.Count)}）");
        foreach (var group in groups)
        {
            sb.AppendLine($"### {Escape(group.TableName)}");
            sb.Append(group.Markdown);
        }
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// "Who references this row" grouped by source table — one group per table for the
    /// tabbed display (v2.32). Each group carries its markdown line list and the count.
    /// </summary>
    public IReadOnlyList<ReferenceGroup> BuildReferenceGroups(GameDataRow row)
        => _analyzer.FindIncoming(row)
            .GroupBy(h => h.SourceRow.TableName)
            .Select(g => new ReferenceGroup(g.Key, RenderReferenceLines(g), g.Count()))
            .ToList();

    private static string RenderReferenceLines(IEnumerable<ReferenceAnalyzer.ReferenceHit> hits)
    {
        var sb = new StringBuilder();
        foreach (var hit in hits)
            sb.AppendLine($"- {RowLink(hit.SourceRow)} — `{Escape(hit.Column)}`");
        return sb.ToString();
    }

    /// <summary>
    /// Gallery images for the row: image reference columns resolved to img/*.png files
    /// (FullPath null → missing on disk; the UI renders those as text).
    /// </summary>
    public IReadOnlyList<WikiImage> GetImageItems(GameDataRow row)
    {
        var images = new List<WikiImage>();
        if (!_refColumns.TryGetValue(row.TableName, out var columns)) return images;
        foreach (var refColumn in columns.Where(c => c.IsImage))
        {
            var raw = Value(row, refColumn.Column);
            if (raw.Length == 0) continue;
            foreach (var (id, _, _) in ReferenceMetadata.ParseSegments(raw, refColumn))
            {
                var fileName = StripNamespace(id);
                images.Add(new WikiImage(fileName, ResolveImagePath(fileName)));
            }
        }
        return images;
    }

    private string? ResolveImagePath(string fileName)
    {
        foreach (var dir in _imageDirs)
        {
            var path = Path.Combine(dir, fileName);
            if (!File.Exists(path) && !fileName.Contains('.'))
                path = Path.Combine(dir, fileName + ".png");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    // ── helpers ──

    private static string Value(GameDataRow row, string column) => ReferenceMetadata.Value(row, column);

    private RefColumn? RefFor(string tableName, string column)
        => _refColumns.TryGetValue(tableName, out var list)
            ? list.FirstOrDefault(c => string.Equals(c.Column, column, StringComparison.OrdinalIgnoreCase))
            : null;

    private IReadOnlyList<RefColumn>? ReferenceColumns(string tableName)
        => _refColumns.TryGetValue(tableName, out var list) ? list : null;

    private static string DisplayName(GameDataRow row)
    {
        foreach (var column in new[] { "strName", "name", "Subject", "strSubject" })
        {
            var value = Value(row, column);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        var first = row.Fields.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.Value));
        return first is null ? $"#{row.RowKey}" : first.Value;
    }

    private static string Escape(string text)
        => text.Replace("\\", "\\\\").Replace("|", "\\|").Replace("[", "\\[")
            .Replace("]", "\\]").Replace("*", "\\*").Replace("_", "\\_").Replace("`", "\\`");

    private string? LinkFor(string tableName, string id)
    {
        var row = _catalog.FindRow(tableName, id);
        if (row is null) return null;
        // The link key is the referenced value itself (raw id) — navigating re-resolves it.
        return $"[{Escape(DisplayName(row))}](db://{tableName}/{Uri.EscapeDataString(id)})";
    }

    private static string RowLink(GameDataRow row)
        => $"[{Escape(DisplayName(row))}](db://{row.TableName}/{Uri.EscapeDataString(row.RowKey)})";

    private static bool IsTrue(string value)
        => value is "1" or "true" or "True" or "TRUE";

    private static string FormatPercent(double prob)
        // P1 with the invariant culture yields "25.0 %" (space before %) — strip it so the
        // displayed percentage matches what the game/editor show ("25.0%").
        => prob.ToString("P1", CultureInfo.InvariantCulture).Replace(" ", "");

    // ── generic table ──

    private string BuildGeneric(GameDataRow row)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {Escape(DisplayName(row))}");
        sb.AppendLine($"> `{Escape(row.TableName)}` · ID `{Escape(row.RowKey)}`");
        sb.AppendLine();
        return sb.ToString();
    }

    // ── recipes ──

    private string BuildRecipe(GameDataRow row)
    {
        var sb = new StringBuilder();
        var name = Value(row, "strName");
        var id = Value(row, "nID");
        sb.AppendLine($"# {Escape(name.Length > 0 ? name : $"#{id}")}");

        var meta = new List<string> { $"ID `{Escape(id)}`" };
        var type = Value(row, "strType");
        if (type.Length > 0) meta.Add(Escape(type));
        var flags = new List<string>();
        if (IsTrue(Value(row, "bIdentify"))) flags.Add("识别");
        if (IsTrue(Value(row, "bScrap"))) flags.Add("可拆解");
        if (IsTrue(Value(row, "bDegradeOutput"))) flags.Add("产物降级");
        if (IsTrue(Value(row, "bTransferComponents"))) flags.Add("转移组件");
        if (flags.Count > 0) meta.Add(string.Join("·", flags));
        var hours = Value(row, "fHours");
        if (hours.Length > 0) meta.Add($"耗时 {hours}h");
        sb.AppendLine($"> {string.Join(" · ", meta)}");
        sb.AppendLine();

        AppendIngredientGroup(sb, row, "strTools", "## 工具");
        AppendIngredientGroup(sb, row, "strConsumed", "## 消耗");
        AppendIngredientGroup(sb, row, "strDestroyed", "## 破坏");

        var treasureId = Value(row, "nTreasureID");
        if (treasureId.Length > 0)
        {
            sb.AppendLine("## 产物");
            sb.AppendLine($"- {LinkFor("treasuretable", treasureId) ?? $"`{Escape(treasureId)}`"}");
            AppendLootPreview(sb, treasureId);
            sb.AppendLine();
        }
        var tempTreasureId = Value(row, "nTempTreasureID");
        if (tempTreasureId.Length > 0 && tempTreasureId != "3" &&
            !string.Equals(tempTreasureId, treasureId, StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("## 临时产物预览");
            sb.AppendLine($"- {LinkFor("treasuretable", tempTreasureId) ?? $"`{Escape(tempTreasureId)}`"}");
            sb.AppendLine();
        }

        AppendReferenceList(sb, row, "vAlsoTry", "## 替代配方");
        AppendReferenceList(sb, row, "nHiddenID", "## 隐藏关联");
        return sb.ToString();
    }

    /// <summary>Recipe columns already shown by the crafting card — excluded from the
    /// field grid (v2.34).</summary>
    private static readonly HashSet<string> RecipeExcludedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "nID", "strName", "strType", "fHours", "strTools", "strConsumed", "strDestroyed",
        "nTreasureID", "nTempTreasureID", "vAlsoTry", "nHiddenID",
        "bIdentify", "bScrap", "bDegradeOutput", "bTransferComponents",
    };

    private void AppendIngredientGroup(StringBuilder sb, GameDataRow row, string column, string heading)
    {
        var raw = Value(row, column);
        if (raw.Length == 0) return;
        var refColumn = RefFor(row.TableName, column);
        if (refColumn is null) return;
        sb.AppendLine(heading);
        foreach (var (id, mult, _) in ReferenceMetadata.ParseSegments(raw, refColumn))
        {
            var qty = mult is { Length: > 0 } and not "1" ? $" ×{mult}" : "";
            sb.AppendLine($"- {LinkFor("ingredients", id) ?? $"`{Escape(id)}`"}{qty}");
        }
        sb.AppendLine();
    }

    private void AppendLootPreview(StringBuilder sb, string treasureId)
    {
        var tt = _catalog.FindRow("treasuretable", treasureId);
        if (tt is null) return;
        var refColumn = RefFor("treasuretable", "aTreasures");
        if (refColumn is null) return;
        var previews = ReferenceMetadata.ParseSegments(Value(tt, "aTreasures"), refColumn)
            .Select(seg => LinkFor("itemtypes", seg.Id))
            .Where(x => x is not null)
            .Take(6)
            .ToList();
        if (previews.Count > 0)
            sb.AppendLine($"  掉落预览：{string.Join("、", previews)}");
    }

    private void AppendReferenceList(StringBuilder sb, GameDataRow row, string column, string heading)
    {
        var raw = Value(row, column);
        if (raw.Length == 0) return;
        var refColumn = RefFor(row.TableName, column);
        if (refColumn is null) return;
        sb.AppendLine(heading);
        var items = ReferenceMetadata.ParseSegments(raw, refColumn)
            .Select(seg => LinkFor(refColumn.TableName, seg.Id) ?? $"`{Escape(seg.Id)}`")
            .ToList();
        sb.AppendLine(string.Join(" · ", items));
        sb.AppendLine();
    }

    // ── treasuretable ──

    private string BuildTreasureTable(GameDataRow row)
    {
        var sb = new StringBuilder();
        var name = Value(row, "strName");
        var id = Value(row, "id");
        sb.AppendLine($"# {Escape(name.Length > 0 ? name : $"#{id}")}");

        var meta = new List<string> { $"ID `{Escape(id)}`" };
        var flags = new List<string>();
        if (IsTrue(Value(row, "bNested"))) flags.Add("Nested");
        if (IsTrue(Value(row, "bSuppress"))) flags.Add("Suppress");
        if (IsTrue(Value(row, "bIdentify"))) flags.Add("Identify");
        if (flags.Count > 0) meta.Add(string.Join("·", flags));
        sb.AppendLine($"> {string.Join(" · ", meta)}");
        sb.AppendLine();

        var raw = Value(row, "aTreasures");
        if (raw.Length == 0)
        {
            sb.AppendLine("_（无掉落条目）_");
            return sb.ToString();
        }

        sb.AppendLine("## 掉落物");
        AppendTreasureItems(sb, raw, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { row.RowKey });
        return sb.ToString();
    }

    private void AppendTreasureItems(StringBuilder sb, string raw, int depth, HashSet<string> visited)
    {
        if (depth > MaxTreasureDepth)
        {
            sb.AppendLine($"{Indent(depth)}- …（嵌套过深）");
            return;
        }
        var refColumn = RefFor("treasuretable", "aTreasures");
        if (refColumn is null) return;

        var items = ReferenceMetadata.ParseSegments(raw, refColumn).ToList();
        var totalWeight = items.Sum(item => ParseWeight(item.Mult));
        foreach (var (id, mult, qty) in items)
        {
            var prob = totalWeight > 0 ? ParseWeight(mult) / totalWeight : 1.0 / Math.Max(1, items.Count);
            var qtyText = qty is { Length: > 0 } and not "1" ? $" ×{qty}" : "";

            // ItemType composite keys ("G.S") → itemtypes; plain ids → nested TreasureTable.
            var itemTypeRow = id.Contains('.') ? _catalog.FindRow("itemtypes", id) : null;
            if (itemTypeRow is not null)
            {
                sb.AppendLine($"{Indent(depth)}- **{LinkFor("itemtypes", id) ?? Escape(id)}** — `{FormatPercent(prob)}`{qtyText}");
                continue;
            }

            var nested = _catalog.FindRow("treasuretable", id);
            if (nested is not null)
            {
                if (visited.Contains(nested.RowKey))
                {
                    sb.AppendLine($"{Indent(depth)}- **{Escape(DisplayName(nested))}** — `{FormatPercent(prob)}`{qtyText}（循环引用）");
                    continue;
                }
                sb.AppendLine($"{Indent(depth)}- **{LinkFor("treasuretable", id) ?? Escape(id)}** — `{FormatPercent(prob)}`{qtyText}");
                var next = new HashSet<string>(visited) { nested.RowKey };
                AppendTreasureItems(sb, Value(nested, "aTreasures"), depth + 1, next);
                continue;
            }

            sb.AppendLine($"{Indent(depth)}- `{Escape(id)}` — `{FormatPercent(prob)}`{qtyText}（未解析）");
        }
    }

    private static string Indent(int depth) => new(' ', depth * 2);

    private static double ParseWeight(string? mult)
        => double.TryParse(mult, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 1.0;

    // ── image gallery (v2.24) ──

    /// <summary>
    /// Legacy markdown gallery (kept for the full-page <see cref="Build"/> output): image
    /// reference columns (strImg/strIMG/vImageList/vSpriteList → ImageAsset) render as a
    /// markdown image grid — 3 per table row. The dialog UI uses <see cref="GetImageItems"/>
    /// with native Image controls instead (v2.26 — markdown table-cell images were unreliable).
    /// </summary>
    private void AppendImageGallery(StringBuilder sb, GameDataRow row)
    {
        var images = GetImageItems(row)
            .Select(img => img.FullPath is null
                ? $"`{Escape(img.FileName)}`（缺失）"
                : $"![{Escape(img.FileName)}](img/{Uri.EscapeDataString(img.FileName)})")
            .ToList();
        if (images.Count == 0) return;

        sb.AppendLine("## 图片");
        for (var i = 0; i < images.Count; i += GalleryColumns)
            sb.AppendLine("| " + string.Join(" | ", images.Skip(i).Take(GalleryColumns)) + " |");
        sb.AppendLine();
    }

    private static string StripNamespace(string id)
    {
        var colon = id.IndexOf(':');
        return colon >= 0 ? id[(colon + 1)..] : id;
    }
}

/// <summary>One gallery image resolved for a row (FullPath null → file missing on disk).</summary>
public sealed record WikiImage(string FileName, string? FullPath)
{
    public bool Exists => FullPath is not null;

    public string DisplayText => Exists ? FileName : $"{FileName}（缺失）";
}

/// <summary>
/// Incoming references of one source table — one entry per tab in the reference
/// TabControl (v2.32); <see cref="Markdown"/> is the per-table line list.
/// </summary>
public sealed record ReferenceGroup(string TableName, string Markdown, int Count);

/// <summary>One field-table row for the UI grid (v2.34) — raw Value keeps multi-line
/// content; reference columns resolve to <see cref="Links"/> (clickable in the UI).</summary>
public sealed record FieldItem(string Column, string Value)
{
    /// <summary>Resolved reference links (Target null → unresolvable, plain text).</summary>
    public IReadOnlyList<FieldLink>? Links { get; init; }

    public bool ShowRawValue => Links is not { Count: > 0 };
}

/// <summary>One resolved reference link inside a field value (v2.34).</summary>
public sealed record FieldLink(string Display, string? Target);
