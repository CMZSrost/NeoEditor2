using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.Services;
using NeoEditor.Plugins.EntityEditor.Visualizers;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// R30 regression: visualizer reference badges must receive the CLEAN raw id
/// ("123"), exactly like the DataGrid cells do via ReferenceText.GetRawString —
/// not the damaged "[123]" ReferenceList.ToString() format. A bracketed id makes
/// ReferenceIndex.Lookup miss while the DataTable resolves fine.
/// </summary>
public class VisualizerReferenceConsistencyTests
{
    private static RecordingReferenceResolver CreateResolver() => new();

    private static VisHelperService CreateVis(RecordingReferenceResolver resolver)
        => new(_ => null, resolver, new StubNavigationRouter(), new StubEntityLookupService(),
            new StubLocalizationService());

    [Fact]
    public void EncounterTrigger_EncounterIdBadge_ReceivesCleanRawId()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = CreateResolver();
        var vis = CreateVis(resolver);
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new EncounterTriggerEntityVisualizer(vis, refNode);

        var et = new EncounterTrigger { EntityId = "et1" };
        et.EncounterId = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "123" } },
        };

        visualizer.BuildDetail(et);

        Assert.Contains("123", resolver.CapturedRawIds);
        Assert.DoesNotContain("[123]", resolver.CapturedRawIds);
    }

    [Fact]
    public void EncounterTrigger_HexTypesBadges_ReceiveCleanSegmentIds()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = CreateResolver();
        var vis = CreateVis(resolver);
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new EncounterTriggerEntityVisualizer(vis, refNode);

        var et = new EncounterTrigger { EntityId = "et1" };
        et.HexTypes = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "5" } },
            new PureRefFormat { Entity = new EntityRef { Id = "9" } },
        };

        visualizer.BuildDetail(et);

        Assert.Contains("5", resolver.CapturedRawIds);
        Assert.Contains("9", resolver.CapturedRawIds);
        Assert.DoesNotContain("[5, 9]", resolver.CapturedRawIds);
    }

    [Fact]
    public void DmcPlace_EncounterIdBadge_ReceivesCleanRawId()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = CreateResolver();
        var vis = CreateVis(resolver);
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new DmcPlaceEntityVisualizer(vis, refNode);

        var dp = new DmcPlace { EntityId = "dp1" };
        dp.EncounterId = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "77" } },
        };

        visualizer.BuildDetail(dp);

        Assert.Contains("77", resolver.CapturedRawIds);
        Assert.DoesNotContain("[77]", resolver.CapturedRawIds);
    }

    [Fact]
    public void HexType_DefaultCampIdBadge_ReceivesCleanRawId()
    {
        TestApp.EnsureAvaloniaInitialized();
        var resolver = CreateResolver();
        var vis = CreateVis(resolver);
        var refNode = new RefNode(resolver, new StubNavigationRouter());
        var visualizer = new HexTypeEntityVisualizer(vis, refNode);

        var ht = new HexType { EntityId = "ht1" };
        ht.DefaultCampId = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "42" } },
        };

        visualizer.BuildDetail(ht);

        Assert.Contains("42", resolver.CapturedRawIds);
        Assert.DoesNotContain("[42]", resolver.CapturedRawIds);
    }

    // ── Recording resolver ─────────────────────────────────────────────────

    private sealed class RecordingReferenceResolver : StubReferenceResolver
    {
        public List<string> CapturedRawIds { get; } = new();

        public override T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : class
        {
            CapturedRawIds.Add(rawId);
            return null;
        }
    }
}
