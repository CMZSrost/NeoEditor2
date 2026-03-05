using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.ViewModels.Dialog;

namespace NeoEditor.Views.Dialog;

public partial class CreateModDialog : Window
{
    public CreateModDialog() : this(App.ServiceProvider.GetRequiredService<CreateModDialogViewModel>())
    {
        InitializeComponent();
    }

    public CreateModDialog(CreateModDialogViewModel viewModel)
    {
        InitializeComponent();
        viewModel.CloseRequested += (s, e) => Close();
        DataContext = viewModel;
    }
}