using Avalonia.Controls;
using NeoEditor.ViewModels.ExplorerPane;

namespace NeoEditor.Views.UserControls;

public partial class SoundsToolView : UserControl
{
    public SoundsToolView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not SoundsToolViewModel vm) return;
        if (SoundList.SelectedItem is SoundsToolViewModel.SoundEntry entry)
        {
            vm.PlayToggleCommand.Execute(entry);
            SoundList.SelectedItem = null; // allow re-clicking the same row
        }
    }
}
