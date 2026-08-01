using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Helper;
using NeoEditor.ViewModels.ExplorerPane;
using NeoEditor.Views.Dialog;

namespace NeoEditor.Views.UserControls;

public partial class Pane : UserControl
{
    public Pane()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnTreeViewKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);

        ResourceManagerViewModel.RenameDialogRequested = async (currentName) =>
        {
            var window = this.FindAncestorOfType<Window>();
            if (window is null) return null;
            var dialog = new RenameDialog(currentName);
            return await dialog.ShowAsync(window);
        };
    }

    // Search result Ctrl+Click
    private void OnSearchResultPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
        if (sender is not Control ctrl || ctrl.DataContext is not SearchResultItem item) return;
        e.Handled = true;

        var point = e.GetCurrentPoint(ctrl);
        if (point.Properties.IsRightButtonPressed)
            ViewServices.NavigationRouter
                .RequestPeek(item.EntityType, item.Entity.EntityId, item.Entity);
        else
            ViewServices.NavigationRouter
                .NavigateToEntity(item.EntityType, item.Entity.EntityId, item.Entity);
    }

    private async void OnTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F2) return;
        if (e.Source is not Control src) return;

        var treeView = src.FindAncestorOfType<TreeView>();
        if (treeView is null) return;

        if (treeView.DataContext is not ResourceManagerViewModel rmVm) return;
        var item = rmVm.SelectedItem;
        if (item is null) return;

        e.Handled = true;

        // Trigger rename through the ViewModel's command (which now uses the dialog)
        if (rmVm.RenameItemCommand.CanExecute(item))
            rmVm.RenameItemCommand.Execute(item);
    }
}
