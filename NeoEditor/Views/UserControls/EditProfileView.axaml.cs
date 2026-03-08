using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;

namespace NeoEditor.Views.UserControls;

public partial class EditProfileView : UserControl
{
    private LocalizationService Loc { get; set; }

    public EditProfileView()
    {
        InitializeComponent();
        Loc = App.ServiceProvider.GetRequiredService<LocalizationService>();
    }
}