using System;
using System.Collections.Generic;
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
/// </summary>
internal static class GameTableMap
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> ByTableName = new(Build, true);

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
}
