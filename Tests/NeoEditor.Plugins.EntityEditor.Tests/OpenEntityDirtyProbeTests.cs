using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// PROBE (temporary): "every entity is marked dirty on open" regression hunt.
/// Simulates the open-entity flow: KeyValueEditorViewModel.LoadEntity must leave
/// every field clean and must NOT mutate the entity or the session dirty set.
/// </summary>
public class OpenEntityDirtyProbeTests
{
    private static KeyValueEditorViewModel CreateVm(StubWorkspaceSession session)
        => new(session, new ReferenceListSerializer());

    private static Creature MakeCreature()
    {
        var c = new Creature { EntityId = "c1", ModId = 0 };
        c.EncounterIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
            new PureRefFormat { Entity = new EntityRef { Id = "14" } },
        };
        c.AttackModes = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "7" } },
        };
        return c;
    }

    [Fact]
    public void LoadEntity_DoesNotMarkAnyFieldDirty()
    {
        var session = new StubWorkspaceSession();
        var vm = CreateVm(session);
        var creature = MakeCreature();

        vm.LoadEntityCommand.Execute(creature);

        var dirty = vm.Sections.SelectMany(s => s.Fields).Where(f => f.IsDirty).ToList();
        Assert.Empty(dirty);
        Assert.Empty(session.DirtyEntities);
    }

    [Fact]
    public void LoadEntity_ThenSwitchEntity_NoSpuriousEdits()
    {
        var session = new StubWorkspaceSession();
        var vm = CreateVm(session);
        vm.LoadEntityCommand.Execute(MakeCreature());

        // Switch to a different entity of the SAME type (fast-path rebuild).
        var second = MakeCreature();
        second.EntityId = "c2";
        vm.LoadEntityCommand.Execute(second);

        var dirty = vm.Sections.SelectMany(s => s.Fields).Where(f => f.IsDirty).ToList();
        Assert.Empty(dirty);
        Assert.Empty(session.DirtyEntities);
    }

    [Fact]
    public void LoadEntity_DoesNotMutateEntityReferenceValues()
    {
        var session = new StubWorkspaceSession();
        var vm = CreateVm(session);
        var creature = MakeCreature();
        var before = creature.EncounterIds.ToRawString(",");

        vm.LoadEntityCommand.Execute(creature);

        // Loading must not touch the entity's reference list entries.
        Assert.Equal(before, creature.EncounterIds.ToRawString(","));
    }
}
