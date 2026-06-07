using Avalonia.Controls;
using Avalonia.Interactivity;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class ConflictsView : UserControl
{
    private BottomToolsViewModel? _vm;

    public ConflictsView() => InitializeComponent();

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        _vm = DataContext as BottomToolsViewModel;
    }

    private void OnRefreshConflictsClick(object? sender, RoutedEventArgs e) => _vm?.LoadConflicts();
}
