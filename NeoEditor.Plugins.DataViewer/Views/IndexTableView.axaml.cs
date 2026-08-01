using Avalonia.Controls;
using NeoEditor.Plugins.DataViewer.ViewModels;

namespace NeoEditor.Plugins.DataViewer.Views;

public partial class IndexTableView : UserControl
{
    public IndexTableView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is IndexTableViewModel vm)
            {
                ForwardGrid.IsVisible = vm.IsForward;
                ReverseGrid.IsVisible = !vm.IsForward;
            }
        };
    }
}
