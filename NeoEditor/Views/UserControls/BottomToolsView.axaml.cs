using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Helper;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class BottomToolsView : UserControl
{
    private BottomToolsViewModel? _vm;

    public BottomToolsView() => InitializeComponent();

    public IRelayCommand<string> SearchRecentTyped =>
        ((BottomToolsViewModel?)DataContext)?.SearchRecentCommand!;

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        _vm = DataContext as BottomToolsViewModel;
    }

    private async void OnBottomSearchClick(object? sender, RoutedEventArgs e)
    {
        if (_vm?.BottomSearchCommand is { } cmd && cmd.CanExecute(null))
            cmd.Execute(null);
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

    private void OnRefreshConflictsClick(object? sender, RoutedEventArgs e) => _vm?.LoadConflicts();

    private void OnRunValidationClick(object? sender, RoutedEventArgs e)
        => App.ServiceProvider!.GetRequiredService<IMessenger>().Send(new RequestValidationMessage());
}
