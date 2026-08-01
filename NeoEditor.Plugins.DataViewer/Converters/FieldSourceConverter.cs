using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NeoEditor.Plugins.DataViewer.Converters;

/// <summary>
/// Converts an EntityId (with column name as parameter) to a tooltip
/// showing which mod set the field value and whether there's a conflict.
/// </summary>
public class FieldSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string entityId || parameter is not string colName) return null;

        var fieldSources = ConverterServiceHelper.DataTable?.FieldSources
            ?? new Dictionary<(string, string), string>();
        var fieldConflicts = ConverterServiceHelper.DataTable?.FieldConflicts
            ?? new HashSet<(string, string)>();

        var hasSource = fieldSources.TryGetValue((entityId, colName), out var modName);
        var isConflict = fieldConflicts.Contains((entityId, colName));

        if (!hasSource) return null;

        var source = isConflict
            ? $"⚠ CONFLICT — current: [{modName}]"
            : $"Source: [{modName}]";

        return string.IsNullOrEmpty(source) ? null : source;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
