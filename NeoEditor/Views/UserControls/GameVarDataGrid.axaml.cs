using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model.Game;
using NeoEditor.ViewModels;

namespace NeoEditor.Views.UserControls;

public partial class GameVarDataGrid : UserControl
{
    public GameVarDataGrid()
    {
        InitializeComponent();
        // 创建对应类型的 ViewModel 并设置为内部控件的 DataContext
        InnerGrid.DataContext = App.ServiceProvider!.GetRequiredService<SearchableDataGridViewModel<GameVar>>();
    }

    // 定义 ItemsSource 依赖属性，供外部绑定
    public static readonly DirectProperty<GameVarDataGrid, IEnumerable<GameVar>?> ItemsSourceProperty =
        AvaloniaProperty.RegisterDirect<GameVarDataGrid, IEnumerable<GameVar>?>(
            nameof(ItemsSource),
            o => o.ItemsSource,
            (o, v) => o.ItemsSource = v);

    private IEnumerable<GameVar>? _itemsSource;

    public IEnumerable<GameVar>? ItemsSource
    {
        get => _itemsSource;
        set => SetAndRaise(ItemsSourceProperty, ref _itemsSource, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            // 将 ItemsSource 传递给内部 ViewModel
            if (InnerGrid.DataContext is SearchableDataGridViewModel<GameVar> vm)
            {
                vm.ItemsSource = ItemsSource;
            }
        }
    }
}