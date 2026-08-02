using System;
using System.Reflection;
using Avalonia.Controls;
using NeoEditor.Helper;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// Handles Ctrl+Click / Ctrl+Hover / context-menu interactions on DataGrid reference cells.
/// Extracted from GenericDataGridHelper.ConfigureColumn to reduce GDH size and
/// enable constructor-injected dependencies (R07).
///
/// R03: Uses injected IReferenceResolver for all reference lookups.
/// R07: Receives all dependencies via constructor injection.
/// </summary>
public interface IDataGridCellInteractionService
{
    /// <summary>
    /// Attach Ctrl+Hover, Ctrl+Click (navigate/peek), and context-menu suppression
    /// handlers to a single-value reference cell grid.
    /// </summary>
    void AttachSingleRefHandlers(Grid grid, object rowItem, PropertyInfo property,
        Type targetType, ReferenceFieldAttribute refAttr, string pattern,
        string propertyName, string refColName);

    /// <summary>
    /// Attach Ctrl+Hover tooltip and context-menu suppression to a multi-value segment border.
    /// </summary>
    void AttachMultiRefSegmentHandlers(Border segBorder, string rawSegment,
        object rowItem, Type targetType, ReferenceFieldAttribute refAttr,
        string pattern, string propertyName, string refColName);

    /// <summary>
    /// Attach a cell-wide Ctrl+Click (navigate/peek) handler to a multi-value wrapPanel.
    /// </summary>
    void AttachMultiRefCellHandler(WrapPanel wrapPanel, object rowItem,
        Type targetType, ReferenceFieldAttribute refAttr, string pattern,
        string propertyName);

    /// <summary>
    /// Format a single reference segment with Subject display name resolved via IReferenceResolver.
    /// </summary>
    string FormatSegmentDisplay(string segment, Type targetType, string? pattern,
        string sourceEntityId, string propertyName, string? targetKey,
        Type? secondaryTargetType = null);
}
