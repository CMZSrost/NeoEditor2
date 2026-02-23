using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NeoEditor.Assets;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.ViewModels;

namespace NeoEditor.Views.UserControls;

public partial class SearchableDataGrid : UserControl
{
    private LocalizationService Loc;
    private readonly ILogger<SearchableDataGrid> _logger;

    public SearchableDataGrid()
    {
        InitializeComponent();
        Loc = App.ServiceProvider!.GetRequiredService<LocalizationService>();
        _logger = App.ServiceProvider!.GetRequiredService<ILogger<SearchableDataGrid>>();
    }

    // 此控件不定义 ItemsSource 依赖属性，完全依赖 DataContext 中的 ViewModel
    // 但如果希望从外部直接设置 ItemsSource，可以定义一个依赖属性并传递给 DataContext
    // 这里为了简化，要求外部直接设置 DataContext 为对应的 SearchableDataGridViewModel<T>

    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // 通过反射获取 ViewModel 的真实类型参数 T
        var vm = DataContext;
        if (vm == null) return;
        _logger.LogInformation($"vm 存在，类型为 {vm.GetType()}");

        var vmType = vm.GetType();
        if (!vmType.IsGenericType || vmType.GetGenericTypeDefinition() != typeof(SearchableDataGridViewModel<>))
            return;

        _logger.LogInformation($"vmType {vmType} is a SearchableDataGridViewModel<>");

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