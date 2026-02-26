using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model;
using NeoEditor.ViewModels;

namespace NeoEditor.Views;

public partial class EditProfileWindow : Window
{
    public EditProfileWindow() : this(
        App.ServiceProvider!.GetRequiredService<EditProfileWindowViewModel>(), null)
    {
        InitializeComponent();
    }

    public EditProfileWindow(ProfileInfo profileInfo) : this(
        App.ServiceProvider!.GetRequiredService<EditProfileWindowViewModel>(), profileInfo)
    {
        InitializeComponent();
    }

    public EditProfileWindow(EditProfileWindowViewModel vm, ProfileInfo? profileInfo)
    {
        vm.CloseRequested += (sender, args) => Close();

        if (Design.IsDesignMode)
            profileInfo = new ProfileInfo();
        vm.ProfileInfo = profileInfo;

        DataContext = vm;
        vm.LoadEntries();
    }
}