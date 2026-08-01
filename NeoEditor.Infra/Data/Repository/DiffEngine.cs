using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using NeoEditor.Core.Abstractions;

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

            var oldVal = before != null ? prop.GetValue(before) : null;
            var newVal = after != null ? prop.GetValue(after) : null;

            if (ValuesEqual(oldVal, newVal)) continue;

            results.Add(new DiffEntry(
                prop.Name,
                SerializeValue(oldVal),
                SerializeValue(newVal),
                before == null ? DiffKind.Added
                : after == null ? DiffKind.Removed
                : DiffKind.Modified
            ));
        }

        return results;
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        // String comparison
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
