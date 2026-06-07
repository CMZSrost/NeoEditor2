using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.ViewModels.ExplorerPane;

namespace NeoEditor.Views.UserControls;

public partial class DataBrowserView : UserControl
{
    public DataBrowserView() => InitializeComponent();

    public IRelayCommand<NeoEditor.Helper.EntityTypeGroup?> OpenEntityTypeTyped =>
        ((DataBrowserViewModel?)DataContext)?.OpenEntityTypeCommand!;
}
