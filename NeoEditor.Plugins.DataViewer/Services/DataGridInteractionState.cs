using System.Collections.Generic;
using Avalonia.Controls;
using NeoEditor.Helper;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// Singleton service that centralizes transient UI interaction flags previously scattered
/// as static mutable fields on GenericDataGridHelper (N01 violation — no static mutable state).
///
/// <list type="table">
///   <item><term>CtrlWasPressed</term><description>Set by Ctrl+PointerPressed, checked by ContextRequested
///     to suppress right-click menu after Ctrl+Click navigation.</description></item>
///   <item><term>SuppressNextSelectionChanged</term><description>Set by Ctrl+PointerPressed inline handlers
///     BEFORE the DataGrid processes the selection change. Prevents OnDataGridSelectionChanged from sending
///     EntitySelectedMessage when the user is Ctrl+clicking a reference cell.</description></item>
///   <item><term>ColumnMetaCache</term><description>Maps (DataGrid, propertyName) → ReferenceFieldAttribute.
///     Populated by ConfigureColumn, consumed by SearchableDataGrid navigation to avoid column-index
///     mismatch (RowHeader offset in Children.IndexOf).</description></item>
/// </list>
/// </summary>
public class DataGridInteractionState
{
    /// <summary>
    /// Set by Ctrl+PointerPressed, checked by ContextRequested to suppress right-click menu.
    /// </summary>
    public bool CtrlWasPressed { get; set; }

    /// <summary>
    /// Set by Ctrl+PointerPressed inline handlers BEFORE DataGrid selection change.
    /// Prevents OnDataGridSelectionChanged from sending EntitySelectedMessage / opening tabs
    /// when the user is Ctrl+clicking a reference (navigate or peek).
    /// Reset to false after being consumed by OnDataGridSelectionChanged.
    /// </summary>
    public bool SuppressNextSelectionChanged { get; set; }

    /// <summary>
    /// Column metadata cache: maps (DataGrid, propertyName) → ReferenceFieldAttribute.
    /// Populated by SearchableDataGrid when columns are auto-generated.
    /// </summary>
    public Dictionary<DataGrid, Dictionary<string, ReferenceFieldAttribute>> ColumnMetaCache { get; } = new();
}
