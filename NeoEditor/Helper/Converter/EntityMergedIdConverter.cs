using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper.Converter;

public class EntityMergedIdConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEntity entity)
        {
            var mid = GenericDataGridHelper.GetEntityMergedId(entity);
            return mid > 0 ? mid.ToString() : "-";
        }
        return "-";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
