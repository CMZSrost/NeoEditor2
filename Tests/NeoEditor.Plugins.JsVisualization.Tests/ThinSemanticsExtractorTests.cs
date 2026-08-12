using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Services;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>
/// C 级薄类型语义（D10 §3.8）：ContainerType 引用聚合 / BarterHex 补货 / Map 规格摘要。
/// 全部输出 TemplateSemantics（Hero + Blocks），无 per-type 渲染器。
/// </summary>
public class ThinSemanticsExtractorTests
{
    private static ThinSemanticsExtractor CreateExtractor(StubEntityLookupService lookup,
        StubReferenceResolver resolver)
        => new(new SemanticsShared(lookup, resolver, new StubLocalizationService(), _ => null));

    // ── ContainerType：被哪些 ItemType 用作内容/格式（ReverseLookup 聚合）──

    [Fact]
    public void ExtractContainerType_GroupsByProperty()
    {
        var ct = new ContainerType { EntityId = "3", Name = "通用背包" };
        var itemA = new ItemType { EntityId = "52", Name = "猎刀" };
        var itemB = new ItemType { EntityId = "90.1", Name = "撬棍" };
        var store = new EntityMergeStore();
        store.ReferenceLookups[typeof(ItemType)] = new List<object> { itemA, itemB };
        // 真实内存反向索引：aContentIDs 引用 52，nFormatID 引用 90.1
        var index = ReferenceIndexService.CreateInMemory();
        index.Open();
        index.AddReverse("3", "52", "aContentIDs", "3");
        index.AddReverse("3", "90.1", "nFormatID", "3");
        store.IndexService = index;
        var lookup = new StubEntityLookupService { ActiveMergeStore = store };

        var sem = CreateExtractor(lookup, new StubReferenceResolver()).ExtractContainerType(ct);

        Assert.Equal(2, sem.Blocks.Count);
        var content = sem.Blocks[0];
        Assert.Contains("Vis.AcceptsContent", content.Title);   // aContentIDs 分组
        Assert.Equal("猎刀", Assert.Single(content.Badges).Text);
        var format = sem.Blocks[1];
        Assert.StartsWith("Vis.UsedBy", format.Title);               // nFormatID 分组
        Assert.Equal("撬棍", Assert.Single(format.Badges).Text);
    }

    [Fact]
    public void ExtractContainerType_NoStore_EmptyBlocks()
    {
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver())
            .ExtractContainerType(new ContainerType { EntityId = "3", Name = "空容器" });
        Assert.Empty(sem.Blocks);
    }

    // ── BarterHex：买/卖徽章 + 补货 TT ────────────────────────────────────

    [Fact]
    public void ExtractBarterHex_BuysAndRestock()
    {
        var tt = new TreasureTable { Id = 7, EntityId = "7", Name = "补货池" };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups = { [typeof(TreasureTable)] = new List<object> { tt } },
        };
        var bh = new BarterHex { EntityId = "b1",  X = 5, Y = 7, Buys = true, RestockTreasureId = 7 };

        var sem = CreateExtractor(lookup, new StubReferenceResolver()).ExtractBarterHex(bh);

        // Hero：买徽章 + 位置
        Assert.Equal("Vis.ShopBuys", Assert.Single(sem.HeroBadges).Text);
        Assert.Equal("(5, 7)", Assert.Single(sem.HeroStats).Value);
        // 问题区：ShopInfo + Restock
        Assert.Equal(2, sem.Blocks.Count);
        Assert.Equal("Vis.RestockTT", sem.Blocks[1].Title);
        Assert.Equal("补货池", Assert.Single(sem.Blocks[1].Badges).Text);
    }

    [Fact]
    public void ExtractBarterHex_SellsAndDefaultRestock_Hidden()
    {
        var bh = new BarterHex { EntityId = "b2",  X = 1, Y = 1, Buys = false, RestockTreasureId = 3 };
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).ExtractBarterHex(bh);

        Assert.Equal("Vis.ShopSells", Assert.Single(sem.HeroBadges).Text);
        Assert.Single(sem.Blocks);   // 3=默认空池 → Restock 块不渲染
    }

    // ── Map：N cells 徽章 + 定义截断 ──────────────────────────────────────

    [Fact]
    public void ExtractMap_CellCountAndDefinitionTruncation()
    {
        var m = new Map { EntityId = "m1", Name = "避难所地图", Definition = string.Join(",", Enumerable.Repeat("1", 4000)) };
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).ExtractMap(m);

        Assert.Equal("4000 cells", Assert.Single(sem.HeroBadges).Text);
        Assert.Equal("避难所地图", sem.Subtitle);
        var block = Assert.Single(sem.Blocks);
        Assert.Equal("Vis.MapDefinition", block.Title);
        Assert.EndsWith("...", block.Text);
        Assert.True(block.Text!.Length <= 3003);
    }

    [Fact]
    public void ExtractMap_NoDefinition_NoBadgesNoBlocks()
    {
        var m = new Map { EntityId = "m2", Name = "空白" };
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).ExtractMap(m);
        Assert.Empty(sem.HeroBadges);
        Assert.Empty(sem.Blocks);
    }
}
