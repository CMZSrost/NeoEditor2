using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper;

/// <summary>
/// Single source of truth for all column keys per entity type.
/// Both the DataGrid column manager and the settings panel use this to
/// ensure they always produce the same set of keys for ColumnVisibility config.
/// Default: all columns visible. User hides columns → incremental remove from set.
/// </summary>
public static class ColumnVisibilityKeys
{
    /// <summary>All 24 entity types that appear as DataGrid tabs.</summary>
    public static readonly Type[] AllEntityTypes = GameDomain.Domains
        .SelectMany(kv => kv.Value)
        .ToArray();

    /// <summary>Get the table name from an entity type (from [Table] attribute).</summary>
    public static string? GetTableName(Type entityType)
        => entityType.GetCustomAttribute<TableAttribute>()?.Name;

    /// <summary>All column keys for a given entity type (property names + synthetic + internal).</summary>
    public static List<string> GetKeys(Type entityType)
    {
        var keys = new List<string>();

        // Entity properties with [Column] attribute (including IEntity base: ModId, FilePath, EntityId)
        foreach (var p in entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (p.GetCustomAttribute<ColumnAttribute>() is null) continue;
            keys.Add(p.Name);
        }

        // Synthetic columns inserted by OnAutoGeneratingColumn
        keys.Add("MergedId");
        keys.Add("Mod");

        return keys;
    }

    /// <summary>Column display name from property name (for settings UI).</summary>
    public static string GetDisplayName(Type entityType, string key)
    {
        var p = entityType.GetProperty(key, BindingFlags.Instance | BindingFlags.Public);
        if (p is not null)
            return p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name;

        return key switch
        {
            "MergedId" => "MergedId (→Id)",
            "Mod" => "Mod",
            "ModId" => "ModId",
            "FilePath" => "FilePath",
            "EntityId" => "EntityId",
            _ => key
        };
    }

    /// <summary>Read whether a column is visible from the config dict.
    /// Defaults to true (all visible) when the table has no saved entry yet.</summary>
    public static bool IsVisible(Dictionary<string, HashSet<string>> cv, string tableName, string key)
        => !cv.TryGetValue(tableName, out var set) || set.Contains(key);

    /// <summary>Seed the config for a table with all keys (all visible).
    /// Called on first toggle from either side.</summary>
    public static void SeedAllVisible(Dictionary<string, HashSet<string>> cv, Type entityType)
    {
        var tableName = GetTableName(entityType);
        if (tableName is null) return;
        if (cv.ContainsKey(tableName)) return;
        cv[tableName] = new HashSet<string>(GetKeys(entityType));
    }
}
