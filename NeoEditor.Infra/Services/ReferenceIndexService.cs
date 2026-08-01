using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
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
        var sql = BuildMultiValuesSql("reference_index",
            ["entity_type", "namespace", "pk", "entity_id", "group_id", "subgroup_id"],
            count);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        for (var i = 0; i < count; i++)
        {
            var entry = entries[offset + i];
            cmd.Parameters.AddWithValue($"@a{i}", entry.EntityType);
            cmd.Parameters.AddWithValue($"@b{i}", entry.Namespace);
            cmd.Parameters.AddWithValue($"@c{i}", entry.Pk);
            cmd.Parameters.AddWithValue($"@d{i}", entry.EntityId);
            cmd.Parameters.AddWithValue($"@e{i}", entry.GroupId.HasValue ? (object)entry.GroupId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue($"@f{i}", entry.SubgroupId.HasValue ? (object)entry.SubgroupId.Value : DBNull.Value);
        }

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Build INSERT OR REPLACE ... VALUES (...), (...), ... for a batch of rows.</summary>
    private static string BuildMultiValuesSql(string table, string[] columns, int rowCount)
    {
        var colList = string.Join(", ", columns);
        var rows = new System.Text.StringBuilder();
        for (var r = 0; r < rowCount; r++)
        {
            if (r > 0) rows.Append(", ");
            var vals = string.Join(", ", columns.Select((c, i) =>
            {
                var ch = (char)('a' + i);
                return $"@{ch}{r}";
            }));
            rows.Append($"({vals})");
        }
        return $"INSERT OR REPLACE INTO {table} ({colList}) VALUES {rows};";
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
        var sql = BuildMultiValuesSql("reference_index",
            ["entity_type", "namespace", "pk", "entity_id", "group_id", "subgroup_id"],
            count);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        for (var i = 0; i < count; i++)
        {
            var entry = entries[offset + i];
            cmd.Parameters.AddWithValue($"@a{i}", entry.EntityType);
            cmd.Parameters.AddWithValue($"@b{i}", entry.Namespace);
            cmd.Parameters.AddWithValue($"@c{i}", entry.Pk);
            cmd.Parameters.AddWithValue($"@d{i}", entry.EntityId);
            cmd.Parameters.AddWithValue($"@e{i}", entry.GroupId.HasValue ? (object)entry.GroupId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue($"@f{i}", entry.SubgroupId.HasValue ? (object)entry.SubgroupId.Value : DBNull.Value);
        }

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
        using var tx = _connection.BeginTransaction();

        using (var delCmd = _connection.CreateCommand())
        {
            delCmd.CommandText = "DELETE FROM reference_reverse;";
            await delCmd.ExecuteNonQueryAsync();
        }

        // Batch insert in chunks of BatchSize rows per INSERT
        for (var offset = 0; offset < entries.Count; offset += BatchSize)
        {
            var count = Math.Min(BatchSize, entries.Count - offset);
            var sql = BuildMultiValuesSql("reference_reverse",
                ["target_entity_id", "source_entity_id", "property_name", "raw_id"],
                count);

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;

            for (var i = 0; i < count; i++)
            {
                var (target, source, prop, raw) = entries[offset + i];
                cmd.Parameters.AddWithValue($"@a{i}", target);
                cmd.Parameters.AddWithValue($"@b{i}", source);
                cmd.Parameters.AddWithValue($"@c{i}", prop);
                cmd.Parameters.AddWithValue($"@d{i}", raw);
            }

            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        sw.Stop();
        Log.Logger.Information(
            "[RefIndex] BuildReverseBatchAsync complete: {Count} reverse entries in {Ms}ms",
            entries.Count, sw.ElapsedMilliseconds);
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
