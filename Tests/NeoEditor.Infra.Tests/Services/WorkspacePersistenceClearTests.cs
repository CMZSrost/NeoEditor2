using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Command;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>
/// R30 (追修 5): WAL must be fully cleared after a save — stale command_log rows replay on
/// restart, re-populate EditStore and re-mark entities dirty (the dirty-on-open regression).
/// Regression: QuickSaveAsync previously only advanced per-mod snapshot markers (skipping
/// ("game", 0) and ModId=0 targets), so game-data edits replayed on EVERY restart.
/// </summary>
public class WorkspacePersistenceClearTests
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

    private static AddEntityCommand NewAddEntity(string id)
        => new("Creature", new Creature { EntityId = id, ModId = 5, Name = $"C-{id}" });

    [Fact]
    public async Task PersistedCommands_ReplayAfterRestart_WhenNeverCleared()
    {
        // Baseline proving the failure mode: commands persisted to ("game", 0) with no
        // snapshot marker replay on every "restart" — the dirty-on-open source.
        var factory = CreateEditorDb();
        var persistence = new WorkspacePersistenceService(factory, null!);

        await persistence.PersistCommandAsync("game", 0, 1, NewAddEntity("g1"));
        await persistence.PersistCommandAsync("mod", 7, 2, NewAddEntity("m7a"));

        var gameCommands = await persistence.LoadCommandsAsync(
            "game", 0, (_, _) => null, () => { });
        var modCommands = await persistence.LoadCommandsAsync(
            "mod", 7, (_, _) => null, () => { });

        Assert.Equal(1, gameCommands.Count);
        Assert.Equal(1, modCommands.Count);
    }

    [Fact]
    public async Task ClearWorkspace_RemovesCommandsAndSnapshot_ForTarget()
    {
        var factory = CreateEditorDb();
        var persistence = new WorkspacePersistenceService(factory, null!);

        await persistence.PersistCommandAsync("game", 0, 1, NewAddEntity("g1"));
        await persistence.PersistCommandAsync("game", 0, 2, NewAddEntity("g2"));
        await persistence.PersistCommandAsync("mod", 7, 3, NewAddEntity("m7a"));
        await persistence.UpdateSnapshotMarkerAsync("game", 0, 2);

        await persistence.ClearWorkspaceAsync("game", 0);

        // The cleared target must be empty (nothing to replay) with no snapshot left.
        Assert.Empty(await persistence.LoadCommandsAsync("game", 0, (_, _) => null, () => { }));
        Assert.Equal(-1, await persistence.GetSnapshotSequenceAsync("game", 0));
        // Other targets are untouched.
        Assert.Single(await persistence.LoadCommandsAsync("mod", 7, (_, _) => null, () => { }));
    }

    [Fact]
    public async Task ClearWorkspace_ModZeroTarget_IsCleared()
    {
        // ModId=0 is a valid mod (e.g. NSEaid) — its WAL must be clearable too, or the
        // commands accumulate forever and replay on restart.
        var factory = CreateEditorDb();
        var persistence = new WorkspacePersistenceService(factory, null!);

        await persistence.PersistCommandAsync("mod", 0, 1, NewAddEntity("m0a"));

        await persistence.ClearWorkspaceAsync("mod", 0);

        Assert.Empty(await persistence.LoadCommandsAsync("mod", 0, (_, _) => null, () => { }));
    }
}
