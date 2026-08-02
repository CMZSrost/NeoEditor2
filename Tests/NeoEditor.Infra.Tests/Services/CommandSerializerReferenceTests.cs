using System.Collections.Generic;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Command;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>
/// R30 (A2): WAL persistence must round-trip reference edits. ReferenceList values
/// serialize as their raw text ("3,14") — JToken.FromObject on the entries array
/// cannot be restored into the IReferenceEntry interface on replay, which silently
/// rolled reference edits back after restart.
/// </summary>
public class CommandSerializerReferenceTests
{
    private static EntityMergeStore Store()
    {
        var store = new EntityMergeStore();
        var enc1 = new Encounter { Id = 3, EntityId = "enc-3", ModId = 0 };
        var enc2 = new Encounter { Id = 14, EntityId = "enc-14", ModId = 0 };
        store.ReferenceLookups[typeof(Encounter)] = new List<object> { enc1, enc2 };
        store.EntityNamespaces["enc-3"] = "0";
        store.EntityNamespaces["enc-14"] = "0";
        store.EntityMergedIds["enc-3"] = 3;
        store.EntityMergedIds["enc-14"] = 14;
        return store;
    }

    [Fact]
    public void BatchEditCommand_ReferenceEdits_RoundTripThroughWAL()
    {
        var creature = new Creature { EntityId = "c1", ModId = 0 };
        var prop = typeof(Creature).GetProperty(nameof(Creature.EncounterIds))!;
        var oldList = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
        };
        var newList = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
            new PureRefFormat { Entity = new EntityRef { Id = "14" } },
        };

        var cmd = new BatchEditCommand(
            [new EditRecord(creature, prop, "vEncounterIDs", oldList, newList)], () => { });
        var (type, data) = CommandSerializer.Serialize(cmd);

        var restored = CommandSerializer.Deserialize(type, data,
            (id, t) => id == "c1" ? creature : null, () => { }) as BatchEditCommand;

        Assert.NotNull(restored);
        Assert.Equal(1, restored!.GetAffectedEntityIds().Count);
    }

    [Fact]
    public void EditCellCommand_ReferenceValue_RoundTripsAsRawText()
    {
        var creature = new Creature { EntityId = "c1", ModId = 0 };
        var prop = typeof(Creature).GetProperty(nameof(Creature.EncounterIds))!;
        var newList = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "14" } },
        };

        var cmd = new EditCellCommand(creature, prop, "vEncounterIDs", null, newList, () => { });
        var (type, data) = CommandSerializer.Serialize(cmd);

        // The persisted payload must carry the raw text — not a JSON entries array.
        Assert.Contains("\"14\"", data);
        Assert.DoesNotContain("IReferenceEntry", data);
    }
}
