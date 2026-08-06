using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// R24: EntityEditorDocument.SaveDocument must route through the unified HostService
/// pipeline (cache + SaveAsync) instead of writing game.db directly.
/// </summary>
public class EntityEditorDocumentSaveTests
{
    private static EntityEditorDocument CreateDoc(Creature creature, StubWorkspaceSession session,
        StubHostService host)
    {
        var serializer = new ReferenceListSerializer();
        return new EntityEditorDocument(
            creature, session, host,
            new StubEntityLookupService(),
            new StubLocalizationService(),
            new StubNotificationService(),
            serializer,
            new StubXmlParser(),
            new StubConfigService());
    }

    [Fact]
    public async Task SaveDocument_UsesHostServiceCacheAndSaveAsync_ClearsDirty()
    {
        var session = new StubWorkspaceSession();
        var host = new StubHostService();
        var creature = new Creature { EntityId = "c1", ModId = 0, Name = "Guard Dog" };
        var doc = CreateDoc(creature, session, host);

        // User made an edit: doc dirty + (in production) the shared session dirty set.
        doc.MarkDirty();
        host.MarkEntityDirty(creature.EntityId);

        await doc.SaveDocumentCommand.ExecuteAsync(null);

        // Saved through HostService: entity entered the cache and the dirty set was cleared.
        Assert.Contains(creature.EntityId, host.Cache.Keys);
        Assert.DoesNotContain(creature.EntityId, host.Dirty);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public async Task SaveDocument_NotDirty_IsNoOp()
    {
        var session = new StubWorkspaceSession();
        var host = new StubHostService();
        var creature = new Creature { EntityId = "c1", ModId = 0 };
        var doc = CreateDoc(creature, session, host);

        await doc.SaveDocumentCommand.ExecuteAsync(null);

        // Guard: no dirty state → nothing cached or saved.
        Assert.Empty(host.Cache);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public async Task SaveDocument_EntityMissingFromDirtySet_ReportsSkipped()
    {
        var session = new StubWorkspaceSession();
        var host = new StubHostService();
        var creature = new Creature { EntityId = "c1", ModId = 0 };
        var doc = CreateDoc(creature, session, host);

        // Doc thinks it is dirty but the (shared) dirty set no longer contains the entity
        // (e.g. a merge-view SaveAll missed the cache) — SaveDocument must not pretend to save:
        // it reports the skip and keeps the dirty state so the user can retry.
        doc.MarkDirty();
        host.ClearDirtyEntities();

        await doc.SaveDocumentCommand.ExecuteAsync(null);

        Assert.True(doc.IsDirty);
        Assert.Empty(host.Dirty);
    }
}
