using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Player.Core.Services;

namespace NeoEditor.Player.Core.Data;

/// <summary>One reference column discovered via [ReferenceField] reflection.</summary>
internal sealed record RefColumn(string Column, string TableName, string? Separator,
    string? OrSeparator, string Pattern, string? SecondaryTableName, bool IsImage);

/// <summary>
/// Reference metadata over the editor's entity model (Docs/42 v2.22): which column of
/// which table references which target table, plus the parse pattern. Shared by the wiki
/// detail builder (outgoing links) and the reference analyzer (incoming references).
/// Image columns ([ReferenceField(typeof(ImageAsset))]) resolve to "Namespace:FileName"
/// style values but have no real target table — they are marked <see cref="RefColumn.IsImage"/>.
/// </summary>
internal static class ReferenceMetadata
{
    /// <summary>All [ReferenceField] columns per table name (known entity tables only).</summary>
    public static Dictionary<string, List<RefColumn>> Build()
    {
        var result = new Dictionary<string, List<RefColumn>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableName in GameTableMap.KnownTableNames)
        {
            var type = GameTableMap.FindType(tableName);
            if (type is null) continue;
            var columns = new List<RefColumn>();
            foreach (var property in type.GetProperties())
            {
                var column = property.GetCustomAttribute<ColumnAttribute>()?.Name;
                var reference = property.GetCustomAttribute<ReferenceFieldAttribute>();
                if (string.IsNullOrEmpty(column) || reference is null) continue;
                var isImage = reference.TargetEntityType == typeof(ImageAsset);
                columns.Add(new RefColumn(column,
                    isImage ? "" : GameTableMap.FindTableName(reference.TargetEntityType) ?? "",
                    reference.Separator, reference.OrSeparator, reference.Pattern ?? "{id}",
                    reference.SecondaryTargetEntityType is null
                        ? null
                        : GameTableMap.FindTableName(reference.SecondaryTargetEntityType),
                    isImage));
            }
            if (columns.Count > 0) result[tableName] = columns;
        }
        return result;
    }

    /// <summary>Read a column value from a row (case-insensitive).</summary>
    public static string Value(GameDataRow row, string column)
        => row.Fields.FirstOrDefault(f =>
            string.Equals(f.Column, column, StringComparison.OrdinalIgnoreCase))?.Value ?? "";

    /// <summary>
    /// Split a raw reference value into segments and parse each one into
    /// (id, mult, qty). OR-groups ("a|b") flatten into their alternatives.
    /// </summary>
    public static IEnumerable<(string Id, string? Mult, string? Qty)> ParseSegments(
        string raw, RefColumn refColumn)
    {
        var topLevel = refColumn.Separator is null
            ? new[] { raw }
            : raw.Split(refColumn.Separator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var top in topLevel)
        {
            var items = refColumn.OrSeparator is null
                ? new[] { top }
                : top.Split(refColumn.OrSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                var (id, mult, qty) = ParseSegment(item.Trim(), refColumn.Pattern);
                if (id is not null) yield return (id, mult, qty);
            }
        }
    }

    /// <summary>
    /// Parse one segment against the pattern ("{id}x{mult}x{qty}", "{mult}x{id}", "{value}={id}"…).
    /// Trailing placeholders are OPTIONAL in the data (e.g. "5x1" for {id}x{mult}x{qty} — qty
    /// omitted), so the full pattern is tried first, then trailing placeholders are dropped
    /// one by one. When the id placeholder itself was dropped (bare "5" for {mult}x{id}),
    /// the whole segment is the id.
    /// </summary>
    public static (string? Id, string? Mult, string? Qty) ParseSegment(string segment, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "{id}") return (segment, null, null);

        var lits = Regex.Split(pattern, @"\{[a-z]+\}");
        var tokens = Regex.Matches(pattern, @"\{[a-z]+\}")
            .Select(m => m.Value[1..^1])
            .ToList();
        for (var count = tokens.Count; count >= 1; count--)
        {
            var regex = new StringBuilder("^");
            regex.Append(Regex.Escape(lits[0]));
            for (var i = 0; i < count; i++)
            {
                regex.Append(GroupPattern(tokens[i]));
                // Only the separators BETWEEN kept placeholders belong — the literal right
                // before the dropped trailing placeholder must not be appended.
                if (i + 1 < count && i + 1 < lits.Length) regex.Append(Regex.Escape(lits[i + 1]));
            }
            regex.Append('$');

            var match = Regex.Match(segment, regex.ToString());
            if (!match.Success) continue;
            var id = Group(match, "id");
            return id is null
                ? (segment, null, null)
                : (id, Group(match, "mult"), Group(match, "qty"));
        }
        return (null, null, null);
    }

    private static string GroupPattern(string token) => token switch
    {
        "id" => "(?<id>[^\\s]+)",
        "mult" => "(?<mult>[^\\s]+)",
        "qty" => "(?<qty>[^\\s]+)",
        "value" => "(?<value>.+)",
        _ => Regex.Escape(token),
    };

    private static string? Group(Match match, string name)
        => match.Groups[name].Success ? match.Groups[name].Value : null;
}
