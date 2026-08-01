using NeoEditor.Services;
using NeoEditor.Helper;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NeoEditor.Views.Dialog;

public partial class DependencyListDialog : Window
{
    private readonly List<DependencyEntry> _entries = [];
    public ILocalizationService Loc => ViewServices.Loc;

    public DependencyListDialog() { InitializeComponent(); }

    public DependencyListDialog(List<DependencyEntry> entries) : this()
    {
        _entries = entries;
        SummaryText.Text = $"{entries.Count} unresolved reference(s) — select rows and Ctrl+C to copy";
        DepGrid.ItemsSource = new ObservableCollection<DependencyEntry>(
            entries.OrderBy(e => e.SourceMod).ThenBy(e => e.SourceEntity));
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Dependency Report",
            DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
            SuggestedFileName = $"dependency_report_{System.DateTime.Now:yyyyMMdd}.csv"
        });
        if (file?.TryGetLocalPath() is not { } path) return;

        var sb = new StringBuilder();
        sb.AppendLine("Source,Mod,Field,Target,Issue");
        foreach (var entry in _entries)
            sb.AppendLine($"\"{entry.SourceEntity}\",\"{entry.SourceMod}\",\"{entry.Field}\",\"{entry.TargetDesc}\",\"{entry.Issue}\"");
        await File.WriteAllTextAsync(path, sb.ToString());
    }
}

public record DependencyEntry(string SourceEntity, string SourceMod, string Field, string TargetDesc, string Issue);
