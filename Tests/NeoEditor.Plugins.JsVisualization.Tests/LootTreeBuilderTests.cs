using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>
/// D09 P1「战利品嵌套树」：概率归一 / 嵌套 TT 递归 / G.S 键解析 / 未解析兜底。
/// 语义沿 D04 BuildTreasureLootTree（`物品x权重x数量`，`|` 与 `,` 都是条目级分隔符，
/// 概率按表内权重 Σ 归一；嵌套概率独立归一）。
/// </summary>
public class LootTreeBuilderTests
{
    private static LootTreeBuilder CreateBuilder(StubEntityLookupService lookup, StubReferenceResolver resolver)
        => new(lookup, resolver);

    [Fact]
    public void Build_WeightsNormalizeAcrossAllEntries()
    {
        var crowbar = new ItemType { EntityId = "90.1", Name = "撬棍", GroupId = 90, SubgroupId = 1 };
        var knife = new ItemType { EntityId = "52", Name = "猎刀", GroupId = 0, SubgroupId = 0 };
        var tt = new TreasureTable
        {
            EntityId = "7", Name = "高级战利品", ModId = -1,
            // 权重 1:3 → 概率 25% / 75%（G.S 键 "90.1" 与裸键 "0.0" 混合）
            Treasures = new NeoEditor.Data.Model.ReferenceList<IReferenceEntry> { RawText = "90.1x1x1,0.0x3x1" },
        };

        var lookup = new StubEntityLookupService
        {
            ReferenceLookups = { [typeof(ItemType)] = new List<object> { crowbar, knife } },
        };
        var tree = CreateBuilder(lookup, new StubReferenceResolver()).Build(tt)!;

        Assert.Equal(2, tree.Items.Count);
        Assert.Equal(0.25, tree.Items[0].Prob, 3);
        Assert.Equal(0.75, tree.Items[1].Prob, 3);
        Assert.Equal("item", tree.Items[0].Kind);
        Assert.Equal("ItemType", tree.Items[0].TargetType);
        Assert.Equal("90.1", tree.Items[0].TargetId);
    }

    [Fact]
    public void Build_NestedTable_RecursesWithIndependentProb()
    {
        var knife = new ItemType { EntityId = "52", Name = "猎刀" };
        var nested = new TreasureTable
        {
            EntityId = "20", Name = "嵌套小表", ModId = -1,
            Treasures = new NeoEditor.Data.Model.ReferenceList<IReferenceEntry> { RawText = "0.0x1x1" },
        };
        var tt = new TreasureTable
        {
            EntityId = "7", Name = "外层", ModId = -1,
            Treasures = new NeoEditor.Data.Model.ReferenceList<IReferenceEntry> { RawText = "20x2x1" },
        };

        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(ItemType)] = new List<object> { knife },
                [typeof(TreasureTable)] = new List<object> { tt, nested },
            },
        };
        var resolver = new StubReferenceResolver { Lookup = { ["20"] = nested, ["52"] = knife } };
        var tree = CreateBuilder(lookup, resolver).Build(tt)!;

        var node = Assert.Single(tree.Items);
        Assert.Equal("table", node.Kind);
        Assert.Equal("嵌套小表", node.Label);
        Assert.Single(node.Children);
        Assert.Equal("猎刀", node.Children[0].Label);
        // 嵌套表内概率独立归一（单条目 = 100%）
        Assert.Equal(1.0, node.Children[0].Prob, 3);
    }

    [Fact]
    public void Build_UnknownItem_StaysVisibleAsUnknown()
    {
        var tt = new TreasureTable
        {
            EntityId = "7", Name = "坏表", ModId = -1,
            Treasures = new NeoEditor.Data.Model.ReferenceList<IReferenceEntry> { RawText = "999.9x1x2" },
        };
        var tree = CreateBuilder(new StubEntityLookupService(), new StubReferenceResolver()).Build(tt)!;

        var node = Assert.Single(tree.Items);
        Assert.Equal("unknown", node.Kind);
        Assert.Equal("999.9", node.Label);
        Assert.Equal("2", node.Qty);
    }

    [Fact]
    public void Build_EmptyOrNoSegments_ReturnsNull()
    {
        var empty = new TreasureTable { EntityId = "3", Name = "空池", ModId = -1 };
        Assert.Null(CreateBuilder(new StubEntityLookupService(), new StubReferenceResolver()).Build(empty));

        var noSegments = new TreasureTable
        {
            EntityId = "4", Name = "无 x 段", ModId = -1,
            Treasures = new NeoEditor.Data.Model.ReferenceList<IReferenceEntry> { RawText = "abc,def" },
        };
        Assert.Null(CreateBuilder(new StubEntityLookupService(), new StubReferenceResolver()).Build(noSegments));
    }

    [Fact]
    public void Build_DepthLimit_StopsRecursion()
    {
        // 5 层嵌套（builder 深度上限 3，越界层返回空）→ 第 5 层不展开
        var tables = new List<TreasureTable>();
        for (int i = 1; i <= 5; i++)
        {
            tables.Add(new TreasureTable
            {
                EntityId = i.ToString(), Name = $"TT{i}", ModId = -1,
                Treasures = new NeoEditor.Data.Model.ReferenceList<IReferenceEntry>
                {
                    RawText = (i + 1) + "x1x1",
                },
            });
        }
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(TreasureTable)] = tables.Cast<object>().ToList() } };
        var resolver = new StubReferenceResolver();
        foreach (var t in tables) resolver.Lookup[t.EntityId] = t;

        var tree = CreateBuilder(lookup, resolver).Build(tables[0])!;
        var n1 = Assert.Single(tree.Items);
        var n2 = Assert.Single(n1.Children);
        var n3 = Assert.Single(n2.Children);
        // 第 4 层仍展开（depth 3 允许），第 5 层（depth 4）被深度上限截断
        var n4 = Assert.Single(n3.Children);
        Assert.Equal("table", n4.Kind);
        Assert.Empty(n4.Children);
    }
}
