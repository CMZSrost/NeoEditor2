using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NeoEditor.Assets;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.ViewModels;

namespace NeoEditor.Views.UserControls;

public partial class SearchableDataGrid : UserControl
{
    public static readonly DirectProperty<SearchableDataGrid, IDataGridCollectionView?> FilteredViewProperty =
        AvaloniaProperty.RegisterDirect<SearchableDataGrid, IDataGridCollectionView?>(
            nameof(FilteredView),
            o => o.FilteredView,
            (o, v) => o.FilteredView = v);

    private IDataGridCollectionView? _filteredView;

    // 过滤后的视图（作为内部属性，供 XAML 绑定）
    public IDataGridCollectionView? FilteredView
    {
        get => _filteredView;
        private set => SetAndRaise(FilteredViewProperty, ref _filteredView, value);
    }

    public IEnumerable ItemsSource  // ObservableCollection<object>不行，必须IEnumerable，否则无法绑定到DataGrid
    {
        get;
        set => SetAndRaise(ItemsSourceProperty, ref field, value);
    }


    private LocalizationService Loc;
    private readonly ILogger<SearchableDataGrid> _logger;

    public static readonly DirectProperty<SearchableDataGrid, IEnumerable> ItemsSourceProperty =
        AvaloniaProperty.RegisterDirect<SearchableDataGrid, IEnumerable>(nameof(ItemsSource),
            o => o.ItemsSource, (o, v) => o.ItemsSource = v);

    public SearchableDataGrid()
    {
        InitializeComponent();
        Console.WriteLine($"start");
        Loc = App.ServiceProvider!.GetRequiredService<LocalizationService>();
        _logger = App.ServiceProvider!.GetRequiredService<ILogger<SearchableDataGrid>>();
        // DataContext = App.ServiceProvider!.GetRequiredService<SearchableDataGridViewModel>();
        // ItemsSource = [new object(){ModId = 1, Name = "Sample Var", Value = "123", Type = "int"}];
        Console.WriteLine($"end");
    }

    private void RefreshView()
    {
        if (ItemsSource == null)
        {
            FilteredView = null;
            return;
        }

        var view = new DataGridCollectionView(ItemsSource);

        // 设置过滤条件
        view.Filter = FilterItem;

        FilteredView = view;
    }

    private bool FilterItem(object item)
    {
        if (DataContext is SearchableDataGridViewModel { FilterText: { } filterText })
        {
            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            // 这里实现你的过滤逻辑，例如检查每个字符串属性是否包含 SearchText
            // 由于不知道数据类型，可以通过反射或约定进行
            // 示例：假设每个数据项都有 ToString() 方法
            return item?.ToString()?.Contains(filterText, StringComparison.OrdinalIgnoreCase) == true;
        }
        else
        {
            return true;
        }
    }

    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // 通过反射获取 ViewModel 的真实类型参数 T
        _logger.LogInformation($"vm检");
        var vm = DataContext;
        _logger.LogInformation($"vm检查，类型为 {vm?.GetType()}");
        if (vm == null) return;
        _logger.LogInformation($"vm 存在，类型为 {vm.GetType()}");

        var vmType = vm.GetType();
        _logger.LogInformation($"vmType {vmType} is a SearchableDataGridViewModel<>");
        if (!vmType.IsGenericType || vmType.GetGenericTypeDefinition() != typeof(SearchableDataGridViewModel))
            return;


        var itemType = vmType.GetGenericArguments()[0];
        // 调用泛型方法 ConfigureColumn
        // 假设 DataContext 中有一个 Loc 属性（您需要确保 ViewModel 中有此属性）
        var locProperty = vmType.GetProperty("Loc");
        var loc = locProperty?.GetValue(vm);
        if (loc != null)
        {
            GenericDataGridHelper.ConfigureColumn(null, key => Loc[key], vmType);
        }
    }
}