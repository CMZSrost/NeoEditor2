using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>
/// D10 §四 P4 全类型铺开：B 级 7 个语义提取（AttackMode/Condition/TreasureTable/HexType/
/// Faction/BattleMove/CampType）+ D 级 10 个通用模板（反射字段表 + 特化）。全部输出
/// TemplateSemantics，JS 侧薄模板渲染器零 per-type 代码。
/// </summary>
public class TemplateSemanticsExtractorTests
{
    private static TemplateSemanticsExtractor CreateExtractor(StubEntityLookupService lookup,
        StubReferenceResolver resolver)
    {
        var shared = new SemanticsShared(lookup, resolver, new StubLocalizationService(), _ => null);
        return new TemplateSemanticsExtractor(shared, new LootTreeBuilder(lookup, resolver));
    }

    // ═══════════════ B 级 ═══════════════

    [Fact]
    public void ExtractAttackMode_CombatBlockWithMode()
    {
        var am = new AttackMode { EntityId = "14", Name = "挥击", DamageCut = 4, Morale = 0.5, Range = 1, Type = AttackType.Melee };
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).ExtractAttackMode(am);

        Assert.Single(sem.HeroBadges);   // 近战类型徽章
        Assert.Contains("Vis.CombatMelee", sem.HeroBadges[0].Text);
        var combat = Assert.Single(sem.Blocks, b => b.Mode is not null);
        Assert.Equal("挥击", combat.Mode!.Name);
        Assert.Equal(4, combat.Mode.DamageBar!.Segments[0].Value);
    }

    [Fact]
    public void ExtractCondition_SeverityAndBipolarModifiers()
    {
        var cond = new Condition { EntityId = "5", Name = "流血", Fatal = true, FieldNames = "m_fBloodLeft,MoveCost", Modifiers = "-20,3" };
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).ExtractCondition(cond);

        Assert.Contains(sem.HeroBadges, b => b.Text == "FATAL" && b.Bg == "#FFEBEE");
        // 效果区块：中文字段名翻译 + 带符号值 + 双向条
        var effect = Assert.Single(sem.Blocks, b => b.Title == "效果");
        Assert.Contains(effect.Rows, r => r.Label.StartsWith("血液总量") && r.Value == "-20");
        Assert.Contains(effect.Rows, r => r.Label.StartsWith("额外行动点消耗") && r.Value == "+3");
        Assert.Equal(2, effect.Bars.Count);
        Assert.Equal("bipolar", effect.Bars[0].Mode);
        Assert.Equal(-20, effect.Bars[0].Segments[0].Value);
    }

    [Fact]
    public void ExtractTreasureTable_LootTreeBlock()
    {
        var knife = new ItemType { EntityId = "52", Name = "猎刀", GroupId = 0, SubgroupId = 0 };
        var tt = new TreasureTable
        {
            EntityId = "7", Name = "战利品", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x1x1" },
        };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups = { [typeof(ItemType)] = new List<object> { knife } },
        };
        var sem = CreateExtractor(lookup, new StubReferenceResolver()).ExtractTreasureTable(tt);

        var loot = Assert.Single(sem.Blocks, b => b.Trees.Count > 0);
        Assert.Equal("猎刀", Assert.Single(Assert.Single(loot.Trees).Items).Label);
    }

    [Fact]
    public void ExtractHexType_LightLevelsAndRefs()
    {
        var ht = new HexType
        {
            EntityId = "h1", Name = "森林", Passable = PassableType.Passable,
            TerrainCost = 2, VizIncrease = 3, VizLimiter = 1,
            LightLevels = "0.2,1.0,0.5",
            CampItems = 3,
        };
        var cond = new Condition { EntityId = "5", Name = "潮湿" };
        ht.ConditionIds = new ReferenceList<IReferenceEntry> { RawText = "5" };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Condition)] = new List<object> { cond } } };
        var resolver = new StubReferenceResolver { Lookup = { ["5"] = cond } };
        var sem = CreateExtractor(lookup, resolver).ExtractHexType(ht);

        Assert.Contains(sem.HeroBadges, b => b.Text == "Passable");
        Assert.Contains(sem.HeroStats, s => s.Value == "能见度 +2");   // 3-1
        var light = Assert.Single(sem.Blocks, b => b.Title == "光照等级");
        // 6 时段热力格（Avalonia BuildLightPanel 同款热力公式）：
        //   0.2 → r=(0.8·198+0.2·46)=167(A7) g=50(32) b=24(18) → #A73218 红调、深字
        //   1.0 → r=46(2E) g=125(7D) → #2E7D00 满光绿、白字
        //   0.5 → r=122(7A) g=250(FA) → #7AFA00 黄（ratio≥0.5 走 g 递减支：125+125）
        Assert.Equal(6, light.LightCells.Count);
        Assert.Equal("Dawn", light.LightCells[0].Label);
        Assert.Equal("0.2", light.LightCells[0].Value);
        Assert.Equal("#A73218", light.LightCells[0].Bg);
        Assert.Equal("#333333", light.LightCells[0].Fg);
        Assert.Equal("Morning", light.LightCells[1].Label);
        Assert.Equal("#2E7D00", light.LightCells[1].Bg);
        Assert.Equal("#FFFFFF", light.LightCells[1].Fg);
        Assert.Equal("0.5", light.LightCells[2].Value);
        Assert.Equal("#7AFA00", light.LightCells[2].Bg);
        var refs = Assert.Single(sem.Blocks, b => b.BadgeGroups.Count > 0);
        Assert.Equal("潮湿", Assert.Single(refs.BadgeGroups[0].Badges).Text);
    }

    [Fact]
    public void ExtractFaction_DiplomacyBipolarBars()
    {
        var faction = new Faction { EntityId = "2", Name = "掠夺者" };
        faction.DictFactions = new ReferenceList<IReferenceEntry> { RawText = "0=100,1=-30" };
        var player = new Faction { Id = 0, EntityId = "0", Name = "玩家" };
        var neutral = new Faction { Id = 1, EntityId = "1", Name = "中立" };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups = { [typeof(Faction)] = new List<object> { player, neutral, faction } },
        };
        var sem = CreateExtractor(lookup, new StubReferenceResolver()).ExtractFaction(faction);

        var diplomacy = Assert.Single(sem.Blocks, b => b.Title == "外交关系");
        Assert.Equal(2, diplomacy.Bars.Count);
        Assert.Equal("bipolar", diplomacy.Bars[0].Mode);
        Assert.Contains("玩家", diplomacy.Bars[1].Text);
        Assert.Contains("同盟", diplomacy.Bars[1].Text);   // 100 → 同盟
        Assert.Contains("中立", diplomacy.Bars[0].Text);   // -30 → 敌对
    }

    [Fact]
    public void ExtractBattleMove_StatsAndConditionGroups()
    {
        var bm = new BattleMove { EntityId = "b1", Name = "突袭", AttackModeType = BattleMoveType.Melee, Offense = true, Chance = 0.8, Fatigue = 2 };
        bm.UsPreConditions = new ReferenceList<IReferenceEntry> { RawText = "5" };
        bm.ThemConditions = new ReferenceList<IReferenceEntry> { RawText = "-8" };
        var hungry = new Condition { EntityId = "5", Name = "饥饿" };
        var wellFed = new Condition { EntityId = "8", Name = "饱食" };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Condition)] = new List<object> { hungry, wellFed } } };
        var resolver = new StubReferenceResolver { Lookup = { ["5"] = hungry, ["8"] = wellFed } };
        var sem = CreateExtractor(lookup, resolver).ExtractBattleMove(bm);

        Assert.Contains(sem.HeroBadges, b => b.Text.Contains("近战") && b.Text.Contains("进攻"));
        var stats = Assert.Single(sem.Blocks, b => b.Title == "决策属性");
        Assert.Contains(stats.Bars, bar => bar.Text.Contains("Chance"));
        var conds = Assert.Single(sem.Blocks, b => b.BadgeGroups.Count > 0);
        Assert.Equal(2, conds.BadgeGroups.Count);
        Assert.Equal("饥饿", Assert.Single(conds.BadgeGroups[0].Badges).Text);
        Assert.Equal("NOT 饱食", Assert.Single(conds.BadgeGroups[1].Badges).Text);   // 否定红
    }

    [Fact]
    public void ExtractCampType_CampStatsAndContentsTree()
    {
        var knife = new ItemType { EntityId = "52", Name = "猎刀", GroupId = 0, SubgroupId = 0 };
        var tt = new TreasureTable
        {
            EntityId = "9", Name = "营地物资", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x1x1" },
        };
        var camp = new CampType { EntityId = "c1", Description = "避难所", Alertness = 0.5, SleepQuality = 0.8 };
        camp.TreasureId = new ReferenceList<IReferenceEntry> { RawText = "9" };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(ItemType)] = new List<object> { knife },
                [typeof(TreasureTable)] = new List<object> { tt },
            },
        };
        var resolver = new StubReferenceResolver { Lookup = { ["9"] = tt } };
        var sem = CreateExtractor(lookup, resolver).ExtractCampType(camp);

        var stats = Assert.Single(sem.Blocks, b => b.Title == "营地属性");
        Assert.Equal(5, stats.Bars.Count);
        var contents = Assert.Single(sem.Blocks, b => b.Trees.Count > 0);
        Assert.Equal("猎刀", Assert.Single(Assert.Single(contents.Trees).Items).Label);
    }

    // ═══════════════ D 级 ═══════════════

    [Fact]
    public void ExtractThin_GenericFieldTable_AllColumnsLocalized()
    {
        var gv = new GameVar { EntityId = "g1", Name = "世界天数", Type = "int", Value = "42" };
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).ExtractThin(gv);

        // Hero：Type 蓝徽章 + Value 绿色大字
        Assert.Equal("int", Assert.Single(sem.HeroBadges).Text);
        Assert.Equal("42", Assert.Single(sem.HeroStats).Value);
        // 字段表：短字段名（[Display] 名，与合并视图列名一致）——不用 FieldDescriptions
        //（长描述/实测值域太长且随数据漂移，用户反馈"字段名是字段描述？太长了"）
        var table = Assert.Single(sem.Blocks, b => b.Rows.Count > 0);
        Assert.Contains(table.Rows, r => r.Label == "Name" && r.Value == "世界天数");
        Assert.Contains(table.Rows, r => r.Label == "Type" && r.Value == "int");
        Assert.DoesNotContain(table.Rows, r => r.Label.Contains("值域") || r.Label.Contains("\n"));
    }

    [Fact]
    public void ExtractThin_Headline_TextBlock()
    {
        var h = new Headline { EntityId = "h1", HeadlineText = "广播：寒冬将至。" };
        var sem = CreateExtractor(new StubEntityLookupService(), new StubReferenceResolver()).ExtractThin(h);

        Assert.Contains(sem.HeroBadges, b => b.Text.Contains("chars"));
        var text = Assert.Single(sem.Blocks, b => b.Text is not null);
        Assert.Equal("广播：寒冬将至。", text.Text);
    }

    [Fact]
    public void ExtractThin_Ingredient_PropGroups()
    {
        var ing = new Ingredient { EntityId = "i1", Name = "胶水" };
        ing.RequiredProps = new ReferenceList<IReferenceEntry> { RawText = "p1" };
        ing.ForbidProps = new ReferenceList<IReferenceEntry> { RawText = "p2" };
        var p1 = new ItemProp { EntityId = "p1", PropertyName = "可粘合" };
        var p2 = new ItemProp { EntityId = "p2", PropertyName = "精良" };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(ItemProp)] = new List<object> { p1, p2 } } };
        var resolver = new StubReferenceResolver { Lookup = { ["p1"] = p1, ["p2"] = p2 } };
        var sem = CreateExtractor(lookup, resolver).ExtractThin(ing);

        var block = Assert.Single(sem.Blocks, b => b.BadgeGroups.Count > 0);
        Assert.Equal(2, block.BadgeGroups.Count);
        Assert.Equal("可粘合", Assert.Single(block.BadgeGroups[0].Badges).Text);
        Assert.Equal("精良", Assert.Single(block.BadgeGroups[1].Badges).Text);
    }

    [Fact]
    public void ExtractThin_CreatureSource_WeightProportion()
    {
        var cs = new CreatureSource { EntityId = "s1", Name = "北门", X = 5, Y = 5, Min = 2, Max = 4, Weight = 0.5 };
        var other = new CreatureSource { EntityId = "s2", Name = "南门", X = 5, Y = 5, Min = 1, Max = 2, Weight = 0.5 };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups = { [typeof(CreatureSource)] = new List<object> { cs, other } },
        };
        var sem = CreateExtractor(lookup, new StubReferenceResolver()).ExtractThin(cs);

        Assert.Contains(sem.HeroBadges, b => b.Text.Contains("(5, 5)"));
        var stat = Assert.Single(sem.HeroStats);
        Assert.Contains("50%", stat.Value);   // 0.5/(0.5+0.5)
    }

    [Fact]
    public void ExtractThin_EncounterTrigger_TypeBadgesAndEncounterRef()
    {
        var enc = new Encounter { EntityId = "90", Name = "加油站" };
        var trigger = new EncounterTrigger { EntityId = "t1", Name = "城市随机", LocBased = true, Unique = false, Chance = 0.3, Area = "10,20,3", DateMin = "12", DateMax = "18" };
        trigger.EncounterId = new ReferenceList<IReferenceEntry> { RawText = "90" };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object> { enc } } };
        var resolver = new StubReferenceResolver { Lookup = { ["90"] = enc } };
        var sem = CreateExtractor(lookup, resolver).ExtractThin(trigger);

        Assert.Contains(sem.HeroBadges, b => b.Text.Contains("LocBased"));
        Assert.Contains(sem.HeroBadges, b => b.Text.Contains("30%"));
        Assert.Contains("10,20,3", sem.Subtitle!);
        var refBlock = Assert.Single(sem.Blocks, b => b.Title == "触发剧情");
        Assert.Equal("加油站", Assert.Single(refBlock.Badges).Text);
    }
}
