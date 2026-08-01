using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Services;
using NeoEditor.ViewModels.MainContent;
using NeoEditor.Helper;
using Serilog;

namespace NeoEditor.Views.UserControls;

public partial class DocumentWorkspaceView : UserControl
{
    public DocumentWorkspaceView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>
    /// Dock.Avalonia 12.1.0 doesn't sync ToolDock.ItemsSource into the layout, so the dynamically
    /// built tools (DocumentWorkspaceViewModel.LeftToolItems/RightToolItems/BottomToolItems) are added
    /// to the dock panes once the DockControl has loaded. See SyncToolDockIntoLayout (D02 §六).
    /// </summary>
    private void OnMainDockControlLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainContent.DocumentWorkspaceViewModel vm)
            vm.SyncToolDockIntoLayout(MainDockControl);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Avalonia 12: DataTransfer replaces Data, Contains is an extension method
        if (e.DataTransfer.Contains(DataFormat.File)) e.DragEffects = DragDropEffects.Copy;
        else e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        // Avalonia 12: use DataTransferExtensions.Contains and TryGetFiles
        if (!e.DataTransfer.Contains(DataFormat.File)) return;
        var files = e.DataTransfer.TryGetFiles() ?? Array.Empty<IStorageItem>();
        if (!files.Any()) return;
        var modManager = ViewServices.ModManager;
        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is not { } path) continue;
            var modPath = Directory.Exists(path) ? path
                : File.Exists(path) && path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(path)! : null!;
            if (modPath == null) continue;
            try
            {
                var mi = await modManager.ImportModAsync(modPath);
                if (mi is not null) IMessengerExtensions.Send(WeakReferenceMessenger.Default,
                    new OpenModGameDataDocumentMessage(mi));
            }
            catch (Exception ex) { ViewServices.Notification.ShowWarning($"Import failed: {ex.Message}"); }
        }
    }
}


