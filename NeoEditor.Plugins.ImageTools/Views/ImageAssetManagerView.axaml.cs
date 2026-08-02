using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NeoEditor.Plugins.ImageTools.ViewModels;

namespace NeoEditor.Plugins.ImageTools.Views;

public partial class ImageAssetManagerView : UserControl
{
    public ImageAssetManagerView()
    {
        InitializeComponent();
        ImageTree.AddHandler(InputElement.DoubleTappedEvent, OnTreeDoubleTapped,
            RoutingStrategies.Bubble, handledEventsToo: true);
        // Right-click selects the item under the pointer so the context-menu command
        // (Add Image) targets the right mod directory.
        ImageTree.AddHandler(InputElement.ContextRequestedEvent, OnTreeContextRequested,
            RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ImageAssetManagerViewModel vm) return;
        if (vm.SelectedNode?.IsImage == true)
        {
            vm.OpenImageCommand.Execute(null);
        }
    }

    private void OnTreeContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not ImageAssetManagerViewModel vm) return;

        if (!e.TryGetPosition(ImageTree, out var point))
            return;

        var hit = ImageTree.InputHitTest(point) as StyledElement;
        while (hit is not null)
        {
            if (hit is TreeViewItem item)
            {
                if (item.DataContext is ModImageTreeNode node)
                    vm.SelectedNode = node;
                return;
            }

            hit = hit.Parent;
        }
    }
}