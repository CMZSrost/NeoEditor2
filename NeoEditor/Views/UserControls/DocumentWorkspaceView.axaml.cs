using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Services;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class DocumentWorkspaceView : UserControl
{
    public DocumentWorkspaceView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return;
        var files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();
        if (!files.Any()) return;

        var modManager = App.ServiceProvider!.GetRequiredService<IModManager>();

        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is not { } path) continue;
            string modPath;
            if (Directory.Exists(path)) modPath = path;
            else if (File.Exists(path) && path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                modPath = Path.GetDirectoryName(path)!;
            else continue;

            try
            {
                var modInfo = await modManager.ImportModAsync(modPath);
                if (modInfo is not null)
                    App.ServiceProvider!.GetRequiredService<IMessenger>()
                        .Send(new OpenModGameDataDocumentMessage(modInfo));
            }
            catch (Exception ex)
            {
                App.Notification.ShowWarning($"Failed to import {path}: {ex.Message}");
            }
        }
    }
}
