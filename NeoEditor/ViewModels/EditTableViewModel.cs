using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.ViewModels.Controls;

namespace NeoEditor.ViewModels;

public class EditTableViewModel : ObservableRecipient
{
    public EditTableViewModel(ReoGridControlViewModel gridViewModel)
    {
        GridViewModel = gridViewModel;
        IsActive = true;
    }

    public ReoGridControlViewModel GridViewModel { get; }
}