using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.ViewModels.Controls;

namespace NeoEditor.Views.Controls;

public partial class EditTablePage : UserControl
{
    public EditTablePage()
    {
        DataContext = App.Services.GetService<EditTableViewModel>();
        InitializeComponent();
    }
}