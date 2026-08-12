using System;
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
/// 共享语义助手：TopBar 审计统计（D10 §3.3：N 字段 · M 有值 · K 未解析）与
/// 反向引用聚合摘要（D10 §3.6 P1 静态版：类型分组 + 前 N 徽章 + more）。
/// </summary>
public class SemanticsSharedTests
{
    private static SemanticsShared CreateShared(StubEntityLookupService lookup, StubReferenceResolver resolver)
        => new(lookup, resolver, new StubLocalizationService(), _ => null);

    // ── 审计统计 ──────────────────────────────────────────────────────────

    [Fact]
    public void BuildAudit_CountsFieldsFilledAndUnresolved()
    {
        var crowbar = new ItemType
        {
            EntityId = "90.1", Name = "撬棍", GroupId = 90, SubgroupId = 1,
            Weight = 1.5,   // 有值
        };
        crowbar.CondId = new ReferenceList<IReferenceEntry> { RawText = "5" };   // stub 解析不到 → 未解析
        var lookup = new StubEntityLookupService();
        var resolver = new StubReferenceResolver();   // 什么都解析不到 → 引用段全未解析

        var audit = CreateShared(lookup, resolver).BuildAudit(crowbar);

        // ItemType 37 列全在（N），只有少数有值（M）；未解析 = 引用字段段数（stub 解析不到）
        Assert.True(audit.Fields >= 30);
        Assert.True(audit.Filled >= 4);   // id/nGroupID/nSubgroupID/strName/fWeight...
        Assert.Equal(1, audit.Unresolved);
        Assert.Contains("Vis.RawFields", audit.Text);
    }

    [Fact]
    public void BuildAudit_ResolvedRefs_NotUnresolved()
    {
        var cond = new Condition { EntityId = "5", Name = "饥饿", Fatal = true };
        var lookup = new StubEntityLookupService();
        var resolver = new StubReferenceResolver { Lookup = { ["5"] = cond } };
        var it = new ItemType { EntityId = "90.1", Name = "撬棍", GroupId = 90, SubgroupId = 1 };
        it.CondId = new ReferenceList<IReferenceEntry> { RawText = "5" };

        var audit = CreateShared(lookup, resolver).BuildAudit(it);

        // CondId 解析成功 → 该段不算未解析
        Assert.Equal(0, audit.Unresolved);
    }

    // ── 反向引用聚合 ──────────────────────────────────────────────────────

    [Fact]
    public void BuildRefSummary_GroupsByTypeWithCapAndMore()
    {
        var ct = new ContainerType { EntityId = "3", Name = "通用背包" };
        var items = new List<ItemType>();
        var store = new EntityMergeStore();
        var objects = new List<object>();
        var index = ReferenceIndexService.CreateInMemory();
        index.Open();
        for (int i = 0; i < 25; i++)
        {
            var item = new ItemType { EntityId = $"52.{i}", Name = $"猎刀{i}" };
            items.Add(item);
            objects.Add(item);
            index.AddReverse("3", item.EntityId, "aContentIDs", "3");
        }
        var creature = new Creature { EntityId = "101", Name = "狗人" };
        objects.Add(creature);
        store.ReferenceLookups[typeof(ItemType)] = objects;
        store.IndexService = index;
        var lookup = new StubEntityLookupService { ActiveMergeStore = store };

        var summary = SemanticsShared.BuildRefSummary(lookup, "3");

        Assert.NotNull(summary);
        Assert.Equal(25, summary!.Total);
        var group = Assert.Single(summary.Groups);
        Assert.Equal("ItemType", group.TypeName);
        Assert.Equal(25, group.Count);
        // P2: cap 提高到 100 —— 25 条全量带出（过滤/滚动加载由 JS 侧做）
        Assert.Equal(25, group.Items.Count);
        Assert.Equal(0, group.More);
    }

    [Fact]
    public void BuildRefSummary_NoRefs_ReturnsNull()
    {
        var summary = SemanticsShared.BuildRefSummary(new StubEntityLookupService(), "nobody");
        Assert.Null(summary);
    }

    // ── 图片解析（Avalonia LoadImage 同款候选链：去前缀/纯文件名/补 .png）──

    [Fact]
    public void ImageUrl_NsPrefix_Stripped()
    {
        // "NSE:img/creature/dog.png" → 去前缀 + 纯文件名 "dog.png"
        var url = SemanticsShared.ImageUrl("NSE:img/creature/dog.png", name => name == "dog.png" ? "D:\\Game\\img\\creature\\dog.png" : null);
        Assert.NotNull(url);
        Assert.Contains("/viz/assets?path=", url);
        Assert.Contains("dog.png", url);
    }

    [Fact]
    public void ImageUrl_SubdirRef_FallsBackToFileName()
    {
        // 子目录引用：FindImage 的 GetFiles 精确全名不支持分隔符 → 退化为纯文件名
        var url = SemanticsShared.ImageUrl("img/scenario/gas_station.png", name => name == "gas_station.png" ? "D:\\Game\\img\\scenario\\gas_station.png" : null);
        Assert.NotNull(url);
        Assert.Contains("gas_station.png", url);
    }

    [Fact]
    public void ImageUrl_NoExtension_AppendsPng()
    {
        // 无扩展名引用（游戏数据常见）→ 补 .png 兜底
        var url = SemanticsShared.ImageUrl("img/creature/dog", name => name == "dog.png" ? "D:\\Game\\img\\creature\\dog.png" : null);
        Assert.NotNull(url);
        Assert.Contains("dog.png", url);
    }

    [Fact]
    public void ImageUrl_NotFound_ReturnsNull()
    {
        Assert.Null(SemanticsShared.ImageUrl("img/missing/ghost.png", _ => null));
        Assert.Null(SemanticsShared.ImageUrl("", _ => "x"));
        Assert.Null(SemanticsShared.ImageUrl("NSE:", _ => "x"));
    }

    // ── 快照 image 字段（BuildById 产出 → JS 页面 <img> 源）────────────────

    [Fact]
    public void BuildById_Creature_ImageField_IsVizAssetUrl()
    {
        var creature = new Creature { EntityId = "101", Name = "狗人" };
        creature.Image = new ReferenceList<IReferenceEntry>
        {
            new EntityRef { Id = "img/creature/dog.png" },
        };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Creature)] = new List<object> { creature } } };
        // 真实 findImage 语义：按文件名命中 → 绝对路径（模拟 ImageService）
        var shared = new SemanticsShared(lookup, new StubReferenceResolver(), new StubLocalizationService(),
            name => name == "dog.png" ? @"D:\Game\img\creature\dog.png" : null);
        var lootTrees = new LootTreeBuilder(lookup, new StubReferenceResolver());
        var service = new VizSnapshotService(
            new StubHostService { Cache = { ["101"] = creature } }, new StubXmlParser(), lookup,
            new EncounterSemanticsExtractor(lookup, new StubReferenceResolver(), new StubLocalizationService(), _ => null, lootTrees),
            new ItemTypeSemanticsExtractor(shared, lootTrees),
            new CreatureSemanticsExtractor(shared, lootTrees),
            new RecipeSemanticsExtractor(shared, lootTrees),
            new ThinSemanticsExtractor(shared),
            new TemplateSemanticsExtractor(shared, lootTrees),
            shared);

        var snapshot = service.BuildById("Creature", "101");

        Assert.NotNull(snapshot);
        // 子目录引用 → 纯文件名命中 → /viz/assets 绝对路径 URL
        Assert.Equal("/viz/assets?path=" + Uri.EscapeDataString(@"D:\Game\img\creature\dog.png"), snapshot!.Image);
    }

    [Fact]
    public void BuildById_AttackMode_ImageField_IsVizAssetUrl()
    {
        // AttackMode（strIMG）在快照级 Hero 图 switch 中：Avalonia 可视化器显示 132px hero 图，
        // JS 模板页 hero 必须拿到同一 URL —— 修复前该类型落入 `_ => null`。
        var attackMode = new AttackMode { EntityId = "14", Name = "Rifle" };
        attackMode.Image = new ReferenceList<IReferenceEntry>
        {
            new EntityRef { Id = "NSE:ItmRifle.png" },
        };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(AttackMode)] = new List<object> { attackMode } } };
        var shared = new SemanticsShared(lookup, new StubReferenceResolver(), new StubLocalizationService(),
            name => name == "ItmRifle.png" ? @"D:\Game\img\ItmRifle.png" : null);
        var lootTrees = new LootTreeBuilder(lookup, new StubReferenceResolver());
        var service = new VizSnapshotService(
            new StubHostService { Cache = { ["14"] = attackMode } }, new StubXmlParser(), lookup,
            new EncounterSemanticsExtractor(lookup, new StubReferenceResolver(), new StubLocalizationService(), _ => null, lootTrees),
            new ItemTypeSemanticsExtractor(shared, lootTrees),
            new CreatureSemanticsExtractor(shared, lootTrees),
            new RecipeSemanticsExtractor(shared, lootTrees),
            new ThinSemanticsExtractor(shared),
            new TemplateSemanticsExtractor(shared, lootTrees),
            shared);

        var snapshot = service.BuildById("AttackMode", "14");

        Assert.NotNull(snapshot);
        // NSE: 前缀剥离 + 纯文件名命中 → /viz/assets 绝对路径 URL
        Assert.Equal("/viz/assets?path=" + Uri.EscapeDataString(@"D:\Game\img\ItmRifle.png"), snapshot!.Image);
    }
}
