using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.Services;
using NeoEditor.Plugins.EntityEditor.Visualizers;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// D06: Encounter story-branch visualizer — single-layer diagram (current → branches),
/// slim node card (image + title + probability + ID/type chips), hover tooltip info card
/// (description / pre-conditions / item), shared BranchData source for cards + Mermaid,
/// pre-condition filter with probability recomputation, and ResponsesPanel merge.
/// </summary>
public class EncounterVisualizerTests
{
    // ── Infrastructure ─────────────────────────────────────────────────────

    private static VisHelperService CreateVis(StubReferenceResolver resolver,
        INavigationRouter router, ILocalizationService loc, Func<string, string?>? findImage = null)
        => new(findImage ?? (_ => null), resolver, router, new StubEntityLookupService(), loc);

    private static List<T> FindAll<T>(Control root) where T : Control
    {
        var results = new List<T>();
        void Walk(Control c)
        {
            if (c is T t) results.Add(t);
            switch (c)
            {
                case Panel p:
                    foreach (var child in p.Children) Walk(child);
                    break;
                case ContentControl cc when cc.Content is Control ccChild:
                    Walk(ccChild);
                    break;
                case Border b when b.Child is Control bChild:
                    Walk(bChild);
                    break;
                case TabControl tc:
                    foreach (var item in tc.Items)
                        if (item is Control itemCtrl) Walk(itemCtrl);
                    break;
            }
        }
        Walk(root);
        return results;
    }

    private static List<string> TextsOf(Control root)
        => FindAll<TextBlock>(root).Select(t => t.Text).Where(t => t is not null).Cast<string>().ToList();

    private static bool ContainsText(Control root, string text)
        => TextsOf(root).Any(t => t.Contains(text, StringComparison.Ordinal));

    /// <summary>Resolved branch cards: 240px wide, #FAFAFA background, 1px border.</summary>
    private static List<Border> BranchCards(Control root)
        => FindAll<Border>(root).Where(b =>
            b.Width == 240 &&
            (b.Background as ISolidColorBrush)?.Color == Color.Parse("#FAFAFA") &&
            b.BorderThickness == new Thickness(1)).ToList();

    /// <summary>The current node card: 240px wide, #E3F2FD background, 2px border.</summary>
    private static Border? CurrentCard(Control root)
        => FindAll<Border>(root).FirstOrDefault(b =>
            b.Width == 240 &&
            (b.Background as ISolidColorBrush)?.Color == Color.Parse("#E3F2FD") &&
            b.BorderThickness == new Thickness(2));

    private static ReferenceList<IReferenceEntry> RefList(params string[] ids)
    {
        var list = new ReferenceList<IReferenceEntry>();
        foreach (var id in ids)
            list.Add(new PureRefFormat { Entity = new EntityRef { Id = id } });
        return list;
    }

    private static Encounter NewEnc(int id, string name, string entityId)
        => new() { EntityId = entityId, Id = id, Name = name };

    /// <summary>Resolver that resolves Encounter/Condition/ItemType by raw id (¬ prefix stripped).</summary>
    private sealed class BranchResolver : StubReferenceResolver
    {
        private readonly Dictionary<string, IEntity> _byId = new();

        public void Add(string rawId, IEntity entity) => _byId[rawId] = entity;

        public override T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : class
        {
            if (_byId.TryGetValue(rawId, out var e)) return (T)(object)e;
            if (rawId.StartsWith('-') && _byId.TryGetValue(rawId[1..], out var neg))
                return (T)(object)neg;
            return null;
        }
    }

    private sealed class ItemLookupStub : StubEntityLookupService
    {
        private readonly Dictionary<string, ItemType> _items;

        public ItemLookupStub(Dictionary<string, ItemType> items) => _items = items;

        public override Dictionary<string, T> GetCompositeEntities<T>(Func<T, string> keySelector,
            int sourceModId = int.MaxValue)
            => _items.Values.Cast<T>().ToDictionary(keySelector);
    }

    private sealed class RecordingRouter : StubNavigationRouter
    {
        public List<(Type Type, string EntityId)> Navigations { get; } = new();
        public List<(Type Type, string EntityId, IEntity? Entity)> Peeks { get; } = new();

        public override void NavigateToEntity(Type entityType, string entityId, IEntity? resolvedEntity = null)
            => Navigations.Add((entityType, entityId));

        public override void RequestPeek(Type entityType, string entityId, IEntity? entity)
            => Peeks.Add((entityType, entityId, entity));
    }

    private sealed class EncLoc : ILocalizationService
    {
        private readonly Dictionary<string, string> _map = new()
        {
            ["Vis.StoryBranch"] = "剧情分支",
            ["Vis.NoBranches"] = "（无分支）",
            ["Vis.PreConditions"] = "前置条件",
            ["Vis.CurrentEncounter"] = "📍 当前剧情",
            ["Vis.CurrentPosition"] = "📍 当前位置",
            ["Vis.StoryText"] = "剧情文本",
            ["Vis.TypeStory"] = "剧情",
            ["Vis.TypeScavenge"] = "搜刮",
            ["Vis.TypeCombat"] = "战斗",
            ["Vis.TypeHack"] = "破解",
            ["Vis.TypeUnknown"] = "类型{0}",
            ["Vis.Responses"] = "Responses",
            ["Vis.Probability"] = "概率",
            ["Vis.ReferencedBy"] = "Referenced By",
            ["Vis.StayEnd"] = "⏹ 停留",
            ["Vis.BlankEnd"] = "☰ 无后续",
            ["Vis.RequireAll"] = "需同时拥有：",
            ["Vis.Consumed"] = "（消耗）",
            ["Vis.SuccessProb"] = "成功概率",
            ["Vis.ScavengePool"] = "搜刮池",
            ["Vis.EventGive"] = "事件给物",
            ["Vis.TriggerGiveItem"] = "触发给予物品",
            // D08 2026-08-08: page lifecycle redesign keys
            ["Vis.FlowView"] = "场景流转",
            ["Vis.HowToEnter"] = "如何进入",
            ["Vis.ContentEffects"] = "内容与效果",
            ["Vis.BackToCurrent"] = "⏎ 回到当前",
            ["Vis.EntryPoint"] = "⛳ 入口",
            ["Vis.TerminalPoint"] = "⏹ 终点",
            ["Vis.Triggers"] = "触发器",
            ["Vis.TriggerLabel"] = "触发条件",
            ["Vis.GiveLoot"] = "🎁 给物",
            ["Vis.LootPool"] = "🎒 战利品池",
            ["Vis.Cost"] = "💰 费用",
            ["Vis.RemoveLoot"] = "🗑 移除",
            ["Vis.TeleportTo"] = "📍 传送",
            ["Vis.SpawnOut"] = "🐾 刷出",
            ["Vis.Repeatable"] = "♻ 可重复",
            ["Vis.HexTypesShort"] = "🧱 格类型",
            ["Vis.GiveItem"] = "给予物品",
            ["Vis.Effects"] = "效果",
            ["Vis.Accidents"] = "意外事件",
        };

        public string this[string key] => _map.TryGetValue(key, out var v) ? v : key;
        public string this[string key, params object[] args] =>
            _map.TryGetValue(key, out var v) ? string.Format(v, args) : key;
        public CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void SetCulture(CultureInfo culture) { }
    }

    /// <summary>Build the visualizer over a 2-branch encounter (16: 便利店, 20: 逃跑).</summary>
    private static (EncounterEntityVisualizer Visualizer, Encounter Enc, Encounter T16, Encounter T20,
        BranchResolver Resolver, RecordingRouter Router) CreateTwoBranchScenario(
        bool withPreConds = true, ItemLookupStub? items = null)
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new BranchResolver();
        var t16 = NewEnc(16, "便利店", "enc16");
        var t20 = NewEnc(20, "逃跑", "enc20");
        if (withPreConds)
        {
            t16.PreConditions = RefList("5");
            t20.PreConditions = RefList("6");
            resolver.Add("5", new Condition { EntityId = "c5", Name = "醉酒" });
            resolver.Add("6", new Condition { EntityId = "c6", Name = "宿醉" });
        }
        resolver.Add("16", t16);
        resolver.Add("20", t20);
        var enc = NewEnc(1, "测试入口", "enc1");
        enc.Responses = "=16x2x0x0x0,=20x1x0x0x0";

        var router = new RecordingRouter();
        var vis = CreateVis(resolver, router, new EncLoc());
        var refNode = new RefNode(resolver, router);
        var visualizer = new EncounterEntityVisualizer(vis, refNode, items ?? new StubEntityLookupService());
        return (visualizer, enc, t16, t20, resolver, router);
    }

    // ── 1. 分支图单层：无反向橙卡 / 无 👈 Referenced By ─────────────────────

    [Fact]
    public void StoryBranchDiagram_SingleLayer_NoReverseOrangeCard_NoReferencedByInTab()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        // 旧左列反向引用橙卡（bg #FFF3E0 且边框 #E65100）不再存在
        var orangeCards = FindAll<Border>(detail).Count(b =>
            (b.Background as ISolidColorBrush)?.Color == Color.Parse("#FFF3E0") &&
            (b.BorderBrush as ISolidColorBrush)?.Color == Color.Parse("#E65100"));
        Assert.Equal(0, orangeCards);

        // Tab 内无「👈 Referenced By」反向链面板
        Assert.DoesNotContain(TextsOf(detail), t => t.Contains("👈 Referenced By"));
        // R64: 无箭头——布局（前驱行→当前行→后继行）已暗示流向
        Assert.NotNull(CurrentCard(detail));
        Assert.Equal(2, BranchCards(detail).Count);
        Assert.DoesNotContain(TextsOf(detail), t => t.Contains("→"));
    }

    // ── 2. 节点单组件：每条响应一张卡，卡片不含散列条件/物品 ─────────────────

    [Fact]
    public void BranchCards_OnePerResponse_SlimLayout_NoScatteredConditionBadges()
    {
        var (visualizer, enc, t16, t20, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        var cards = BranchCards(detail);
        Assert.Equal(2, cards.Count);

        // 每条响应一张卡：标题 + ID chip + 类型 chip + 概率胶囊
        foreach (var card in cards)
        {
            var texts = TextsOf(card);
            Assert.True(ContainsText(card, "ID: 16") || ContainsText(card, "ID: 20"),
                "branch card should carry its target ID chip");
            Assert.True(ContainsText(card, "剧情") || ContainsText(card, "搜刮"),
                "branch card should carry a type chip");
            Assert.True(ContainsText(card, "便利店") || ContainsText(card, "逃跑"),
                "branch card should carry the target title");
            // 概率胶囊：权重 + 有效概率
            Assert.True(ContainsText(card, "2.0(") || ContainsText(card, "1.0("),
                "branch card should carry the probability pill");
            // 图片或兜底图标
            Assert.True(FindAll<Image>(card).Count + FindAll<SymbolIcon>(card).Count == 1,
                "branch card should have exactly one image or fallback icon");
            // 默认（未展开/未 hover）状态：无 📋 计数、无条件名、无物品徽章——复杂信息全部在 tooltip
            Assert.DoesNotContain(texts, t => t.Contains("📋"));
            Assert.DoesNotContain(texts, t => t.Contains("醉酒") || t.Contains("宿醉"));
            Assert.DoesNotContain(texts, t => t.Contains("🛡"));
        }
        // 未解析前条件名不散落在卡片上（仅存在于 tooltip）
        Assert.DoesNotContain(TextsOf(BranchCards(detail)[0]), t => t.Contains("醉酒"));

        // 目标存在前置条件 → 卡片的 hover tooltip 才是条件信息卡（含条件名）
        var tooltip = ToolTip.GetTip(cards.First(c => ContainsText(c, "便利店"))) as Control;
        Assert.NotNull(tooltip);
        Assert.True(ContainsText(tooltip!, "醉酒"), "tooltip info card should carry the condition name");
    }

    // ── 3. 图片：有 strImg → Image；无 → SymbolIcon(BookOpen) 兜底 ────────────

    [Fact]
    public void BranchCard_WithImage_ShowsThumbnail()
    {
        var (visualizer, enc, t16, _, resolver, _) = CreateTwoBranchScenario();
        var pngPath = CreateTempPng();
        try
        {
            var vis = CreateVis(resolver, new RecordingRouter(), new EncLoc(), _ => pngPath);
            var refNode = new RefNode(resolver, new StubNavigationRouter());
            var v2 = new EncounterEntityVisualizer(vis, refNode, new StubEntityLookupService());

            t16.Image = RefList("SomeImage.png");
            t16.Image.RawText = "SomeImage.png"; // serializer populates RawText in production
            var detail = v2.BuildDetail(enc);

            var card = BranchCards(detail).First(c => ContainsText(c, "便利店"));
            var image = FindAll<Image>(card).Single();
            Assert.NotNull(image.Source);
            Assert.Equal(Stretch.Uniform, image.Stretch);
            // R59 v2: image is the main body of the card (~70% of the 240px width)
            Assert.Equal(168, image.Width);
            Assert.Empty(FindAll<SymbolIcon>(card));
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void BranchCard_WithoutImage_FallsBackToBookOpenIcon()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        foreach (var card in BranchCards(detail))
        {
            Assert.Empty(FindAll<Image>(card));
            var icons = FindAll<SymbolIcon>(card);
            Assert.Single(icons);
            Assert.Equal(Symbol.BookOpen, icons[0].Symbol);
        }
    }

    // ── 5. 分支卡不渲染目标 Encounter 的 Hero 上下文徽章（🎒🐾⚡）─────────────

    [Fact]
    public void BranchCard_NoHeroContextBadges()
    {
        var (visualizer, enc, t16, _, _, _) = CreateTwoBranchScenario();
        // 目标 Encounter 带满 Hero 上下文数据——分支卡不得渲染
        t16.TreasureId = RefList("3", "5");
        t16.CreatureId = RefList("17");
        t16.Conditions = RefList("7");

        var detail = visualizer.BuildDetail(enc);
        foreach (var card in BranchCards(detail))
        {
            var texts = TextsOf(card);
            Assert.DoesNotContain(texts, t => t.Contains("🎒"));
            Assert.DoesNotContain(texts, t => t.Contains("🐾"));
            Assert.DoesNotContain(texts, t => t.Contains("⚡"));
        }
        // Hero 区块仍渲染本实体的类型 chip（共享映射）
        Assert.True(ContainsText(detail, "剧情"), "Hero type chip (shared mapping) should remain");
    }

    // ── 6. 前置条件过滤：纯函数数值 + UI 同步 ────────────────────────────────

    [Fact]
    public void PrepareBranches_FilterRecomputesProbabilities()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();

        // 无过滤：2/3 与 1/3
        var (branches, validTotal) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.Equal(3.0, validTotal, 6);
        Assert.Equal(2.0 / 3.0, branches[0].EffectiveProb, 6);
        Assert.Equal(1.0 / 3.0, branches[1].EffectiveProb, 6);
        Assert.All(branches, b => Assert.True(b.IsSatisfied));

        // 勾选 醉酒(5)：16 满足（2/2=100%），20 需 宿醉(6) → 0
        var (filtered, filteredTotal) = visualizer.PrepareBranches(enc, new HashSet<string> { "5" });
        Assert.Equal(2.0, filteredTotal, 6);
        Assert.Equal(1.0, filtered[0].EffectiveProb, 6);
        Assert.True(filtered[0].IsSatisfied);
        Assert.Equal(0.0, filtered[1].EffectiveProb, 6);
        Assert.False(filtered[1].IsSatisfied);

        // 勾选 宿醉(6)：16 不满足、20 满足
        var (other, _) = visualizer.PrepareBranches(enc, new HashSet<string> { "6" });
        Assert.Equal(0.0, other[0].EffectiveProb, 6);
        Assert.Equal(1.0, other[1].EffectiveProb, 6);

        // Y/N 极性：目标前置条件为 -5（玩家有 5 则不可达）
        var (visualizer2, enc2, t16b, _, resolver2, _) = CreateTwoBranchScenario(withPreConds: false);
        t16b.PreConditions = RefList("-5");
        resolver2.Add("5", new Condition { EntityId = "c5", Name = "醉酒" });
        resolver2.Add("16", t16b);
        enc2.Responses = "=16x1x0x0x0,=20x1x0x0x0";
        resolver2.Add("20", NewEnc(20, "逃跑", "enc20"));
        var (negOn, _) = visualizer2.PrepareBranches(enc2, new HashSet<string> { "5" });
        Assert.False(negOn[0].IsSatisfied); // 玩家有 5 → -5 不满足
        Assert.Equal(0.0, negOn[0].EffectiveProb, 6);
        var (negOff, _) = visualizer2.PrepareBranches(enc2, new HashSet<string> { "7" });
        Assert.True(negOff[0].IsSatisfied); // 玩家无 5 → -5 满足
        Assert.Equal(0.5, negOff[0].EffectiveProb, 6);
    }

    [Fact]
    public void FilterCheckbox_DimsUnsatisfiedCard()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        // 初始：两卡全亮，概率 66.67% / 33.33%
        var before = BranchCards(detail);
        Assert.Equal(1.0, before[0].Opacity);
        Assert.Equal(1.0, before[1].Opacity);
        Assert.Contains(TextsOf(before[0]), t => t.Contains("66.67%"));
        Assert.Contains(TextsOf(before[1]), t => t.Contains("33.33%"));

        // 勾选「醉酒」(5)：16 满足 → 100%；20 需宿醉(6) → 0% + 半透明
        var cb = FindAll<CheckBox>(detail).Single(c => ContainsText(c, "醉酒"));
        cb.IsChecked = true;

        var after = BranchCards(detail);
        var card16 = after.Single(c => ContainsText(c, "便利店"));
        var card20 = after.Single(c => ContainsText(c, "逃跑"));
        Assert.Equal(1.0, card16.Opacity);
        Assert.Contains(TextsOf(card16), t => t.Contains("2.0(100%)"));
        Assert.Equal(0.5, card20.Opacity);
        Assert.Contains(TextsOf(card20), t => t.Contains("1.0(0%)"));
    }

    // ── 7. ResponsesPanel 合并：无独立 Vis.Responses 区块，格式提示在分支图内 ─

    [Fact]
    public void ResponsesPanel_Merged_NoStandaloneSection_FormatHintInBranchSection()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        // 无独立 Responses 节标题（合并进流转主视图）
        Assert.DoesNotContain(TextsOf(detail), t => t == "Responses");
        // 格式提示行保留在流转主视图内
        Assert.Contains(TextsOf(detail), t => t.Contains("[物品ID]x[数量]=[剧情ID]x[权重]"));
        // ② 场景流转节标题仍在（D08 v1.2：Tab 名由「剧情分支」改为「场景流转」）
        Assert.Contains(TextsOf(detail), t => t == "场景流转");
    }

    // ── 8. 类型映射：0-3 四色 chip，未知值灰兜底 ─────────────────────────────

    [Fact]
    public void TypeChip_MapsZeroToThree_GreyFallback()
    {
        var cases = new[]
        {
            (Raw: 0, Label: "剧情", Bg: "#E3F2FD"),
            (Raw: 1, Label: "搜刮", Bg: "#FFF3E0"),
            (Raw: 2, Label: "战斗", Bg: "#FFEBEE"),
            (Raw: 3, Label: "破解", Bg: "#F3E5F5"),
            (Raw: 4, Label: "类型4", Bg: "#F5F5F5"),
        };
        foreach (var (raw, label, bg) in cases)
        {
            TestApp.EnsureAvaloniaInitialized();
            var resolver = new BranchResolver();
            var vis = CreateVis(resolver, new RecordingRouter(), new EncLoc());
            var visualizer = new EncounterEntityVisualizer(vis, new RefNode(resolver, new StubNavigationRouter()),
                new StubEntityLookupService());

            var enc = NewEnc(100 + raw, "类型测试", $"enc{100 + raw}");
            enc.Type = (EncounterType)raw;
            var detail = visualizer.BuildDetail(enc);

            // Hero 的类型 chip：bg 色 + 文本 同时匹配（ID chip 同为 #E3F2FD，用文本区分）
            var chip = FindAll<Border>(detail).FirstOrDefault(b =>
                (b.Background as ISolidColorBrush)?.Color == Color.Parse(bg) && ContainsText(b, label));
            Assert.NotNull(chip);
            Assert.Contains(TextsOf(detail), t => t == label);
        }
    }

    // ── 9. 当前卡：含「📍 当前剧情」、不挂 tooltip、不触发导航 ────────────────

    [Fact]
    public void CurrentCard_HasLabel_NoTooltip_NoNavigation()
    {
        var (visualizer, enc, _, _, _, router) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        var current = CurrentCard(detail);
        Assert.NotNull(current);
        Assert.True(ContainsText(current!, "📍 当前剧情"));
        Assert.Null(ToolTip.GetTip(current!)); // 当前卡不挂分支信息卡

        // Ctrl+Click 当前卡 → 无任何导航/peek 记录
        (current.Parent as Panel)?.Children.Remove(current); // 独立挂到窗口（避免重复父级）
        var window = new Window { Width = 500, Height = 300, Content = current };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var pt = new Point(150, 30); // 标题区（避开左侧图片区）
            window.MouseDown(pt, MouseButton.Left, RawInputModifiers.Control);
            window.MouseUp(pt, MouseButton.Left, RawInputModifiers.Control);
            Assert.Empty(router.Navigations);
            Assert.Empty(router.Peeks);
        }
        finally
        {
            window.Close();
        }
    }

    // ── 10. 分支卡导航：Ctrl+Click 跳转 / Ctrl+RMB peek ─────────────────────

    [Fact]
    public void BranchCard_CtrlClickNavigates_CtrlRmbPeeks()
    {
        var (visualizer, enc, _, _, _, router) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);
        var card = BranchCards(detail).Single(c => ContainsText(c, "便利店"));
        (card.Parent as Panel)?.Children.Remove(card); // 独立挂到窗口（避免重复父级）

        var window = new Window { Width = 500, Height = 300, Content = card };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var pt = new Point(150, 30);

            window.MouseDown(pt, MouseButton.Left, RawInputModifiers.Control);
            window.MouseUp(pt, MouseButton.Left, RawInputModifiers.Control);
            Assert.Contains(router.Navigations, n => n.Type == typeof(Encounter) && n.EntityId == "enc16");

            window.MouseDown(pt, MouseButton.Right, RawInputModifiers.Control);
            window.MouseUp(pt, MouseButton.Right, RawInputModifiers.Control);
            Assert.Contains(router.Peeks, p => p.Type == typeof(Encounter) && p.EntityId == "enc16");
            var peek16 = router.Peeks.First(p => p.EntityId == "enc16");
            Assert.NotNull(peek16.Entity); // R64: peek 必须收到已解析的目标实体，而非 null/当前实体
            Assert.Equal("enc16", peek16.Entity!.EntityId);
        }
        finally
        {
            window.Close();
        }
    }

    // ── 11. Tooltip 信息卡：描述 + 前置条件满足情况 + 物品 + 概率 ─────────────

    [Fact]
    public void BranchTooltip_ContainsDescriptionConditionsAndItem()
    {
        var (visualizer, enc, t16, _, resolver, _) = CreateTwoBranchScenario();
        t16.Description = "一家亮着灯的便利店，货架上的罐头落满灰尘。这是测试用的长描述内容，用来验证 tooltip 截断。";
        resolver.Add("16", t16);

        var detail = visualizer.BuildDetail(enc);
        var card = BranchCards(detail).Single(c => ContainsText(c, "便利店"));
        var tooltip = ToolTip.GetTip(card) as Control;
        Assert.NotNull(tooltip);

        // 描述（截断 ~200 字）在 tooltip 内
        Assert.True(ContainsText(tooltip!, "便利店"), "tooltip should carry the target description");
        // 前置条件满足情况：无过滤时全部满足 → ✓ 醉酒（16 的前置条件）
        Assert.True(ContainsText(tooltip!, "✓ 醉酒"), "tooltip should show satisfied condition state");
        // 概率行
        Assert.True(ContainsText(tooltip!, "概率"), "tooltip should carry the probability line");
    }

    // ── 12. 回归：3 个 Tab / 空分支占位 / 实测格式解析 ───────────────────────

    [Fact]
    public void Regression_NoTabs_NoBranchesPlaceholder_ResponseFormats()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        // R64: TabControl 已移除——场景流转内容直接内联（无 TabItem、无剧情链/Mermaid 源码）
        Assert.Empty(FindAll<TabItem>(detail));
        // 场景流转节标题仍在
        Assert.Contains(TextsOf(detail), t => t == "场景流转");

        // 空 Responses（无有效条目）→ 「无分支」占位
        var emptyEnc = NewEnc(2, "空入口", "enc2");
        emptyEnc.Responses = ",,";
        var emptyDetail = visualizer.BuildDetail(emptyEnc);
        Assert.Contains(TextsOf(emptyDetail), t => t.Contains("（无分支）"));

        // =1x1x0x0x0 → 目标 1、权重 1、无物品、概率 100%
        var singleEnc = NewEnc(3, "单分支", "enc3");
        singleEnc.Responses = "=1x1x0x0x0";
        var (single, total1) = visualizer.PrepareBranches(singleEnc, new HashSet<string>());
        Assert.Single(single);
        Assert.Equal(1, single[0].TargetId);
        Assert.Single(single[0].Items);
        Assert.Null(single[0].Items[0].ItemId);
        Assert.Null(single[0].Items[0].Item);
        Assert.Equal(1.0, single[0].Weight, 6);
        Assert.Equal(1.0, single[0].EffectiveProb, 6);
        Assert.Equal(1.0, total1, 6);

        // 90.3x2=16x2x0x0x0,=16x1x0x0x0 → 同目标 16 的两个段合并为一张分支
        // （物品 90.3 ×2 + 空物品），权重累加 2+1=3
        var itemEnc = NewEnc(4, "物品分支", "enc4");
        itemEnc.Responses = "90.3x2=16x2x0x0x0,=16x1x0x0x0";
        var itemTarget = NewEnc(16, "便利店", "enc16");
        var resolver = new BranchResolver();
        resolver.Add("16", itemTarget);
        var items = new ItemLookupStub(new Dictionary<string, ItemType>
        {
            ["90.3"] = new() { EntityId = "it903", Name = "撬棍", Description = "撬棍", GroupId = 90, SubgroupId = 3 },
        });
        var vis = CreateVis(resolver, new RecordingRouter(), new EncLoc());
        var v2 = new EncounterEntityVisualizer(vis, new RefNode(resolver, new StubNavigationRouter()), items);
        var (multi, total2) = v2.PrepareBranches(itemEnc, new HashSet<string>());
        Assert.Single(multi); // 同目标合并 → 只有一张分支
        Assert.Equal(16, multi[0].TargetId);
        Assert.Equal(2, multi[0].Items.Count); // 两个触发段：撬棍 ×2 + 空物品
        Assert.Equal("90.3", multi[0].Items[0].ItemId);
        Assert.NotNull(multi[0].Items[0].Item);
        Assert.Equal(2.0, multi[0].Items[0].ItemMult, 6);
        Assert.Null(multi[0].Items[1].ItemId);
        Assert.Equal(3.0, multi[0].Weight, 6); // 权重累加
        Assert.Equal(3.0, total2, 6);
        Assert.Equal(1.0, multi[0].EffectiveProb, 6);
    }

    // ── 13. 多段同目标合并：一张分支卡，tooltip 列出全部触发物品 ─────────────

    [Fact]
    public void MergeSameTargetSegments_OneCard_TooltipListsAllTriggerItems()
    {
        var (visualizer, enc, t16, _, resolver, _) = CreateTwoBranchScenario(withPreConds: false,
            items: new ItemLookupStub(new Dictionary<string, ItemType>
            {
                ["90.3"] = new() { EntityId = "it903", Name = "撬棍", Description = "撬棍", GroupId = 90, SubgroupId = 3 },
                ["91.4"] = new() { EntityId = "it914", Name = "打火机", Description = "打火机", GroupId = 91, SubgroupId = 4 },
            }));
        // 同一目标 16 的两个触发段：撬棍 ×2 与打火机 ×1（真实数据有 31 例同款）
        enc.Responses = "90.3x2=16x2x0x0x0,91.4x1=16x1x0x0x0";

        var detail = visualizer.BuildDetail(enc);

        // 只有一张分支卡（目标合并）
        var cards = BranchCards(detail);
        Assert.Single(cards);

        // R64: 去路标注（物品触发）在卡片底部行中间——多段用 ｜ 分隔
        Assert.True(ContainsText(cards[0], "🛡 撬棍 ×2 ｜ 🛡 打火机 ×1"),
            "successor annotation should list all trigger items in the card");

        // tooltip 仍列出全部触发物品（两个徽章）
        var tooltip = ToolTip.GetTip(cards[0]) as Control;
        Assert.NotNull(tooltip);
        Assert.True(ContainsText(tooltip!, "🛡 撬棍 ×2"), "tooltip should carry the first trigger item");
        Assert.True(ContainsText(tooltip!, "🛡 打火机"), "tooltip should carry the second trigger item");
    }

    // ── D07 1. 终止判定纯函数：自指 → Stay；=1 且当前≠1 → Blank；正常 → None ──

    [Fact]
    public void EndKind_PureFunction_StayBlankNone()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario(withPreConds: false);

        // 自指段（TargetId == 当前 Id）→ Stay；正常段 → None（纯数据断言）
        enc.Id = 236;
        enc.Responses = "=236x1x0x0x0,=20x1x0x0x0";
        var (stay, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.Equal(EncounterEntityVisualizer.BranchEndKind.Stay, stay[0].EndKind);
        Assert.Equal(EncounterEntityVisualizer.BranchEndKind.None, stay[1].EndKind);
        // 终止段仍保留 Weight/EffectiveProb（概率照算）
        Assert.Equal(1.0, stay[0].Weight, 6);
        Assert.Equal(0.5, stay[0].EffectiveProb, 6);

        // 指向 id=1（Blank）且当前 id≠1 → Blank
        var blankEnc = NewEnc(3, "单分支", "enc3");
        blankEnc.Responses = "=1x1x0x0x0";
        var (blank, _) = visualizer.PrepareBranches(blankEnc, new HashSet<string>());
        Assert.Single(blank);
        Assert.Equal(EncounterEntityVisualizer.BranchEndKind.Blank, blank[0].EndKind);

        // 正在查看 id=1 时自指优先 → Stay（而非 Blank）
        var selfBlank = NewEnc(1, "Blank", "enc1");
        selfBlank.Responses = "=1x1x0x0x0";
        var (selfStay, _) = visualizer.PrepareBranches(selfBlank, new HashSet<string>());
        Assert.Equal(EncounterEntityVisualizer.BranchEndKind.Stay, selfStay[0].EndKind);

        // 纯函数直接断言
        Assert.Equal(EncounterEntityVisualizer.BranchEndKind.Stay,
            EncounterEntityVisualizer.DetermineEndKind(236, 236));
        Assert.Equal(EncounterEntityVisualizer.BranchEndKind.Blank,
            EncounterEntityVisualizer.DetermineEndKind(1, 236));
        Assert.Equal(EncounterEntityVisualizer.BranchEndKind.None,
            EncounterEntityVisualizer.DetermineEndKind(20, 236));
        Assert.Equal(EncounterEntityVisualizer.BranchEndKind.Stay,
            EncounterEntityVisualizer.DetermineEndKind(1, 1));
    }

    // ── D07 2. 终止渲染：胶囊（⏹ 停留 / ☰ 无后续）而非卡片 ─────────────────────

    [Fact]
    public void EndBranches_RenderCapsules_NotCards()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario(withPreConds: false);
        enc.Id = 236;
        enc.Responses = "=236x1x0x0x0,=1x1x0x0x0,=16x2x0x0x0";
        var detail = visualizer.BuildDetail(enc);

        // 只有真实目标（16）渲染为分支卡；Stay/Blank 不是卡片
        var cards = BranchCards(detail);
        Assert.Single(cards);
        Assert.True(ContainsText(cards[0], "便利店"));

        // 终止标记 = 灰色胶囊（MiniBadge 底色 #ECEFF1）含 ⏹ 停留 / ☰ 无后续
        var capsules = FindAll<Border>(detail).Where(b =>
            (b.Background as ISolidColorBrush)?.Color == Color.Parse("#ECEFF1")).ToList();
        Assert.Equal(2, capsules.Count);
        Assert.Contains(TextsOf(capsules[0]), t => t.Contains("⏹ 停留") || t.Contains("☰ 无后续"));
        Assert.Contains(TextsOf(capsules[1]), t => t.Contains("⏹ 停留") || t.Contains("☰ 无后续"));
        Assert.Contains(TextsOf(detail), t => t.Contains("⏹ 停留"));
        Assert.Contains(TextsOf(detail), t => t.Contains("☰ 无后续"));

        // 胶囊旁显示概率（停留 25%），胶囊行内无图片
        Assert.True(ContainsText(detail, "25%"), "end capsule should still show the probability");
        Assert.Empty(FindAll<Image>(detail));
    }

    // ── D07 4. Ingredient 双目标：纯数字 → Ingredient nID（52=撬棍）───────────

    [Fact]
    public void Ingredient_NumericId_ResolvesIngredient_TooltipCrowbar()
    {
        var (visualizer, enc, t16, _, resolver, _) = CreateTwoBranchScenario(withPreConds: false);
        // Doc 37 §5.1: 52=撬棍（Ingredient nID）——纯数字优先 Ingredient
        resolver.Add("52", new Ingredient { EntityId = "ing52", Id = 52, Name = "撬棍" });
        resolver.Add("16", t16);
        enc.Responses = "52x1=16x1x0x0x0";

        var (branches, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.Single(branches);
        var item = branches[0].Items[0];
        Assert.NotNull(item.Ing);
        Assert.Null(item.Item);
        Assert.Equal("撬棍", item.Ing!.Name);

        // tooltip 含 🛠 撬棍（工具色系 #E8EAF6/#283593）
        var detail = visualizer.BuildDetail(enc);
        var tooltip = ToolTip.GetTip(BranchCards(detail).Single()) as Control;
        Assert.NotNull(tooltip);
        Assert.True(ContainsText(tooltip!, "🛠 撬棍"), "tooltip should carry the ingredient badge");
        var ingBadge = FindAll<Border>(tooltip!).FirstOrDefault(b =>
            (b.Background as ISolidColorBrush)?.Color == Color.Parse("#E8EAF6"));
        Assert.NotNull(ingBadge);
    }

    // ── D07 5. 兜底：纯数字查不到 Ingredient/ItemType → 灰 Item #52 不崩溃 ────

    [Fact]
    public void Ingredient_UnresolvedNumericId_GreyItemFallback_NoCrash()
    {
        var (visualizer, enc, t16, _, _, _) = CreateTwoBranchScenario(withPreConds: false);
        // resolver 没有 Ingredient/ItemType #52 → 灰 Item #52 兜底
        enc.Responses = "52x1=16x1x0x0x0";

        var (branches, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        var item = branches[0].Items[0];
        Assert.Null(item.Ing);
        Assert.Null(item.Item);
        Assert.Equal("52", item.ItemId);

        var detail = visualizer.BuildDetail(enc);
        var tooltip = ToolTip.GetTip(BranchCards(detail).Single()) as Control;
        Assert.NotNull(tooltip);
        Assert.True(ContainsText(tooltip!, "Item #52"), "unresolved numeric id should fall back to grey Item #id");
    }

    // ── D07 6. p2 销毁标记：p2=1 → （消耗）后缀；p2=0 无 ─────────────────────

    [Fact]
    public void P2DestroyOnUse_AppendsConsumedSuffix_WhenOne()
    {
        var (visualizer, enc, t16, _, resolver, _) = CreateTwoBranchScenario(withPreConds: false,
            items: new ItemLookupStub(new Dictionary<string, ItemType>
            {
                ["49.1"] = new() { EntityId = "it491", Name = "手持光源", Description = "手持光源", GroupId = 49, SubgroupId = 1 },
            }));
        t16.Description = "";
        resolver.Add("16", t16);

        // p2=1（encParts[2]）→ 物品由回应直接销毁
        enc.Responses = "49.1x1=16x1x1x1x0";
        var (branches, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.True(branches[0].Items[0].DestroyOnUse);
        var detail = visualizer.BuildDetail(enc);
        var tooltip = ToolTip.GetTip(BranchCards(detail).Single()) as Control;
        Assert.NotNull(tooltip);
        Assert.True(ContainsText(tooltip!, "（消耗）"), "p2=1 badge should carry the （消耗） suffix");

        // p2=0 → 无后缀
        enc.Responses = "49.1x1=16x1x0x0x0";
        var (noConsume, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.False(noConsume[0].Items[0].DestroyOnUse);
        var detail2 = visualizer.BuildDetail(enc);
        var tooltip2 = ToolTip.GetTip(BranchCards(detail2).Single()) as Control;
        Assert.NotNull(tooltip2);
        Assert.DoesNotContain(TextsOf(tooltip2!), t => t.Contains("（消耗）"));
    }

    // ── D07 7. p3 成功概率：p3=0.5 → 成功概率 50%；p3=1/0 不显示 ─────────────

    [Fact]
    public void P3SuccessProb_ShowsSuccessChanceLine_WhenBelowOne()
    {
        var (visualizer, enc, t16, _, resolver, _) = CreateTwoBranchScenario(withPreConds: false,
            items: new ItemLookupStub(new Dictionary<string, ItemType>
            {
                ["8.7"] = new() { EntityId = "it87", Name = "平板", Description = "平板", GroupId = 8, SubgroupId = 7 },
            }));
        t16.Description = "";
        resolver.Add("16", t16);

        // p3=0.5（encParts[3]）→ 成功概率 50%
        enc.Responses = "8.7x1=16x1x0x0.5x0";
        var (branches, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.Equal(0.5, branches[0].SuccessProb!.Value, 6);
        var detail = visualizer.BuildDetail(enc);
        var tooltip = ToolTip.GetTip(BranchCards(detail).Single()) as Control;
        Assert.NotNull(tooltip);
        Assert.True(ContainsText(tooltip!, "成功概率 50%"), "p3=0.5 should render the success-chance line");

        // p3=1（必成功）→ 不显示
        enc.Responses = "8.7x1=16x1x0x1x0";
        var (certain, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.Null(certain[0].SuccessProb);
        var detail2 = visualizer.BuildDetail(enc);
        var tooltip2 = ToolTip.GetTip(BranchCards(detail2).Single()) as Control;
        Assert.NotNull(tooltip2);
        Assert.DoesNotContain(TextsOf(tooltip2!), t => t.Contains("成功概率"));

        // p3=0（数据填充位）→ 视为默认 1.0，不显示（去噪）
        enc.Responses = "8.7x1=16x1x0x0x0";
        var (zero, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.Null(zero[0].SuccessProb);
    }

    // ── D07 8. AND 多物品：91.8x1+91.3x1 → 需同时拥有 + 连接；合并段仍并列 ────

    [Fact]
    public void AndItems_RequireAllPrefix_PlusConnector_ButMergedSegmentsStayParallel()
    {
        var (visualizer, enc, t16, _, resolver, _) = CreateTwoBranchScenario(withPreConds: false,
            items: new ItemLookupStub(new Dictionary<string, ItemType>
            {
                ["91.8"] = new() { EntityId = "it918", Name = "技能A", Description = "技能A", GroupId = 91, SubgroupId = 8 },
                ["91.3"] = new() { EntityId = "it913", Name = "技能B", Description = "技能B", GroupId = 91, SubgroupId = 3 },
            }));
        t16.Description = "";
        resolver.Add("16", t16);

        // 同段 '+' 连接 = AND（需同时拥有两个技能）
        enc.Responses = "91.8x1+91.3x1=16x1x0x0x0";
        var (branches, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.Equal(2, branches[0].Items.Count);
        Assert.False(branches[0].Items[0].IsAnd);
        Assert.True(branches[0].Items[1].IsAnd);

        var detail = visualizer.BuildDetail(enc);
        var tooltip = ToolTip.GetTip(BranchCards(detail).Single()) as Control;
        Assert.NotNull(tooltip);
        Assert.True(ContainsText(tooltip!, "需同时拥有"), "AND group should carry the require-all prefix");
        Assert.True(ContainsText(tooltip!, "+"), "AND group badges are joined with +");
        Assert.True(ContainsText(tooltip!, "🛡 技能A"));
        Assert.True(ContainsText(tooltip!, "🛡 技能B"));

        // 多段同目标合并（非 AND）→ 并列徽章，无「需同时拥有」前缀
        enc.Responses = "91.8x2=16x2x0x0x0,=16x1x0x0x0";
        var (merged, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.All(merged[0].Items, i => Assert.False(i.IsAnd));
        var detail2 = visualizer.BuildDetail(enc);
        var tooltip2 = ToolTip.GetTip(BranchCards(detail2).Single()) as Control;
        Assert.NotNull(tooltip2);
        Assert.DoesNotContain(TextsOf(tooltip2!), t => t.Contains("需同时拥有"));
        Assert.True(ContainsText(tooltip2!, "🛡 技能A ×2"));
    }

    // ── D08 §5.2. vLoot → ④ 效果区「🎁 给物」行（D07 §七 搜刮池/事件给物 双标签
    //    被 D08 统一为 🎁 给物 效果行；哨兵 0/3 排除语义保留）──────────────────

    [Fact]
    public void VLootEffectRow_GiveLootRow_WithPoolName()
    {
        var (visualizer, enc, _, _, resolver, _) = CreateTwoBranchScenario();
        enc.Loot = RefList("77");
        enc.Loot.RawText = "77"; // serializer populates RawText in production
        resolver.Add("77", new TreasureTable { EntityId = "tt77", Id = 77, Name = "营地补给" });

        var detail = visualizer.BuildDetail(enc);
        // 效果区行 = MiniBadge「🎁 给物」+ 池名徽章（可跳转）
        Assert.Contains(TextsOf(detail), t => t == "🎁 给物");
        Assert.True(ContainsText(detail, "营地补给"), "vLoot row should carry the resolved pool name");
    }

    [Fact]
    public void VLootEffectRow_Sentinels_ZeroAndThree_AreHidden()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();

        // vLoot = "0"（默认无）→ 无「🎁 给物」行
        enc.Loot = RefList("0");
        enc.Loot.RawText = "0";
        Assert.DoesNotContain(TextsOf(visualizer.BuildDetail(enc)), t => t == "🎁 给物");

        // vLoot = "3"（空白池，aTreasures="0.0x0.0x0"）→ 同样不显示（577 条实测）
        enc.Loot = RefList("3");
        enc.Loot.RawText = "3";
        Assert.DoesNotContain(TextsOf(visualizer.BuildDetail(enc)), t => t == "🎁 给物");

        // 真值（≠0/3）→ 出现
        enc.Loot = RefList("77");
        enc.Loot.RawText = "77";
        Assert.Contains(TextsOf(visualizer.BuildDetail(enc)), t => t == "🎁 给物");
    }

    // ── D08 §5.2. nItemsID → ④ 效果区「📦 给予物品」行 + 物品名 ───────────────

    [Fact]
    public void NItemsIDEffectRow_GiveItem_ShowsItemName()
    {
        var (visualizer, enc, _, _, resolver, _) = CreateTwoBranchScenario();
        enc.ItemsId = RefList("90");
        enc.ItemsId.RawText = "90"; // serializer populates RawText in production
        resolver.Add("90", new ItemType { EntityId = "it90", Name = "开锁器", GroupId = 0, SubgroupId = 0 });

        var detail = visualizer.BuildDetail(enc);
        Assert.Contains(TextsOf(detail), t => t == "给予物品"); // R64: 资源键值自带语义（GiveItem 键无 emoji 前缀）
        Assert.True(ContainsText(detail, "开锁器"), "nItemsID badge should carry the item name");
    }

    // ═══════════════════════════ D08 全页生命周期重排（v1.2）═══════════════════════════

    /// <summary>
    /// D08 §九.1: BuildDetail 顶层顺序 = Raw Data → ① 身份 → ② 场景流转 → ③ 如何进入 → ④ 内容与效果 → 被引用。
    /// </summary>
    [Fact]
    public void PageOrder_IdentityFlowEntryContentEffects()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        enc.Description = "这是一段用于页面顺序断言的剧情描述。";
        enc.Conditions = RefList("5");
        enc.Conditions.RawText = "5"; // serializer populates RawText in production
        enc.Price = 10;

        var detail = visualizer.BuildDetail(enc);
        var root = FindAll<StackPanel>(detail)
            .First(sp => sp.Margin == new Thickness(16) && sp.Spacing == 16);

        // R64: [0] Raw Data（兜底），[1] Hero，[2] 内容与效果（两栏，放流转上方），
        // [3] 场景流转，[4] 如何进入，[5] 被引用（stub 无 store → 空）
        Assert.Equal(6, root.Children.Count);
        Assert.True(ContainsText(root.Children[1], "ID: 1"), "① 身份（Hero）应在第二个位置");
        Assert.Contains(TextsOf(root.Children[2]), t => t == "内容与效果");
        Assert.Contains(TextsOf(root.Children[3]), t => t == "场景流转");
        Assert.Contains(TextsOf(root.Children[4]), t => t == "如何进入");
    }

    /// <summary>
    /// D08 §九.2/§九.5: 另一 Encounter 的 Responses 指向本实体 → 前驱区渲染节点卡
    /// （名称 + 来路标注「🛡 物品 ×1 →」）；引用语法 90.1x1=12x1x0x0x0 被翻译。
    /// </summary>
    [Fact]
    public void FlowView_PredecessorCard_WithIncomingItemLabel()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new BranchResolver();
        var enc = NewEnc(12, "当前场景", "enc12");
        var src = NewEnc(50, "撬棍前驱", "enc50");
        src.Responses = "90.1x1=12x1x0x0x0";
        resolver.Add("12", enc);
        resolver.Add("50", src);

        var lookup = new ItemLookupStub(new Dictionary<string, ItemType>
        {
            ["90.1"] = new() { EntityId = "it901", Name = "撬棍", Description = "撬棍", GroupId = 90, SubgroupId = 1 },
        });
        lookup.ReferenceLookups = new Dictionary<Type, List<object>>
        {
            [typeof(Encounter)] = new List<object> { src },
        };

        var router = new RecordingRouter();
        var vis = CreateVis(resolver, router, new EncLoc());
        var visualizer = new EncounterEntityVisualizer(vis, new RefNode(resolver, router), lookup);

        var detail = visualizer.BuildDetail(enc);
        // 前驱卡（240px #FAFAFA 1px = 与分支卡同款节点卡语言）出现，含来源名称
        var cards = BranchCards(detail);
        var predCard = cards.Single(c => ContainsText(c, "撬棍前驱"));
        // 无概率胶囊（前驱卡），但 ID chip 在
        Assert.True(ContainsText(predCard, "ID: 50"));
        // R64: 来路标注进卡片底部行中间（无 → 箭头）
        Assert.True(ContainsText(predCard, "🛡 撬棍 ×1"),
            "predecessor annotation should translate 90.1x1=12x1x0x0x0 into the card");
    }

    /// <summary>D08 §九.5: AND 多物品来路 → 需同时拥有：A + B →。</summary>
    [Fact]
    public void FlowView_PredecessorIncoming_AndItems_RequireAllLabel()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new BranchResolver();
        var enc = NewEnc(12, "当前场景", "enc12");
        var src = NewEnc(50, "双技能前驱", "enc50");
        src.Responses = "91.8x1+91.3x1=12x1x0x0x0";
        resolver.Add("12", enc);
        resolver.Add("50", src);

        var lookup = new ItemLookupStub(new Dictionary<string, ItemType>
        {
            ["91.8"] = new() { EntityId = "it918", Name = "技能A", Description = "技能A", GroupId = 91, SubgroupId = 8 },
            ["91.3"] = new() { EntityId = "it913", Name = "技能B", Description = "技能B", GroupId = 91, SubgroupId = 3 },
        });
        lookup.ReferenceLookups = new Dictionary<Type, List<object>>
        {
            [typeof(Encounter)] = new List<object> { src },
        };

        var vis = CreateVis(resolver, new RecordingRouter(), new EncLoc());
        var visualizer = new EncounterEntityVisualizer(vis, new RefNode(resolver, new StubNavigationRouter()), lookup);

        var detail = visualizer.BuildDetail(enc);
        Assert.True(ContainsText(detail, "需同时拥有：🛡 技能A + 🛡 技能B"),
            "AND incoming segments should render 需同时拥有：A + B");
    }

    /// <summary>D08 §九.2: 无前驱 → 流转视图左侧显示 ⛳ 入口 徽章。</summary>
    [Fact]
    public void FlowView_NoPredecessors_ShowsEntryBadge()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);
        Assert.True(ContainsText(detail, "⛳ 入口"), "no predecessors → entry badge in the flow view");
    }

    /// <summary>D08 §九.4 (R64): 三行布局——前驱层第一行、当前卡第二行（居中）、后继层第三行。</summary>
    [Fact]
    public void FlowView_CurrentCard_Centered_BetweenPredAndSucc()
    {
        var (visualizer, enc, t16, t20, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        // R64: 当前卡在 currentHolder（三行布局的第二行）内；flowRow 是纵向 StackPanel
        var current = CurrentCard(detail);
        Assert.NotNull(current);
        var flowRow = FindAll<StackPanel>(detail)
            .First(sp => sp.Orientation == Orientation.Vertical && sp.Children.OfType<StackPanel>()
                .Any(holder => holder.Children.Contains(current!)));

        // [0] 前驱行（本场景无前驱 → ⛳ 入口）→ [1] 当前行（含当前卡）→ [2] 后继行
        Assert.Equal(3, flowRow.Children.Count);
        Assert.True(ContainsText(flowRow.Children[0], "⛳ 入口"));
        Assert.True(flowRow.Children[1] is StackPanel holder && holder.Children.Contains(current!),
            "current card should sit in the middle row");
        Assert.True(ContainsText(flowRow.Children[2], "便利店") || ContainsText(flowRow.Children[2], "逃跑"),
            "successor row should sit after the current card");
    }

    /// <summary>D08 §九.6 (R64): 「⏎ 回到当前」= 组件内焦点复位（不再页面跳转）——
    /// 左键点击后继卡切换焦点后，点击按钮恢复为最初场景。</summary>
    [Fact]
    public void BackToCurrentButton_ResetsFocus_AfterLeftClickNavigation()
    {
        TestApp.EnsureAvaloniaInitialized();
        var (visualizer, enc, t16, t20, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        // 初始焦点 = 页面实体 enc（测试入口）
        var currentBefore = CurrentCard(detail);
        Assert.NotNull(currentBefore);
        Assert.True(ContainsText(currentBefore!, "测试入口"), "initial focus is the page entity");

        // 左键点击后继卡「便利店」→ 组件内焦点切换
        var succCard = BranchCards(detail).Single(c => ContainsText(c, "便利店"));
        (succCard.Parent as Panel)?.Children.Remove(succCard);
        var window = new Window { Width = 500, Height = 300, Content = succCard };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var pt = new Point(150, 30);
            window.MouseDown(pt, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(pt, MouseButton.Left, RawInputModifiers.None);
        }
        finally
        {
            window.Close();
        }
        // 焦点已切换：当前卡变为「便利店」（原页面实体不再是当前卡）
        var currentAfter = CurrentCard(detail);
        Assert.NotNull(currentAfter);
        Assert.True(ContainsText(currentAfter!, "便利店"),
            "left-click on a successor card switches the in-view focus to it");

        // 点击「⏎ 回到当前」→ 焦点复位
        var btn = FindAll<Button>(detail).Single(b => (b.Content as string)?.Contains("⏎") == true);
        (btn.Parent as Panel)?.Children.Remove(btn);
        var window2 = new Window { Width = 300, Height = 120, Content = btn };
        try
        {
            window2.Show();
            Dispatcher.UIThread.RunJobs();
            btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        finally
        {
            window2.Close();
        }
        var currentReset = CurrentCard(detail);
        Assert.NotNull(currentReset);
        Assert.True(ContainsText(currentReset!, "测试入口"), "back-to-current resets focus to the page entity");
    }

    /// <summary>D08 §九.7: 前驱卡 Ctrl+LMB 跳转前驱 / Ctrl+RMB Peek；当前卡不触发。</summary>
    [Fact]
    public void FlowView_PredecessorCard_CtrlClickNavigates_CtrlRmbPeeks_CurrentCardInert()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new BranchResolver();
        var enc = NewEnc(12, "当前场景", "enc12");
        var src = NewEnc(50, "前驱场景", "enc50");
        src.Responses = "=12x1x0x0x0";
        resolver.Add("12", enc);
        resolver.Add("50", src);

        var lookup = new ItemLookupStub(new Dictionary<string, ItemType>());
        lookup.ReferenceLookups = new Dictionary<Type, List<object>>
        {
            [typeof(Encounter)] = new List<object> { src },
        };

        var router = new RecordingRouter();
        var vis = CreateVis(resolver, router, new EncLoc());
        var visualizer = new EncounterEntityVisualizer(vis, new RefNode(resolver, router), lookup);

        var detail = visualizer.BuildDetail(enc);

        // 前驱卡（本场景无出边 → 唯一的 240px 卡）
        var predCard = BranchCards(detail).Single(c => ContainsText(c, "前驱场景"));
        (predCard.Parent as Panel)?.Children.Remove(predCard);
        var window = new Window { Width = 500, Height = 300, Content = predCard };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var pt = new Point(150, 30);
            window.MouseDown(pt, MouseButton.Left, RawInputModifiers.Control);
            window.MouseUp(pt, MouseButton.Left, RawInputModifiers.Control);
            Assert.Contains(router.Navigations, n => n.Type == typeof(Encounter) && n.EntityId == "enc50");

            window.MouseDown(pt, MouseButton.Right, RawInputModifiers.Control);
            window.MouseUp(pt, MouseButton.Right, RawInputModifiers.Control);
            Assert.Contains(router.Peeks, p => p.Type == typeof(Encounter) && p.EntityId == "enc50");
            var peek50 = router.Peeks.First(p => p.EntityId == "enc50");
            Assert.NotNull(peek50.Entity); // R64: 前驱卡 peek 收到前驱实体本身
            Assert.Equal("enc50", peek50.Entity!.EntityId);
        }
        finally
        {
            window.Close();
        }

        // 当前卡不接导航
        var current = CurrentCard(detail);
        Assert.NotNull(current);
        (current!.Parent as Panel)?.Children.Remove(current);
        var window2 = new Window { Width = 500, Height = 300, Content = current };
        try
        {
            window2.Show();
            Dispatcher.UIThread.RunJobs();
            var pt = new Point(150, 30);
            window2.MouseDown(pt, MouseButton.Left, RawInputModifiers.Control);
            window2.MouseUp(pt, MouseButton.Left, RawInputModifiers.Control);
            window2.MouseDown(pt, MouseButton.Right, RawInputModifiers.Control);
            window2.MouseUp(pt, MouseButton.Right, RawInputModifiers.Control);
            Assert.DoesNotContain(router.Navigations, n => n.EntityId == "enc12");
            Assert.DoesNotContain(router.Peeks, p => p.EntityId == "enc12");
        }
        finally
        {
            window2.Close();
        }
    }

    /// <summary>D08 §九.8: ✨ 效果区 6 字段各自聚合行 + vLoot 内联树。</summary>
    [Fact]
    public void EffectsPanel_SixEffectRows_WithInlineLootTree()
    {
        var (visualizer, enc, _, _, resolver, _) = CreateTwoBranchScenario(withPreConds: false,
            items: new ItemLookupStub(new Dictionary<string, ItemType>
            {
                ["90.3"] = new() { EntityId = "it903", Name = "撬棍", Description = "撬棍", GroupId = 90, SubgroupId = 3 },
            }));
        var tt77 = new TreasureTable { EntityId = "tt77", Id = 77, Name = "营地补给" };
        tt77.Treasures.RawText = "90.3x1"; // serializer populates RawText in production
        var tt66 = new TreasureTable { EntityId = "tt66", Id = 66, Name = "旧装备" };
        var tt88 = new TreasureTable { EntityId = "tt88", Id = 88, Name = "随身杂物" };
        resolver.Add("77", tt77);
        resolver.Add("66", tt66);
        resolver.Add("88", tt88);
        resolver.Add("90", new ItemType { EntityId = "it90", Name = "开锁器", GroupId = 0, SubgroupId = 0 });
        resolver.Add("7", new Creature { EntityId = "cr7", Id = 7, Name = "野狗" });

        enc.Loot = RefList("77");
        enc.Loot.RawText = "77";
        enc.TreasureId = RefList("88");
        enc.TreasureId.RawText = "88";
        enc.RemoveTreasureId = RefList("66");
        enc.RemoveTreasureId.RawText = "66";
        enc.ItemsId = RefList("90");
        enc.ItemsId.RawText = "90";
        enc.Price = 5.5;
        enc.Teleport = "10,20";
        enc.CreatureId = RefList("7");
        enc.CreatureId.RawText = "7";
        enc.CreatureHex = "1,2";

        var detail = visualizer.BuildDetail(enc);
        Assert.Contains(TextsOf(detail), t => t == "🎁 给物");
        Assert.Contains(TextsOf(detail), t => t == "🎒 战利品池");
        Assert.Contains(TextsOf(detail), t => t == "给予物品"); // R64: GiveItem 键值本身无 emoji
        Assert.Contains(TextsOf(detail), t => t == "💰 费用");
        Assert.Contains(TextsOf(detail), t => t == "🗑 移除");
        Assert.Contains(TextsOf(detail), t => t == "📍 传送");
        Assert.Contains(TextsOf(detail), t => t == "🐾 刷出");
        Assert.True(ContainsText(detail, "$5.50"), "cost row should show the price");
        Assert.True(ContainsText(detail, "开锁器"), "give-item row should show the item name");
        Assert.True(ContainsText(detail, "野狗"), "spawn row should show the creature name");
        Assert.True(ContainsText(detail, "半径 1,2"), "spawn row should show the creature hex radius");
        // vLoot 内联战利品树（复用 TreasureTable 行，概率归一）
        Assert.True(ContainsText(detail, "撬棍"), "vLoot row should embed the inline loot tree");
        Assert.True(ContainsText(detail, "营地补给"), "vLoot row should carry the pool name");
    }

    /// <summary>D08 §九.9: 效果字段全空 → 无「效果」节（节标题数 = 0，整段隐藏）。</summary>
    [Fact]
    public void EffectsPanel_AllDefault_Hidden()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario(withPreConds: false);
        var detail = visualizer.BuildDetail(enc);
        Assert.DoesNotContain(TextsOf(detail), t => t.Contains("内容与效果"));
        Assert.DoesNotContain(TextsOf(detail), t => t.Contains("✨ 效果"));
    }

    /// <summary>D08 §九.10: 触发条件 "1"/"0" 不显示；有值 → 语义色徽章（Fatal 红）。</summary>
    [Fact]
    public void TriggerConditions_HidesOneZero_ShowsSemanticBadges()
    {
        var (visualizer, enc, _, _, resolver, _) = CreateTwoBranchScenario();
        resolver.Add("8", new Condition
        {
            EntityId = "c8", Id = 8, Name = "重伤", Fatal = true,
            FieldNames = "m_fMoveCost", Modifiers = "+0.5"
        });

        // "1"（无条件占位）与 "0" → 无触发条件区
        enc.Conditions = RefList("1");
        enc.Conditions.RawText = "1";
        Assert.DoesNotContain(TextsOf(visualizer.BuildDetail(enc)), t => t == "触发条件");

        enc.Conditions = RefList("0");
        enc.Conditions.RawText = "0";
        Assert.DoesNotContain(TextsOf(visualizer.BuildDetail(enc)), t => t == "触发条件");

        // 真值 → 触发条件区 + Fatal 语义色徽章 + hover 效果翻译
        enc.Conditions = RefList("8");
        enc.Conditions.RawText = "8";
        var detail = visualizer.BuildDetail(enc);
        Assert.Contains(TextsOf(detail), t => t == "触发条件");
        Assert.True(ContainsText(detail, "重伤"));
        var fatalBadge = FindAll<Border>(detail).FirstOrDefault(b =>
            (b.Background as ISolidColorBrush)?.Color == Color.Parse("#FFEBEE") && ContainsText(b, "重伤"));
        Assert.NotNull(fatalBadge);
        var tip = ToolTip.GetTip(fatalBadge!) as Control;
        Assert.NotNull(tip);
        Assert.True(ContainsText(tip!, "⚡ m_fMoveCost +0.5"), "hover should translate the condition effect");
    }

    /// <summary>D08 §九.11: 触发器摘要 📍 区域 / 📅 日期 / 🧱 格类型 / ♻ 可重复 + 可跳转。</summary>
    [Fact]
    public void TriggerSummary_AreaDateHexRepeatable_Navigable()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new BranchResolver();
        var enc = NewEnc(1, "测试入口", "enc1");
        resolver.Add("1", enc);

        var trigger = new EncounterTrigger
        {
            EntityId = "trig1", Id = 9, Name = "道路事件",
            Area = "5,10,2", DateMin = "30", DateMax = "120",
            HexTypes = RefList("5"), EncounterId = RefList("1"),
        };
        var lookup = new ItemLookupStub(new Dictionary<string, ItemType>());
        lookup.ReferenceLookups = new Dictionary<Type, List<object>>
        {
            [typeof(EncounterTrigger)] = new List<object> { trigger },
        };

        var router = new RecordingRouter();
        var vis = CreateVis(resolver, router, new EncLoc());
        var visualizer = new EncounterEntityVisualizer(vis, new RefNode(resolver, router), lookup);

        var detail = visualizer.BuildDetail(enc);
        Assert.Contains(TextsOf(detail), t => t == "触发器");
        Assert.True(ContainsText(detail, "📍 (5,10,r=2)"), "aArea → 📍 (x,y,r=dist)");
        Assert.True(ContainsText(detail, "📅 30~120"), "dateMin/dateMax → 📅");
        Assert.True(ContainsText(detail, "🧱 格类型"), "aHexTypes non-empty → 🧱");
        Assert.True(ContainsText(detail, "♻ 可重复"), "bUnique=false → ♻ 可重复");
        Assert.True(ContainsText(detail, "道路事件"), "trigger name shown");

        // 可跳转 EncounterTrigger（Ctrl+LMB）——名称徽章是 CornerRadius 4 的 Badge
        var badge = FindAll<Border>(detail).First(b =>
            b.CornerRadius == new CornerRadius(4) && ContainsText(b, "道路事件"));
        (badge.Parent as Panel)?.Children.Remove(badge);
        var window = new Window { Width = 400, Height = 200, Content = badge };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var pt = new Point(60, 20);
            window.MouseDown(pt, MouseButton.Left, RawInputModifiers.Control);
            window.MouseUp(pt, MouseButton.Left, RawInputModifiers.Control);
            Assert.Contains(router.Navigations,
                n => n.Type == typeof(EncounterTrigger) && n.EntityId == "trig1");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>D08 §九.12: 入口/终点纯函数 + Hero 徽章同源。</summary>
    [Fact]
    public void EntryTerminal_PureFunction_AndHeroBadges()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario(withPreConds: false);
        // 中间节点：有入边 + 正常出边 → 无标记
        enc.Id = 100;
        enc.Responses = "=16x1x0x0x0,=20x1x0x0x0";
        var (middleBranches, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        var middle = EncounterEntityVisualizer.DetermineEntryTerminal(2, middleBranches);
        Assert.False(middle.IsEntry);
        Assert.False(middle.IsTerminal);

        // 入口：无入边 + 正常出边 → 仅 IsEntry
        var (entryBranches, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        var entry = EncounterEntityVisualizer.DetermineEntryTerminal(0, entryBranches);
        Assert.True(entry.IsEntry);
        Assert.False(entry.IsTerminal);

        // 终点：全终止出边（自指）+ 有入边 → 仅 IsTerminal
        enc.Id = 236;
        enc.Responses = "=236x1x0x0x0";
        var (terminalBranches, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        var terminal = EncounterEntityVisualizer.DetermineEntryTerminal(1, terminalBranches);
        Assert.False(terminal.IsEntry);
        Assert.True(terminal.IsTerminal);

        // 孤立节点：无入边 + 全终止出边 → 两者都有（Hero 不显示，普通）
        var (isoBranches, _) = visualizer.PrepareBranches(enc, new HashSet<string>());
        var isolated = EncounterEntityVisualizer.DetermineEntryTerminal(0, isoBranches);
        Assert.True(isolated.IsEntry);
        Assert.True(isolated.IsTerminal);

        // UI 同源：无入边 → Hero ⛳ 入口
        var (visualizer2, enc2, _, _, _, _) = CreateTwoBranchScenario(withPreConds: false);
        enc2.Responses = "=16x1x0x0x0,=20x1x0x0x0";
        var detailEntry = visualizer2.BuildDetail(enc2);
        Assert.True(ContainsText(detailEntry, "⛳ 入口"), "Hero should carry the entry badge");

        // UI 同源：全终止出边 + 有入边 → Hero ⏹ 终点
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new BranchResolver();
        var encT = NewEnc(236, "终点场景", "enc236");
        encT.Responses = "=236x1x0x0x0";
        var srcT = NewEnc(3, "入边场景", "enc3");
        srcT.Responses = "=236x1x0x0x0";
        resolver.Add("236", encT);
        resolver.Add("3", srcT);
        var lookupT = new ItemLookupStub(new Dictionary<string, ItemType>());
        lookupT.ReferenceLookups = new Dictionary<Type, List<object>>
        {
            [typeof(Encounter)] = new List<object> { srcT },
        };
        var visT = CreateVis(resolver, new RecordingRouter(), new EncLoc());
        var visualizerT = new EncounterEntityVisualizer(visT, new RefNode(resolver, new StubNavigationRouter()), lookupT);
        var detailTerminal = visualizerT.BuildDetail(encT);
        Assert.True(ContainsText(detailTerminal, "⏹ 终点"), "Hero should carry the terminal badge");
    }

    /// <summary>R64: 内容只放描述（图片已在 Hero，不重复）——文本 Wrap 收进 Card；空描述整段隐藏。</summary>
    [Fact]
    public void StoryPage_BookStyle_ImageAndText_HiddenWhenEmpty()
    {
        var (visualizer, enc, _, _, resolver, _) = CreateTwoBranchScenario();
        var pngPath = CreateTempPng();
        try
        {
            var vis = CreateVis(resolver, new RecordingRouter(), new EncLoc(), _ => pngPath);
            var v2 = new EncounterEntityVisualizer(vis, new RefNode(resolver, new StubNavigationRouter()),
                new StubEntityLookupService());
            enc.Description = "书页式描述：内容只放描述文本，图片已在 Hero 不重复。";
            enc.Image = RefList("Page.png");
            enc.Image.RawText = "Page.png";

            var detail = v2.BuildDetail(enc);
            // 内容区：描述文本在 Card 内（无 96px 缩略图——图片归属 Hero）
            Assert.True(ContainsText(detail, "书页式描述"), "description text should be present");
            Assert.DoesNotContain(FindAll<Image>(detail), i => i.Width == 96);
        }
        finally
        {
            File.Delete(pngPath);
        }

        // 空描述 + 无效果 + 无地图标注 → ④ 整段隐藏
        var empty = NewEnc(2, "空场景", "enc2");
        var detailEmpty = visualizer.BuildDetail(empty);
        Assert.DoesNotContain(TextsOf(detailEmpty), t => t.Contains("内容与效果"));
    }

    /// <summary>D08 §九.8 补充：地图标注小节（aMinimapHexes/ptEditor）保留在 ④ 底部。</summary>
    [Fact]
    public void MapNotes_MinimapHexesAndEditor_UnderContentEffects()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        // 真实格式（Doc 38 §11）：x*x*y=标签，逗号分隔多个标记，标签可空
        enc.MinimapHexes = "25x36=格雷林营地,40x108";
        enc.Editor = "5,6";

        var detail = visualizer.BuildDetail(enc);
        Assert.True(ContainsText(detail, "(25x36) 格雷林营地"), "minimap marker with label");
        Assert.True(ContainsText(detail, "(40x108)"), "minimap marker without label");
        Assert.True(ContainsText(detail, "(5,6)"), "editor placement");
        Assert.Contains(TextsOf(detail), t => t == "内容与效果");
    }

    /// <summary>Write a tiny valid PNG to disk so LoadImage can decode a real thumbnail.</summary>
    private static string CreateTempPng()
    {
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
        var path = Path.Combine(Path.GetTempPath(), $"neotest_enc_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
