using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Context;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>
/// Docs/41: pending-export table — the persisted "edited, NOT yet exported to game XML" set.
/// Written on auto/quick save, cleared on Save &amp; Export, drives the ⚠ badge and the
/// highlight restore after restart (EditStore is session-scoped, this table is not).
/// </summary>
public class WorkspacePendingExportTests
{
    private static IDbContextFactory<EditorDbContext> CreateEditorDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<EditorDbContext>().UseSqlite(conn).Options;
        using (var db = new EditorDbContext(options))
        {
            db.Database.EnsureCreated();
        }

        return new TestDbFactory(options);
    }

    private sealed class TestDbFactory(DbContextOptions<EditorDbContext> options)
        : IDbContextFactory<EditorDbContext>
    {
        public EditorDbContext CreateDbContext() => new(options);
        public Task<EditorDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    [Fact]
    public async Task Upsert_Get_Has_Clear_RoundTrip()
    {
        var persistence = new WorkspacePersistenceService(CreateEditorDb(), null!);

        Assert.False(await persistence.HasPendingExportsAsync(7));

        // Upsert two markers for mod 7 (one new, one modified).
        await persistence.UpsertPendingExportsAsync(7,
            [("e-1", null, true), ("e-2", null, false)]);
        Assert.True(await persistence.HasPendingExportsAsync(7));
        Assert.False(await persistence.HasPendingExportsAsync(8));

        var pending = await persistence.GetPendingExportsAsync(7);
        Assert.Equal(2, pending.Count);
        Assert.Contains(("e-1", null, true), pending);
        Assert.Contains(("e-2", null, false), pending);

        // Upsert is idempotent per (modId, entityId, columnName) — same key overwrites, no dup row.
        await persistence.UpsertPendingExportsAsync(7,
            [("e-1", null, false)]); // e-1 no longer new
        pending = await persistence.GetPendingExportsAsync(7);
        Assert.Equal(2, pending.Count);
        Assert.Contains(("e-1", null, false), pending); // IsNew flipped, still one row

        // Clear only mod 7 — mod 8 markers (if any) untouched.
        await persistence.ClearPendingExportsAsync([7]);
        Assert.False(await persistence.HasPendingExportsAsync(7));
    }

    [Fact]
    public async Task PerColumnMarkers_CountDistinctEntities()
    {
        var persistence = new WorkspacePersistenceService(CreateEditorDb(), null!);

        // One entity with two edited columns → two marker rows, but one dirty entity.
        await persistence.UpsertPendingExportsAsync(7,
            [("e-1", "strName", false), ("e-1", "strDesc", false), ("e-2", "strName", true)]);
        Assert.Equal(2, await persistence.CountPendingExportsAsync(7));

        // Upserting another column of e-1 must not duplicate the existing rows.
        await persistence.UpsertPendingExportsAsync(7, [("e-1", "strDesc", false)]);
        Assert.Equal(2, await persistence.CountPendingExportsAsync(7));
        Assert.Equal(3, (await persistence.GetPendingExportsAsync(7)).Count);
    }

    [Fact]
    public async Task EmptyUpsert_DoesNothing_AndClearMultipleMods()
    {
        var persistence = new WorkspacePersistenceService(CreateEditorDb(), null!);

        await persistence.UpsertPendingExportsAsync(1, []);
        Assert.False(await persistence.HasPendingExportsAsync(1));

        await persistence.UpsertPendingExportsAsync(1, [("a", null, false)]);
        await persistence.UpsertPendingExportsAsync(2, [("b", null, true)]);
        await persistence.ClearPendingExportsAsync([1, 2, 3]); // includes a mod with no rows
        Assert.False(await persistence.HasPendingExportsAsync(1));
        Assert.False(await persistence.HasPendingExportsAsync(2));
    }

    [Fact]
    public async Task RemovePendingExportEntity_RemovesAllRowsOfEntity()
    {
        var persistence = new WorkspacePersistenceService(CreateEditorDb(), null!);
        await persistence.UpsertPendingExportsAsync(7,
            [("e-1", "strName", false), ("e-1", "strDesc", false), ("e-2", "strName", true)]);

        // Docs/41 追修: legacy-marker upgrade removes the entity's rows before re-writing
        // per-column markers — must not touch other entities or other mods.
        await persistence.RemovePendingExportEntityAsync(7, "e-1");

        var pending = await persistence.GetPendingExportsAsync(7);
        Assert.Single(pending);
        Assert.Equal(("e-2", "strName", true), pending[0]);
        Assert.Equal(1, await persistence.CountPendingExportsAsync(7));

        // Removing a non-existent entity is a no-op.
        await persistence.RemovePendingExportEntityAsync(7, "e-missing");
        Assert.Equal(1, await persistence.CountPendingExportsAsync(7));
    }
}
