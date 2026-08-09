using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Player.Core.Services;

/// <summary>
/// Game table name (e.g. "itemtypes" — the <c>data/*.xml</c> file names the SWF hardcodes)
/// to entity Type resolution, scanning the editor's entity model for [Table] classes
/// (Docs/42 §3.6). v2.27: self-contained reflection so Player.Core does NOT reference
/// NeoEditor.Infra — the whole EF Core stack stays out of the player package.
/// v2.72: also maps (table, XML column) → display key for the data browser localization
/// (same [Display] metadata the editor's data table uses).
/// </summary>
public static class GameTableMap
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> ByTableName = new(Build, true);

    private static readonly Lazy<IReadOnlyDictionary<string, string>> FieldKeysByColumn =
        new(BuildFieldKeys, true);

    private static IReadOnlyDictionary<string, Type> Build()
        => typeof(IEntity).Assembly.GetTypes()
            .Where(type => type.IsClass
                           && !type.IsAbstract
                           && type != typeof(IEntity)
                           && type.Namespace == typeof(IEntity).Namespace
                           && typeof(IEntity).IsAssignableFrom(type)
                           && type.GetCustomAttribute<TableAttribute>() is { Name: { Length: > 0 } })
            .GroupBy(static type => type.GetCustomAttribute<TableAttribute>()!.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First());

    /// <summary>Resolve a table name ("itemtypes") to its entity type, or null when unknown.</summary>
    public static Type? FindType(string tableName)
        => string.IsNullOrWhiteSpace(tableName) ? null : ByTableName.Value.GetValueOrDefault(tableName);

    /// <summary>Resolve an entity type back to its table name, or null (wiki reference targets).</summary>
    public static string? FindTableName(Type type)
        => ByTableName.Value.FirstOrDefault(kv => kv.Value == type).Key;

    /// <summary>All known game entity table names (the editor's typed tables — the 24 data classes).</summary>
    public static IReadOnlyCollection<string> KnownTableNames => ByTableName.Value.Keys.ToList();

    /// <summary>
    /// Field display key for an XML column: the property's [Display(Name=…)] value, or the
    /// property name when no [Display] exists — the resx key <c>FieldName.{key}</c> holds the
    /// localized label and <c>FieldDesc.{key}</c> the description (v2.72, mirror of the
    /// editor's data-table header/tooltip metadata). Null for unknown tables/columns.
    /// </summary>
    public static string? GetFieldDisplayKey(string tableName, string column)
        => string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(column)
            ? null
            : FieldKeysByColumn.Value.GetValueOrDefault($"{tableName}.{column}");

    private static IReadOnlyDictionary<string, string> BuildFieldKeys()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in ByTableName.Value.Values)
        {
            var table = type.GetCustomAttribute<TableAttribute>()?.Name;
            if (string.IsNullOrEmpty(table)) continue;
            foreach (var prop in type.GetProperties())
            {
                // Columns without [Column] map by property name (EF convention) — the
                // game XML uses the attribute name when present.
                var column = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                var display = prop.GetCustomAttribute<DisplayAttribute>()?.Name;
                map[$"{table}.{column}"] = display ?? prop.Name;
            }
        }
        return map;
    }
}
