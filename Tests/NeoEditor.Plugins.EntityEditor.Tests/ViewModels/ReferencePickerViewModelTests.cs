using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

public class ReferencePickerViewModelTests
{
    private static StubEntity E(string id, string subject, int modId = 1)
        => new(id, subject) { ModId = modId };

    private static StubEntityLookupService Lookup(params IEntity[] entities)
    {
        var lookup = new StubEntityLookupService();
        foreach (var entity in entities)
        {
            var type = entity.GetType();
            if (!lookup.ReferenceLookups.ContainsKey(type))
                lookup.ReferenceLookups[type] = [];
            lookup.ReferenceLookups[type].Add(entity);
            lookup.EntityModNames[entity.EntityId] = $"Mod{entity.ModId}";
            lookup.EntityNamespaces[entity.EntityId] = entity.ModId == 0 ? "0" : $"Mod{entity.ModId}";
        }
        return lookup;
    }

    private sealed class StubSer : IReferenceListSerializer
    {
        public ReferenceList<IReferenceEntry> Deserialize(string raw, ReferenceFieldAttribute metadata)
        {
            var result = new ReferenceList<IReferenceEntry>();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            var parts = metadata.Separator is null ? [raw] : raw.Split(metadata.Separator);
            foreach (var part in parts)
            {
                var t = part.Trim();
                if (t.Length == 0) continue;
                result.Add(t[0] == '-'
                    ? new NegatedRefFormat { Inner = new PureRefFormat { Entity = Parse(t[1..]) } }
                    : new PureRefFormat { Entity = Parse(t) });
            }
            return result;
        }

        public string Serialize(ReferenceList<IReferenceEntry> list, ReferenceFieldAttribute metadata)
            => string.Join(metadata.Separator ?? ",", list.Select(e => e.ToRawString()));

        private static EntityRef Parse(string raw)
        {
            if (raw.Contains(':')) { var p = raw.Split(':'); return new EntityRef { Namespace = p[0], Id = p[1] }; }
            return new EntityRef { Id = raw };
        }
    }

    private static ReferencePickerViewModel Vm(string raw = "", string? sep = null,
        string? pattern = null, params IEntity[] entities)
    {
        var lookup = entities.Length > 0 ? Lookup(entities) : Lookup(E("1", "TestEntity"));
        return new ReferencePickerViewModel(typeof(StubEntity), null, sep, pattern ?? "{id}", "{Id}",
            raw, lookup, new StubSer());
    }

    [Fact] public void Ctor_Empty() { var v = Vm(); Assert.Empty(v.SelectedEntries); Assert.Equal("", v.PreviewRawText); }

    [Fact] public void Ctor_SingleValue()
    {
        var v = Vm("211", null, null, E("211", "Sword"));
        Assert.Single(v.SelectedEntries);
        Assert.Contains("211", v.SelectedEntries[0].RawId);
    }

    [Fact] public void Ctor_MultiValue()
    {
        var v = Vm("211,86.6,42", ",", null, E("211", "Sword"), E("86.6", "Shield"));
        Assert.Equal(3, v.SelectedEntries.Count);
    }

    [Fact] public void Ctor_NegatedRef()
    {
        var v = new ReferencePickerViewModel(typeof(StubEntity), null, null, "{id}", "{Id}",
            "-211", Lookup(E("211", "Sword")), new StubSer());
        Assert.Single(v.SelectedEntries);
        Assert.True(v.SelectedEntries[0].IsNegated);
    }

    [Fact] public void Search_InitialFilter_HasEntities()
    {
        var v = Vm("", null, null, E("1", "Alpha"), E("2", "Beta"), E("3", "Gamma"));
        Assert.NotEmpty(v.FilteredEntities);
    }

    [Fact] public void Add_SingleValue_Replaces()
    {
        var v = Vm("old", null, null, E("1", "Ent1"), E("2", "Ent2"));
        v.SelectedEntity = v.FilteredEntities[0];
        v.AddSelectedEntityCommand.Execute(null);
        Assert.Single(v.SelectedEntries);
        Assert.Contains("1", v.SelectedEntries[0].RawId);
    }

    [Fact] public void Add_MultiValue_Appends()
    {
        var v = Vm("1", ",", null, E("1", "Ent1"), E("2", "Ent2"));
        v.SelectedEntity = v.FilteredEntities.First(e => e.EntityId == "2");
        v.AddSelectedEntityCommand.Execute(null);
        Assert.Equal(2, v.SelectedEntries.Count);
    }

    [Fact] public void Add_MultiValue_NoDup()
    {
        var v = Vm("1", ",", null, E("1", "Ent1"), E("2", "Ent2"));
        v.SelectedEntity = v.FilteredEntities.First(e => e.EntityId == "1");
        v.AddSelectedEntityCommand.Execute(null);
        Assert.Single(v.SelectedEntries);
    }

    [Fact] public void Remove_Works()
    {
        var v = Vm("1,2", ",", null, E("1", "Ent1"), E("2", "Ent2"));
        Assert.Equal(2, v.SelectedEntries.Count);
        v.RemoveEntryCommand.Execute(v.SelectedEntries[0]);
        Assert.Single(v.SelectedEntries);
    }

    [Fact] public void Confirm_SetsResult()
    {
        var v = Vm("211", null, null, E("211", "Sword"));
        v.ConfirmCommand.Execute(null);
        Assert.NotNull(v.ResultRawText);
        Assert.Contains("211", v.ResultRawText);
    }

    [Fact] public void Cancel_NullifiesResult()
    {
        var v = Vm("211", null, null, E("211", "Sword"));
        v.CancelCommand.Execute(null);
        Assert.Null(v.ResultRawText);
        Assert.Null(v.ResultReferenceList);
    }

    [Fact] public void IsMulti_False_WhenNullSep() => Assert.False(Vm("").IsMultiValue);
    [Fact] public void IsMulti_True_WhenSepSet() => Assert.True(Vm("", ",").IsMultiValue);

    [Fact] public void Multiplier_True_WhenMultPattern()
    {
        var v = new ReferencePickerViewModel(typeof(StubEntity), null, null, "{id}x{mult}", "{Id}",
            "1", Lookup(E("1", "Test")), new StubSer());
        Assert.True(v.SupportsMultiplier);
    }

    [Fact] public void Multiplier_False_WhenPlainPattern() => Assert.False(Vm("", null, "{id}").SupportsMultiplier);
    [Fact] public void Negation_AlwaysTrue() => Assert.True(Vm("").SupportsNegation);

    [Fact] public void Preview_Updates()
    {
        var v = Vm("", null, null, E("1", "Ent1"));
        v.SelectedEntity = v.FilteredEntities[0];
        v.AddSelectedEntityCommand.Execute(null);
        Assert.Contains("1", v.PreviewRawText);
    }
}
