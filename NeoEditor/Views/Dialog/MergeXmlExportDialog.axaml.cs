using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit.Document;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;

namespace NeoEditor.Views.Dialog;

public partial class MergeXmlExportDialog : Window
{
    public record ExportItem(string ModName, string FileName, string FilePath, string OldXml, string NewXml);

    public LocalizationService Loc { get; }
    private List<ExportItem> _items = [];

    public MergeXmlExportDialog()
    {
        DataContext = this;
        Loc = App.ServiceProvider.GetRequiredService<LocalizationService>();
        InitializeComponent();
    }

    public MergeXmlExportDialog(List<ExportItem> items) : this()
    {
        _items = items;
        FileListBox.ItemsSource = new ObservableCollection<ExportItem>(items);
        FileListBox.SelectionChanged += OnFileSelectionChanged;

        if (items.Count > 0)
            FileListBox.SelectedIndex = 0;
    }

    private void OnFileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FileListBox.SelectedItem is not ExportItem item) return;
        var diffText = BuildDiffText(item.OldXml, item.NewXml);
        DiffView.OldXml = new TextDocument(item.OldXml);
        DiffView.NewXml = new TextDocument(diffText);
    }

    private void OnConfirmAllClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private static string BuildDiffText(string oldXml, string newXml)
    {
        var tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "NeoEditor", "MergeXmlExport", System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var oldPath = System.IO.Path.Combine(tempDir, "old.xml");
            var newPath = System.IO.Path.Combine(tempDir, "new.xml");
            System.IO.File.WriteAllText(oldPath, oldXml, System.Text.Encoding.UTF8);
            System.IO.File.WriteAllText(newPath, newXml, System.Text.Encoding.UTF8);
            return Helper.XmlCompareHelper.Compare(oldPath, newPath);
        }
        catch
        {
            return newXml;
        }
        finally
        {
            try { System.IO.Directory.Delete(tempDir, true); } catch { }
        }
    }

    public static async Task<List<ExportItem>?> ShowAsync(Window owner, List<ExportItem> items)
    {
        var dialog = new MergeXmlExportDialog(items);
        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true ? dialog._items : null;
    }
}
