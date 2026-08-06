using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NeoEditor.Player.ViewModels;

/// <summary>
/// IsChecked binding for the theme/language radio menu items (v2.28):
/// true when the bound string equals the ConverterParameter.
/// </summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public static StringEqualsConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
