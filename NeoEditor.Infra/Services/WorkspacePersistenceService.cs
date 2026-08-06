using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Data.Command;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Services;

public interface IWorkspacePersistenceService
{
    /// <summary>Persist a single command to command_log.</summary>
    Task PersistCommandAsync(string targetType, int targetId, int sequence, IEditorCommand command);

    /// <summary>Take a snapshot: upsert all entities to game.db + update snapshot marker.</summary>
    Task TakeSnapshotAsync(string targetType, int targetId, IReadOnlyList<IEntity> entities, int lastCommandSequence);

    /// <summary>Update only the snapshot marker without rewriting game.db (for when game.db is already current).</summary>
    Task UpdateSnapshotMarkerAsync(string targetType, int targetId, int lastCommandSequence);

    /// <summary>Load and deserialize commands for a target (since last snapshot). Returns persisted commands ready for replay.</summary>
    Task<List<(int sequence, IEditorCommand command)>> LoadCommandsAsync(
        string targetType, int targetId,
        Func<string, Type, IEntity?> entityResolver,
        Action onChanged);

    /// <summary>Get the snapshot's last covered command sequence (or -1 if no snapshot).</summary>
    Task<int> GetSnapshotSequenceAsync(string targetType, int targetId);

    /// <summary>Clear workspace: delete snapshot + all command_log entries for this target. Called on full Save & Export.</summary>
    Task ClearWorkspaceAsync(string targetType, int targetId);

    /// <summary>Get the next command sequence number for a target.</summary>
    Task<int> GetNextSequenceAsync(string targetType, int targetId);

    /// <summary>Get the maximum command sequence from command_log for a target (or 0 if none).</summary>
    Task<int> GetMaxSequenceAsync(string targetType, int targetId);

    /// <summary>Migrate legacy ("mod", modId) WAL rows (pre per-profile isolation) to a
    /// profile target: unsaved commands move over (sequences appended after the target's
    /// max), saved commands + the old snapshot are dropped (their values are already in
    /// game.db). Returns the number of moved commands.</summary>
    Task<int> MigrateWalTargetAsync(string fromType, int fromId, string toType, int toId);

    // ── Docs/41 追修(C): per-profile edit overlay (multi-profile isolation) ──

    /// <summary>All overlay rows of a profile (per-column overrides + new/deleted markers).</summary>
    Task<List<ProfileEdit>> GetProfileEditsAsync(int profileId);

    /// <summary>Replace the overlay of the given entities with new rows (save = diff baseline).</summary>
    Task ReplaceProfileEditsAsync(int profileId, IEnumerable<ProfileEdit> edits);

    /// <summary>Remove a profile's overlay rows for the given entities (after export).</summary>
    Task ClearProfileEditsAsync(int profileId, IEnumerable<string> entityIds);

    /// <summary>Check if there are unsaved commands (sequence > snapshot) for a target.</summary>
    Task<bool> HasUnsavedCommandsAsync(string targetType, int targetId);

    // ── Docs/41: pending-export set ("saved to DB, NOT yet written to game XML") ──

    /// <summary>Upsert the "edited, not yet exported" markers for a mod — one row per
    /// (EntityId, ColumnName); ColumnName NULL = entity-level marker (e.g. new rows).</summary>
    Task UpsertPendingExportsAsync(int modId, IEnumerable<(string EntityId, string? ColumnName, bool IsNew)> entities);

    /// <summary>Clear all pending-export markers for the given mods — called after Save &amp; Export commits.</summary>
    Task ClearPendingExportsAsync(IEnumerable<int> modIds);

    /// <summary>Remove ALL pending-export marker rows of one entity in a mod — used when
    /// upgrading a legacy entity-level marker to per-column markers.</summary>
    Task RemovePendingExportEntityAsync(int modId, string entityId);

    /// <summary>Read the pending-export set for a mod (restore highlights after restart).</summary>
    Task<List<(string EntityId, string? ColumnName, bool IsNew)>> GetPendingExportsAsync(int modId);

    /// <summary>Any pending-export marker for a mod? (⚠ badge source — survives restart.)</summary>
    Task<bool> HasPendingExportsAsync(int modId);

    /// <summary>Number of DISTINCT entities with pending-export markers for a mod.</summary>
    Task<int> CountPendingExportsAsync(int modId);

    /// <summary>Entity ids touched by un-saved WAL commands (crash window since last snapshot).</summary>
    Task<IReadOnlyCollection<string>> GetUnsavedEntityIdsAsync(string targetType, int targetId);

    /// <summary>Distinct entity ids with un-exported changes for a mod: pending-export
    /// markers (survive restart) ∪ WAL-window edits. Game data (ModId=-1) is additionally
    /// checked against the merge-editor ("game", 0) target.</summary>
    Task<ISet<string>> GetDirtyEntityIdsAsync(int modId);
}

public class WorkspacePersistenceService : IWorkspacePersistenceService
{
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;

    public WorkspacePersistenceService(
        IDbContextFactory<EditorDbContext> editorDbFactory,
        IDbContextFactory<GameDbContext> gameDbFactory)
    {
        _editorDbFactory = editorDbFactory;
        _gameDbFactory = gameDbFactory;
    }

    public async Task PersistCommandAsync(string targetType, int targetId, int sequence, IEditorCommand command)
    {
        var (commandType, serializedData) = CommandSerializer.Serialize(command);

        System.Diagnostics.Debug.WriteLine(
            $"[WAL-DB] PersistCommand: {targetType}:{targetId} seq={sequence} type={commandType} dataLen={serializedData.Length}");

        var cmdLog = new CommandLog
        {
            TargetType = targetType,
            TargetId = targetId,
            Sequence = sequence,
            CommandType = commandType,
            SerializedData = serializedData,
            IsUnsaved = true,
            CreatedAt = DateTime.Now
        };

        await using var db = await _editorDbFactory.CreateDbContextAsync();
        db.CommandLogs.Add(cmdLog);
        await db.SaveChangesAsync();

        System.Diagnostics.Debug.WriteLine(
            $"[WAL-DB] PersistCommand DONE: {targetType}:{targetId} seq={sequence}");
    }

    public async Task TakeSnapshotAsync(string targetType, int targetId, IReadOnlyList<IEntity> entities, int lastCommandSequence)
    {
        // Write all entities to game.db
        await using var gameDb = await _gameDbFactory.CreateDbContextAsync();
        var grouped = entities.GroupBy(e => e.GetType());
        foreach (var group in grouped)
        {
            var listType = typeof(List<>).MakeGenericType(group.Key);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            foreach (var entity in group)
                list.Add(entity);
            await gameDb.DbBulkInsertOrUpdate(group.Key, list);
        }
        await gameDb.SaveChangesAsync();

        // Update snapshot marker
        await using var editorDb = await _editorDbFactory.CreateDbContextAsync();
        var existing = await editorDb.WorkspaceSnapshots
            .FirstOrDefaultAsync(s => s.TargetType == targetType && s.TargetId == targetId);
        if (existing != null)
        {
            existing.LastCommandSequence = lastCommandSequence;
            existing.CreatedAt = DateTime.Now;
        }
        else
        {
            editorDb.WorkspaceSnapshots.Add(new WorkspaceSnapshot
            {
                TargetType = targetType,
                TargetId = targetId,
                LastCommandSequence = lastCommandSequence,
                CreatedAt = DateTime.Now
            });
        }
        await editorDb.SaveChangesAsync();
    }

    public async Task UpdateSnapshotMarkerAsync(string targetType, int targetId, int lastCommandSequence)
    {
        await using var editorDb = await _editorDbFactory.CreateDbContextAsync();
        var existing = await editorDb.WorkspaceSnapshots
            .FirstOrDefaultAsync(s => s.TargetType == targetType && s.TargetId == targetId);
        if (existing != null)
        {
            existing.LastCommandSequence = lastCommandSequence;
            existing.CreatedAt = DateTime.Now;
        }
        else
        {
            editorDb.WorkspaceSnapshots.Add(new WorkspaceSnapshot
            {
                TargetType = targetType,
                TargetId = targetId,
                LastCommandSequence = lastCommandSequence,
                CreatedAt = DateTime.Now
            });
        }
        await editorDb.SaveChangesAsync();
    }

    public async Task<List<(int sequence, IEditorCommand command)>> LoadCommandsAsync(
        string targetType, int targetId,
        Func<string, Type, IEntity?> entityResolver,
        Action onChanged)
    {
        var snapshotSeq = await GetSnapshotSequenceAsync(targetType, targetId);

        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var logs = await db.CommandLogs
            .Where(c => c.TargetType == targetType && c.TargetId == targetId && c.Sequence > snapshotSeq)
            .OrderBy(c => c.Sequence)
            .ToListAsync();

        System.Diagnostics.Debug.WriteLine(
            $"[WAL-DB] LoadCommands: {targetType}:{targetId}, snapshotSeq={snapshotSeq}, found {logs.Count} rows in command_log");

        var results = new List<(int sequence, IEditorCommand command)>();
        foreach (var log in logs)
        {
            try
            {
                var cmd = CommandSerializer.Deserialize(log.CommandType, log.SerializedData, entityResolver, onChanged);
                results.Add((log.Sequence, cmd));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LoadCommands] skip seq={log.Sequence} type={log.CommandType}: {ex.Message}");
            }
        }
        return results;
    }

    public async Task<int> GetSnapshotSequenceAsync(string targetType, int targetId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var snapshot = await db.WorkspaceSnapshots
            .FirstOrDefaultAsync(s => s.TargetType == targetType && s.TargetId == targetId);
        return snapshot?.LastCommandSequence ?? -1;
    }

    public async Task ClearWorkspaceAsync(string targetType, int targetId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();

        var snapshot = await db.WorkspaceSnapshots
            .FirstOrDefaultAsync(s => s.TargetType == targetType && s.TargetId == targetId);
        if (snapshot != null)
            db.WorkspaceSnapshots.Remove(snapshot);

        var commands = await db.CommandLogs
            .Where(c => c.TargetType == targetType && c.TargetId == targetId)
            .ToListAsync();
        db.CommandLogs.RemoveRange(commands);

        await db.SaveChangesAsync();
    }

    public async Task<int> GetNextSequenceAsync(string targetType, int targetId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var maxSeq = await db.CommandLogs
            .Where(c => c.TargetType == targetType && c.TargetId == targetId)
            .MaxAsync(c => (int?)c.Sequence);
        return (maxSeq ?? 0) + 1;
    }

    public async Task<int> GetMaxSequenceAsync(string targetType, int targetId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var maxSeq = await db.CommandLogs
            .Where(c => c.TargetType == targetType && c.TargetId == targetId)
            .MaxAsync(c => (int?)c.Sequence);
        return maxSeq ?? 0;
    }

    public async Task<int> MigrateWalTargetAsync(string fromType, int fromId, string toType, int toId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var snapSeq = await db.WorkspaceSnapshots
            .Where(s => s.TargetType == fromType && s.TargetId == fromId)
            .Select(s => (int?)s.LastCommandSequence)
            .FirstOrDefaultAsync() ?? -1;

        // Unsaved commands (after the old snapshot) carry this profile's pending edits —
        // move them to the profile target, sequences appended after its current max.
        var rows = await db.CommandLogs
            .Where(c => c.TargetType == fromType && c.TargetId == fromId && c.Sequence > snapSeq)
            .ToListAsync();
        if (rows.Count > 0)
        {
            var nextSeq = (await db.CommandLogs
                .Where(c => c.TargetType == toType && c.TargetId == toId)
                .MaxAsync(c => (int?)c.Sequence) ?? 0) + 1;
            foreach (var row in rows)
            {
                row.TargetType = toType;
                row.TargetId = toId;
                row.Sequence = nextSeq++;
            }
        }

        // Saved commands + the old snapshot are already reflected in game.db — drop them so
        // the migrated commands replay cleanly (and the old target never resurrects them).
        var snap = await db.WorkspaceSnapshots
            .FirstOrDefaultAsync(s => s.TargetType == fromType && s.TargetId == fromId);
        if (snap != null) db.WorkspaceSnapshots.Remove(snap);
        var saved = await db.CommandLogs
            .Where(c => c.TargetType == fromType && c.TargetId == fromId && c.Sequence <= snapSeq)
            .ToListAsync();
        if (saved.Count > 0) db.CommandLogs.RemoveRange(saved);

        if (rows.Count > 0 || snap != null || saved.Count > 0)
            await db.SaveChangesAsync();
        return rows.Count;
    }

    public async Task<bool> HasUnsavedCommandsAsync(string targetType, int targetId)
    {
        var maxSeq = await GetMaxSequenceAsync(targetType, targetId);
        if (maxSeq == 0) return false;
        var snapSeq = await GetSnapshotSequenceAsync(targetType, targetId);
        return maxSeq > snapSeq;
    }

    // ── Docs/41: pending-export set ──────────────────────────────────────

    public async Task UpsertPendingExportsAsync(int modId,
        IEnumerable<(string EntityId, string? ColumnName, bool IsNew)> entities)
    {
        var list = entities.ToList();
        if (list.Count == 0) return;

        await using var db = await _editorDbFactory.CreateDbContextAsync();
        foreach (var (entityId, columnName, isNew) in list)
        {
            var existing = await db.PendingExports
                .FirstOrDefaultAsync(p => p.ModId == modId && p.EntityId == entityId
                                          && p.ColumnName == columnName);
            if (existing is null)
            {
                db.PendingExports.Add(new PendingExport
                {
                    ModId = modId,
                    EntityId = entityId,
                    ColumnName = columnName,
                    IsNew = isNew,
                    UpdatedAt = DateTime.Now,
                });
            }
            else
            {
                existing.IsNew = isNew;
                existing.UpdatedAt = DateTime.Now;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task ClearPendingExportsAsync(IEnumerable<int> modIds)
    {
        var ids = modIds.Distinct().ToList();
        if (ids.Count == 0) return;

        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var rows = await db.PendingExports.Where(p => ids.Contains(p.ModId)).ToListAsync();
        if (rows.Count > 0)
        {
            db.PendingExports.RemoveRange(rows);
            await db.SaveChangesAsync();
        }
    }

    public async Task RemovePendingExportEntityAsync(int modId, string entityId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var rows = await db.PendingExports
            .Where(p => p.ModId == modId && p.EntityId == entityId)
            .ToListAsync();
        if (rows.Count == 0) return;
        db.PendingExports.RemoveRange(rows);
        await db.SaveChangesAsync();
    }

    public async Task<List<(string EntityId, string? ColumnName, bool IsNew)>> GetPendingExportsAsync(int modId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        return await db.PendingExports
            .Where(p => p.ModId == modId)
            .OrderBy(p => p.EntityId)
            .Select(p => new ValueTuple<string, string?, bool>(p.EntityId, p.ColumnName, p.IsNew))
            .ToListAsync();
    }

    public async Task<bool> HasPendingExportsAsync(int modId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        return await db.PendingExports.AnyAsync(p => p.ModId == modId);
    }

    public async Task<int> CountPendingExportsAsync(int modId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        return await db.PendingExports
            .Where(p => p.ModId == modId)
            .Select(p => p.EntityId)
            .Distinct()
            .CountAsync();
    }

    public async Task<IReadOnlyCollection<string>> GetUnsavedEntityIdsAsync(string targetType, int targetId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var snapshotSeq = await db.WorkspaceSnapshots
            .Where(s => s.TargetType == targetType && s.TargetId == targetId)
            .Select(s => (int?)s.LastCommandSequence)
            .FirstOrDefaultAsync() ?? -1;

        var logs = await db.CommandLogs
            .Where(c => c.TargetType == targetType && c.TargetId == targetId && c.Sequence > snapshotSeq)
            .ToListAsync();

        var ids = new HashSet<string>();
        foreach (var log in logs)
        {
            try
            {
                var cmd = CommandSerializer.Deserialize(log.CommandType, log.SerializedData,
                    CreateStubEntity, () => { });
                ids.UnionWith(cmd.GetAffectedEntityIds());
            }
            catch
            {
                // Skip unparseable commands — same tolerance as WAL replay.
            }
        }
        return ids;
    }

    public async Task<ISet<string>> GetDirtyEntityIdsAsync(int modId)
    {
        var ids = new HashSet<string>((await GetPendingExportsAsync(modId)).Select(p => p.EntityId));
        ids.UnionWith(await GetUnsavedEntityIdsAsync("mod", modId));
        if (modId == -1)
            ids.UnionWith(await GetUnsavedEntityIdsAsync("game", 0));
        return ids;
    }

    /// <summary>Entity stub for counting — GetAffectedEntityIds only needs the id.</summary>
    private static IEntity CreateStubEntity(string entityId, Type type)
    {
        var entity = (IEntity)Activator.CreateInstance(type)!;
        entity.EntityId = entityId;
        return entity;
    }

    // ── Docs/41 追修(C): per-profile edit overlay ─────────────────────────

    public async Task<List<ProfileEdit>> GetProfileEditsAsync(int profileId)
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        return await db.ProfileEdits
            .Where(p => p.ProfileId == profileId)
            .OrderBy(p => p.EntityId).ThenBy(p => p.ColumnName)
            .ToListAsync();
    }

    public async Task ReplaceProfileEditsAsync(int profileId, IEnumerable<ProfileEdit> edits)
    {
        var list = edits.ToList();
        if (list.Count == 0) return;

        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var entityIds = list.Select(e => e.EntityId).Distinct().ToList();
        var existing = await db.ProfileEdits
            .Where(p => p.ProfileId == profileId && entityIds.Contains(p.EntityId))
            .ToListAsync();
        if (existing.Count > 0)
            db.ProfileEdits.RemoveRange(existing);

        foreach (var edit in list)
        {
            edit.Id = 0; // insert as new rows
            edit.ProfileId = profileId;
            edit.UpdatedAt = DateTime.Now;
            db.ProfileEdits.Add(edit);
        }

        await db.SaveChangesAsync();
    }

    public async Task ClearProfileEditsAsync(int profileId, IEnumerable<string> entityIds)
    {
        var ids = entityIds.Distinct().ToList();
        if (ids.Count == 0) return;

        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var rows = await db.ProfileEdits
            .Where(p => p.ProfileId == profileId && ids.Contains(p.EntityId))
            .ToListAsync();
        if (rows.Count == 0) return;
        db.ProfileEdits.RemoveRange(rows);
        await db.SaveChangesAsync();
    }
}
