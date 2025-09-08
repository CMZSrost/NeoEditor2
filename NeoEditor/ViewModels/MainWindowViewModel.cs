using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.ViewModels.Controls;

namespace NeoEditor.ViewModels;

public class MainWindowViewModel : ObservableRecipient
{
    public MainWindowViewModel(
        MenuViewModel menuVm,
        EditTableViewModel editTableVm,
        ProjectViewModel projectVm,
        LoggerViewModel loggerViewModel
    )
    {
        IsActive = true;
        MenuVm = menuVm;
        EditTableVm = editTableVm;
        ProjectVm = projectVm;
        LoggerVm = loggerViewModel;
    }
    
    public LoggerViewModel LoggerVm { get; set; }

    public MenuViewModel MenuVm { get; set; }
    public ProjectViewModel ProjectVm { get; set; }
    public EditTableViewModel EditTableVm { get; set; }
}