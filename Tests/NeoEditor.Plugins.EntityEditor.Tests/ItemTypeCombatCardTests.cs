using System;
using System.Collections.Generic;
using System.ComponentModel;
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
/// R36: ItemType combat card — stacked total-damage bar (Doc 21 §7 P3),
/// expandable attack-mode rows (inline detail: ammo/conditions/phrases/notes)
/// and semantic condition colors (Fatal red / Permanent orange / Stackable
/// green / Duration blue).
/// </summary>
public class ItemTypeCombatCardTests
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
    /// Mirrors the real resolver's {value}={id} extraction for slot-prefixed segments.</summary>
    private sealed class ReturningResolver : StubReferenceResolver
    {
        public List<string> CapturedRawIds { get; } = new();
        private readonly Dictionary<string, IEntity> _byId = new();

        public void Add(string rawId, IEntity entity) => _byId[rawId] = entity;

        public override T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : class
        {
            CapturedRawIds.Add(rawId);
            var key = rawId;
            if (!_byId.ContainsKey(key))
            {
                var eq = rawId.IndexOf('=');
                if (eq > 0) key = rawId[(eq + 1)..].Trim();
            }
            return _byId.TryGetValue(key, out var e) ? (T)(object)e : null;
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
        public event PropertyChangedEventHandler? PropertyChanged;
        public void SetCulture(CultureInfo culture) { }
    }

    private static int CountSegments(Control root, string colorHex)
    {
        var c = Color.Parse(colorHex);
        return FindAll<Border>(root).Count(b => (b.Background as ISolidColorBrush)?.Color == c);
    }

    [Fact]
    public void BuildDetail_AttackModes_ShowsTotalDamageStackAndRows()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        resolver.Add("14", new AttackMode { EntityId = "am14", Name = "Rifle Shot", DamageCut = 2.5, DamageBlunt = 2.0, Range = 80, Penetration = 3, Morale = 0.25 });
        resolver.Add("7", new AttackMode { EntityId = "am7", Name = "Club", DamageCut = 0.5, DamageBlunt = 4.5 });
        var vis = CreateVis(resolver, new FormattingLoc());
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new ItemTypeEntityVisualizer(vis, refNode, new StubEntityLookupService());

        var it = new ItemType { EntityId = "it1", Name = "Test Weapon" };
        it.AttackModes = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "20=14" } },
            new PureRefFormat { Entity = new EntityRef { Id = "21=7" } },
        };

        var detail = visualizer.BuildDetail(it);
        var texts = FindAll<TextBlock>(detail).Select(t => t.Text).Where(t => t is not null).ToList();

        // both attack modes resolved into rows
        Assert.Contains(texts, t => t!.Contains("Rifle Shot"));
        Assert.Contains(texts, t => t!.Contains("Club"));
        // stacked bar segments: total bar (1 cut + 1 blunt) + per-row bars
        Assert.True(CountSegments(detail, "#E57373") >= 3, "cut segments: total + RifleShot + Club (+ combat section accent)");
        Assert.True(CountSegments(detail, "#64B5F6") >= 3, "blunt segments: total + RifleShot + Club (+ Hero mod badge)");
        // row meta: range + penetration + weapon morale modifier from the first mode
        Assert.Contains(texts, t => t!.Contains("Range 80"));
        Assert.Contains(texts, t => t!.Contains("Penetration 3"));
        Assert.Contains(texts, t => t!.Contains("Morale +25%"));
        // R37: morale-adjusted effective damage in the inline detail (hidden but in the tree)
        Assert.Contains(texts, t => t!.Contains("Effective 5.6 (×1.25)")); // Rifle Shot 2.5+2.0=4.5 ×1.25
        // resolver received the raw slot segments (clean, no brackets)
        Assert.Contains("20=14", resolver.CapturedRawIds);
        Assert.DoesNotContain("[20=14, 21=7]", resolver.CapturedRawIds);
    }

    [Fact]
    public void AttackModeRow_DetailInitiallyHidden()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        resolver.Add("14", new AttackMode { EntityId = "am14", Name = "Rifle Shot", DamageCut = 2.5, DamageBlunt = 2.0 });
        var vis = CreateVis(resolver, new FormattingLoc());
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new ItemTypeEntityVisualizer(vis, refNode, new StubEntityLookupService());

        var it = new ItemType { EntityId = "it1", Name = "Test Weapon" };
        it.AttackModes = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "14" } },
        };

        var detail = visualizer.BuildDetail(it);

        // the inline detail (contains the Ctrl+Click hint) exists but starts collapsed
        var hint = FindAll<TextBlock>(detail).FirstOrDefault(t => t.Text == "Ctrl+Click → open detail");
        Assert.NotNull(hint);
        var detailPanel = hint!.Parent;
        while (detailPanel is not null && detailPanel is not StackPanel sp) detailPanel = detailPanel.Parent;
        Assert.NotNull(detailPanel);
        Assert.False(((StackPanel)detailPanel).IsVisible);
    }

    [Fact]
    public void ConditionSection_FatalAndDuration_SemanticColors()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        resolver.Add("3", new Condition { EntityId = "c3", Name = "Bleeding", Fatal = true });
        resolver.Add("5", new Condition { EntityId = "c5", Name = "WellFed", Duration = 12 });
        var vis = CreateVis(resolver, new FormattingLoc());
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new ItemTypeEntityVisualizer(vis, refNode, new StubEntityLookupService());

        var it = new ItemType { EntityId = "it1", Name = "Test Food" };
        it.UseConditions = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
            new PureRefFormat { Entity = new EntityRef { Id = "5" } },
        };

        var detail = visualizer.BuildDetail(it);

        // Fatal → red badge, Duration → blue badge
        Assert.True(CountSegments(detail, "#FFEBEE") >= 1, "fatal condition badge should be #FFEBEE");
        Assert.True(CountSegments(detail, "#E3F2FD") >= 1, "duration condition badge should be #E3F2FD");
        // severity suffix readable on the badge text
        var texts = FindAll<TextBlock>(detail).Select(t => t.Text).Where(t => t is not null).ToList();
        Assert.Contains(texts, t => t!.Contains("Bleeding") && t.Contains("FATAL"));
        Assert.Contains(texts, t => t!.Contains("WellFed") && t.Contains("12h"));
    }
}
