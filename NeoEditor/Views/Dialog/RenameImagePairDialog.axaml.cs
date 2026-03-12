using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.ViewModels.Dialog;

namespace NeoEditor.Views.Dialog;

public partial class RenameImagePairDialog : Window
{
    public RenameImagePairDialog() : this(App.ServiceProvider.GetRequiredService<RenameImagePairDialogViewModel>())
    {
    }

    public RenameImagePairDialogViewModel ViewModel => (RenameImagePairDialogViewModel)DataContext!;

    public RenameImagePairDialog(RenameImagePairDialogViewModel viewModel)
    {
        InitializeComponent();
        viewModel.CloseRequested += (_, _) => Close();
        DataContext = viewModel;
    }
}
