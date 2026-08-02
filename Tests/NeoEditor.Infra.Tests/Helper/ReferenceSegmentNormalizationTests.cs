using System.Collections.Generic;
using System.Linq;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Helper;

/// <summary>
/// R30: ReferenceIndex.Lookup normalizes the segment per the SOURCE field's parse
/// pattern internally ("67x0.05" → "67"), so every caller — DataGrid cells,
/// Value Editor badges, visualizer badges — resolves with identical semantics
/// whether it passes a full segment or an already-extracted id (ExtractRawId is
/// idempotent). Regression for "AttackMode conditions resolve in the DataGrid but
/// not in the visualizer".
/// </summary>
public class ReferenceSegmentNormalizationTests
{
    private static (EntityMergeStore store, ReferenceIndex index) BuildConditionStore()
    {
        var store = new EntityMergeStore();
        var conditions = new List<object>();
        foreach (var id in new[] { 67, 115, 137, 155, 211 })
        {
            var c = new Condition { Id = id, Name = $"Cond{id}", EntityId = $"cond-{id}", ModId = 0 };
            conditions.Add(c);
            store.EntityNamespaces[$"cond-{id}"] = "0";
            store.EntityMergedIds[$"cond-{id}"] = id;
        }

        store.ReferenceLookups[typeof(Condition)] = conditions;
        var index = new ReferenceIndex(store);
        index.Build();
        return (store, index);
    }

    [Fact]
    public void FullSegment_67x0_05_Resolves_LikeDataGrid()
    {
        // AttackMode.vAttackerConditions = "67x0.05" — the visualizer passes the FULL
        // segment to LookupRef; the DataGrid passes the extracted "67". Both must hit.
        var (store, index) = BuildConditionStore();
        var am = new AttackMode { EntityId = "am-1", ModId = 0 };
        store.ReferenceLookups[typeof(AttackMode)] = new List<object> { am };
        store.EntityNamespaces["am-1"] = "0";
        store.EntityMergedIds["am-1"] = 1;
        index.Build();

        Assert.Equal("cond-67", index.Lookup("am-1", nameof(AttackMode.AttackerConditions), typeof(Condition), "67x0.05"));
    }

    [Fact]
    public void NegatedSegment_Minus115x1_0_Resolves()
    {
        var (store, index) = BuildConditionStore();
        var am = new AttackMode { EntityId = "am-1", ModId = 0 };
        store.ReferenceLookups[typeof(AttackMode)] = new List<object> { am };
        store.EntityNamespaces["am-1"] = "0";
        store.EntityMergedIds["am-1"] = 1;
        index.Build();

        // "-115x1.0" = must NOT have condition 115 → resolves to the positive id.
        Assert.Equal("cond-115", index.Lookup("am-1", nameof(AttackMode.AttackerConditions), typeof(Condition), "-115x1.0"));
    }

    [Fact]
    public void AlreadyExtractedId_IsIdempotent()
    {
        var (store, index) = BuildConditionStore();
        var am = new AttackMode { EntityId = "am-1", ModId = 0 };
        store.ReferenceLookups[typeof(AttackMode)] = new List<object> { am };
        store.EntityNamespaces["am-1"] = "0";
        store.EntityMergedIds["am-1"] = 1;
        index.Build();

        // DataGrid path: ExtractRawId then Lookup — must still hit after normalization.
        Assert.Equal("cond-67", index.Lookup("am-1", nameof(AttackMode.AttackerConditions), typeof(Condition), "67"));
    }

    [Fact]
    public void BracketSegment_And_NegatedBracket_Resolve()
    {
        // BattleMove.vUsConditions = "[155,0,0]" / "[-137,0,0]" (negated = must NOT have).
        var (store, index) = BuildConditionStore();
        var bm = new BattleMove { EntityId = "bm-1", ModId = 0 };
        store.ReferenceLookups[typeof(BattleMove)] = new List<object> { bm };
        store.EntityNamespaces["bm-1"] = "0";
        store.EntityMergedIds["bm-1"] = 1;
        index.Build();

        Assert.Equal("cond-155", index.Lookup("bm-1", nameof(BattleMove.UsConditions), typeof(Condition), "[155,0,0]"));
        Assert.Equal("cond-137", index.Lookup("bm-1", nameof(BattleMove.UsConditions), typeof(Condition), "[-137,0,0]"));
    }

    [Fact]
    public void ValueEqualsId_CompositeKey_Resolves()
    {
        // ItemType.aSwitchIDs = "Hood Off=8.7" → ItemType composite key 8.7.
        var store = new EntityMergeStore();
        var item = new ItemType { Id = 100, GroupId = 8, SubgroupId = 7, EntityId = "item-8-7", ModId = 0 };
        store.ReferenceLookups[typeof(ItemType)] = new List<object> { item };
        store.EntityNamespaces["item-8-7"] = "0";
        store.EntityMergedIds["item-8-7"] = 100;

        var source = new ItemType { EntityId = "it-src", ModId = 0 };
        store.ReferenceLookups[typeof(ItemType)] = [item, source];
        store.EntityNamespaces["it-src"] = "0";
        store.EntityMergedIds["it-src"] = 101;

        var index = new ReferenceIndex(store);
        index.Build();

        Assert.Equal("item-8-7", index.Lookup("it-src", nameof(ItemType.SwitchIds), typeof(ItemType), "Hood Off=8.7"));
    }

    [Fact]
    public void IdEqualsValue_Resolves()
    {
        // ItemType.aEquipConditions = "11=19" (slot=condition) → condition 19.
        var (store, index) = BuildConditionStore();
        var it = new ItemType { EntityId = "it-src", ModId = 0 };
        store.ReferenceLookups[typeof(ItemType)] = new List<object> { it };
        store.EntityNamespaces["it-src"] = "0";
        store.EntityMergedIds["it-src"] = 101;
        index.Build();

        Assert.Equal("cond-211", index.Lookup("it-src", nameof(ItemType.EquipConditions), typeof(Condition), "11=211"));
    }

    [Fact]
    public void MultXId_Resolves()
    {
        // Recipe.strTools = "1x2" ({mult}x{id}, target Ingredient) → ingredient 2.
        var store = new EntityMergeStore();
        var ing = new Ingredient { Id = 2, EntityId = "ing-2", ModId = 0 };
        store.ReferenceLookups[typeof(Ingredient)] = new List<object> { ing };
        store.EntityNamespaces["ing-2"] = "0";
        store.EntityMergedIds["ing-2"] = 2;

        var recipe = new Recipe { EntityId = "r-1", ModId = 0 };
        store.ReferenceLookups[typeof(Recipe)] = new List<object> { recipe };
        store.EntityNamespaces["r-1"] = "0";
        store.EntityMergedIds["r-1"] = 1;

        var index = new ReferenceIndex(store);
        index.Build();

        Assert.Equal("ing-2", index.Lookup("r-1", nameof(Recipe.Tools), typeof(Ingredient), "1x2"));
    }

    [Fact]
    public void NamespacePrefixed_FullSegment_Resolves()
    {
        // "NSE:67x1.0" — namespace prefix must survive extraction.
        var store = new EntityMergeStore();
        var cond = new Condition { Id = 67, Name = "NSE Cond", EntityId = "cond-nse-67", ModId = 5 };
        store.ReferenceLookups[typeof(Condition)] = new List<object> { cond };
        store.EntityNamespaces["cond-nse-67"] = "NSE";
        store.EntityMergedIds["cond-nse-67"] = 500;

        var am = new AttackMode { EntityId = "am-1", ModId = 0 };
        store.ReferenceLookups[typeof(AttackMode)] = new List<object> { am };
        store.EntityNamespaces["am-1"] = "0";
        store.EntityMergedIds["am-1"] = 1;

        var index = new ReferenceIndex(store);
        index.Build();

        Assert.Equal("cond-nse-67", index.Lookup("am-1", nameof(AttackMode.AttackerConditions), typeof(Condition), "NSE:67x1.0"));
    }

    [Fact]
    public void GlobalLookup_WithoutSourceContext_StillWorks()
    {
        // Global lookup has no source field → no pattern; callers (LookupGlobal) pass clean ids.
        var (_, index) = BuildConditionStore();
        Assert.Equal("cond-67", index.Lookup(typeof(Condition), "67"));
    }
}
