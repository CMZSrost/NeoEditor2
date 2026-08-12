using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.JsVisualization.Services;
using NeoEditor.Services;
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
            ["Vis.RawFields"] = "{0} 字段 · {1} 有值", ["Vis.RawUnresolved"] = " · {0} 未解析",
            ["Vis.WhenCarried"] = "携带时", ["Vis.WhenUsed"] = "使用时", ["Vis.WhenEquipped"] = "装备时",
            ["Vis.RequiredCondition"] = "辨识条件", ["Vis.Properties"] = "属性",
            ["Vis.Durability"] = "耐久", ["Vis.PerHour"] = "每小时", ["Vis.PerHourEquipped"] = "装备每小时",
            ["Vis.PerUse"] = "每次", ["Vis.Lifespan"] = "寿命推演", ["Vis.BreakParts"] = "破损产物",
            ["Vis.Capacity"] = "容量", ["Vis.AcceptsContent"] = "可容纳", ["Vis.Format"] = "格式",
            ["Vis.SwitchStates"] = "切换状态", ["Vis.TreasureTable"] = "来源战利品表", ["Vis.Component"] = "组件",
            ["Vis.UsedBy"] = "被使用", ["Vis.ShopBuys"] = "收购", ["Vis.ShopSells"] = "出售",
            ["Vis.Position"] = "位置", ["Vis.ShopInfo"] = "商店信息", ["Vis.RestockTT"] = "补货战利品表",
            ["Vis.MapDefinition"] = "地图定义", ["Vis.Yes"] = "是", ["Vis.No"] = "否",
            ["Vis.FistsOnly"] = "仅有空手攻击", ["Vis.TowardPlayer"] = "对玩家",
            ["Vis.MovesPerTurn"] = "行动点", ["Vis.Attacks"] = "攻击模式", ["Vis.SpawnStatus"] = "出场状态",
            ["Vis.LootTable"] = "战利品池", ["Vis.Activities"] = "日常行为", ["Vis.EncounterChain"] = "出场事件链",
            ["Vis.AppearsIn"] = "会出现在", ["Vis.SpawnPoints"] = "刷新点",
            ["Vis.CarriedLoot"] = "随身携带", ["Vis.CorpseLoot"] = "尸体掉落", ["Vis.Hidden"] = "隐藏配方",
            ["Vis.Range"] = "射程", ["Vis.Penetration"] = "穿透", ["Morale"] = "士气",
            ["Vis.Effective"] = "有效伤害", ["Vis.CombatMelee"] = "近战", ["Vis.CombatRanged"] = "远程",
            ["Vis.ChargeAmmo"] = "弹药", ["Vis.AttackerConditions"] = "攻击者条件",
            ["Vis.AttackPhrases"] = "攻击短语", ["Vis.CtrlClickHint"] = "Ctrl+点击 跳转 / Ctrl+右键 预览",
            ["Vis.EncTypeStory"] = "剧情", ["Vis.EncTypeScavenge"] = "搜刮",
            ["Vis.EncTypeCombat"] = "战斗", ["Vis.EncTypeHack"] = "破解",
            ["Vis.Secret"] = "秘密名", ["Vis.Hours"] = "耗时", ["Vis.Reverse"] = "可逆",
            ["Vis.DegradeOutput"] = "降级产物", ["Vis.Required"] = "必需", ["Vis.Forbidden"] = "禁止",
            ["Vis.Ingredients"] = "原料", ["Vis.Tools"] = "工具", ["Vis.Consumed"] = "消耗",
            ["Vis.Identified"] = "已辨识", ["Vis.Mirrored"] = "镜像", ["Vis.SlotDepth"] = "槽深",
            ["Vis.EquipSlots"] = "装备槽位", ["Vis.UseSlots"] = "使用槽位", ["Vis.SocketLocked"] = "插槽锁定",
            ["Vis.Sound"] = "音效", ["Vis.Loot"] = "战利品", ["Vis.Combat"] = "战斗",
            ["Vis.Equipment"] = "装备", ["Vis.Effects"] = "效果", ["Vis.Lifecycle"] = "生命周期",
            ["Vis.Container"] = "容器", ["Vis.Associations"] = "来源与产出", ["Vis.Attributes"] = "属性与出场状态",
            ["Vis.Encounters"] = "遭遇",
        };

        public string this[string key] => Map.TryGetValue(key, out var v) ? v : key;
        public string this[string key, params object[] args] => Map.TryGetValue(key, out var v)
            ? string.Format(v, args) : key;
        public System.Globalization.CultureInfo CurrentCulture => System.Globalization.CultureInfo.InvariantCulture;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void SetCulture(System.Globalization.CultureInfo culture) { }
    }

    [Fact]
    public void Generate_Encounter90_Sample()
    {
        // ── 关联数据：物品 / 条件 / 触发器 / 前驱剧情 ──────────────────────
        var crowbar = new ItemType { EntityId = "90.1", Name = "撬棍", GroupId = 90, SubgroupId = 1 };
        var knife = new ItemType { EntityId = "52", Name = "猎刀", GroupId = 0, SubgroupId = 0 };
        var lootTable = new TreasureTable { EntityId = "3", Name = "通用战利品" };
        // P1: 效果区战利品树 —— 表内放实际条目（G.S 键 "90.1" / 裸键 "0.0"）
        var advancedLoot = new TreasureTable
        {
            EntityId = "7", Name = "高级战利品", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "90.1x1x2,0.0x2x1" },
        };
        var badLoot = new TreasureTable
        {
            EntityId = "9", Name = "负产物", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x1x1" },
        };

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
                ["3"] = lootTable, ["7"] = advancedLoot, ["9"] = badLoot,
                ["101"] = new Creature { EntityId = "101", Name = "变异犬" },
                ["5"] = condHungry, ["8"] = condCarried,
                ["200"] = new Encounter { EntityId = "200", Name = "店外枪声" },
                ["201"] = new Encounter { EntityId = "201", Name = "天花板塌陷" },
            },
        };
        var extractor = new EncounterSemanticsExtractor(lookup, resolver, new SampleLoc(),
            path => path, new LootTreeBuilder(lookup, resolver)); // findImage: 原样返回 → /viz/assets?path=…（浏览器里 404 则占位图）
        var service = new VizSnapshotService(new StubHostService(), new StubXmlParser(), lookup,
            extractor,
            new ItemTypeSemanticsExtractor(new SemanticsShared(lookup, resolver, new SampleLoc(), path => path),
                new LootTreeBuilder(lookup, resolver)),
            new CreatureSemanticsExtractor(new SemanticsShared(lookup, resolver, new SampleLoc(), path => path),
                new LootTreeBuilder(lookup, resolver)),
            new RecipeSemanticsExtractor(new SemanticsShared(lookup, resolver, new SampleLoc(), path => path),
                new LootTreeBuilder(lookup, resolver)),
            new ThinSemanticsExtractor(new SemanticsShared(lookup, resolver, new SampleLoc(), path => path)),
            new TemplateSemanticsExtractor(new SemanticsShared(lookup, resolver, new SampleLoc(), path => path),
                new LootTreeBuilder(lookup, resolver)),
            new SemanticsShared(lookup, resolver, new SampleLoc(), path => path));

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

    /// <summary>P1: ItemType 样本（猎刀，完整语义：战斗/效果/生命周期/容器/来源产出）。
    /// P0 时为"非 Encounter → semantics=null"兜底样本，P1 起为真实 ItemType 渲染验证资产。</summary>
    [Fact]
    public void Generate_ItemType_Sample()
    {
        var knife = new ItemType
        {
            EntityId = "52", Name = "猎刀", GroupId = 0, SubgroupId = 0,
            Description = "锋利的猎刀，适合剥皮与近战。",
            DescriptionAlt = "刀刃有缺口的旧猎刀",
            Weight = 0.8, MonetaryValue = 12.0, MonetaryValueAlt = 30.0, StackLimit = 1,
            Durability = 0.6, DegradePerHour = 0.005, DegradePerUse = 0.02,
            Capacities = "2x3", SocketLocked = false,
            Mirrored = true, SlotDepth = 1,
        };
        knife.AttackModes = new ReferenceList<IReferenceEntry> { RawText = "21=14,5=15" };
        knife.PossessConditions = new ReferenceList<IReferenceEntry> { RawText = "5" };
        knife.UseConditions = new ReferenceList<IReferenceEntry> { RawText = "20=5" };
        knife.EquipConditions = new ReferenceList<IReferenceEntry> { RawText = "6" };
        knife.CondId = new ReferenceList<IReferenceEntry> { RawText = "6" };
        knife.Properties = new ReferenceList<IReferenceEntry> { RawText = "p1" };
        knife.ContentIds = new ReferenceList<IReferenceEntry> { RawText = "3" };
        knife.FormatId = new ReferenceList<IReferenceEntry> { RawText = "3" };
        knife.SwitchIds = new ReferenceList<IReferenceEntry> { RawText = "90.1" };
        knife.TreasureId = new ReferenceList<IReferenceEntry> { RawText = "8" };
        knife.ComponentId = new ReferenceList<IReferenceEntry> { RawText = "7" };
        knife.DegradeTreasureIds = new ReferenceList<IReferenceEntry> { RawText = "9" };
        knife.ChargeProfiles = new ReferenceList<IReferenceEntry> { RawText = "c1" };
        knife.EquipSlots = "21=0=1";
        knife.UseSlots = "211";
        knife.Sounds = "cueKnifeSwing,cueKnifeHit";

        var slash = new AttackMode { EntityId = "14", Name = "劈砍", DamageCut = 4, DamageBlunt = 1, Morale = 0.5, Range = 1, Penetration = 1, Type = AttackType.Melee };
        var stab = new AttackMode { EntityId = "15", Name = "突刺", DamageCut = 2, DamageBlunt = 2, Morale = 0.25, Range = 1, Transfer = true };
        var hungry = new Condition { EntityId = "5", Name = "饥饿", Fatal = true };
        var wellFed = new Condition { EntityId = "6", Name = "饱食", Stackable = true, Duration = 12 };
        var prop = new ItemProp { EntityId = "p1", PropertyName = "可剥皮" };
        var ct = new ContainerType { EntityId = "3", Name = "通用背包" };
        var crowbar = new ItemType { EntityId = "90.1", Name = "开灯", GroupId = 90, SubgroupId = 1, Description = "亮" };
        var lootTt = new TreasureTable { EntityId = "8", Name = "尸体池", ModId = -1, Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x2x1" } };
        var compTt = new TreasureTable { EntityId = "7", Name = "组件池", ModId = -1, Treasures = new ReferenceList<IReferenceEntry> { RawText = "9x1x1" } };
        var breakTt = new TreasureTable { EntityId = "9", Name = "破损碎片", ModId = -1, Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x1x3" } };
        var bone = new ItemType { EntityId = "0.0", Name = "骨头", GroupId = 0, SubgroupId = 0 };
        var charge = new ChargeProfile { EntityId = "c1", Name = "猎刀弹药", PerUse = 0.5, Degrade = true };

        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(ItemType)] = new List<object> { knife, crowbar, bone },
                [typeof(AttackMode)] = new List<object> { slash, stab },
                [typeof(Condition)] = new List<object> { hungry, wellFed },
                [typeof(ItemProp)] = new List<object> { prop },
                [typeof(ContainerType)] = new List<object> { ct },
                [typeof(TreasureTable)] = new List<object> { lootTt, compTt, breakTt },
                [typeof(ChargeProfile)] = new List<object> { charge },
                [typeof(Encounter)] = new List<object> { new Encounter { EntityId = "90", Name = "加油站" } },
            },
        };
        // 反向引用：加油站（nItemsID）引用猎刀 → RefPanel 聚合
        var store = new EntityMergeStore();
        var index = ReferenceIndexService.CreateInMemory();
        index.Open();
        index.AddReverse("52", "90", "nItemsID", "52");
        store.ReferenceLookups[typeof(Encounter)] = lookup.ReferenceLookups[typeof(Encounter)];
        store.IndexService = index;
        lookup.ActiveMergeStore = store;
        var resolver = new StubReferenceResolver
        {
            Lookup =
            {
                ["21=14"] = slash, ["5=15"] = stab,
                ["5"] = hungry, ["20=5"] = hungry, ["6"] = wellFed,
                ["p1"] = prop, ["3"] = ct, ["90.1"] = crowbar,
                ["8"] = lootTt, ["7"] = compTt, ["9"] = breakTt,
                ["0.0"] = bone, ["c1"] = charge,
            },
        };
        var service = TestSemantics.CreateService(
            new StubHostService { Cache = { ["52"] = knife } }, new StubXmlParser(), lookup, resolver,
            new SampleLoc());

        var snapshot = service.BuildById("ItemType", "52");
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot!.Semantics as ItemTypeSemantics);

        Directory.CreateDirectory(SamplesDir);
        File.WriteAllText(Path.Combine(SamplesDir, "itemtype52.json"), service.Serialize(snapshot!));
    }

    /// <summary>P1 样本：Creature / Recipe / ContainerType / BarterHex / Map
    /// —— 构造真实感数据，走与 /viz/data 同一提取管线（浏览器 ?sample= 直接渲染）。</summary>
    [Fact]
    public void Generate_P1_TypeSamples()
    {
        var loc = new SampleLoc();

        // ── 关联数据：攻击模式 / 条件 / 阵营 / 战利品表 / 物品 ─────────────
        var claw = new AttackMode { EntityId = "14", Name = "撕咬", DamageCut = 3, DamageBlunt = 2, Morale = 0.5, Range = 1 };
        var fists = new AttackMode { EntityId = "1", Name = "拳头", Id = 1, DamageCut = 1 };
        var hungry = new Condition { EntityId = "5", Name = "饥饿", Fatal = true };
        var pack = new Condition { EntityId = "8", Name = "成群", Stackable = true, Duration = 6 };
        var faction = new Faction { EntityId = "2", Name = "掠夺者" };
        faction.DictFactions = new ReferenceList<IReferenceEntry> { RawText = "0=-100" };

        var dogFood = new TreasureTable
        {
            EntityId = "7", Name = "狗粮池", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x1x2,1.0x1x1" },
        };
        var corpseLoot = new TreasureTable
        {
            EntityId = "8", Name = "尸体池", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "52x2x1" },
        };
        var bone = new ItemType { EntityId = "0.0", Name = "骨头", GroupId = 0, SubgroupId = 0 };
        var hide = new ItemType { EntityId = "1.0", Name = "兽皮", GroupId = 1, SubgroupId = 0, Description = "剥下的兽皮" };
        var knife = new ItemType { EntityId = "52", Name = "猎刀", GroupId = 0, SubgroupId = 0 };
        var glue = new Ingredient
        {
            EntityId = "i1", Name = "胶水",
            RequiredProps = new ReferenceList<IReferenceEntry> { RawText = "p1" },
        };
        var prop = new ItemProp { EntityId = "p1", PropertyName = "可粘合" };

        // ── Creature 101：变异犬 ───────────────────────────────────────────
        var enc1 = new Encounter { Id = 90, EntityId = "90", Name = "加油站", Type = EncounterType.Normal };
        var enc2 = new Encounter { Id = 200, EntityId = "200", Name = "埋伏", Type = EncounterType.Scavenge };
        enc2.CreatureId = new ReferenceList<IReferenceEntry> { RawText = "101" };
        var srcA = new CreatureSource { Id = 1, EntityId = "s1", Name = "北门", X = 5, Y = 5, Min = 2, Max = 4, Weight = 0.5 };
        var srcB = new CreatureSource { Id = 2, EntityId = "s2", Name = "南门", X = 5, Y = 5, Min = 1, Max = 2, Weight = 0.5 };
        srcA.CreatureId = new ReferenceList<IReferenceEntry> { RawText = "101" };
        srcB.CreatureId = new ReferenceList<IReferenceEntry> { RawText = "101" };
        var dog = new Creature { Id = 101, EntityId = "101", Name = "变异犬", MovesPerTurn = 2, Activities = "巡逻,吠叫,啃食" };
        dog.AttackModes = new ReferenceList<IReferenceEntry> { RawText = "14,1" };
        dog.BaseConditions = new ReferenceList<IReferenceEntry> { RawText = "5=0.5,8=0.3" };
        dog.TreasureId = new ReferenceList<IReferenceEntry> { RawText = "7" };
        dog.CorpseId = new ReferenceList<IReferenceEntry> { RawText = "8" };
        dog.EncounterIds = new ReferenceList<IReferenceEntry> { RawText = "90" };
        dog.Faction = new ReferenceList<IReferenceEntry> { RawText = "2" };

        // ── Recipe r1：剥皮刀（工具/消耗/产物）─────────────────────────────
        var tool = new Ingredient { EntityId = "i2", Name = "小刀" };
        var recipe = new Recipe { EntityId = "r1", Name = "剥皮", Type = "手工", Hours = 1.5, Reverse = 1, Scrap = true };
        recipe.Tools = new ReferenceList<IReferenceEntry> { RawText = "1xi2" };
        recipe.Consumed = new ReferenceList<IReferenceEntry> { RawText = "1xi1" };
        recipe.TreasureId = new ReferenceList<IReferenceEntry> { RawText = "8" };

        // ── C 级：ContainerType / BarterHex / Map ──────────────────────────
        var store = new EntityMergeStore();
        var index = ReferenceIndexService.CreateInMemory();
        index.Open();
        index.AddReverse("3", "52", "aContentIDs", "3");
        index.AddReverse("101", "200", "nCreatureID", "101");   // 遭遇 200 引用变异犬
        index.AddReverse("r1", "52", "nTreasureID", "r1");      // 猎刀出现在配方产物
        store.ReferenceLookups[typeof(ItemType)] = new List<object> { bone, hide, knife };
        store.IndexService = index;
        var container = new ContainerType { EntityId = "3", Name = "通用背包" };
        var barter = new BarterHex { EntityId = "b1", X = 5, Y = 7, Buys = true, RestockTreasureId = 7 };
        var map = new Map { EntityId = "m1", Name = "img/map/shelter.png", Definition = string.Join(",", Enumerable.Repeat("1", 120)) };

        var lookup = new StubEntityLookupService
        {
            ActiveMergeStore = store,
            ReferenceLookups =
            {
                [typeof(AttackMode)] = new List<object> { claw, fists },
                [typeof(Condition)] = new List<object> { hungry, pack },
                [typeof(Faction)] = new List<object> { faction },
                [typeof(TreasureTable)] = new List<object> { dogFood, corpseLoot },
                [typeof(ItemType)] = new List<object> { bone, hide, knife },
                [typeof(Ingredient)] = new List<object> { glue, tool },
                [typeof(ItemProp)] = new List<object> { prop },
                [typeof(Encounter)] = new List<object> { enc1, enc2 },
                [typeof(CreatureSource)] = new List<object> { srcA, srcB },
                [typeof(Creature)] = new List<object> { dog },
                [typeof(Recipe)] = new List<object> { recipe },
                [typeof(ContainerType)] = new List<object> { container },
                [typeof(BarterHex)] = new List<object> { barter },
                [typeof(Map)] = new List<object> { map },
            },
        };
        var resolver = new StubReferenceResolver
        {
            Lookup =
            {
                ["14"] = claw, ["1"] = fists,
                ["5=0.5"] = hungry, ["8=0.3"] = pack, ["5"] = hungry, ["8"] = pack,
                ["2"] = faction, ["7"] = dogFood, ["8"] = corpseLoot,
                ["0.0"] = bone, ["1.0"] = hide, ["52"] = knife,
                ["1xi2"] = tool, ["1xi1"] = glue, ["p1"] = prop,
                ["90"] = enc1, ["101"] = dog,
            },
        };
        var service = TestSemantics.CreateService(new StubHostService(), new StubXmlParser(), lookup, resolver, loc);

        Directory.CreateDirectory(SamplesDir);
        var samples = new (string File, IEntity Entity, string Type)[]
        {
            ("creature101.json", dog, "Creature"),
            ("recipe1.json", recipe, "Recipe"),
            ("containertype3.json", container, "ContainerType"),
            ("barterhex1.json", barter, "BarterHex"),
            ("map1.json", map, "Map"),
        };
        foreach (var (file, entity, type) in samples)
        {
            var snapshot = service.BuildById(type, entity.EntityId);
            Assert.NotNull(snapshot);
            File.WriteAllText(Path.Combine(SamplesDir, file), service.Serialize(snapshot!));
        }

        // 自检：样本契约的关键结构
        Assert.Contains("\"combat\"", File.ReadAllText(Path.Combine(SamplesDir, "creature101.json")));
        Assert.Contains("\"spawnPoints\"", File.ReadAllText(Path.Combine(SamplesDir, "creature101.json")));
        Assert.Contains("\"ingredientGroups\"", File.ReadAllText(Path.Combine(SamplesDir, "recipe1.json")));
        Assert.Contains("\"blocks\"", File.ReadAllText(Path.Combine(SamplesDir, "containertype3.json")));
        Assert.Contains("\"blocks\"", File.ReadAllText(Path.Combine(SamplesDir, "map1.json")));
    }

    /// <summary>P4 全类型铺开：B/D 级代表样本（AttackMode/Condition/TreasureTable/Faction/
    /// BattleMove/GameVar/EncounterTrigger/HexType/Ingredient）——24 类型全覆盖验证资产。</summary>
    [Fact]
    public void Generate_RemainingType_Samples()
    {
        var loc = new SampleLoc();

        var knife = new ItemType { EntityId = "0.0", Name = "骨头", GroupId = 0, SubgroupId = 0 };
        var tt = new TreasureTable
        {
            EntityId = "7", Name = "高级战利品", ModId = -1,
            Treasures = new ReferenceList<IReferenceEntry> { RawText = "0.0x1x2" },
        };
        var hungry = new Condition
        {
            EntityId = "5", Name = "饥饿", Fatal = true,
            FieldNames = "m_fBloodLeft,MoveCost", Modifiers = "-20,3",
            Effects = "流血不止……",
        };
        var wellFed = new Condition { EntityId = "6", Name = "饱食", Stackable = true, Duration = 12 };
        var am = new AttackMode
        {
            EntityId = "14", Name = "劈砍", DamageCut = 4, DamageBlunt = 1, Morale = 0.5,
            Range = 1, Type = AttackType.Melee, Penetration = 1, Sound = "cueSwing",
            WieldPhrase = "猛地一挥", Notes = "近战主武器",
        };
        var faction = new Faction { EntityId = "2", Name = "掠夺者" };
        faction.DictFactions = new ReferenceList<IReferenceEntry> { RawText = "0=100,1=-30" };
        var player = new Faction { Id = 0, EntityId = "0", Name = "玩家" };
        var neutral = new Faction { Id = 1, EntityId = "1", Name = "中立" };
        var bm = new BattleMove
        {
            EntityId = "b1", Name = "突袭", StrId = "AMBUSH", AttackModeType = BattleMoveType.Melee,
            Offense = true, Chance = 0.8, Fatigue = 2, PopUp = "敌人发动突袭！", Success = "成功接近", Fail = "被识破",
        };
        bm.UsPreConditions = new ReferenceList<IReferenceEntry> { RawText = "5" };
        bm.ThemConditions = new ReferenceList<IReferenceEntry> { RawText = "-6" };
        var gv = new GameVar { EntityId = "g1", Name = "世界天数", Type = "int", Value = "42" };
        var trigger = new EncounterTrigger
        {
            EntityId = "t1", Name = "城市随机", LocBased = true, Chance = 0.3,
            Area = "10,20,3", DateMin = "12", DateMax = "18",
        };
        trigger.EncounterId = new ReferenceList<IReferenceEntry> { RawText = "90" };
        var enc = new Encounter { EntityId = "90", Name = "加油站" };
        var ht = new HexType
        {
            EntityId = "h1", Name = "森林", Passable = PassableType.Passable,
            TerrainCost = 2, VizIncrease = 3, VizLimiter = 1, CampItems = 3,
            LightLevels = "0.2,1.0,0.5,0.3",
        };
        ht.ConditionIds = new ReferenceList<IReferenceEntry> { RawText = "5" };
        var ing = new Ingredient { EntityId = "i1", Name = "胶水" };
        ing.RequiredProps = new ReferenceList<IReferenceEntry> { RawText = "p1" };
        var prop = new ItemProp { EntityId = "p1", PropertyName = "可粘合" };

        var lookup = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(ItemType)] = new List<object> { knife },
                [typeof(TreasureTable)] = new List<object> { tt },
                [typeof(Condition)] = new List<object> { hungry, wellFed },
                [typeof(Faction)] = new List<object> { player, neutral, faction },
                [typeof(Encounter)] = new List<object> { enc },
                [typeof(ItemProp)] = new List<object> { prop },
                [typeof(AttackMode)] = new List<object> { am },
                [typeof(BattleMove)] = new List<object> { bm },
                [typeof(GameVar)] = new List<object> { gv },
                [typeof(EncounterTrigger)] = new List<object> { trigger },
                [typeof(HexType)] = new List<object> { ht },
                [typeof(Ingredient)] = new List<object> { ing },
            },
        };
        var resolver = new StubReferenceResolver
        {
            Lookup =
            {
                ["0.0"] = knife, ["7"] = tt, ["5"] = hungry, ["6"] = wellFed,
                ["90"] = enc, ["p1"] = prop,
            },
        };
        var service = TestSemantics.CreateService(new StubHostService(), new StubXmlParser(), lookup, resolver, loc);

        Directory.CreateDirectory(SamplesDir);
        var samples = new (string File, IEntity Entity, string Type)[]
        {
            ("attackmode14.json", am, "AttackMode"),
            ("condition5.json", hungry, "Condition"),
            ("treasuretable7.json", tt, "TreasureTable"),
            ("faction2.json", faction, "Faction"),
            ("battlemove1.json", bm, "BattleMove"),
            ("gamevar1.json", gv, "GameVar"),
            ("encountertrigger1.json", trigger, "EncounterTrigger"),
            ("hextype1.json", ht, "HexType"),
            ("ingredient1.json", ing, "Ingredient"),
        };
        foreach (var (file, entity, type) in samples)
        {
            var snapshot = service.BuildById(type, entity.EntityId);
            Assert.NotNull(snapshot);
            File.WriteAllText(Path.Combine(SamplesDir, file), service.Serialize(snapshot!));
        }

        // 自检：B/D 级样本的关键结构
        Assert.Contains("\"mode\"", File.ReadAllText(Path.Combine(SamplesDir, "attackmode14.json")));
        Assert.Contains("\"bipolar\"", File.ReadAllText(Path.Combine(SamplesDir, "condition5.json")));
        Assert.Contains("\"trees\"", File.ReadAllText(Path.Combine(SamplesDir, "treasuretable7.json")));
        Assert.Contains("\"bipolar\"", File.ReadAllText(Path.Combine(SamplesDir, "faction2.json")));
        Assert.Contains("\"badgeGroups\"", File.ReadAllText(Path.Combine(SamplesDir, "battlemove1.json")));
        Assert.Contains("\"blocks\"", File.ReadAllText(Path.Combine(SamplesDir, "gamevar1.json")));
    }
}
