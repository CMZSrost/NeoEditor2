using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Helper;

namespace NeoEditor.Plugins.DataViewer.Converters;

/// <summary>
/// R30: bridges DataGrid edit controls (TextBox/ComboBox work with string) and
/// ReferenceList properties. Reads via <see cref="ReferenceText.GetRawString"/>
/// (raw "3,14" — never the damaged "[3, 14]"), writes back through the serializer
/// so cell edits actually reach the entity.
/// </summary>
public class ReferenceListConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ReferenceText.GetRawString(value, parameter as ReferenceFieldAttribute);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not ReferenceFieldAttribute attr) return value;
        if (value is not string s) return value;
        try
        {
            return new ReferenceListSerializer().Deserialize(s, attr);
        }
        catch (Exception ex)
        {
            // Unparseable input: keep the edit from corrupting the entity.
            Serilog.Log.Logger.Warning(ex, "[RefListConverter] Failed to parse '{Val}'", s);
            return Avalonia.Data.BindingNotification.UnsetValue;
        }
    }
}
