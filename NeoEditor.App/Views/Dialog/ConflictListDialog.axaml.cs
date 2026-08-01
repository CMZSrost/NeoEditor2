using NeoEditor.Services;
using NeoEditor.Helper;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NeoEditor.Views.Dialog;

public partial class ConflictListDialog : Window
{
    private readonly List<ConflictEntry> _entries = [];
    public ILocalizationService Loc => ViewServices.Loc;

    public ConflictListDialog() { InitializeComponent(); }

    public ConflictListDialog(List<ConflictEntry> conflicts) : this()
    {
        _entries = conflicts;
        SummaryText.Text = $"{conflicts.Count} field conflict(s) — select rows and Ctrl+C to copy";
        ConflictGrid.ItemsSource = new ObservableCollection<ConflictEntry>(
            conflicts.OrderBy(c => c.EntityType).ThenBy(c => c.EntityLabel));
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Conflict Report",
            DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
            SuggestedFileName = $"conflict_report_{System.DateTime.Now:yyyyMMdd}.csv"
        });
        if (file?.TryGetLocalPath() is not { } path) return;

        var sb = new StringBuilder();
        sb.AppendLine("EntityType,Entity,Field,ConflictingMods");
        foreach (var entry in _entries)
            sb.AppendLine($"\"{entry.EntityType}\",\"{entry.EntityLabel}\",\"{entry.Field}\",\"{entry.ModNames}\"");
        await File.WriteAllTextAsync(path, sb.ToString());
    }
}

public record ConflictEntry(string EntityType, string EntityLabel, string Field, string ModNames);
