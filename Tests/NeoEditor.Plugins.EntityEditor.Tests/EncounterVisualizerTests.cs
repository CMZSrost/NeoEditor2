using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

    private static TextBlock? MermaidBlock(Control root)
        => FindAll<TextBlock>(root).FirstOrDefault(t => t.Text?.StartsWith("flowchart LR") == true);

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
        public List<(Type Type, string EntityId)> Peeks { get; } = new();

        public override void NavigateToEntity(Type entityType, string entityId, IEntity? resolvedEntity = null)
            => Navigations.Add((entityType, entityId));

        public override void RequestPeek(Type entityType, string entityId, IEntity? sourceEntity)
            => Peeks.Add((entityType, entityId));
    }

    private sealed class EncLoc : ILocalizationService
    {
        private readonly Dictionary<string, string> _map = new()
        {
            ["Vis.StoryBranch"] = "剧情分支",
            ["Vis.EncounterChain"] = "剧情链",
            ["Vis.MermaidSource"] = "Mermaid源码",
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
        // 图形区仍有两列（当前卡 + 分支卡）与箭头
        Assert.NotNull(CurrentCard(detail));
        Assert.Equal(2, BranchCards(detail).Count);
        Assert.Contains(TextsOf(detail), t => t.Contains("→"));
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
            Assert.Equal(52, image.Width);
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

    // ── 4. Mermaid 与图形同数据源：无反向 R 节点，含名称+ID+权重+有效概率 ─────

    [Fact]
    public void MermaidText_NoReverseNodes_SharedDataModel()
    {
        var (visualizer, enc, t16, t20, _, _) = CreateTwoBranchScenario();
        var (branches, validTotal) = visualizer.PrepareBranches(enc, new HashSet<string>());
        Assert.Equal(3.0, validTotal, 6);

        var text = EncounterEntityVisualizer.BuildMermaidText(branches, enc, new HashSet<string>());

        // 当前 + 分支节点都带 名称 + (#id)
        Assert.Contains("A[\"📍 测试入口 (#1)\"]", text);
        Assert.Contains("B0[\"便利店 (#16)\"]", text);
        Assert.Contains("B1[\"逃跑 (#20)\"]", text);
        // 每条边带权重 + 有效概率（无 P2 空格）
        Assert.Contains("2.0(66.67%)", text);
        Assert.Contains("1.0(33.33%)", text);
        // 目标有前置条件 → 📋×n 附加
        Assert.Contains("[📋1]", text);
        // 无反向 R 节点、无 ctx 标签（🎒🐾📋pre:）
        Assert.DoesNotContain("R0[", text);
        Assert.DoesNotContain("← ", text);
        Assert.DoesNotContain("🎒", text);
        Assert.DoesNotContain("🐾", text);
        Assert.DoesNotContain("📋pre:", text);
    }

    [Fact]
    public void MermaidText_ItemEdge_CarriesItemNameAndMult()
    {
        var (visualizer, enc, t16, _, resolver, _) = CreateTwoBranchScenario();
        var items = new ItemLookupStub(new Dictionary<string, ItemType>
        {
            ["90.3"] = new() { EntityId = "it903", Name = "撬棍", Description = "撬棍", GroupId = 90, SubgroupId = 3 },
        });
        t16.PreConditions = new ReferenceList<IReferenceEntry>();
        resolver.Add("16", t16);
        enc.Responses = "90.3x2=16x2x0x0x0";

        var vis = CreateVis(resolver, new RecordingRouter(), new EncLoc());
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var v2 = new EncounterEntityVisualizer(vis, refNode, items);

        var (branches, _) = v2.PrepareBranches(enc, new HashSet<string>());
        var text = EncounterEntityVisualizer.BuildMermaidText(branches, enc, new HashSet<string>());
        Assert.Contains("撬棍 ×2 | 2.0(100%)", text);

        // 卡片 tooltip 也带物品徽章
        var detail = v2.BuildDetail(enc);
        var card = BranchCards(detail).Single();
        var tooltip = ToolTip.GetTip(card) as Control;
        Assert.NotNull(tooltip);
        Assert.True(ContainsText(tooltip!, "🛡 撬棍 ×2"), "tooltip should carry the item badge");
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
    public void FilterCheckbox_DimsUnsatisfiedCard_AndSyncsMermaid()
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

        // Mermaid 同步刷新：满足分支 100%、不满足分支 0% + ⚠ 标记
        var mermaid = MermaidBlock(detail)?.Text ?? "";
        Assert.Contains("2.0(100%)", mermaid);
        Assert.Contains("1.0(0%)", mermaid);
        Assert.Contains("[⚠0/1]", mermaid);
    }

    // ── 7. ResponsesPanel 合并：无独立 Vis.Responses 区块，格式提示在分支图内 ─

    [Fact]
    public void ResponsesPanel_Merged_NoStandaloneSection_FormatHintInBranchSection()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        // 无独立 Responses 节标题（合并进分支图）
        Assert.DoesNotContain(TextsOf(detail), t => t == "Responses");
        // 格式提示行保留在分支图内
        Assert.Contains(TextsOf(detail), t => t.Contains("[物品ID]x[数量]=[剧情ID]x[权重]"));
        // 分支节标题仍在
        Assert.Contains(TextsOf(detail), t => t == "剧情分支");
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
    public void Regression_ThreeTabs_NoBranchesPlaceholder_ResponseFormats()
    {
        var (visualizer, enc, _, _, _, _) = CreateTwoBranchScenario();
        var detail = visualizer.BuildDetail(enc);

        // TabControl 仍含 3 个 Tab：剧情分支 / 剧情链 / Mermaid源码
        var tabItems = FindAll<TabItem>(detail);
        Assert.Equal(3, tabItems.Count);
        var tabHeaders = tabItems.Select(t => t.Header?.ToString()).ToList();
        Assert.Contains("剧情分支", tabHeaders);
        Assert.Contains("剧情链", tabHeaders);
        Assert.Contains("Mermaid源码", tabHeaders);

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
        Assert.Null(single[0].ItemId);
        Assert.Null(single[0].Item);
        Assert.Equal(1.0, single[0].Weight, 6);
        Assert.Equal(1.0, single[0].EffectiveProb, 6);
        Assert.Equal(1.0, total1, 6);

        // 90.3x2=16x2x0x0x0,=16x1x0x0x0 → 物品 90.3 ×2 + 空物品分支，同目标 16
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
        Assert.Equal(2, multi.Count);
        Assert.Equal("90.3", multi[0].ItemId);
        Assert.NotNull(multi[0].Item);
        Assert.Equal(2.0, multi[0].ItemMult, 6);
        Assert.Equal(2.0, multi[0].Weight, 6);
        Assert.Null(multi[1].ItemId);
        Assert.Equal(1.0, multi[1].Weight, 6);
        Assert.Equal(3.0, total2, 6);
        Assert.Equal(2.0 / 3.0, multi[0].EffectiveProb, 6);
        Assert.Equal(1.0 / 3.0, multi[1].EffectiveProb, 6);
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
