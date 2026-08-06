using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NeoEditor.Plugins.DataViewer.Converters;

/// <summary>
/// Docs/41 需求: field-level diff on the DataGrid — binds a cell's Background to the
/// session EditStore. A cell is yellow when its (entityId, column) was edited this session
/// (or the "*" wildcard used by KV/XML edit paths). Primary-key cells act as an ANCHOR:
/// they light up whenever the row has ANY edit (keys are immutable, so they would never
/// highlight on their own, and the edited row becomes hard to find).
/// ConverterParameter: "key:&lt;column&gt;" for primary-key columns, "&lt;column&gt;" otherwise.
/// </summary>
public sealed class CellEditedHighlightConverter : IValueConverter
{
    private readonly NeoEditor.Services.IWorkspaceSession _session;
    private static readonly SolidColorBrush EditedBrush = new(Color.FromRgb(255, 255, 220));

    public CellEditedHighlightConverter(NeoEditor.Services.IWorkspaceSession session)
    {
        _session = session;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string entityId || string.IsNullOrEmpty(entityId))
            return null;

        var edited = _session.ActiveEditStore?.EditedCells;
        if (edited is null) return null;

        var param = parameter as string ?? "";
        var isKey = param.StartsWith("key:", StringComparison.Ordinal);
        var col = isKey ? param[4..] : param;

        var cellEdited = edited.Contains((entityId, "*"))
                         || edited.Contains((entityId, col));
        // Anchor: the immutable key cell lights up whenever the row has ANY edit
        // (keys are never edited themselves, so they need the row-level signal).
        if (isKey && !cellEdited)
            cellEdited = edited.Any(c => c.EntityId == entityId);

        return cellEdited ? EditedBrush : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
