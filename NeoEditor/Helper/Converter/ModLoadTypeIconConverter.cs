using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using NeoEditor.Data.Model;

namespace NeoEditor.Helper.Converter;

public sealed class ModLoadTypeIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ModType.Insert => Symbol.Insert,
            ModType.Merge => Symbol.Merge,
            _ => Symbol.Question
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}