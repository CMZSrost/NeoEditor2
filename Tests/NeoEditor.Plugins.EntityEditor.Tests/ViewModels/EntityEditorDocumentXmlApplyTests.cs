using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// R30 (追修 7): opening an entity must not produce spurious XML edits.
/// The XML tab's TextChanged auto-apply (debounced) fired on the programmatic
/// initial text load, and ApplyXmlToEntity compared ReferenceList values with
/// Equals() — always false (no value equality) — so every open wrote fake
/// BatchEditCommands to the WAL and marked the entity dirty (dirty-on-open).
/// </summary>
public class EntityEditorDocumentXmlApplyTests
{
    private sealed class StubDbContextFactory : IDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext()
            => throw new NotSupportedException("DB not used in XML apply tests");

        public Task<GameDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("DB not used in XML apply tests");
    }

    private static EntityEditorDocument CreateDoc(Creature creature, StubWorkspaceSession session)
    {
        var serializer = new ReferenceListSerializer();
        return new EntityEditorDocument(
            creature, session,
            new StubDbContextFactory(),
            new StubEntityLookupService(),
            new StubLocalizationService(),
            new StubNotificationService(),
            serializer);
    }

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
    public void ApplyXml_WithUnchangedGeneratedFragment_ProducesNoEdits()
    {
        // Simulates the open flow: the document generates the XML fragment from the
        // entity (constructor / OnEntityChanged), then the auto-apply runs it back.
        // Note: StubWorkspaceSession is a no-op — dirty visibility is asserted via
        // doc.IsDirty (which drives the session in production).
        var session = new StubWorkspaceSession();
        var creature = MakeCreature();
        var doc = CreateDoc(creature, session);
        Assert.Equal(EntityXmlHelper.GenerateXmlFragment(creature), doc.XmlContent.Text);

        doc.ApplyXmlToEntityCommand.Execute(null);

        // ReferenceList fields + null string columns must NOT count as diffs.
        Assert.False(doc.IsDirty);
        Assert.Equal("3,14", creature.EncounterIds.ToRawString(","));
        Assert.Equal("7", creature.AttackModes.ToRawString(","));
    }

    [Fact]
    public void ApplyXml_WithRealReferenceChange_ProducesEditAndMarksDirty()
    {
        var session = new StubWorkspaceSession();
        var creature = MakeCreature();
        var doc = CreateDoc(creature, session);

        // Real user edit: append a third encounter id to the XML.
        doc.XmlContent.Text = doc.XmlContent.Text.Replace(">3,14<", ">3,14,22<");
        doc.ApplyXmlToEntityCommand.Execute(null);

        Assert.True(doc.IsDirty);
        Assert.Equal("3,14,22", creature.EncounterIds.ToRawString(","));
    }

    [Fact]
    public void ApplyXml_NonReferenceField_Unchanged_NoEdits()
    {
        var session = new StubWorkspaceSession();
        var creature = MakeCreature();
        var doc = CreateDoc(creature, session);

        doc.ApplyXmlToEntityCommand.Execute(null);
        doc.ApplyXmlToEntityCommand.Execute(null);

        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void ApplyXml_RealStringChange_ProducesEdit()
    {
        var session = new StubWorkspaceSession();
        var creature = MakeCreature();
        var doc = CreateDoc(creature, session);

        doc.XmlContent.Text = doc.XmlContent.Text.Replace(
            "<column name=\"strName\"></column>",
            "<column name=\"strName\">Guard Dog</column>");
        doc.ApplyXmlToEntityCommand.Execute(null);

        Assert.True(doc.IsDirty);
        Assert.Equal("Guard Dog", creature.Name);
    }
}
