using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.ViewModels.Dialog;
using NeoEditor.Helper;

namespace NeoEditor.Views.Dialog;

public partial class RenameImagePairDialog : Window
{
    /// <summary>Static factory (Q7=C). Preferred entry point for runtime callers.</summary>
    public static RenameImagePairDialog Create(IServiceProvider sp)
        => new(sp.GetRequiredService<RenameImagePairDialogViewModel>());

    /// <summary>Parameterless ctor for AXAML preview only. Runtime callers use <see cref="Create"/>.</summary>
    /// <remarks>Framework exemption: Avalonia XAML loader requires a parameterless constructor.
    /// App.ServiceProvider is the only way to resolve VM deps from a parameterless ctor.
    /// All runtime code paths use the <see cref="Create"/> factory method instead.</remarks>
    public RenameImagePairDialog() : this(ViewServices.Get<RenameImagePairDialogViewModel>())
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
