using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit.Document;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.Helper;

namespace NeoEditor.Plugins.EntityEditor.Views;

public partial class MergeXmlExportDialog : Window
{
    public record ExportItem(string ModName, string FileName, string FilePath, string OldXml, string NewXml);

    private ILocalizationService? _loc;
    public ILocalizationService Loc => _loc ??= GetService<ILocalizationService>();
    private List<ExportItem> _items = [];

    private static T GetService<T>() where T : notnull
        => (Application.Current?.Resources["Services"] as IServiceProvider)!.GetRequiredService<T>();

    public MergeXmlExportDialog()
    {
        DataContext = this;
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
        DiffView.OldXml = new TextDocument(item.OldXml);
        DiffView.NewXml = new TextDocument(item.NewXml);
    }

    private void OnConfirmAllClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    public static async Task<List<ExportItem>?> ShowAsync(Window owner, List<ExportItem> items)
    {
        var dialog = new MergeXmlExportDialog(items);
        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true ? dialog._items : null;
    }
}
