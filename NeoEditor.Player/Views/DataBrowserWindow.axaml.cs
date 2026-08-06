using Avalonia.Controls;
using NeoEditor.Player.Core.Data;
using NeoEditor.Player.ViewModels;
using Avalonia.Input;

namespace NeoEditor.Player.Views;

/// <summary>
/// Standalone data-browser dialog (Docs/42 v2.14): a normal bordered window so it can be
/// moved/resized and compared side-by-side with the player. Data source: DataBrowserService
/// (read-only pma_xml_export parsing of data/ + Mods).
/// </summary>
public partial class DataBrowserWindow : Window
{
    public DataBrowserWindow()
    {
        InitializeComponent();
    }

    /// <summary>Field-grid reference link clicked — navigate the browser (v2.34).</summary>
    private void OnFieldLinkClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: FieldLink link } && DataContext is DataBrowserViewModel vm)
            vm.NavigateTo(link.Target);
    }
}
