using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Player.Core.Services;

namespace NeoEditor.Player.Core.Data;

/// <summary>
/// Incoming-reference analysis over the merged catalog (Docs/42 v2.24): for a target row,
/// find every row/column that references it. Uses the same [ReferenceField] metadata as the
/// wiki detail builder, with a lazy lookup index so the whole catalog is scanned once.
/// Image columns are skipped (they have no real target table).
/// </summary>
public sealed class ReferenceAnalyzer
{
    /// <summary>One place that references the target row.</summary>
    public sealed record ReferenceHit(GameDataRow SourceRow, string Column);

    private readonly GameDataCatalog _catalog;
    private readonly Dictionary<string, List<RefColumn>> _refColumns;
    private readonly Lazy<(Dictionary<string, GameDataRow> ByKey, Dictionary<string, List<GameDataRow>> ByValue)> _index;

    public ReferenceAnalyzer(GameDataCatalog catalog)
    {
        _catalog = catalog;
        _refColumns = ReferenceMetadata.Build();
        _index = new Lazy<(Dictionary<string, GameDataRow>, Dictionary<string, List<GameDataRow>>)>(BuildIndex, true);
    }

    /// <summary>All rows (and their referencing columns) that point at the target row.</summary>
    public IReadOnlyList<ReferenceHit> FindIncoming(GameDataRow target)
    {
        var (byKey, byValue) = _index.Value;
        var hits = new List<ReferenceHit>();
        var seen = new HashSet<(string Table, string Key, string Column)>();

        foreach (var (tableName, columns) in _refColumns)
        {
            foreach (var row in _catalog.GetRows(tableName))
            {
                foreach (var refColumn in columns)
                {
                    var raw = ReferenceMetadata.Value(row, refColumn.Column);
                    if (raw.Length == 0) continue;

                    foreach (var (id, _, _) in ReferenceMetadata.ParseSegments(raw, refColumn))
                    {
                        if (refColumn.TableName.Length > 0 &&
                            IsTarget(byKey, byValue, refColumn.TableName, id, target))
                            Add(hits, seen, row, refColumn.Column);
                        if (refColumn.SecondaryTableName is { Length: > 0 } secondary &&
                            IsTarget(byKey, byValue, secondary, id, target))
                            Add(hits, seen, row, refColumn.Column);
                    }
                }
            }
        }

        return hits;
    }

    private bool IsTarget(
        Dictionary<string, GameDataRow> byKey,
        Dictionary<string, List<GameDataRow>> byValue,
        string tableName, string id, GameDataRow target)
    {
        var row = Resolve(byKey, byValue, tableName, id);
        // Table name matters too: same RowKey values exist across tables (nID 3 in
        // creatures vs recipes) and must not cross-match.
        return row is not null &&
               string.Equals(row.TableName, target.TableName, StringComparison.OrdinalIgnoreCase) &&
               row.RowKey == target.RowKey;
    }

    private static void Add(List<ReferenceHit> hits, HashSet<(string, string, string)> seen,
        GameDataRow row, string column)
    {
        var key = (row.TableName, row.RowKey, column);
        if (!seen.Add(key)) return;
        hits.Add(new ReferenceHit(row, column));
    }

    /// <summary>
    /// Resolve an id to a row: merge key first, then any exact field value; dotted composite
    /// keys ("G.S" GroupId.SubgroupId style) fall back to a linear scan of the target table.
    /// </summary>
    private GameDataRow? Resolve(
        Dictionary<string, GameDataRow> byKey,
        Dictionary<string, List<GameDataRow>> byValue,
        string tableName, string id)
    {
        if (byKey.TryGetValue($"{tableName}|{id}", out var byKeyRow)) return byKeyRow;
        if (byValue.TryGetValue($"{tableName}|{id}", out var byValueRows) && byValueRows.Count > 0)
            return byValueRows[0];

        if (id.Contains('.'))
        {
            var parts = id.Split('.', 2);
            return _catalog.GetRows(tableName).FirstOrDefault(r =>
                r.Fields.Any(f1 => string.Equals(f1.Value, parts[0], StringComparison.OrdinalIgnoreCase) &&
                                   r.Fields.Any(f2 => string.Equals(f2.Value, parts[1], StringComparison.OrdinalIgnoreCase))));
        }
        return null;
    }

    private (Dictionary<string, GameDataRow> ByKey, Dictionary<string, List<GameDataRow>> ByValue) BuildIndex()
    {
        var byKey = new Dictionary<string, GameDataRow>(StringComparer.OrdinalIgnoreCase);
        var byValue = new Dictionary<string, List<GameDataRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableName in _catalog.TableNames)
        {
            foreach (var row in _catalog.GetRows(tableName))
            {
                byKey[$"{tableName}|{row.RowKey}"] = row;
                foreach (var field in row.Fields)
                {
                    if (field.Value.Length == 0) continue;
                    var key = $"{tableName}|{field.Value}";
                    if (!byValue.TryGetValue(key, out var list))
                    {
                        list = [];
                        byValue[key] = list;
                    }
                    list.Add(row);
                }
            }
        }
        return (byKey, byValue);
    }
}
