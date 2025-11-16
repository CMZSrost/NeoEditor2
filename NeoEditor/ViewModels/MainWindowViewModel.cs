using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.ViewModels.Controls;

namespace NeoEditor.ViewModels;

public class MainWindowViewModel : ObservableRecipient
{
    public MainWindowViewModel(
        MenuViewModel menuVm,
        EditTableViewModel editTableVm,
        ProjectViewModel projectVm,
        LoggerViewModel loggerViewModel,
        FileSystemViewModel fileSystemVm)
    {
        IsActive = true;
        MenuVm = menuVm;
        EditTableVm = editTableVm;
        ProjectVm = projectVm;
        LoggerVm = loggerViewModel;
        FileSystemVm = fileSystemVm;
    }

    public LoggerViewModel LoggerVm { get; set; }

    public MenuViewModel MenuVm { get; set; }
    public ProjectViewModel ProjectVm { get; set; }
    public FileSystemViewModel FileSystemVm { get; set; }
    public EditTableViewModel EditTableVm { get; set; }
}