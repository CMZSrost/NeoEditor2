using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>
/// D09: Encounter semantics extraction — pure-data port of the D06/D07/D08 page
/// semantics. All display strings are pre-localized; the JS page only renders.
/// </summary>
public class EncounterSemanticsExtractorTests
{
    private static EncounterSemanticsExtractor CreateExtractor(StubEntityLookupService lookup,
        StubReferenceResolver resolver, Func<string, string?>? findImage = null)
        => new(lookup, resolver, new StubLocalizationService(), findImage ?? (_ => null),
            new LootTreeBuilder(lookup, resolver));

    // ── 纯函数：D07 §3.1 终止语义 ─────────────────────────────────────────

    [Theory]
    [InlineData(90, 90, EncounterSemanticsExtractor.BranchEndKind.Stay)]   // 自指 → 停留（胜过 Blank）
    [InlineData(1, 90, EncounterSemanticsExtractor.BranchEndKind.Blank)]   // 指向 Blank(id=1) → 无后续
    [InlineData(1, 1, EncounterSemanticsExtractor.BranchEndKind.Stay)]     // 看 Blank 本身 → 自指优先
    [InlineData(941, 90, EncounterSemanticsExtractor.BranchEndKind.None)]
    public void DetermineEndKind_MatchesD07Rules(int targetId, int currentId,
        EncounterSemanticsExtractor.BranchEndKind expected)
    {
        Assert.Equal(expected, EncounterSemanticsExtractor.DetermineEndKind(targetId, currentId));
    }

    // ── 纯函数：D08 §三 入口/终点拓扑 ─────────────────────────────────────

    [Fact]
    public void DetermineEntryTerminal_NoInEdges_IsEntry()
    {
        // 无入边 + 非全终止 → 入口
        var branches = new List<EncounterSemanticsExtractor.BranchData>
        {
            MakeBranch(941, EncounterSemanticsExtractor.BranchEndKind.None),
        };
        var (isEntry, isTerminal) = EncounterSemanticsExtractor.DetermineEntryTerminal(0, branches);
        Assert.True(isEntry);
        Assert.False(isTerminal);
    }

    [Fact]
    public void DetermineEntryTerminal_AllOutEdgesEnd_IsTerminal()
    {
        var branches = new List<EncounterSemanticsExtractor.BranchData>
        {
            MakeBranch(90, EncounterSemanticsExtractor.BranchEndKind.Stay),
            MakeBranch(1, EncounterSemanticsExtractor.BranchEndKind.Blank),
        };
        var (isEntry, isTerminal) = EncounterSemanticsExtractor.DetermineEntryTerminal(3, branches);
        Assert.False(isEntry);
        Assert.True(isTerminal);
    }

    private static EncounterSemanticsExtractor.BranchData MakeBranch(int targetId,
        EncounterSemanticsExtractor.BranchEndKind endKind) => new(targetId, null, [], 1, 1, true, [], endKind);

    // ── 纯函数：前置条件满足判定（D06 过滤语义）──────────────────────────

    [Fact]
    public void IsPreCondSatisfied_NoActiveFilter_EverythingSatisfied()
    {
        Assert.True(EncounterSemanticsExtractor.IsPreCondSatisfied("5", new HashSet<string>()));
        Assert.True(EncounterSemanticsExtractor.IsPreCondSatisfied("-5", new HashSet<string>()));
    }

    [Fact]
    public void IsPreCondSatisfied_Polarity()
    {
        var active = new HashSet<string> { "5" };
        Assert.True(EncounterSemanticsExtractor.IsPreCondSatisfied("5", active));   // 正条件：勾选即满足
        Assert.False(EncounterSemanticsExtractor.IsPreCondSatisfied("-5", active)); // ¬ 条件：勾选则不满足
        Assert.True(EncounterSemanticsExtractor.IsPreCondSatisfied("-8", active));  // ¬ 未勾选 → 满足
    }

    // ── 纯函数：概率格式（无文化空格）────────────────────────────────────

    [Theory]
    [InlineData(0.5, "50%")]
    [InlineData(2.0 / 3.0, "66.67%")]
    [InlineData(0.999, "99.9%")]
    [InlineData(1.5, "100%")]   // clamp
    public void FormatProbability(double p, string expected)
        => Assert.Equal(expected, EncounterSemanticsExtractor.FormatProbability(p));

    // ── Extract：分支合并 + 概率归一（D06 §4.5）──────────────────────────

    [Fact]
    public void Extract_MergesSameTargetSegments_AndNormalizesProbability()
    {
        // "91.4x1=941x1x0x0x0,103.8x1=941x1x0x0x0,=12x1x0x0x0" — 两个物品段指向同一 941，一段默认指向 12
        var enc = new Encounter
        {
            Id = 90,
            EntityId = "90",
            Name = "Test",

            Type = EncounterType.Normal,
            Responses = "91.4x1=941x1x0x0x0,103.8x1=941x1x0x0x0,=12x1x0x0x0",
        };
        var it91 = new ItemType { EntityId = "91.4", Name = "撬棍", GroupId = 91, SubgroupId = 4 };
        var it103 = new ItemType { EntityId = "103.8", Name = "绳子", GroupId = 103, SubgroupId = 8 };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(Encounter)] = new List<object>(),
                [typeof(ItemType)] = new List<object> { it91, it103 },
            },
        };
        var sem = CreateExtractor(lookup, new StubReferenceResolver()).Extract(enc);

        Assert.Equal(2, sem.Flow.Branches.Count);

        var b941 = sem.Flow.Branches.First(b => b.TargetId == 941);
        var b12 = sem.Flow.Branches.First(b => b.TargetId == 12);
        // 941 权重 1+1=2，12 权重 1 → 总 3
        Assert.Equal(2.0, b941.Weight);
        Assert.Equal(1.0, b12.Weight);
        Assert.Equal("66.67%", FormatProb(b941.EffectiveProb));
        Assert.Equal("33.33%", FormatProb(b12.EffectiveProb));
        // 同目标多段合并 → 两个物品徽章（D07 §六：多段 = 任一）
        Assert.Equal(2, b941.ItemBadges.Count);
        Assert.Equal("🛡", b941.ItemBadges[0].Icon);
        Assert.Contains("撬棍", b941.ItemBadges[0].Text);
        // 默认响应（= 开头）→ 无物品徽章
        Assert.Empty(b12.ItemBadges);
    }

    private static string FormatProb(double p) => EncounterSemanticsExtractor.FormatProbability(p);

    // ── Extract：D07 §3.1 终止胶囊 ───────────────────────────────────────

    [Fact]
    public void Extract_BranchEntityId_ResolvedTargetExposedForNavigation()
    {
        // 分支目标解析成功时携带 EntityId（页面导航键——缓存按 EntityId 查，数字 id 会 404）
        var target941 = new Encounter { Id = 941, EntityId = "941", Name = "加油站", Type = EncounterType.Normal };
        var resolver = new StubReferenceResolver { Lookup = { ["941"] = target941 } };
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "Self", Type = EncounterType.Normal, Responses = "=941x1x0x0x0" };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var sem = CreateExtractor(lookup, resolver).Extract(enc);

        var b = Assert.Single(sem.Flow.Branches);
        Assert.True(b.Resolved);
        Assert.Equal("941", b.EntityId);
        Assert.Equal("加油站", b.DisplayName);
    }

    [Fact]
    public void Extract_SelfReferenceBranch_IsStayCapsule()
    {
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "Self", Type = EncounterType.Normal, Responses = "=90x1x0x0x0" };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var sem = CreateExtractor(lookup, new StubReferenceResolver()).Extract(enc);

        var b = Assert.Single(sem.Flow.Branches);
        Assert.Equal("stay", b.EndKind);
    }

    // ── Extract：入口/终点 Hero 标记 ─────────────────────────────────────

    [Fact]
    public void Extract_NoPredsAndAllEnds_BothEntryAndTerminal()
    {
        var enc = new Encounter
        {
            Id = 90, EntityId = "90", Name = "Both", Type = EncounterType.Normal,
            Responses = "=90x1x0x0x0", // 自指 → 全终止
        };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var sem = CreateExtractor(lookup, new StubReferenceResolver()).Extract(enc);

        Assert.True(sem.IsEntry);     // 无入边
        Assert.True(sem.IsTerminal);  // 唯一出边是自指（Stay）
    }

    // ── Extract：前驱反查（D08 §二）──────────────────────────────────────

    [Fact]
    public void Extract_FindsPredecessors_FromEncounterScan()
    {
        var source = new Encounter
        {
            Id = 41, EntityId = "41", Name = "前驱剧情", Type = EncounterType.Normal,
            Responses = "90.1x1=90x1x0x0x0",
        };
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "Cur", Type = EncounterType.Normal };
        var it901 = new ItemType { EntityId = "90.1", Name = "撬棍", GroupId = 90, SubgroupId = 1 };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(Encounter)] = new List<object> { source, enc },
                [typeof(ItemType)] = new List<object> { it901 },
            },
        };
        var sem = CreateExtractor(lookup, new StubReferenceResolver()).Extract(enc);

        var pred = Assert.Single(sem.Flow.Predecessors);
        Assert.Equal("41", pred.Id);
        Assert.Equal("前驱剧情", pred.DisplayName);
        Assert.Contains("🛡", pred.Annotation ?? ""); // 来路标注（物品触发）
    }

    // ── Extract：✨ 效果区（D08 §五）────────────────────────────────────

    [Fact]
    public void Extract_Effects_RowsForEachSemanticField()
    {
        var item = new ItemType { EntityId = "52", Name = "撬棍", GroupId = 0, SubgroupId = 0 };
        var resolver = new StubReferenceResolver { Lookup = { ["52"] = item } };
        var enc = new Encounter
        {
            Id = 90, EntityId = "90", Name = "Fx", Type = EncounterType.Normal,
            Price = 5, ItemsId = new ReferenceList<IReferenceEntry> { RawText = "52" },
            Teleport = "3,4",
        };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var sem = CreateExtractor(lookup, resolver).Extract(enc);

        Assert.NotNull(sem.Effects);
        var rows = sem.Effects!.Rows;
        // 💰 Cost（Price=5）→ 文本 "$5.00"
        Assert.Contains(rows, r => r.Text == "$5.00");
        // 📦 GiveItem（ItemsId="52"）→ 已解析徽章带跳转目标
        var give = rows.First(r => r.Badges.Count == 1 && r.Badges[0].TargetType == "ItemType");
        Assert.Equal("52", give.Badges[0].TargetId);
        // 📍 Teleport（≠"0,0"）
        Assert.Contains(rows, r => r.Text == "(3,4)");
    }

    [Fact]
    public void Extract_NoEffects_ReturnsNull()
    {
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "Empty", Type = EncounterType.Normal };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var sem = CreateExtractor(lookup, new StubReferenceResolver()).Extract(enc);
        Assert.Null(sem.Effects);
    }

    // ── Extract：如何进入（D08 §四）──────────────────────────────────────

    [Fact]
    public void Extract_Entry_ConditionsAndTriggers()
    {
        var cond = new Condition { EntityId = "5", Name = "饥饿", Fatal = true };
        var resolver = new StubReferenceResolver { Lookup = { ["5"] = cond } };
        var trigger = new EncounterTrigger
        {
            EntityId = "t1", Name = "城市触发器", Area = "10,20,3", Unique = false,
            EncounterId = new ReferenceList<IReferenceEntry> { new EntityRef { Id = "90" } },
        };
        var enc = new Encounter
        {
            Id = 90, EntityId = "90", Name = "E", Type = EncounterType.Normal,
            Conditions = new ReferenceList<IReferenceEntry> { RawText = "5,1" }, // "1" 占位去噪
        };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(Encounter)] = new List<object>(),
                [typeof(EncounterTrigger)] = new List<object> { trigger },
            },
        };
        var sem = CreateExtractor(lookup, resolver).Extract(enc);

        Assert.NotNull(sem.Entry);
        var condBadge = Assert.Single(sem.Entry!.Conditions);
        Assert.Equal("饥饿", condBadge.Text);
        Assert.Equal("#FFEBEE", condBadge.Bg);       // Fatal 红
        Assert.NotNull(condBadge.Tooltip);           // 条件效果翻译
        var t = Assert.Single(sem.Entry.Triggers);
        Assert.Equal("城市触发器", t.Name);
        Assert.Contains("📍", t.Summary ?? "");      // 区域摘要
    }

    // ── Extract：type chip 语义色（D06 §4.2）─────────────────────────────

    [Theory]
    [InlineData(0, "#E3F2FD")]  // 剧情 → 蓝
    [InlineData(1, "#FFF3E0")]  // 搜刮 → 橙
    [InlineData(2, "#FFEBEE")]  // 战斗 → 红
    [InlineData(3, "#F3E5F5")]  // 破解 → 紫
    public void Extract_TypeChip_SemanticColors(int rawType, string bg)
    {
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "T", Type = (EncounterType)rawType };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var sem = CreateExtractor(lookup, new StubReferenceResolver()).Extract(enc);
        Assert.Equal(bg, sem.TypeChip.Bg);
    }
}
