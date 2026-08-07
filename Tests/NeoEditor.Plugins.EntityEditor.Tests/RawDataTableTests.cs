using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.Services;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// R31: raw-data audit view (grouped full-field table + typed rendering).
/// Covers FieldGroupMetadata grouping, reference-badge resolution through the
/// canonical LookupRef&lt;T&gt; path (same as Detail badges), unresolved amber
/// highlighting, stats computation and the expander label format.
/// </summary>
public class RawDataTableTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static VisHelperService CreateVis(StubReferenceResolver resolver, ILocalizationService? loc = null)
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

    /// <summary>Resolver that records LookupRef&lt;T&gt; calls and returns an entity per raw id.</summary>
    private sealed class ReturningResolver : StubReferenceResolver
    {
        public List<string> CapturedRawIds { get; } = new();
        private readonly Dictionary<string, IEntity> _byId = new();

        public void Add(string rawId, IEntity entity) => _byId[rawId] = entity;

        public override T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : class
        {
            CapturedRawIds.Add(rawId);
            return _byId.TryGetValue(rawId, out var e) ? (T)(object)e : null;
        }
    }

    private sealed class FormattingLoc : ILocalizationService
    {
        private readonly Dictionary<string, string> _map = new()
        {
            ["Vis.RawData"] = "Raw Data",
            ["Vis.RawFields"] = "{0} fields · {1} set",
            ["Vis.RawUnresolved"] = " · {0} unresolved refs",
            ["Vis.RawOriginal"] = "Raw: {0}",
        };

        public string this[string key] => _map.TryGetValue(key, out var v) ? v : key;
        public string this[string key, params object[] args] =>
            _map.TryGetValue(key, out var v) ? string.Format(v, args) : key;
        public CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
        public event PropertyChangedEventHandler? PropertyChanged;
        public void SetCulture(CultureInfo culture) { }
    }

    // ── grouping ─────────────────────────────────────────────────────────────

    [Fact]
    public void BuildRawDataTable_ItemType_GroupsByFieldGroupMetadata()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        var vis = CreateVis(resolver);

        var it = new ItemType { EntityId = "it1", Weight = 1.5f };
        var table = vis.BuildRawDataTable(it);

        var groupHeaders = FindAll<Border>(table)
            .Where(b => b.Tag is string s && s.Length > 0)
            .Select(b => (string)b.Tag!)
            .ToList();

        var expected = FieldGroupMetadata.GetSections(typeof(ItemType))
            .Where(s => HasFieldOfSection(it, s))
            .ToList();

        Assert.NotEmpty(groupHeaders);
        Assert.Equal(expected, groupHeaders); // authoring order preserved
    }

    private static bool HasFieldOfSection(ItemType entity, string section)
        => entity.GetType().GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(ColumnAttribute), true).Length > 0
                        && p.DeclaringType != typeof(IEntity))
            .Any(p => FieldGroupMetadata.GetSection(entity.GetType(), ColumnName(p)) == section);

    private static string ColumnName(System.Reflection.PropertyInfo p)
        => p.GetCustomAttributes(typeof(ColumnAttribute), true).Cast<ColumnAttribute>().First().Name ?? p.Name;

    // ── reference badge resolution (canonical LookupRef<T> path) ─────────────

    [Fact]
    public void BuildRawDataTable_RefColumn_ResolvesViaLookupRef()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        resolver.Add("7", new TreasureTable { EntityId = "tt7", Name = "Scrap Table" });
        var vis = CreateVis(resolver);

        var it = new ItemType { EntityId = "it1" };
        it.DegradeTreasureIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "7" } },
        };

        var table = vis.BuildRawDataTable(it);

        // resolved badge shows the subject, and LookupRef received the CLEAN id ("7", not "[7]").
        var badgeTexts = FindAll<TextBlock>(table).Select(t => t.Text).Where(t => t is not null).ToList();
        Assert.Contains("Scrap Table", badgeTexts);
        Assert.Contains("7", resolver.CapturedRawIds);
        Assert.DoesNotContain("[7]", resolver.CapturedRawIds);
    }

    [Fact]
    public void BuildRawDataTable_UnresolvedRefSegment_AmberBadge()
    {
        TestApp.EnsureAvaloniaInitialized();
        var vis = CreateVis(new ReturningResolver()); // never resolves

        var it = new ItemType { EntityId = "it1" };
        it.DegradeTreasureIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "999" } },
        };

        var table = vis.BuildRawDataTable(it);

        // Note: Avalonia 12 Brush.Parse returns ImmutableSolidColorBrush — match the interface.
        var amberBadges = FindAll<Border>(table)
            .Where(b => (b.Background as ISolidColorBrush)?.Color == Color.Parse("#FFF8E1"))
            .ToList();
        Assert.Single(amberBadges);
        // raw segment text preserved for audit
        var badgeText = FindAll<TextBlock>(amberBadges[0]).FirstOrDefault()?.Text;
        Assert.Equal("999", badgeText);
    }

    // ── stats ────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeRawDataStats_CountsFields()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = new ReturningResolver();
        resolver.Add("7", new TreasureTable { EntityId = "tt7", Name = "Scrap Table" });
        var vis = CreateVis(resolver);

        var it = new ItemType { EntityId = "it1", Weight = 1.5f };
        it.DegradeTreasureIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "7" } },
            new PureRefFormat { Entity = new EntityRef { Id = "8" } }, // unresolved
        };

        var stats = vis.ComputeRawDataStats(it);

        var totalProps = it.GetType().GetProperties()
            .Count(p => p.GetCustomAttributes(typeof(ColumnAttribute), true).Length > 0
                        && p.DeclaringType != typeof(IEntity));
        Assert.Equal(totalProps, stats.TotalFields);
        Assert.True(stats.FieldsWithValue >= 2); // Weight + DegradeTreasureIds
        Assert.Equal(1, stats.UnresolvedRefSegments); // "8" unresolved, "7" resolved
    }

    // ── expander label ───────────────────────────────────────────────────────

    [Fact]
    public void BuildRawDataLabel_FormatsStats()
    {
        TestApp.EnsureAvaloniaInitialized();
        var vis = CreateVis(new ReturningResolver(), new FormattingLoc());

        var label = vis.BuildRawDataLabel(new VisHelperService.RawDataStats(24, 12, 2));
        Assert.Equal("Raw Data  (24 fields · 12 set · 2 unresolved refs)", label);

        var noUnresolved = vis.BuildRawDataLabel(new VisHelperService.RawDataStats(24, 12, 0));
        Assert.Equal("Raw Data  (24 fields · 12 set)", noUnresolved);
    }

    // ── BuildRawData combined section ────────────────────────────────────────

    [Fact]
    public void BuildRawData_ReturnsExpanderWithStatsLabel()
    {
        TestApp.EnsureAvaloniaInitialized();
        var vis = CreateVis(new ReturningResolver(), new FormattingLoc());

        var it = new ItemType { EntityId = "it1", Weight = 1.5f };
        var section = vis.BuildRawData(it);

        // header (BuildExpander) + hidden body
        Assert.IsType<StackPanel>(section);
        var texts = FindAll<TextBlock>(section).Select(t => t.Text).Where(t => t is not null).ToList();
        Assert.Contains(texts, t => t!.Contains("Raw Data"));
    }

    // ── R38: non-entity target types (ImageAsset) ───────────────────────────

    [Fact]
    public void BuildRawDataTable_ImageAssetRefs_RenderPlainRawText_NotAmber()
    {
        TestApp.EnsureAvaloniaInitialized();
        var vis = CreateVis(new ReturningResolver()); // never resolves anything

        var it = new ItemType { EntityId = "it1" };
        it.ImageList = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "ItmStick.png" } },
        };

        var table = vis.BuildRawDataTable(it);

        // ImageAsset is NOT an IEntity — LookupRef<T> would violate its generic
        // constraint (R38 regression: ArgumentException spam + amber mislabel).
        var amberBadges = FindAll<Border>(table)
            .Where(b => (b.Background as ISolidColorBrush)?.Color == Color.Parse("#FFF8E1"))
            .ToList();
        Assert.Empty(amberBadges);

        var plainBadges = FindAll<Border>(table)
            .Where(b => (b.Background as ISolidColorBrush)?.Color == Color.Parse("#F5F5F5"))
            .ToList();
        Assert.Contains(plainBadges, b =>
            FindAll<TextBlock>(b).FirstOrDefault()?.Text == "ItmStick.png");

        // stats must not count file-name refs as unresolved
        var stats = vis.ComputeRawDataStats(it);
        Assert.Equal(0, stats.UnresolvedRefSegments);
    }
}
