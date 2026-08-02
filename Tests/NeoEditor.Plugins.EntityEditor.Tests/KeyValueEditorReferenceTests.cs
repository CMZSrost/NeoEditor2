using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// Regression: the KV value editor must (1) show reference fields as raw XML text —
/// not the damaged "[a, b]" ReferenceList.ToString() — and (2) persist reference edits
/// via the serializer (ValueConverter.ChangeType throws on ReferenceList, silently
/// dropping picker edits).
/// </summary>
public class KeyValueEditorReferenceTests
{
    private static KeyValueEditorViewModel CreateVm()
        => new(new StubWorkspaceSession(), new ReferenceListSerializer());

    private static FieldRow ReferenceFieldRow(KeyValueEditorViewModel vm, string columnName)
        => vm.Sections.SelectMany(s => s.Fields).First(f => f.PropertyName == columnName);

    [Fact]
    public void LoadEntity_ReferenceField_CurrentValueIsRawText_NotBrokenBrackets()
    {
        var vm = CreateVm();
        var creature = new Creature { EntityId = "c1" };
        creature.EncounterIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
            new PureRefFormat { Entity = new EntityRef { Id = "14" } },
        };

        vm.LoadEntityCommand.Execute(creature);

        // "3,14" not "[3, 14]" — a bracketed value would fail badge resolution.
        Assert.Equal("3,14", ReferenceFieldRow(vm, "vEncounterIDs").CurrentValue);
    }

    [Fact]
    public void LoadEntity_ReferenceField_PreservesNamespacePrefix()
    {
        var vm = CreateVm();
        var creature = new Creature { EntityId = "c1" };
        creature.AttackModes = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Namespace = "NSE", Id = "7" } },
        };

        vm.LoadEntityCommand.Execute(creature);

        Assert.Equal("NSE:7", ReferenceFieldRow(vm, "vAttackModes").CurrentValue);
    }

    [Fact]
    public void ApplyChanges_ReferenceField_PersistsPickedValueToEntity()
    {
        TestApp.EnsureAvaloniaInitialized();
        var vm = CreateVm();
        var creature = new Creature { EntityId = "c1" };
        creature.EncounterIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
        };
        vm.LoadEntityCommand.Execute(creature);

        var row = ReferenceFieldRow(vm, "vEncounterIDs");
        row.CurrentValue = "3,14"; // ReferencePicker wrote the new raw text

        vm.ApplyChangesCommand.Execute(null);

        Assert.Equal("3,14", creature.EncounterIds.ToRawString(","));
    }
}