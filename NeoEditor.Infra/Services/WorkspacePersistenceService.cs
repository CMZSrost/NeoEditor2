using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
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

    /// <summary>Check if there are unsaved commands (sequence > snapshot) for a target.</summary>
    Task<bool> HasUnsavedCommandsAsync(string targetType, int targetId);
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

    public async Task<bool> HasUnsavedCommandsAsync(string targetType, int targetId)
    {
        var maxSeq = await GetMaxSequenceAsync(targetType, targetId);
        if (maxSeq == 0) return false;
        var snapSeq = await GetSnapshotSequenceAsync(targetType, targetId);
        return maxSeq > snapSeq;
    }
}
