using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class SearchResultsView : UserControl
{
    public LocalizationService Loc => App.Localizor;
    private BottomToolsViewModel? _vm;

    public SearchResultsView() => InitializeComponent();

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        _vm = DataContext as BottomToolsViewModel;
    }

    private void OnSearchResultPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
        if (sender is not Control ctrl || ctrl.DataContext is not SearchResultItem item) return;
        e.Handled = true;

        var point = e.GetCurrentPoint(ctrl);
        if (point.Properties.IsRightButtonPressed)
            App.ServiceProvider!.GetRequiredService<IMessenger>().Send(new PeekRequestedMessage(item.EntityType, item.Entity.EntityId, item.Entity));
        else
            _vm?.NavigateToResult(item);
    }

    private void OnSearchResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control ctrl || ctrl.DataContext is not SearchResultItem item) return;
        _vm?.NavigateToResult(item);
    }
}
