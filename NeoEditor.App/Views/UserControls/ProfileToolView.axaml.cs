using Avalonia.Controls;
using Avalonia.Input;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class ProfileToolView : UserControl
{
    public ProfileToolView()
    {
        InitializeComponent();
    }

    /// <summary>Double-click an XML row → open the read-only XML document (R04: input → VM command).</summary>
    private void OnDataGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ProfileToolViewModel vm)
            vm.OpenXmlCommand.Execute(null);
    }
}