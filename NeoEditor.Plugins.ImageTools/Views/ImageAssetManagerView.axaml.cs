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
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ImageAssetManagerViewModel vm) return;
        if (vm.SelectedNode?.IsImage == true)
        {
            vm.OpenImageCommand.Execute(null);
        }
    }
}
