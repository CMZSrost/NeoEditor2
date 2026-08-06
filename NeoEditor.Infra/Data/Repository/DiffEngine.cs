using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Data.Repository;

/// <summary>
/// Static helper that computes field-level diffs between two entity instances
/// by reflecting on [Column]-attributed properties.
/// </summary>
public static class DiffEngine
{
    /// <summary>
    /// Compute a list of field-level diffs between two versions of the same entity.
    /// Compares all public instance properties marked with [Column].
    /// Skips [NotMapped] properties.
    /// </summary>
    public static List<DiffEntry> ComputeDiff<T>(T? before, T? after) where T : class
    {
        var results = new List<DiffEntry>();
        if (before == null && after == null) return results;

        var type = (before ?? after)!.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            // Only compare [Column]-marked properties; skip [NotMapped].
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            if (colAttr == null) continue;
            if (prop.GetCustomAttribute<NotMappedAttribute>() != null) continue;

            // Reference fields must compare/serialize as their canonical raw text
            // (ReferenceText.GetRawString) — never ReferenceList.ToString(), which
            // emits the damaged "[a, b]" format and misreports unchanged references.
            var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();
            var oldVal = before != null ? prop.GetValue(before) : null;
            var newVal = after != null ? prop.GetValue(after) : null;

            var oldText = oldVal is ReferenceList<IReferenceEntry>
                ? ReferenceText.GetRawString(oldVal, refAttr)
                : SerializeValue(oldVal);
            var newText = newVal is ReferenceList<IReferenceEntry>
                ? ReferenceText.GetRawString(newVal, refAttr)
                : SerializeValue(newVal);

            if (ValuesEqual(oldText, newText)) continue;

            results.Add(new DiffEntry(
                prop.Name,
                oldText,
                newText,
                before == null ? DiffKind.Added
                : after == null ? DiffKind.Removed
                : DiffKind.Modified
            ));
        }

        return results;
    }

    /// <summary>
    /// Column names (XML keys, i.e. <c>[Column(Name=...)] ?? property name</c>) that differ
    /// between two versions of the same entity. Used to upgrade legacy entity-level
    /// pending-export markers to per-column markers by diffing the game XML original
    /// against the current (DB) value.
    /// </summary>
    public static List<string> ComputeChangedColumns(IEntity before, IEntity after)
    {
        var columns = new List<string>();
        foreach (var diff in ComputeDiff(before, after))
        {
            var prop = after.GetType().GetProperty(diff.PropertyName);
            var colAttr = prop?.GetCustomAttribute<ColumnAttribute>();
            columns.Add(colAttr?.Name ?? diff.PropertyName);
        }
        return columns;
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        // Reference fields arrive here as canonical raw text (string).
        if (a is string sa && b is string sb)
            return string.Equals(sa, sb, StringComparison.Ordinal);

        // Value type comparison
        return a.Equals(b);
    }

    private static string? SerializeValue(object? value)
    {
        if (value == null) return null;
        if (value is string s) return s;
        if (value is Enum e) return Convert.ToInt32(e).ToString();
        return value.ToString();
    }
}
