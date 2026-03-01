using System;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model;
using NeoEditor.ViewModels;
using System.Linq;
using Avalonia.Input;
using NeoEditor.Helper.DragDropHandler;

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

        vm.ProfileInfo = profileInfo;

        DataContext = vm;
        vm.LoadEntries();
    }

    private void OnEntriesLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        var behaviors = Interaction.GetBehaviors(e.Row);
        if (!behaviors.Any(b => b is ContextDragBehavior))
        {
            behaviors.Add(new ContextDragBehavior());
        }
    }

    private void InputElement_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        throw new NotImplementedException();
    }
}