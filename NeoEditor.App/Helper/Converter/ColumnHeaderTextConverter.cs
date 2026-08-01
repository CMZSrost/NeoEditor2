using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace NeoEditor.Helper.Converter;

/// <summary>
/// Extracts display text from a DataGridColumn.Header value.
/// When headers are StackPanel-based (for sort icons + text),
/// this converter avoids visual-parenting conflicts in
/// DataGridColumnChooser by returning a plain string.
/// </summary>
public class ColumnHeaderTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ExtractText(value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    internal static string ExtractText(object? header)
    {
        if (header is string s) return s;
        if (header is StackPanel sp)
        {
            foreach (var child in sp.Children)
            {
                if (child is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
                    return tb.Text;
                if (child is Avalonia.Controls.Presenters.TextPresenter tp
                    && !string.IsNullOrEmpty(tp.Text))
                    return tp.Text;
            }
        }
        if (header is TextBlock t) return t.Text ?? "";
        if (header is Avalonia.Controls.Presenters.TextPresenter tp2)
            return tp2.Text ?? "";
        return header?.ToString() ?? "";
    }
}
