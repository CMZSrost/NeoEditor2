using Avalonia.Controls;
using Avalonia.Input;
using NeoEditor.Helper;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Plugins.DataViewer.ViewModels;

namespace NeoEditor.Plugins.DataViewer.Views;

public partial class SearchResultsView : UserControl
{
    /// <summary>
    /// Injectable navigation router. Falls back to Application.Current DI if not set.
    /// </summary>
    public INavigationRouter? NavigationRouter { get; set; }

    private SearchResultViewModel? _vm;

    public SearchResultsView() => InitializeComponent();

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        _vm = DataContext as SearchResultViewModel;
    }

    private void OnSearchResultPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
        if (sender is not Control ctrl || ctrl.DataContext is not SearchResultItem item) return;
        e.Handled = true;

        var point = e.GetCurrentPoint(ctrl);
        if (point.Properties.IsRightButtonPressed)
        {
            var nav = NavigationRouter
                ?? (Avalonia.Application.Current?.Resources["Services"] as System.IServiceProvider)
                    ?.GetService(typeof(INavigationRouter)) as INavigationRouter;
            nav?.RequestPeek(item.EntityType, item.Entity.EntityId, item.Entity);
        }
        else
            _vm?.NavigateToResult(item);
    }

    private void OnSearchResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control ctrl || ctrl.DataContext is not SearchResultItem item) return;
        _vm?.NavigateToResult(item);
    }
}
