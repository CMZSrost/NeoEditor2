using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.ViewModels.Dialog;

public partial class CreateModDialogViewModel: ViewModelBase
{
    [ObservableProperty] public partial string Author { get; set; } = "";
    [ObservableProperty] public partial string Name { get; set; } = "";
}