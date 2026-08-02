using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.Services;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using NeoEditor.Plugins.EntityEditor.Views;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// R30: the Value Editor explains every field (embedded Docs/38 meaning + reference
/// format summary), and reference badges carry the same Ctrl navigation semantics as
/// the DataGrid (Ctrl+Click → detail, Ctrl+RMB → peek) plus a hover preview (P6).
/// </summary>
public class KeyValueEditorFieldExplanationTests
{
    private static KeyValueEditorViewModel CreateVm()
        => new(new StubWorkspaceSession(), new ReferenceListSerializer());

    private static FieldRow ReferenceFieldRow(KeyValueEditorViewModel vm, string columnName)
        => vm.Sections.SelectMany(s => s.Fields).First(f => f.PropertyName == columnName);

    [Fact]
    public void LoadEntity_AllFieldRows_HaveDescriptions()
    {
        var vm = CreateVm();
        var creature = new Creature { EntityId = "c1" };
        vm.LoadEntityCommand.Execute(creature);

        var rows = vm.Sections.SelectMany(s => s.Fields).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Description)));
    }

    [Fact]
    public void LoadEntity_ReferenceField_Description_IncludesTargetAndFormat()
    {
        var vm = CreateVm();
        var creature = new Creature { EntityId = "c1" };
        vm.LoadEntityCommand.Execute(creature);

        var row = ReferenceFieldRow(vm, "vEncounterIDs");
        // Embedded meaning (Doc 38: creatures.vEncounterIDs → Encounter) + format summary.
        Assert.Contains("Encounter", row.Description);
        Assert.Contains("引用", row.Description);
    }

    [Fact]
    public void LoadEntity_NormalField_Description_IsDoc38Meaning()
    {
        var vm = CreateVm();
        var attackMode = new AttackMode { EntityId = "a1" };
        vm.LoadEntityCommand.Execute(attackMode);

        var row = vm.Sections.SelectMany(s => s.Fields).First(f => f.PropertyName == "fDamageCut");
        Assert.Contains("切割伤害", row.Description);
    }

    [Fact]
    public void ResolveClickAction_PlainClick_DoesNothing()
    {
        Assert.Equal((false, false), ReferenceFieldEditor.ResolveClickAction(KeyModifiers.None, false));
        Assert.Equal((false, false), ReferenceFieldEditor.ResolveClickAction(KeyModifiers.None, true));
    }

    [Fact]
    public void ResolveClickAction_CtrlLeft_Navigates_CtrlRight_Peeks()
    {
        Assert.Equal((true, false), ReferenceFieldEditor.ResolveClickAction(KeyModifiers.Control, false));
        Assert.Equal((false, true), ReferenceFieldEditor.ResolveClickAction(KeyModifiers.Control, true));
    }

    [Fact]
    public void Badge_ResolvedRef_HasNavigationCursorAndHoverPreview()
    {
        TestApp.EnsureAvaloniaInitialized();

        // Stub services: lookup resolves rawId "3" → Encounter enc3.
        var target = new Encounter { EntityId = "enc3", Name = "Test Encounter" };
        var lookup = new ResolvingLookup(target);
        var router = new StubNavigationRouter();
        var services = new ServiceCollection()
            .AddSingleton<IReferenceListSerializer>(new ReferenceListSerializer())
            .AddSingleton<IEntityLookupService>(lookup)
            .AddSingleton<INavigationRouter>(router)
            .AddSingleton<VisHelperService>(new VisHelperService(
                _ => null, new StubReferenceResolver(), router, lookup, new StubLocalizationService()));
        Application.Current!.Resources["Services"] = services.BuildServiceProvider();

        var vm = CreateVm();
        var creature = new Creature { EntityId = "c1" };
        creature.EncounterIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
        };
        vm.LoadEntityCommand.Execute(creature);
        var row = ReferenceFieldRow(vm, "vEncounterIDs");

        var editor = new ReferenceFieldEditor { DataContext = row };

        var badge = editor.BadgePanel.Children.OfType<Border>().FirstOrDefault(b => b.Child is StackPanel);
        Assert.NotNull(badge);

        // C1: resolved badges advertise navigation with a hand cursor.
        Assert.NotNull(badge!.Cursor); // hand cursor = navigation wiring
        // P6: hover preview attached.
        Assert.NotNull(ToolTip.GetTip(badge));
    }

    [Fact]
    public void Badge_UnresolvedRef_HasNoNavigationCursor()
    {
        TestApp.EnsureAvaloniaInitialized();

        // Lookup resolves nothing → badge stays a plain label (no navigation wiring).
        var lookup = new StubEntityLookupService();
        var router = new StubNavigationRouter();
        var services = new ServiceCollection()
            .AddSingleton<IReferenceListSerializer>(new ReferenceListSerializer())
            .AddSingleton<IEntityLookupService>(lookup)
            .AddSingleton<INavigationRouter>(router)
            .AddSingleton<VisHelperService>(new VisHelperService(
                _ => null, new StubReferenceResolver(), router, lookup, new StubLocalizationService()));
        Application.Current!.Resources["Services"] = services.BuildServiceProvider();

        var vm = CreateVm();
        var creature = new Creature { EntityId = "c1" };
        creature.EncounterIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "999" } },
        };
        vm.LoadEntityCommand.Execute(creature);
        var row = ReferenceFieldRow(vm, "vEncounterIDs");

        var editor = new ReferenceFieldEditor { DataContext = row };

        var badge = editor.BadgePanel.Children.OfType<Border>().FirstOrDefault(b => b.Child is StackPanel);
        Assert.NotNull(badge);
        Assert.Null(badge!.Cursor); // unresolved → plain label
        Assert.Null(ToolTip.GetTip(badge));
    }

    // ── Stubs ──────────────────────────────────────────────────────────────

    private sealed class ResolvingLookup : StubEntityLookupService
    {
        private readonly IEntity _target;

        public ResolvingLookup(IEntity target) => _target = target;

        public override IEntity? FindBestMatch(System.Type entityType, string rawId, string? targetKey,
            string sourceEntityId = "", string propertyName = "")
            => rawId == "3" ? _target : null;
    }
}
