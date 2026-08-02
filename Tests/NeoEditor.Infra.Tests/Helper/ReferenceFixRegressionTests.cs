using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.Infra.Tests.Helper;

/// <summary>
/// Regressions for Doc 37 "与代码现状的偏差": composite keys like "86.6" must resolve,
/// and the reverse index must not mangle raw refs.
/// </summary>
public class ReferenceFixRegressionTests
{
    [Fact]
    public void ReferenceIndex_composite_86_6_resolves()
    {
        var store = new EntityMergeStore();
        var item = new ItemType { Id = 100, GroupId = 86, SubgroupId = 6, ModId = 0, EntityId = "item-86-6" };
        store.ReferenceLookups[typeof(ItemType)] = new List<object> { item };
        store.EntityNamespaces["item-86-6"] = "0";
        store.EntityMergedIds["item-86-6"] = 100;

        var index = new ReferenceIndex(store);
        index.Build();

        Assert.Equal("item-86-6", index.Lookup("", "", typeof(ItemType), "86.6"));
        Assert.Equal("item-86-6", index.Lookup("", "", typeof(ItemType), "0:86.6"));
    }

    [Fact]
    public void ReferenceIndex_buildreverse_preserves_reference_value()
    {
        var store = new EntityMergeStore();
        var enc = new Encounter { Id = 1328, EntityId = "enc-1328", ModId = 0 };
        store.ReferenceLookups[typeof(Encounter)] = new List<object> { enc };
        store.EntityNamespaces["enc-1328"] = "0";
        store.EntityMergedIds["enc-1328"] = 1328;

        var creature = new Creature { Id = 1, EntityId = "cre-1", ModId = 0 };
        creature.EncounterIds.Add(new PureRefFormat { Entity = new EntityRef { Id = "1328" } });
        store.ReferenceLookups[typeof(Creature)] = new List<object> { creature };
        store.EntityNamespaces["cre-1"] = "0";
        store.EntityMergedIds["cre-1"] = 1;

        var index = new ReferenceIndex(store);
        index.Build();

        var rev = index.ReverseLookup("enc-1328");
        Assert.Contains(rev, r => r.SourceEntityId == "cre-1" && r.RawId == "1328");
    }
}
