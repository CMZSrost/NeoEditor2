using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>
/// D09 §六 (AI verification loop): generates samples/*.json from constructed
/// encounters via the SAME extraction pipeline the /viz/data endpoint uses —
/// the browser opens index.html?sample=encounter90 and renders the real snapshot
/// contract. Files land in the plugin's Web/viz/samples (shipped as Content).
/// </summary>
public class SampleSnapshotGeneratorTests
{
    private static readonly string SamplesDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory,
            "../../../../../NeoEditor.Plugins.JsVisualization/Web/viz/samples"));

    /// <summary>samples 走真实键值（stub 只返回键名，会污染页面文案）。</summary>
    private sealed class SampleLoc : ILocalizationService
    {
        private static readonly Dictionary<string, string> Map = new()
        {
            ["Vis.Consumed"] = "（消耗）",
            ["Vis.RequireAll"] = "需同时拥有：",
            ["Vis.TypeStory"] = "剧情", ["Vis.TypeScavenge"] = "搜刮",
            ["Vis.TypeCombat"] = "战斗", ["Vis.TypeHack"] = "破解", ["Vis.TypeUnknown"] = "类型 {0}",
            ["Vis.StayEnd"] = "⏹ 停留", ["Vis.BlankEnd"] = "☰ 无后续",
            ["Vis.HexTypesShort"] = "🧱 格类型", ["Vis.Repeatable"] = "♻ 可重复",
            ["Vis.GiveLoot"] = "🎁 获得战利品", ["Vis.LootPool"] = "🎁 战利品池",
            ["Vis.GiveItem"] = "📦 给予物品", ["Vis.Cost"] = "💰 费用",
            ["Vis.RemoveLoot"] = "🗑 移除战利品", ["Vis.TeleportTo"] = "📍 传送至",
            ["Vis.SpawnOut"] = "刷出", ["Vis.Accidents"] = "💥 意外",
            ["Vis.MapNotes"] = "🗺 地图标注",
        };

        public string this[string key] => Map.TryGetValue(key, out var v) ? v : key;
        public string this[string key, params object[] args] => Map.TryGetValue(key, out var v) ? v : key;
        public System.Globalization.CultureInfo CurrentCulture => System.Globalization.CultureInfo.InvariantCulture;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void SetCulture(System.Globalization.CultureInfo culture) { }
    }

    [Fact]
    public void Generate_Encounter90_Sample()
    {
        // ── 关联数据：物品 / 条件 / 触发器 / 前驱剧情 ──────────────────────
        var crowbar = new ItemType { EntityId = "90.1", Name = "撬棍", GroupId = 90, SubgroupId = 1 };
        var knife = new ItemType { EntityId = "52", Name = "猎刀" };
        var lootTable = new TreasureTable { EntityId = "3", Name = "通用战利品" };

        var condHungry = new Condition { EntityId = "5", Name = "饥饿", Fatal = true };
        var condCarried = new Condition { EntityId = "8", Name = "携带武器", Stackable = true, Duration = 5 };

        var trigger = new EncounterTrigger
        {
            EntityId = "t1", Name = "城市随机遭遇",
            Area = "10,20,3", DateMin = "12", DateMax = "18", Unique = false,
            EncounterId = new ReferenceList<IReferenceEntry> { new EntityRef { Id = "90" } },
        };

        // ── 前驱（反查：谁通向 90）─────────────────────────────────────────
        var pred = new Encounter
        {
            Id = 41, EntityId = "41", Name = "翻找垃圾桶",
            Type = EncounterType.Normal,
            Responses = "90.1x1=90x1x0x0x0",
        };

        // ── 当前场景 90 ────────────────────────────────────────────────────
        var enc = new Encounter
        {
            Id = 90, EntityId = "90", ModId = -1, Name = "加油站便利店",
            Type = EncounterType.Normal,
            Description = "废弃的加油站便利店。货架被洗劫一空，收银台后的收音机还在沙沙作响。" +
                          "你闻到一股刺鼻的汽油味，夹杂着某种腐烂的甜腻气息……",
            Image = new ReferenceList<IReferenceEntry> { new EntityRef { Id = "img/scenario/gas_station.png" } },
            Price = 5,
            LootChance = 0.65,
            AccidentChance = 0.2,
            CreatureChance = 0.35,
            Loot = new ReferenceList<IReferenceEntry> { RawText = "3" },
            TreasureId = new ReferenceList<IReferenceEntry> { RawText = "7" },
            ItemsId = new ReferenceList<IReferenceEntry> { RawText = "52" },
            RemoveTreasureId = new ReferenceList<IReferenceEntry> { RawText = "9" },
            Teleport = "12,8",
            CreatureId = new ReferenceList<IReferenceEntry> { RawText = "101" },
            CreatureHex = "2,1",
            Accidents = new ReferenceList<IReferenceEntry> { RawText = "200,201" },
            MinimapHexes = "5,5=加油站,7,9=废弃仓库",
            Editor = "3,3",
            RemoveCreatures = true,
            Conditions = new ReferenceList<IReferenceEntry> { RawText = "5,1" },
            PreConditions = new ReferenceList<IReferenceEntry> { RawText = "-3" },
            // D07 语法：物品触发（含 p2 消耗 / p3 成功率）＋ 默认响应 ＋ 自指停留
            Responses =
                "90.1x1=12x1x0x0x0" +
                ",52x2=12x1x1x0x0" +      // 消耗猎刀×2，p3 成功率
                ",=9x2x0x0x0" +
                ",=90x1x0x0x0",           // 自指 → ⏹ 停留原地
        };

        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(Encounter)] = new List<object> { pred, enc },
                [typeof(ItemType)] = new List<object> { crowbar, knife },
                [typeof(TreasureTable)] = new List<object> { lootTable },
                [typeof(Condition)] = new List<object> { condHungry, condCarried },
                [typeof(EncounterTrigger)] = new List<object> { trigger },
            },
        };
        var resolver = new StubReferenceResolver
        {
            Lookup =
            {
                ["90.1"] = crowbar, ["52"] = knife,
                ["3"] = lootTable, ["7"] = new TreasureTable { EntityId = "7", Name = "高级战利品" },
                ["9"] = new TreasureTable { EntityId = "9", Name = "负产物" },
                ["101"] = new Creature { EntityId = "101", Name = "变异犬" },
                ["5"] = condHungry, ["8"] = condCarried,
                ["200"] = new Encounter { EntityId = "200", Name = "店外枪声" },
                ["201"] = new Encounter { EntityId = "201", Name = "天花板塌陷" },
            },
        };
        var extractor = new EncounterSemanticsExtractor(lookup, resolver, new SampleLoc(),
            path => path); // findImage: 原样返回 → /viz/assets?path=…（浏览器里 404 则占位图）
        var service = new VizSnapshotService(new StubHostService(), new StubXmlParser(), lookup, extractor);

        var snapshot = service.BuildById("Encounter", "90");
        Assert.NotNull(snapshot);

        Directory.CreateDirectory(SamplesDir);
        var target = Path.Combine(SamplesDir, "encounter90.json");
        File.WriteAllText(target, service.Serialize(snapshot!));

        // 自检：samples 契约的关键结构
        var json = File.ReadAllText(target);
        Assert.Contains("\"type\": \"Encounter\"", json);
        Assert.Contains("加油站便利店", json);
        Assert.Contains("\"semantics\"", json);
        Assert.Contains("\"branches\"", json);   // 流转三行数据在
        Assert.Contains("\"predecessors\"", json);

        // 前驱 41 的快照：sample 模式下焦点切换（autoplay 动画验证）的本地数据源
        var predSnapshot = service.BuildById("Encounter", "41");
        Assert.NotNull(predSnapshot);
        File.WriteAllText(Path.Combine(SamplesDir, "encounter41.json"), service.Serialize(predSnapshot!));
    }

    [Fact]
    public void Generate_NonEncounter_Sample()
    {
        // 非 Encounter 类型 → 通用快照（semantics=null）→ 页面显示"渲染器未实现"兜底
        var item = new ItemType { EntityId = "52", Name = "猎刀", GroupId = 0, SubgroupId = 0 };
        var extractor = new EncounterSemanticsExtractor(new StubEntityLookupService(),
            new StubReferenceResolver(), new StubLocalizationService(), _ => null);
        var service = new VizSnapshotService(new StubHostService { Cache = { ["52"] = item } },
            new StubXmlParser(), new StubEntityLookupService(), extractor);

        var snapshot = service.BuildById("ItemType", "52");
        Assert.NotNull(snapshot);

        Directory.CreateDirectory(SamplesDir);
        File.WriteAllText(Path.Combine(SamplesDir, "itemtype52.json"), service.Serialize(snapshot!));
    }
}
