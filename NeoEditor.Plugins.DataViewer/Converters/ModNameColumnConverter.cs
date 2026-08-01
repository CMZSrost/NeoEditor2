using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Plugins.DataViewer.Converters;

public class ModNameColumnConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string entityId)
        {
            var entityModNames = ConverterServiceHelper.DataTable?.EntityModNames
                ?? new System.Collections.Generic.Dictionary<string, string>();
            return entityModNames.TryGetValue(entityId, out var name) ? name : "";
        }
        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
