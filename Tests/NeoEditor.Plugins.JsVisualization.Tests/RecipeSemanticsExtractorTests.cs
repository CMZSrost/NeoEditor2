using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>
/// Recipe 语义：Hero（Type/flags/耗时/可逆）、原料三组（数量/Required/Forbidden 属性）、
/// 产物 TT 树、Temp Product、AlsoTry/Hidden。
/// </summary>
public class RecipeSemanticsExtractorTests
{
    private static RecipeSemanticsExtractor CreateExtractor(StubEntityLookupService lookup,
        StubReferenceResolver resolver)
    {
        var shared = new SemanticsShared(lookup, resolver, new StubLocalizationService(), _ => null);
        return new RecipeSemanticsExtractor(shared, new LootTreeBuilder(lookup, resolver));
    }

    [Fact]
    public void Extract_Hero_TypeFlagsAndStats()
    {
        var r = new Recipe
        {
            EntityId = "r1", Name = "拆解撬棍", Type = "拆解", Hours = 2.5, Reverse = 1,
            Scrap = true, Identify = true, DegradeOutput = false, TransferComponents = true,
        };

        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).Extract(r);

        Assert.Equal("拆解", sem.Type);
        Assert.Equal(["Scrap", "Identify", "TransferComponents"], sem.Flags);
        Assert.Contains(sem.HeroStats, s => s.Label == "Vis.Hours" && s.Value == "2.5");
        Assert.Contains(sem.HeroStats, s => s.Label == "Vis.Reverse" && s.Value == "Vis.Yes");
    }

    [Fact]
    public void Extract_IngredientGroups_QtyAndProps()
    {
        var ing = new Ingredient
        {
            EntityId = "i1", Name = "金属碎片",
            RequiredProps = new ReferenceList<IReferenceEntry> { RawText = "p1" },
            ForbidProps = new ReferenceList<IReferenceEntry> { RawText = "p2" },
        };
        var prop1 = new ItemProp { EntityId = "p1", PropertyName = "可拆解" };
        var prop2 = new ItemProp { EntityId = "p2", PropertyName = "精良" };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(Ingredient)] = new List<object> { ing },
                [typeof(ItemProp)] = new List<object> { prop1, prop2 },
            },
        };
        // stub 按原段键匹配（{mult}x{id} pattern："2xi1"）
        var resolver = new StubReferenceResolver
        {
            Lookup = { ["2xi1"] = ing, ["p1"] = prop1, ["p2"] = prop2 },
        };
        var r = new Recipe { EntityId = "r1", Name = "拆解" };
        r.Consumed = new ReferenceList<IReferenceEntry> { RawText = "2xi1" };
        r.Tools = new ReferenceList<IReferenceEntry> { RawText = "1xi1" };
        r.Destroyed = new ReferenceList<IReferenceEntry> { RawText = "1xi1" };

        var sem = CreateExtractor(lookup, resolver).Extract(r);

        Assert.Equal(3, sem.IngredientGroups.Count);
        var consumed = sem.IngredientGroups[1];
        Assert.Equal("Vis.Consumed", consumed.Label);
        Assert.Equal("#FFEBEE", consumed.Bg);
        var item = Assert.Single(consumed.Items);
        Assert.Equal("金属碎片", item.Name);
        Assert.Equal("2", item.Qty);   // ×2
        // Required 绿 / Forbidden 红
        Assert.Equal("可拆解", Assert.Single(item.Required).Text);
        Assert.Equal("精良", Assert.Single(item.Forbidden).Text);
    }

    [Fact]
    public void Extract_UnresolvedIngredient_GreyFallback()
    {
        var r = new Recipe { EntityId = "r1", Name = "未知配方" };
        r.Tools = new ReferenceList<IReferenceEntry> { RawText = "3x99" };

        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).Extract(r);

        var item = Assert.Single(Assert.Single(sem.IngredientGroups).Items);
        Assert.False(item.Resolved);
        Assert.Equal("3x99", item.Name);
    }

    [Fact]
    public void Extract_Product_TreeAndTempProduct()
    {
        var tt = new TreasureTable
        {
            EntityId = "7", Name = "产物表", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x1x1" },
        };
        var crowbar = new ItemType { EntityId = "52", Name = "撬棍", GroupId = 0, SubgroupId = 0 };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(TreasureTable)] = new List<object> { tt },
                [typeof(ItemType)] = new List<object> { crowbar },
            },
        };
        var resolver = new StubReferenceResolver { Lookup = { ["7"] = tt, ["52"] = crowbar } };
        var r = new Recipe { EntityId = "r1", Name = "锻造" };
        r.TreasureId = new ReferenceList<IReferenceEntry> { RawText = "7" };
        r.TempTreasureId = new ReferenceList<IReferenceEntry> { RawText = "7" };   // 与主产物相同 → 隐藏

        var sem = CreateExtractor(lookup, resolver).Extract(r);

        Assert.Equal("产物表", sem.Product!.Title);
        Assert.Equal("撬棍", Assert.Single(sem.Product.Items).Label);
        Assert.Empty(sem.TempProduct);   // Temp == Treasure → 去噪
    }

    [Fact]
    public void Extract_AlsoTryAndHidden_Badges()
    {
        var r2 = new Recipe { EntityId = "r2", Name = "备用配方" };
        var r3 = new Recipe { EntityId = "r3", Name = "鉴定解锁" };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Recipe)] = new List<object> { r2, r3 } } };
        var resolver = new StubReferenceResolver { Lookup = { ["r2"] = r2, ["r3"] = r3 } };
        var r = new Recipe { EntityId = "r1", Name = "主配方" };
        r.AlsoTry = new ReferenceList<IReferenceEntry> { RawText = "r2" };
        r.HiddenId = new ReferenceList<IReferenceEntry> { RawText = "r3" };

        var sem = CreateExtractor(lookup, resolver).Extract(r);

        Assert.Equal("备用配方", Assert.Single(sem.AlsoTry).Text);
        Assert.Equal("鉴定解锁", Assert.Single(sem.Hidden).Text);
    }
}
