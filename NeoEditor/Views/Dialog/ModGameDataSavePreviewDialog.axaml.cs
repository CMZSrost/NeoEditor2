using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit.Document;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;

namespace NeoEditor.Views.Dialog;

public partial class ModGameDataSavePreviewDialog : Window
{
    public LocalizationService Loc { get; set; }
    public TextDocument OldXml { get; }
    public TextDocument NewXml { get; }

    public ModGameDataSavePreviewDialog() : this(null, string.Empty, string.Empty)
    {
    }

    public ModGameDataSavePreviewDialog(string? modName, string oldXml, string newXml)
    {
        OldXml = new TextDocument(oldXml);
        NewXml = new TextDocument(newXml);
        Loc = App.ServiceProvider.GetRequiredService<LocalizationService>();

        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(modName)
            ? Loc["ModGameDataSavePreviewTitle"]
            : Loc["ModGameDataSavePreviewTitleFormat", modName];
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}