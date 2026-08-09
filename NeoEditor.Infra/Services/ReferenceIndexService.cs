using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NeoEditor.Diagnostics;
using Serilog;

namespace NeoEditor.Services;

/// <summary>
/// SQLite-backed reference index service.
/// Replaces the in-memory 4-dictionary ReferenceIndex with a single reference_index table.
///
/// Two modes:
///   - File-based (index.db alongside game.db) — global browser index, persists across sessions
///   - In-memory (:memory:) — per-merge-view index, created on first open, destroyed on close
///
/// Table schema:
///   reference_index(
///     entity_type  TEXT NOT NULL,
///     namespace    TEXT NOT NULL,
///     pk           TEXT NOT NULL,
///     entity_id    TEXT NOT NULL,
///     group_id     INTEGER,
///     subgroup_id  INTEGER,
///     PRIMARY KEY (entity_type, namespace, pk)
///   )
///
/// Build flow:
///   1. Load modId=-1 Game data (ns="0") → INSERT
///   2. Load mods in order → INSERT OR REPLACE (same ns+pk = override)
///
/// Query rules:
///   - Has namespace prefix → SELECT FROM reference_index
///   - No namespace prefix → caller queries mod data directly (game.db or in-memory)
/// </summary>
public sealed class ReferenceIndexService : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _tableEnsured;
    private bool _disposed;

    public ReferenceIndexService(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
    }

    /// <summary>Create a file-based index.</summary>
    public static ReferenceIndexService CreateFileBased(string dbPath)
        => new($"Data Source={dbPath}");

    /// <summary>Create an in-memory index (per merge-view).</summary>
    public static ReferenceIndexService CreateInMemory()
        => new("Data Source=:memory:");

    // ═══════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    public async Task OpenAsync()
    {
        await _connection.OpenAsync();
        await EnsureTableAsync();
    }

    public void Open()
    {
        _connection.Open();
        EnsureTable();
    }

    public void Close()
    {
        _connection.Close();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Schema
    // ═══════════════════════════════════════════════════════════════════════

    private void EnsureTable()
    {
        if (_tableEnsured) return;
        _tableEnsured = true;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS reference_index (
                entity_type  TEXT NOT NULL,
                namespace    TEXT NOT NULL,
                pk           TEXT NOT NULL,
                entity_id    TEXT NOT NULL,
                group_id     INTEGER,
                subgroup_id  INTEGER,
                PRIMARY KEY (entity_type, namespace, pk)
            );
            """;
        cmd.ExecuteNonQuery();

        // Lookup index
        using var cmd2 = _connection.CreateCommand();
        cmd2.CommandText = """
            CREATE INDEX IF NOT EXISTS idx_reference_index_lookup
                ON reference_index(entity_type, namespace, pk);
            """;
        cmd2.ExecuteNonQuery();

        // Composite key index for ItemType (GroupId, SubgroupId)
        using var cmd3 = _connection.CreateCommand();
        cmd3.CommandText = """
            CREATE INDEX IF NOT EXISTS idx_reference_index_composite
                ON reference_index(entity_type, namespace, group_id, subgroup_id)
                WHERE group_id IS NOT NULL AND subgroup_id IS NOT NULL;
            """;
        cmd3.ExecuteNonQuery();

        // Reverse index table
        using var cmd4 = _connection.CreateCommand();
        cmd4.CommandText = """
            CREATE TABLE IF NOT EXISTS reference_reverse (
                target_entity_id TEXT NOT NULL,
                source_entity_id TEXT NOT NULL,
                property_name   TEXT NOT NULL,
                raw_id          TEXT NOT NULL,
                PRIMARY KEY (target_entity_id, source_entity_id, property_name, raw_id)
            );
            """;
        cmd4.ExecuteNonQuery();

        using var cmd5 = _connection.CreateCommand();
        cmd5.CommandText = """
            CREATE INDEX IF NOT EXISTS idx_reference_reverse_target
                ON reference_reverse(target_entity_id);
            """;
        cmd5.ExecuteNonQuery();

        // Migrate: rename legacy subgraph_id → subgroup_id (idempotent)
        try
        {
            using var migrateCmd = _connection.CreateCommand();
            migrateCmd.CommandText =
                "ALTER TABLE reference_index RENAME COLUMN subgraph_id TO subgroup_id;";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* column already renamed or doesn't exist */ }
    }

    private async Task EnsureTableAsync()
    {
        if (_tableEnsured) return;
        _tableEnsured = true;

        var commands = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS reference_index (
                entity_type  TEXT NOT NULL,
                namespace    TEXT NOT NULL,
                pk           TEXT NOT NULL,
                entity_id    TEXT NOT NULL,
                group_id     INTEGER,
                subgroup_id  INTEGER,
                PRIMARY KEY (entity_type, namespace, pk)
            );
            """,
            "CREATE INDEX IF NOT EXISTS idx_reference_index_lookup ON reference_index(entity_type, namespace, pk);",
            """
            CREATE INDEX IF NOT EXISTS idx_reference_index_composite
                ON reference_index(entity_type, namespace, group_id, subgroup_id)
                WHERE group_id IS NOT NULL AND subgroup_id IS NOT NULL;
            """,
            """
            CREATE TABLE IF NOT EXISTS reference_reverse (
                target_entity_id TEXT NOT NULL,
                source_entity_id TEXT NOT NULL,
                property_name   TEXT NOT NULL,
                raw_id          TEXT NOT NULL,
                PRIMARY KEY (target_entity_id, source_entity_id, property_name, raw_id)
            );
            """,
            "CREATE INDEX IF NOT EXISTS idx_reference_reverse_target ON reference_reverse(target_entity_id);"
        };

        foreach (var sql in commands)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        // Migrate: rename legacy subgraph_id → subgroup_id (idempotent)
        try
        {
            using var migrateCmd = _connection.CreateCommand();
            migrateCmd.CommandText =
                "ALTER TABLE reference_index RENAME COLUMN subgraph_id TO subgroup_id;";
            await migrateCmd.ExecuteNonQueryAsync();
        }
        catch { /* column already renamed or doesn't exist */ }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Build
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Represents a single entity entry for index building.</summary>
    public readonly record struct IndexEntry(
        string EntityType,
        string Namespace,
        string Pk,
        string EntityId,
        int? GroupId = null,
        int? SubgroupId = null
    );

    /// <summary>Clear and rebuild the index from entries.</summary>
    public async Task BuildAsync(IReadOnlyList<IndexEntry> entries)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Perf: reference_index is a rebuildable cache table — disable fsync for the
        // rebuild (worst case: cache lost on crash, rebuilt on next open), then restore.
        var prevSync = await GetSynchronousAsync();
        await SetSynchronousAsync("OFF");
        try
        {
            using var tx = _connection.BeginTransaction();

            // Clear existing
            using (var delCmd = _connection.CreateCommand())
            {
                delCmd.CommandText = "DELETE FROM reference_index;";
                await delCmd.ExecuteNonQueryAsync();
            }

            // Bulk insert
            await InsertBatchAsync(entries);

            await tx.CommitAsync();
        }
        finally
        {
            await SetSynchronousAsync(prevSync);
        }
        sw.Stop();
        Log.Logger.Information(
            "[RefIndex] BuildAsync complete: {Count} entries in {Ms}ms",
            entries.Count, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Insert entries in batches. Uses INSERT OR REPLACE so later entries
    /// with the same (entity_type, namespace, pk) override earlier ones.
    /// Batches 500 rows per INSERT to minimise SQLite P/Invoke overhead.
    /// Should be called inside a transaction for performance.
    /// Values are written as string literals (see BuildIndexLiteralSql) — parameter
    /// binding (AddWithValue × 6/row) was the dominant cost of the ~0.6s build.
    /// </summary>
    private const int BatchSize = 500;

    public async Task InsertBatchAsync(IReadOnlyList<IndexEntry> entries)
    {
        for (var offset = 0; offset < entries.Count; offset += BatchSize)
        {
            var count = Math.Min(BatchSize, entries.Count - offset);
            await InsertBatchChunkAsync(entries, offset, count);
        }
    }

    private async Task InsertBatchChunkAsync(IReadOnlyList<IndexEntry> entries, int offset, int count)
    {
        var sql = BuildIndexLiteralSql(entries, offset, count);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Build INSERT OR REPLACE for reference_index with literal (non-parameterized)
    /// values. All values are app-internal (sha256 entity ids, C# type/namespace names, parsed
    /// keys) — only single quotes are escaped. Avoids AddWithValue type-inference per cell.</summary>
    private static string BuildIndexLiteralSql(IReadOnlyList<IndexEntry> entries, int offset, int count)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("INSERT OR REPLACE INTO reference_index (entity_type, namespace, pk, entity_id, group_id, subgroup_id) VALUES ");
        for (var r = 0; r < count; r++)
        {
            var e = entries[offset + r];
            if (r > 0) sb.Append(", ");
            sb.Append("('").Append(EscapeSql(e.EntityType)).Append("','")
              .Append(EscapeSql(e.Namespace)).Append("','")
              .Append(EscapeSql(e.Pk)).Append("','")
              .Append(EscapeSql(e.EntityId)).Append("',")
              .Append(e.GroupId.HasValue ? e.GroupId.Value.ToString() : "NULL").Append(',')
              .Append(e.SubgroupId.HasValue ? e.SubgroupId.Value.ToString() : "NULL")
              .Append(')');
        }
        sb.Append(';');
        return sb.ToString();
    }

    /// <summary>Escape a string for a SQLite single-quoted literal.</summary>
    private static string EscapeSql(string s) => s.Replace("'", "''");

    /// <summary>Build INSERT OR REPLACE for reference_reverse with literal (non-parameterized)
    /// values — all app-internal strings (sha256 ids, C# property names, parsed raw segments),
    /// only single quotes are escaped. Parameter binding × 4/row was the ~2s hot spot.</summary>
    private static string BuildReverseLiteralSql(
        IReadOnlyList<(string TargetEntityId, string SourceEntityId, string PropertyName, string RawId)> entries,
        int offset, int count)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("INSERT OR REPLACE INTO reference_reverse (target_entity_id, source_entity_id, property_name, raw_id) VALUES ");
        for (var r = 0; r < count; r++)
        {
            var (target, source, prop, raw) = entries[offset + r];
            if (r > 0) sb.Append(", ");
            sb.Append("('").Append(EscapeSql(target)).Append("','")
              .Append(EscapeSql(source)).Append("','")
              .Append(EscapeSql(prop)).Append("','")
              .Append(EscapeSql(raw)).Append("')");
        }
        sb.Append(';');
        return sb.ToString();
    }

    /// <summary>Sync version of InsertBatchAsync.</summary>
    public void InsertBatch(IReadOnlyList<IndexEntry> entries)
    {
        for (var offset = 0; offset < entries.Count; offset += BatchSize)
        {
            var count = Math.Min(BatchSize, entries.Count - offset);
            InsertBatchChunk(entries, offset, count);
        }
    }

    private void InsertBatchChunk(IReadOnlyList<IndexEntry> entries, int offset, int count)
    {
        var sql = BuildIndexLiteralSql(entries, offset, count);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Lookup
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lookup by (entity_type, namespace, pk) — for namespace-prefixed references like "0:38".
    /// Returns the merged EntityId (highest priority mod wins due to INSERT OR REPLACE order).
    /// </summary>
    public async Task<string?> LookupByNsAsync(string entityType, string ns, string pk)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT entity_id FROM reference_index
            WHERE entity_type = @etype AND namespace = @ns AND pk = @pk
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@etype", entityType);
        cmd.Parameters.AddWithValue("@ns", ns);
        cmd.Parameters.AddWithValue("@pk", pk);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    /// <summary>Sync version of LookupByNs.</summary>
    public string? LookupByNs(string entityType, string ns, string pk)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT entity_id FROM reference_index
            WHERE entity_type = @etype AND namespace = @ns AND pk = @pk
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@etype", entityType);
        cmd.Parameters.AddWithValue("@ns", ns);
        cmd.Parameters.AddWithValue("@pk", pk);

        return cmd.ExecuteScalar() as string;
    }

    /// <summary>
    /// Lookup by composite key (entity_type, namespace, group_id, subgroup_id).
    /// For namespace-prefixed ItemType references like "0:90.3".
    /// </summary>
    public async Task<string?> LookupByNsCompositeAsync(string entityType, string ns, int groupId, int subgroupId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT entity_id FROM reference_index
            WHERE entity_type = @etype AND namespace = @ns
              AND group_id = @gid AND subgroup_id = @sid
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@etype", entityType);
        cmd.Parameters.AddWithValue("@ns", ns);
        cmd.Parameters.AddWithValue("@gid", groupId);
        cmd.Parameters.AddWithValue("@sid", subgroupId);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    /// <summary>Sync version of LookupByNsComposite.</summary>
    public string? LookupByNsComposite(string entityType, string ns, int groupId, int subgroupId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT entity_id FROM reference_index
            WHERE entity_type = @etype AND namespace = @ns
              AND group_id = @gid AND subgroup_id = @sid
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@etype", entityType);
        cmd.Parameters.AddWithValue("@ns", ns);
        cmd.Parameters.AddWithValue("@gid", groupId);
        cmd.Parameters.AddWithValue("@sid", subgroupId);

        return cmd.ExecuteScalar() as string;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Reverse index
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Add a reverse reference entry.</summary>
    public async Task AddReverseAsync(string targetEntityId, string sourceEntityId,
        string propertyName, string rawId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO reference_reverse
                (target_entity_id, source_entity_id, property_name, raw_id)
            VALUES (@target, @source, @prop, @raw);
            """;
        cmd.Parameters.AddWithValue("@target", targetEntityId);
        cmd.Parameters.AddWithValue("@source", sourceEntityId);
        cmd.Parameters.AddWithValue("@prop", propertyName);
        cmd.Parameters.AddWithValue("@raw", rawId);
        await cmd.ExecuteNonQueryAsync();
    }

    public void AddReverse(string targetEntityId, string sourceEntityId,
        string propertyName, string rawId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO reference_reverse
                (target_entity_id, source_entity_id, property_name, raw_id)
            VALUES (@target, @source, @prop, @raw);
            """;
        cmd.Parameters.AddWithValue("@target", targetEntityId);
        cmd.Parameters.AddWithValue("@source", sourceEntityId);
        cmd.Parameters.AddWithValue("@prop", propertyName);
        cmd.Parameters.AddWithValue("@raw", rawId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Remove all reverse entries for a specific (source, property, rawId) combination.</summary>
    public async Task RemoveReverseAsync(string sourceEntityId, string propertyName, string rawId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM reference_reverse
            WHERE source_entity_id = @source AND property_name = @prop AND raw_id = @raw;
            """;
        cmd.Parameters.AddWithValue("@source", sourceEntityId);
        cmd.Parameters.AddWithValue("@prop", propertyName);
        cmd.Parameters.AddWithValue("@raw", rawId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Remove all reverse entries for a target entity.</summary>
    public async Task RemoveReverseForTargetAsync(string targetEntityId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM reference_reverse WHERE target_entity_id = @target;";
        cmd.Parameters.AddWithValue("@target", targetEntityId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Find all entities that reference the target entity.</summary>
    public List<(string SourceEntityId, string PropertyName, string RawId)> ReverseLookup(string targetEntityId)
    {
        var results = new List<(string, string, string)>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT source_entity_id, property_name, raw_id
            FROM reference_reverse
            WHERE target_entity_id = @target;
            """;
        cmd.Parameters.AddWithValue("@target", targetEntityId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));

        Log.Logger.Information(
            "[ReverseLookup] targetEid={TargetEid} resultCount={Count}",
            targetEntityId, results.Count);
        return results;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Maintenance
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Delete all rows from both reference_index and reference_reverse.</summary>
    public void Clear()
    {
        using var cmd1 = _connection.CreateCommand();
        cmd1.CommandText = "DELETE FROM reference_index;";
        var count1 = cmd1.ExecuteNonQuery();

        using var cmd2 = _connection.CreateCommand();
        cmd2.CommandText = "DELETE FROM reference_reverse;";
        var count2 = cmd2.ExecuteNonQuery();

        Log.Logger.Debug(
            "[RefIndex] Clear: removed {IdxCount} index rows, {RevCount} reverse rows",
            count1, count2);
    }

    /// <summary>
    /// Batch-populate the reference_reverse table. Clears existing entries first.
    /// Each tuple: (targetEntityId, sourceEntityId, propertyName, rawId).
    /// </summary>
    public async Task BuildReverseBatchAsync(
        IReadOnlyList<(string TargetEntityId, string SourceEntityId, string PropertyName, string RawId)> entries)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Perf: reference_reverse is a rebuildable cache table — disable fsync for the
        // rebuild (worst case: cache lost on crash, rebuilt on next open), then restore.
        // With 94k+ rows this was ~1.8s of the merge-view open; sync=OFF cuts it to ~0.
        var prevSync = await GetSynchronousAsync();
        await SetSynchronousAsync("OFF");
        try
        {
            using var tx = _connection.BeginTransaction();

            using (PerfTracer.Scope("profile-open", "MergeView.Reverse.Delete"))
            {
                using var delCmd = _connection.CreateCommand();
                delCmd.CommandText = "DELETE FROM reference_reverse;";
                await delCmd.ExecuteNonQueryAsync();
            }

            using (PerfTracer.Scope("profile-open", "MergeView.Reverse.Insert"))
            {
                // Batch insert in chunks of BatchSize rows per INSERT.
                // Perf: literal SQL (no AddWithValue) — parameter binding × 4/row was
                // the ~2s hot spot for 94k reverse entries (37.8M bindings).
                for (var offset = 0; offset < entries.Count; offset += BatchSize)
                {
                    var count = Math.Min(BatchSize, entries.Count - offset);
                    var sql = BuildReverseLiteralSql(entries, offset, count);

                    using var cmd = _connection.CreateCommand();
                    cmd.CommandText = sql;
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
        }
        finally
        {
            await SetSynchronousAsync(prevSync);
        }
        sw.Stop();
        Log.Logger.Information(
            "[RefIndex] BuildReverseBatchAsync complete: {Count} reverse entries in {Ms}ms",
            entries.Count, sw.ElapsedMilliseconds);
    }

    private async Task<string> GetSynchronousAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA synchronous;";
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? "0";
    }

    private async Task SetSynchronousAsync(string mode)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"PRAGMA synchronous = {mode};";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Get ALL entries from the reference_index table (6 columns).</summary>
    public List<(string EntityType, string Namespace, string Pk, string EntityId, long? GroupId, long? SubgroupId)> GetAllIndexEntries()
    {
        var results = new List<(string, string, string, string, long?, long?)>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT entity_type, namespace, pk, entity_id, group_id, subgroup_id FROM reference_index;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt64(4),
                reader.IsDBNull(5) ? null : reader.GetInt64(5)));
        return results;
    }

    /// <summary>Get ALL entries from the reference_reverse table.</summary>
    public List<(string TargetEntityId, string SourceEntityId, string PropertyName, string RawId)> GetAllReverseEntries()
    {
        var results = new List<(string, string, string, string)>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT target_entity_id, source_entity_id, property_name, raw_id FROM reference_reverse;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        return results;
    }

    /// <summary>Count of entries in reference_index. For cache validation.</summary>
    public int Count
    {
        get
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM reference_index;";
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }
}
