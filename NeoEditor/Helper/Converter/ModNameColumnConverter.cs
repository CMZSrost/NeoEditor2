using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper.Converter;

public class ModNameColumnConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string entityId)
            return GenericDataGridHelper.EntityModNames.TryGetValue(entityId, out var name)
                ? name
                : "";
        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
