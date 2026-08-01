using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;
using NeoEditor.Helper;

namespace NeoEditor.Views.Dialog;

public partial class CsvImportDiffDialog : Window
{
    public ILocalizationService Loc { get; }
    public List<CsvDiffRow> Rows { get; private set; }

    public CsvImportDiffDialog()
    {
        Loc = ViewServices.Loc;
        InitializeComponent();
    }

    public CsvImportDiffDialog(List<CsvDiffRow> rows) : this()
    {
        Rows = rows;
        var displayRows = new ObservableCollection<DiffDisplayRow>(
            rows.Select(r => new DiffDisplayRow
            {
                Key = r.Key,
                Field = r.Field,
                OldValue = r.OldValue,
                NewValue = r.NewValue,
                Status = r.Status.ToString(),
                RowBackground = r.Status == DiffStatus.Added
                    ? new SolidColorBrush(Color.FromRgb(220, 255, 220))
                    : r.Status == DiffStatus.Modified
                        ? new SolidColorBrush(Color.FromRgb(255, 255, 220))
                        : null
            }));
        DiffGrid.ItemsSource = displayRows;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    public static async Task<bool> ShowAsync(Window owner, List<CsvDiffRow> rows)
    {
        var dialog = new CsvImportDiffDialog(rows);
        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }

    public class DiffDisplayRow
    {
        public string Key { get; set; } = "";
        public string Field { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string Status { get; set; } = "";
        public IBrush? RowBackground { get; set; }
    }
}
