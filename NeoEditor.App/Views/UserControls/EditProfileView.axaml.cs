using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls;

public partial class EditProfileView : UserControl
{
    private ILocalizationService Loc { get; set; }

    public EditProfileView()
    {
        InitializeComponent();
        Loc = ViewServices.Loc;
    }
}