using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NeoEditor.Plugins.DataViewer.Converters;

/// <summary>
/// Returns a light red background for cells where two different Merge mods
/// set different values for the same field.
/// </summary>
public class FieldConflictBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush ConflictBrush = new(Color.FromRgb(255, 220, 220));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string entityId || parameter is not string colName) return AvaloniaProperty.UnsetValue;
        var fieldConflicts = ConverterServiceHelper.DataTable?.FieldConflicts
            ?? new System.Collections.Generic.HashSet<(string, string)>();
        return fieldConflicts.Contains((entityId, colName))
            ? ConflictBrush
            : AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
