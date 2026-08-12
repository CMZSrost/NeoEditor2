using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.JsVisualization.Services;

/// <summary>
/// D09 P1「战利品嵌套树」：TreasureTable → LootTreeDto 纯数据构建。
/// 语义沿 D04 BuildTreasureLootTree / TreasureTableEntityVisualizer.BuildNestedItems：
///  - `|` 与 `,` 都是条目级分隔符；概率必须按**当前表内全部条目**的权重 Σ 归一；
///  - 物品键 = "G.S"（GroupId.SubgroupId）；解析不到则尝试嵌套 TT（递归，depth ≤ 3）；
///  - 嵌套 TT 的概率在嵌套表内独立归一，不与父链相乘；
///  - 未解析条目保留原始 id（灰色 unknown），可审计不静默丢失。
/// 供 ItemType/Creature/Recipe/Encounter 效果区共用。
/// </summary>
public sealed class LootTreeBuilder
{
    private const int MaxDepth = 3;

    private readonly IEntityLookupService _dataTable;
    private readonly IReferenceResolver _resolver;

    public LootTreeBuilder(IEntityLookupService dataTable, IReferenceResolver resolver)
    {
        _dataTable = dataTable;
        _resolver = resolver;
    }

    /// <summary>一张 TT → 树（Title 为表名，可跳转）；空表返回 null。</summary>
    public LootTreeDto? Build(TreasureTable tt)
    {
        var items = BuildItems(tt, 0);
        if (items.Count == 0) return null;
        return new LootTreeDto
        {
            Title = tt.Subject ?? tt.Name ?? $"TT#{tt.Id}",
            TargetType = "TreasureTable",
            TargetId = tt.EntityId,
            Items = items,
        };
    }

    private List<LootNodeDto> BuildItems(TreasureTable tt, int depth)
    {
        var result = new List<LootNodeDto>();
        if (depth > MaxDepth || string.IsNullOrWhiteSpace(SemanticsShared.Raw(tt.Treasures, ","))) return result;

        var itemTypes = TryBuildItemTypes(tt.ModId);

        var allSegs = SemanticsShared.Raw(tt.Treasures, ",").Split('|', ',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && s.Contains('x'))
            .ToList();
        if (allSegs.Count == 0) return result;

        var allParsed = new List<(string ItemId, double Weight, string Qty)>();
        double totalWeight = 0;
        foreach (var seg in allSegs)
        {
            var parts = seg.Split('x');
            if (parts.Length < 2) continue;
            var itemId = parts[0].Trim();
            var weight = double.TryParse(parts[1].Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var w) ? w : 1.0;
            totalWeight += weight;
            allParsed.Add((itemId, weight, parts.Length > 2 ? parts[2].Trim() : "1"));
        }

        foreach (var (itemId, weight, qty) in allParsed)
        {
            var prob = totalWeight > 0 ? weight / totalWeight : 1.0 / allParsed.Count;

            if (itemTypes is not null && itemTypes.TryGetValue(itemId, out var matched))
            {
                result.Add(new LootNodeDto
                {
                    Label = !string.IsNullOrWhiteSpace(matched.Description) ? matched.Description
                        : !string.IsNullOrWhiteSpace(matched.Name) ? matched.Name : itemId,
                    Kind = "item",
                    TargetType = "ItemType",
                    TargetId = matched.EntityId,
                    Weight = weight,
                    Prob = prob,
                    Qty = qty,
                });
                continue;
            }

            var nested = _resolver.LookupRef<TreasureTable>(tt, nameof(TreasureTable.Treasures), itemId);
            if (nested is not null)
            {
                result.Add(new LootNodeDto
                {
                    Label = nested.Name ?? $"TT#{nested.Id}",
                    Kind = "table",
                    TargetType = "TreasureTable",
                    TargetId = nested.EntityId,
                    Weight = weight,
                    Prob = prob,
                    Qty = qty,
                    Children = BuildItems(nested, depth + 1),
                });
                continue;
            }

            result.Add(new LootNodeDto { Label = itemId, Kind = "unknown", Weight = weight, Prob = prob, Qty = qty });
        }

        return result;
    }

    private Dictionary<string, ItemType>? TryBuildItemTypes(int sourceModId)
    {
        try
        {
            return _dataTable.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", sourceModId);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
