using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit.Document;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.EntityEditor.Views;

public partial class ModGameDataSavePreviewDialog : Window
{
    private ILocalizationService? _loc;

    public ILocalizationService Loc => _loc ??= GetService<ILocalizationService>();
    public TextDocument OldXml { get; }
    public TextDocument NewXml { get; }

    private static T GetService<T>() where T : notnull
        => (Application.Current?.Resources["Services"] as IServiceProvider)!.GetRequiredService<T>();

    public ModGameDataSavePreviewDialog() : this(null, string.Empty, string.Empty)
    {
    }

    public ModGameDataSavePreviewDialog(string? modName, string oldXml, string newXml)
    {
        OldXml = new TextDocument(oldXml);
        NewXml = new TextDocument(newXml);

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
