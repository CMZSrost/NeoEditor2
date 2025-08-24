using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.ViewModels.Controls;

namespace NeoEditor.ViewModels;

public class MainWindowViewModel : ObservableRecipient
{
    public MainWindowViewModel(
        MenuViewModel menuVm,
        EditTableViewModel editTableVm,
        ProjectViewModel projectVm
    )
    {
        IsActive = true;
        MenuVm = menuVm;
        EditTableVm = editTableVm;
        ProjectVm = projectVm;
    }

    public MenuViewModel MenuVm { get; set; }
    public ProjectViewModel ProjectVm { get; set; }
    public EditTableViewModel EditTableVm { get; set; }
}