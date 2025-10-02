using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.ViewModels.Controls;

public class EditTableViewModel : ObservableRecipient
{
    public EditTableViewModel(ReoGridControlViewModel gridViewModel)
    {
        GridViewModel = gridViewModel;
        IsActive = true;
    }

    public ReoGridControlViewModel GridViewModel { get; }
}