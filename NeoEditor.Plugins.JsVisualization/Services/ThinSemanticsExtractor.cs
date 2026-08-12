using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.JsVisualization.Services;

/// <summary>
/// C 级薄类型语义（D10 §3.8：模板 + 问题集轻量增强）——ContainerType（引用聚合）、
/// BarterHex（买卖徽章 + 补货战利品表）、Map（规格摘要行 + 定义截断）。
/// 全部输出 <see cref="TemplateSemantics"/>（Hero + 问题区 Blocks），JS 侧由
/// 薄模板渲染器组合，无 per-type 渲染器（D10 §二 模板规则 5）。
/// </summary>
public sealed class ThinSemanticsExtractor
{
    private readonly SemanticsShared _shared;

    public ThinSemanticsExtractor(SemanticsShared shared)
    {
        _shared = shared;
    }

    public string Loc(string key) => _shared.Loc(key);

    // ═══════════════ ContainerType ═══════════════

    /// <summary>C 级增强：被哪些 ItemType 用作内容/格式（ReverseLookup 按属性分组聚合）。</summary>
    public TemplateSemantics ExtractContainerType(ContainerType ct)
    {
        var store = _shared.DataTable.BrowserStore ?? _shared.DataTable.ActiveMergeStore;
        var blocks = new List<TemplateBlockDto>();
        if (store is not null)
        {
            var rawRefs = store.IndexService?.ReverseLookup(ct.EntityId) ?? [];
            var itemTypes = store.ReferenceLookups.TryGetValue(typeof(ItemType), out var list)
                ? list.OfType<ItemType>().Where(i => i.EntityId.Length > 0)
                    .ToDictionary(i => i.EntityId, i => i) : [];

            // 按属性分组：aContentIDs=内容 / nFormatID=格式
            var groups = rawRefs
                .Where(r => itemTypes.ContainsKey(r.SourceEntityId))
                .GroupBy(r => r.PropertyName);
            foreach (var g in groups)
            {
                var items = g.Select(r => new BadgeDto
                {
                    Text = itemTypes[r.SourceEntityId].Subject ?? r.SourceEntityId,
                    Bg = "#E3F2FD", Fg = "#1565C0",
                    TargetType = "ItemType", TargetId = r.SourceEntityId,
                }).Take(20).ToList();
                var label = g.Key is "aContentIDs" or "ContentIds" or "aContentID"
                    ? $"{Loc("Vis.AcceptsContent")} ({g.Count()})"
                    : $"{Loc("Vis.UsedBy")} ({g.Count()})";
                blocks.Add(new TemplateBlockDto
                {
                    Title = label,
                    Accent = "#283593",
                    Badges = items,
                });
                if (g.Count() > items.Count)
                    blocks.Add(new TemplateBlockDto { Title = $"+{g.Count() - items.Count} more", Accent = "#9E9E9E" });
            }
            if (rawRefs.Count > 0 && blocks.Count == 0)
                blocks.Add(new TemplateBlockDto
                {
                    Title = $"{Loc("Vis.UsedBy")} ({rawRefs.Count})",
                    Accent = "#283593",
                });
        }

        return new TemplateSemantics { Blocks = blocks, Refs = SemanticsShared.BuildRefSummary(_shared.DataTable, ct.EntityId) };
    }

    // ═══════════════ BarterHex ═══════════════

    /// <summary>C 级增强：商店信息（买/卖 + 位置）+ 补货战利品表（RestockTreasureId）。</summary>
    public TemplateSemantics ExtractBarterHex(BarterHex bh)
    {
        var blocks = new List<TemplateBlockDto>
        {
            new()
            {
                Title = Loc("Vis.ShopInfo"),
                Accent = "#00695C",
                Rows =
                {
                    new FieldRowDto { Label = Loc("Vis.Position"), Value = $"({bh.X}, {bh.Y})" },
                    new FieldRowDto { Label = Loc("Vis.Buys"), Value = bh.Buys ? Loc("Vis.Yes") : Loc("Vis.No"), Color = bh.Buys ? "#2E7D32" : "#999" },
                },
            },
        };

        if (bh.RestockTreasureId > 0 && bh.RestockTreasureId != 3)
        {
            var tt = _shared.DataTable.ReferenceLookups.TryGetValue(typeof(TreasureTable), out var list)
                ? list.OfType<TreasureTable>().FirstOrDefault(t => t.Id == bh.RestockTreasureId)
                : null;
            blocks.Add(new TemplateBlockDto
            {
                Title = Loc("Vis.RestockTT"),
                Accent = "#2E7D32",
                Badges = [tt is not null
                    ? new BadgeDto { Text = tt.Subject ?? tt.Name ?? $"TT#{tt.Id}", Bg = "#E8F5E9", Fg = "#2E7D32", TargetType = "TreasureTable", TargetId = tt.EntityId }
                    : new BadgeDto { Text = $"TT #{bh.RestockTreasureId}", Bg = "#F5F5F5", Fg = "#999" }],
            });
        }

        return new TemplateSemantics
        {
            HeroBadges = [new BadgeDto
            {
                Text = bh.Buys ? Loc("Vis.ShopBuys") : Loc("Vis.ShopSells"),
                Bg = bh.Buys ? "#E8F5E9" : "#FCE4EC",
                Fg = bh.Buys ? "#2E7D32" : "#C62828",
            }],
            HeroStats = [new FieldRowDto { Label = Loc("Vis.Position"), Value = $"({bh.X}, {bh.Y})", Color = "#888" }],
            Blocks = blocks,
            Refs = SemanticsShared.BuildRefSummary(_shared.DataTable, bh.EntityId),
        };
    }

    // ═══════════════ Map ═══════════════

    /// <summary>C 级增强：N cells 规格徽章 + 定义摘要（3000 截断，等宽字体）。</summary>
    public TemplateSemantics ExtractMap(Map m)
    {
        var blocks = new List<TemplateBlockDto>();
        if (!string.IsNullOrWhiteSpace(m.Definition))
            blocks.Add(new TemplateBlockDto
            {
                Title = Loc("Vis.MapDefinition"),
                Accent = "#546E7A",
                Text = m.Definition.Length > 3000 ? m.Definition[..3000] + "..." : m.Definition,
            });

        return new TemplateSemantics
        {
            HeroBadges = string.IsNullOrWhiteSpace(m.Definition) ? [] :
            [
                new BadgeDto
                {
                    Text = $"{m.Definition.Split(',').Length} cells",
                    Bg = "#E8EAF6", Fg = "#283593",
                },
            ],
            Subtitle = string.IsNullOrWhiteSpace(m.Name) ? null : m.Name,
            Blocks = blocks,
            Refs = SemanticsShared.BuildRefSummary(_shared.DataTable, m.EntityId),
        };
    }
}
