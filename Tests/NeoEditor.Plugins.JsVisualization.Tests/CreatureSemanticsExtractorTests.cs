using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>
/// D05 语义（Creature）：Hero 徽章、战斗三层（+空手去噪/阵营关系）、属性与出场状态、
/// 战利品双池（3=空池隐藏）、遭遇三侧（事件链/出现于/刷新点权重归一）。
/// </summary>
public class CreatureSemanticsExtractorTests
{
    private static CreatureSemanticsExtractor CreateExtractor(StubEntityLookupService lookup,
        StubReferenceResolver resolver)
    {
        var shared = new SemanticsShared(lookup, resolver, new StubLocalizationService(), _ => null);
        return new CreatureSemanticsExtractor(shared, new LootTreeBuilder(lookup, resolver));
    }

    [Fact]
    public void Extract_HeroBadges_MovesAndFaction()
    {
        var faction = new Faction { EntityId = "3", Name = "掠夺者" };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Faction)] = new List<object> { faction } } };
        var resolver = new StubReferenceResolver { Lookup = { ["3"] = faction } };
        var c = new Creature { EntityId = "101", Name = "变异犬", MovesPerTurn = 2 };
        c.Faction = new ReferenceList<IReferenceEntry> { RawText = "3" };

        var sem = CreateExtractor(lookup, resolver).Extract(c);

        Assert.Equal(2, sem.HeroBadges.Count);
        Assert.Contains(sem.HeroBadges, b => b.Text == "2 moves/turn");
        Assert.Contains(sem.HeroBadges, b => b.Text == "掠夺者" && b.TargetId == "3");
    }

    // ── 战斗：空手去噪 + 阵营关系条 ───────────────────────────────────────

    [Fact]
    public void Extract_Combat_FistsOnlyNote_AndFactionRelation()
    {
        var fists = new AttackMode { EntityId = "1", Name = "拳头", Id = 1, DamageCut = 1, Morale = 0.25 };
        var faction = new Faction { EntityId = "2", Name = "中立者" };
        faction.DictFactions = new ReferenceList<IReferenceEntry> { RawText = "0=-100" };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(AttackMode)] = new List<object> { fists },
                [typeof(Faction)] = new List<object> { faction },
            },
        };
        var resolver = new StubReferenceResolver { Lookup = { ["1"] = fists, ["2"] = faction } };
        var c = new Creature { EntityId = "101", Name = "狗人" };
        c.AttackModes = new ReferenceList<IReferenceEntry> { RawText = "1" };
        c.Faction = new ReferenceList<IReferenceEntry> { RawText = "2" };

        var sem = CreateExtractor(lookup, resolver).Extract(c);

        // 全拳头（Id=1）→ 去噪注释
        Assert.Contains("Vis.FistsOnly", sem.Combat!.FistsOnlyNote);
        // 阵营关系：-100 → 敌对（红）
        Assert.Contains("-100", sem.FactionRelation!.Text);
        Assert.Contains("敌对", sem.FactionRelation.Text);
        Assert.Equal("#E57373", sem.FactionRelation.Segments[0].Color);
    }

    [Fact]
    public void Extract_FactionRelation_MissingZeroEntry_Hidden()
    {
        var faction = new Faction { EntityId = "2", Name = "中立者" };
        faction.DictFactions = new ReferenceList<IReferenceEntry> { RawText = "5=100" };   // 无 "0="
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Faction)] = new List<object> { faction } } };
        var resolver = new StubReferenceResolver { Lookup = { ["2"] = faction } };
        var c = new Creature { EntityId = "101", Name = "狗人" };
        c.Faction = new ReferenceList<IReferenceEntry> { RawText = "2" };

        var sem = CreateExtractor(lookup, resolver).Extract(c);

        Assert.Null(sem.FactionRelation);   // 解析失败静默隐藏
    }

    // ── 属性与出场状态 ─────────────────────────────────────────────────────

    [Fact]
    public void Extract_Attributes_SpawnStatusAndActivities()
    {
        var hungry = new Condition { EntityId = "5", Name = "饥饿", Fatal = true };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Condition)] = new List<object> { hungry } } };
        var resolver = new StubReferenceResolver { Lookup = { ["5=0.5"] = hungry } };
        var c = new Creature { EntityId = "101", Name = "狗人", MovesPerTurn = 3 };
        c.AttackModes = new ReferenceList<IReferenceEntry> { RawText = "1,2" };
        c.BaseConditions = new ReferenceList<IReferenceEntry> { RawText = "5=0.5" };
        c.TreasureId = new ReferenceList<IReferenceEntry> { RawText = "7" };
        c.CorpseId = new ReferenceList<IReferenceEntry> { RawText = "3" };   // 3=空池
        c.Activities = string.Join(",", Enumerable.Range(1, 35).Select(i => $"活动{i}"));

        var sem = CreateExtractor(lookup, resolver).Extract(c);

        // 属性格：行动点 3 / 攻击 2 / 出场状态 1 / 池数 1（尸体 3 跳过）
        Assert.Contains(sem.AttributeCells, cell => cell.Label == "Vis.MovesPerTurn" && cell.Value == "3");
        Assert.Contains(sem.AttributeCells, cell => cell.Label == "Vis.Attacks" && cell.Value == "2");
        Assert.Contains(sem.AttributeCells, cell => cell.Label == "Vis.LootTable" && cell.Value == "1");
        // 出场状态概率后缀
        var badge = Assert.Single(sem.SpawnStatus);
        Assert.StartsWith("饥饿", badge.Text);
        Assert.Contains("50%", badge.Text);
        Assert.Equal("#FFEBEE", badge.Bg);   // Fatal 红
        // Activities 截断 30 + "+5 more"
        Assert.Equal(31, sem.Activities.Count);
        Assert.Equal("+5 more", sem.Activities[^1]);
    }

    // ── 战利品双池 ────────────────────────────────────────────────────────

    [Fact]
    public void Extract_LootPools_SkipEmptyPool()
    {
        var tt = new TreasureTable
        {
            EntityId = "7", Name = "狗粮池", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x1x1" },
        };
        var bone = new ItemType { EntityId = "52", Name = "骨头", GroupId = 0, SubgroupId = 0 };
        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(TreasureTable)] = new List<object> { tt },
                [typeof(ItemType)] = new List<object> { bone },
            },
        };
        var resolver = new StubReferenceResolver { Lookup = { ["7"] = tt, ["52"] = bone } };
        var c = new Creature { EntityId = "101", Name = "狗人" };
        c.TreasureId = new ReferenceList<IReferenceEntry> { RawText = "7" };
        c.CorpseId = new ReferenceList<IReferenceEntry> { RawText = "3" };   // 空池

        var sem = CreateExtractor(lookup, resolver).Extract(c);

        var pool = Assert.Single(sem.LootPools);   // 只有携带池
        Assert.Equal("Vis.CarriedLoot", pool.Label);
        Assert.Equal("狗粮池", pool.Tree!.Title);
        Assert.Equal("骨头", Assert.Single(pool.Tree.Items).Label);
    }

    // ── 遭遇三侧 ──────────────────────────────────────────────────────────

    [Fact]
    public void Extract_Encounters_ChainAppearsInAndSpawnPoints()
    {
        var enc1 = new Encounter { Id = 90, EntityId = "90", Name = "加油站", Type = EncounterType.Normal };
        var enc2 = new Encounter { Id = 200, EntityId = "200", Name = "埋伏", Type = EncounterType.Scavenge };
        enc2.CreatureId = new ReferenceList<IReferenceEntry> { RawText = "101" };
        var srcA = new CreatureSource { Id = 1, EntityId = "s1", Name = "北门", X = 5, Y = 5, Min = 2, Max = 4, Weight = 0.5 };
        var srcB = new CreatureSource { Id = 2, EntityId = "s2", Name = "南门", X = 5, Y = 5, Min = 1, Max = 2, Weight = 0.5 };
        var srcOther = new CreatureSource { Id = 3, EntityId = "s3", Name = "别处", X = 9, Y = 9, Min = 1, Max = 1, Weight = 1.0 };
        srcA.CreatureId = new ReferenceList<IReferenceEntry> { RawText = "101" };
        srcB.CreatureId = new ReferenceList<IReferenceEntry> { RawText = "101" };
        srcOther.CreatureId = new ReferenceList<IReferenceEntry> { RawText = "999" };

        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(Encounter)] = new List<object> { enc1, enc2 },
                [typeof(CreatureSource)] = new List<object> { srcA, srcB, srcOther },
            },
        };
        var resolver = new StubReferenceResolver { Lookup = { ["90"] = enc1 } };
        var c = new Creature { Id = 101, EntityId = "101", Name = "狗人" };
        c.EncounterIds = new ReferenceList<IReferenceEntry> { RawText = "90" };

        var sem = CreateExtractor(lookup, resolver).Extract(c);

        // 正向事件链：名 + 类型标签
        var chain = Assert.Single(sem.EncounterChain);
        Assert.StartsWith("加油站", chain.Text);
        Assert.Contains("Vis.EncTypeStory", chain.Text);
        // 反向出现于：Encounter.CreatureId → 本生物（含 creatureHex）
        var appears = Assert.Single(sem.AppearsIn);
        Assert.Equal("埋伏", appears.Text);
        // 刷新点：同点 (5,5) 权重归一 → 各占 50%
        Assert.Equal(2, sem.SpawnPoints.Count);
        foreach (var sp in sem.SpawnPoints)
        {
            Assert.Equal("(5, 5)", sp.Position);
            Assert.Contains("50%", sp.WeightText);
        }
        Assert.All(sem.SpawnPoints, sp => Assert.Equal("CreatureSource", sp.TargetType));
    }
}
