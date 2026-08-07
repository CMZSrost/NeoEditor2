using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.Services;
using NeoEditor.Plugins.EntityEditor.Visualizers;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// D05: Creature visualizer — Hero 身份/阵营解析、攻击模式 Σ 伤害堆叠条、
/// 出场状态概率后缀、反向刷新点（同点权重归一）与未解析引用灰色兜底。
/// </summary>
public class CreatureVisualizerTests
{
    private static VisHelperService CreateVis(ReturningResolver resolver, ILocalizationService? loc = null)
        => new(_ => null, resolver, new StubNavigationRouter(), new StubEntityLookupService(),
            loc ?? new StubLocalizationService());

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
            }
        }
        Walk(root);
        return results;
    }

    /// <summary>Resolver that records LookupRef&lt;T&gt; calls and returns an entity per raw id.
    /// Mirrors the real resolver's {id}={value} extraction for assignment-formatted segments.</summary>
    private sealed class ReturningResolver : StubReferenceResolver
    {
        public List<string> CapturedRawIds { get; } = new();
        private readonly Dictionary<string, IEntity> _byId = new();

        public void Add(string rawId, IEntity entity) => _byId[rawId] = entity;

        public override T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : class
        {
            CapturedRawIds.Add(rawId);
            if (_byId.TryGetValue(rawId, out var e)) return (T)(object)e;
            // 处理 "20=14"（槽位=ID）与 "52=0.5"（ID=概率）两种赋值格式
            var eq = rawId.IndexOf('=');
            if (eq > 0)
            {
                var before = rawId[..eq].Trim();
                if (_byId.TryGetValue(before, out var b)) return (T)(object)b;
                var after = rawId[(eq + 1)..].Trim();
                if (_byId.TryGetValue(after, out var a)) return (T)(object)a;
            }
            return null;
        }
    }

    private sealed class FormattingLoc : ILocalizationService
    {
        private readonly Dictionary<string, string> _map = new()
        {
            ["Vis.TotalDamage"] = "Total Damage",
            ["Vis.Cut"] = "Cut",
            ["Vis.Blunt"] = "Blunt",
            ["Vis.Range"] = "Range",
            ["Vis.Penetration"] = "Penetration",
            ["Morale"] = "Morale",
            ["Vis.Effective"] = "Effective",
            ["Vis.CtrlClickHint"] = "Ctrl+Click → open detail",
        };

        public string this[string key] => _map.TryGetValue(key, out var v) ? v : key;
        public string this[string key, params object[] args] =>
            _map.TryGetValue(key, out var v) ? string.Format(v, args) : key;
        public CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void SetCulture(CultureInfo culture) { }
    }

    private static int CountSegments(Control root, string colorHex)
    {
        var c = Color.Parse(colorHex);
        return FindAll<Border>(root).Count(b => (b.Background as ISolidColorBrush)?.Color == c);
    }

    /// <summary>构建含原始段（如 "52=0.5"）的引用列表（Count&gt;0，R30 空守卫）。</summary>
    private static ReferenceList<IReferenceEntry> RefList(params string[] ids)
    {
        var list = new ReferenceList<IReferenceEntry>();
        foreach (var id in ids)
            list.Add(new PureRefFormat { Entity = new EntityRef { Id = id } });
        return list;
    }

    private static Creature NewCreature(int id, string name) => new()
    {
        EntityId = $"creature{id}",
        Id = id,
        Name = name,
    };

    [Fact]
    public void BuildDetail_ShowsIdentity_And_FactionName()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        resolver.Add("3", new Faction { EntityId = "f3", Name = "Dogman Pack" });
        var vis = CreateVis(resolver);
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new CreatureEntityVisualizer(vis, refNode, new StubEntityLookupService());

        var c = NewCreature(17, "Dogman");
        c.NamePublic = "Stranger";
        c.Notes = "JD";
        c.Image = RefList("CreDogman.png");
        c.MovesPerTurn = 4;
        c.Faction = RefList("3");

        var detail = visualizer.BuildDetail(c);
        var texts = FindAll<TextBlock>(detail).Select(t => t.Text).Where(t => t is not null).ToList();

        // Hero：名称标题 / strNamePublic 斜体副文本 / strNotes
        Assert.Contains(texts, t => t!.Contains("Dogman"));
        Assert.Contains(texts, t => t!.Contains("Stranger"));
        Assert.Contains(texts, t => t!.Contains("JD"));
        // ID 徽章 + 行动点 chip + 解析后的阵营名 chip
        Assert.Contains(texts, t => t!.Contains("ID: 17"));
        Assert.Contains(texts, t => t!.Contains("4 moves/turn"));
        Assert.Contains(texts, t => t!.Contains("Dogman Pack"));
        // 阵营解析走了 LookupRef<Faction>
        Assert.Contains("3", resolver.CapturedRawIds);
    }

    [Fact]
    public void BuildDetail_DamageStack_FromAttackModes()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        resolver.Add("7", new AttackMode { EntityId = "am7", Name = "Claws", DamageCut = 0.5, DamageBlunt = 4.5, Morale = 0.25 });
        resolver.Add("17", new AttackMode { EntityId = "am17", Name = "Rifle Shot", DamageCut = 2.5, DamageBlunt = 2.0, Range = 80, Penetration = 3, Morale = 0.25 });
        var vis = CreateVis(resolver, new FormattingLoc());
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new CreatureEntityVisualizer(vis, refNode, new StubEntityLookupService());

        var c = NewCreature(17, "Dogman");
        c.AttackModes = RefList("7", "17");

        var detail = visualizer.BuildDetail(c);
        var texts = FindAll<TextBlock>(detail).Select(t => t.Text).Where(t => t is not null).ToList();

        // 两个攻击模式都解析为行
        Assert.Contains(texts, t => t!.Contains("Claws"));
        Assert.Contains(texts, t => t!.Contains("Rifle Shot"));
        // Σ 总伤害条：总条(1 cut + 1 blunt) + 每行一条
        Assert.True(CountSegments(detail, "#E57373") >= 3, "cut segments: total bar + per-row bars");
        Assert.True(CountSegments(detail, "#64B5F6") >= 3, "blunt segments: total bar + per-row bars");
        // Σ 有效伤害：(0.5+4.5)×1.25 + (2.5+2.0)×1.25 = 11.875 → "11.9 (×1.25)"（ValueRow 值与标签分列）
        Assert.Contains(texts, t => t!.Contains("11.9 (×1.25)"));
        // 行 meta：射程 / 穿透 / 士气补正
        Assert.Contains(texts, t => t!.Contains("Range 80"));
        Assert.Contains(texts, t => t!.Contains("Penetration 3"));
        Assert.Contains(texts, t => t!.Contains("Morale +25%"));
        // 解析收到干净裸 ID（无括号）
        Assert.Contains("7", resolver.CapturedRawIds);
        Assert.Contains("17", resolver.CapturedRawIds);
        Assert.DoesNotContain("[7, 17]", resolver.CapturedRawIds);
    }

    [Fact]
    public void BuildDetail_BaseConditions_ShowProbability()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        resolver.Add("52", new Condition { EntityId = "c52", Name = "Dysentery", Duration = 48 });
        resolver.Add("38", new Condition { EntityId = "c38", Name = "WellFed", Duration = 12 });
        var vis = CreateVis(resolver);
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new CreatureEntityVisualizer(vis, refNode, new StubEntityLookupService());

        var c = NewCreature(17, "Dogman");
        c.BaseConditions = RefList("52=0.5", "38=1");

        var detail = visualizer.BuildDetail(c);
        var texts = FindAll<TextBlock>(detail).Select(t => t.Text).Where(t => t is not null).ToList();

        // 52=0.5 → 概率后缀 "· 50%"；38=1 → 全量携带无后缀
        Assert.Contains(texts, t => t!.Contains("Dysentery") && t.Contains("50%"));
        Assert.Contains(texts, t => t!.Contains("WellFed") && !t.Contains("50%"));
        // 概率语义色：Duration 条件 → 蓝色徽章（D04 条件语义色）
        Assert.True(CountSegments(detail, "#E3F2FD") >= 2, "duration-condition badges should be #E3F2FD");
    }

    [Fact]
    public void BuildDetail_ReverseSpawnSources()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        var vis = CreateVis(resolver);
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var dataTable = new StubEntityLookupService
        {
            ReferenceLookups =
            {
                [typeof(CreatureSource)] = new List<object>
                {
                    new CreatureSource
                    {
                        EntityId = "cs1", Name = "North Dog Pack", X = 40, Y = 100,
                        Min = 2, Max = 4, Weight = 0.5, CreatureId = RefList("17")
                    },
                    new CreatureSource
                    {
                        EntityId = "cs2", Name = "South Dog Pack", X = 40, Y = 100,
                        Min = 1, Max = 2, Weight = 0.5, CreatureId = RefList("17")
                    },
                    // 指向其他生物（5）：不应出现在本生物的刷新点里，但它的权重计入同点 Σ
                    new CreatureSource
                    {
                        EntityId = "cs3", Name = "Other Pack", X = 40, Y = 100,
                        Min = 1, Max = 3, Weight = 0.5, CreatureId = RefList("5")
                    },
                }
            }
        };
        var visualizer = new CreatureEntityVisualizer(vis, refNode, dataTable);

        var c = NewCreature(17, "Dogman");

        var detail = visualizer.BuildDetail(c);
        var texts = FindAll<TextBlock>(detail).Select(t => t.Text).Where(t => t is not null).ToList();

        // 只有指向 17 的两个刷新点出现
        Assert.Contains(texts, t => t!.Contains("North Dog Pack"));
        Assert.Contains(texts, t => t!.Contains("South Dog Pack"));
        Assert.DoesNotContain(texts, t => t!.Contains("Other Pack"));
        // 行格式：点名 (x,y) · 数量 · 权重（占同点比例）
        Assert.Contains(texts, t => t!.Contains("(40, 100)"));
        Assert.Contains(texts, t => t!.Contains("2–4"));
        // 同点 (40,100) Σ 权重 = 1.5（含 Other Pack）→ cs1 占 0.5/1.5 ≈ 33%
        Assert.Contains(texts, t => t!.Contains("权重 0.50") && t.Contains("占同点 33%"));
    }

    [Fact]
    public void BuildDetail_UnresolvedRefs_FallBackToGrayBadge()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver(); // 不注册任何实体 → 引用全部未解析
        var vis = CreateVis(resolver);
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new CreatureEntityVisualizer(vis, refNode, new StubEntityLookupService());

        var c = NewCreature(17, "Dogman");
        c.EncounterIds = RefList("9999");
        c.AttackModes = RefList("8888");

        var detail = visualizer.BuildDetail(c);
        var texts = FindAll<TextBlock>(detail).Select(t => t.Text).Where(t => t is not null).ToList();

        // 未解析引用：灰色兜底（#F5F5F5），不崩溃、不静默丢失
        Assert.Contains(texts, t => t!.Contains("9999"));
        Assert.Contains(texts, t => t!.Contains("8888"));
        Assert.True(CountSegments(detail, "#F5F5F5") >= 1, "grey fallback badge for unresolved encounter");
        // 旧的 OnEnterConditions 错误标签（vEncounterIDs 指向 Encounter）不再出现
        Assert.DoesNotContain(texts, t => t!.Contains("OnEnterConditions"));
    }
}
