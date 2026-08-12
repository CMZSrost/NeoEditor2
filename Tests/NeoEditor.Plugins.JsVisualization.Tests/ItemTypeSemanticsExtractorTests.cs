using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>
/// D04 语义（ItemType）：Hero（G.S/辨识/关键数字）、战斗三层（Σ 条/Σ 有效/模式行展开）、
/// 效果条件组（槽位/否定/语义色）、生命周期（耐久/寿命推演/破损产物）、容器、来源产出。
/// 注：StubLocalizationService 返回键名本身（如 "Vis.Effective"），断言用键名或硬编码后缀。
/// </summary>
public class ItemTypeSemanticsExtractorTests
{
    private static ItemTypeSemanticsExtractor CreateExtractor(StubEntityLookupService lookup,
        StubReferenceResolver resolver)
    {
        var shared = new SemanticsShared(lookup, resolver, new StubLocalizationService(), _ => null);
        return new ItemTypeSemanticsExtractor(shared, new LootTreeBuilder(lookup, resolver));
    }

    private static ItemType MakeItem() => new()
    {
        EntityId = "90.1", Name = "撬棍", GroupId = 90, SubgroupId = 1,
        Weight = 1.5, MonetaryValue = 5.0, MonetaryValueAlt = 25.0, StackLimit = 3,
        Durability = 0.8, DegradePerHour = 0.01, DegradePerUse = 0.05,
        Description = "撬门利器",
    };

    [Fact]
    public void Extract_HeroStats_WeightValueStack()
    {
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver())
            .Extract(MakeItem());

        Assert.Equal("90.1", sem.Gs);
        Assert.Equal("撬门利器", sem.Description);
        Assert.Contains(sem.HeroStats, s => s.Value == "1.5 kg");
        Assert.Contains(sem.HeroStats, s => s.Value == "$5.00 → $25.00");   // 价格箭头
        Assert.Contains(sem.HeroStats, s => s.Value == "×3");
    }

    [Fact]
    public void Extract_Identified_OnlyWhenDescAlt()
    {
        var it = MakeItem();
        it.DescriptionAlt = "";
        var empty = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).Extract(it);
        Assert.Null(empty.IdentifiedLabel);

        it = MakeItem();
        it.DescriptionAlt = "未识别的撬棍";
        var with = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).Extract(it);
        Assert.StartsWith("✦", with.IdentifiedLabel);
        Assert.Equal("未识别的撬棍", with.IdentifiedDesc);
    }

    // ── 战斗三层 ──────────────────────────────────────────────────────────

    [Fact]
    public void Extract_Combat_TotalsAndMoraleEffective()
    {
        var bat = new AttackMode { EntityId = "14", Name = "挥击", DamageCut = 4, DamageBlunt = 2, Morale = 0.5, Range = 1 };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(AttackMode)] = new List<object> { bat } } };
        var resolver = new StubReferenceResolver { Lookup = { ["21=14"] = bat } };   // stub 按原段键匹配
        var it = MakeItem();
        it.AttackModes = new ReferenceList<IReferenceEntry> { RawText = "21=14" };

        var sem = CreateExtractor(lookup, resolver).Extract(it);

        var combat = sem.Combat!;
        // Σ 条：Cut 4 + Blunt 2
        Assert.Equal(4, combat.TotalBar!.Segments[0].Value);
        Assert.Equal(2, combat.TotalBar.Segments[1].Value);
        // Σ 有效 = (4+2)×(1+0.5) = 9.0，倍率 ×1.50
        Assert.Equal("9.0 (×1.50)", combat.TotalEffective);
        // 模式行：槽位名前缀 + 士气 + 展开详情
        var mode = Assert.Single(combat.Modes);
        Assert.Equal("R-Hand: 挥击", mode.Name);   // 槽位 20 = R-Hand
        Assert.Equal("Morale 50%", mode.MoraleText);
        Assert.Contains("9.0 (×1.50)", mode.EffectiveText);
        Assert.NotNull(mode.FormulaNote);
    }

    [Fact]
    public void Extract_Combat_UnresolvedMode_GreyRow()
    {
        var it = MakeItem();
        it.AttackModes = new ReferenceList<IReferenceEntry> { RawText = "999" };
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).Extract(it);

        var mode = Assert.Single(sem.Combat!.Modes);
        Assert.False(mode.Resolved);
        Assert.Equal("999", mode.Name);
    }

    // ── 效果条件组（槽位/否定/语义色）────────────────────────────────────

    [Fact]
    public void Extract_ConditionGroups_SlotNegationAndColors()
    {
        var fatal = new Condition { EntityId = "5", Name = "流血", Fatal = true };
        var stackable = new Condition { EntityId = "8", Name = "饱食", Stackable = true, Duration = 12 };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Condition)] = new List<object> { fatal, stackable } } };
        // stub 按原段键匹配；否定语法 = 带槽位 "-20=8"（{value}={id} pattern）
        var resolver = new StubReferenceResolver
        {
            Lookup = { ["5"] = fatal, ["-20=8"] = stackable, ["20=5"] = fatal, ["8"] = stackable },
        };
        var it = MakeItem();
        it.PossessConditions = new ReferenceList<IReferenceEntry> { RawText = "5,-20=8" };
        it.UseConditions = new ReferenceList<IReferenceEntry> { RawText = "20=5" };
        it.EquipConditions = new ReferenceList<IReferenceEntry> { RawText = "8" };

        var sem = CreateExtractor(lookup, resolver).Extract(it);

        Assert.Equal(3, sem.ConditionGroups.Count);
        var carried = sem.ConditionGroups[0];
        // 无槽位 → 纯状态名 + 语义色（Fatal 红）
        Assert.Contains(carried.Conditions, c => c.Label == "流血 · FATAL" && c.Bg == "#FFEBEE");
        // 否定槽位 → 灰色 + ~ 前缀
        Assert.Contains(carried.Conditions, c => c.Label == "L-Hand: ~饱食 · Stackable" && c.Bg == "#F5F5F5");
        // 槽位前缀（20 = R-Hand）
        var used = sem.ConditionGroups[1];
        Assert.Contains(used.Conditions, c => c.Label == "L-Hand: 流血 · FATAL");
    }

    // ── 生命周期 ──────────────────────────────────────────────────────────

    [Fact]
    public void Extract_Lifecycle_DurabilityLifespanAndBreakParts()
    {
        var tt = new TreasureTable
        {
            EntityId = "9", Name = "破损产物", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x1x1" },
        };
        var knife = new ItemType { EntityId = "52", Name = "碎铁", GroupId = 0, SubgroupId = 0 };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(TreasureTable)] = new List<object> { tt },
                [typeof(ItemType)] = new List<object> { knife },
            },
        };
        var resolver = new StubReferenceResolver { Lookup = { ["9"] = tt, ["52"] = knife } };
        var it = MakeItem();
        it.DegradeTreasureIds = new ReferenceList<IReferenceEntry> { RawText = "9" };

        var sem = CreateExtractor(lookup, resolver).Extract(it);

        var lc = sem.Lifecycle!;
        Assert.EndsWith("80%", lc.Durability!.Text);           // 0.8 → 80%
        Assert.Contains(lc.LossRates, r => r.Value == "0.010");  // DegradePerHour 0.01
        // 寿命推演：0.8 / 0.01 ≈ 80h
        Assert.Contains("≈80h", lc.Lifespan!);
        // 破损产物树
        var tree = Assert.Single(lc.BreakParts);
        Assert.Equal("破损产物", tree.Title);
        Assert.Equal("碎铁", Assert.Single(tree.Items).Label);
    }

    // ── 容器 / 来源产出 ───────────────────────────────────────────────────

    [Fact]
    public void Extract_Container_And_Associations()
    {
        var ct = new ContainerType { EntityId = "3", Name = "通用背包" };
        var sw = new ItemType { EntityId = "8.1", Name = "开灯", GroupId = 8, SubgroupId = 1, Description = "亮" };
        var tt = new TreasureTable
        {
            EntityId = "7", Name = "高级战利品", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "90.1x1x1" },
        };
        var crowbar = MakeItem();
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(ContainerType)] = new List<object> { ct },
                [typeof(ItemType)] = new List<object> { crowbar, sw },
                [typeof(TreasureTable)] = new List<object> { tt },
            },
        };
        var resolver = new StubReferenceResolver
        {
            Lookup = { ["3"] = ct, ["8.1"] = sw, ["7"] = tt, ["90.1"] = crowbar },
        };
        var it = MakeItem();
        it.Capacities = "5x3";
        it.ContentIds = new ReferenceList<IReferenceEntry> { RawText = "3" };
        it.FormatId = new ReferenceList<IReferenceEntry> { RawText = "3" };
        it.SwitchIds = new ReferenceList<IReferenceEntry> { RawText = "8.1" };
        it.TreasureId = new ReferenceList<IReferenceEntry> { RawText = "7" };

        var sem = CreateExtractor(lookup, resolver).Extract(it);

        Assert.Equal("5x3", sem.Container!.Capacity);
        Assert.Equal("通用背包", sem.Container.ContentIds[0].Text);
        Assert.Equal("通用背包", sem.Container.Format);
        // Switch 徽章：G.S 前缀 + 短描述
        var swBadge = Assert.Single(sem.Associations!.Switches);
        Assert.Equal("8.1 开灯(亮)", swBadge.Text);
        // 来源战利品树
        var tree = Assert.Single(sem.Associations.LootTrees);
        Assert.Equal("高级战利品", tree.Title);
        Assert.Equal("撬门利器", Assert.Single(tree.Items).Label);
    }

    [Fact]
    public void Extract_NoContent_BlocksNull()
    {
        var it = MakeItem();
        it.Capacities = "";
        it.ContentIds = new ReferenceList<IReferenceEntry>();
        it.FormatId = new ReferenceList<IReferenceEntry>();
        it.SwitchIds = new ReferenceList<IReferenceEntry>();
        it.TreasureId = new ReferenceList<IReferenceEntry>();
        it.ComponentId = new ReferenceList<IReferenceEntry>();
        it.Durability = 0;
        it.DegradePerHour = 0;
        it.DegradePerUse = 0;
        it.EquipDegradePerHour = 0;
        it.DegradeTreasureIds = new ReferenceList<IReferenceEntry>();
        it.ChargeProfiles = new ReferenceList<IReferenceEntry>();
        it.PossessConditions = new ReferenceList<IReferenceEntry>();
        it.UseConditions = new ReferenceList<IReferenceEntry>();
        it.EquipConditions = new ReferenceList<IReferenceEntry>();
        it.CondId = new ReferenceList<IReferenceEntry>();
        it.Properties = new ReferenceList<IReferenceEntry>();
        it.AttackModes = new ReferenceList<IReferenceEntry>();

        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).Extract(it);

        Assert.Null(sem.Container);
        Assert.Null(sem.Associations);
        Assert.Null(sem.Lifecycle);
        Assert.Null(sem.Combat);
        Assert.Empty(sem.ConditionGroups);
    }
}
