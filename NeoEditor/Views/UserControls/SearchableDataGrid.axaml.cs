using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
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
    private LocalizationService Loc;
    private readonly ILogger<SearchableDataGrid> _logger;
    public static readonly StyledProperty<bool> ReadOnlyProperty = AvaloniaProperty.Register<SearchableDataGrid, bool>("ReadOnly");

    public IEnumerable? ItemsSource // ObservableCollection<object>不行，必须IEnumerable，否则无法绑定到DataGrid
    {
        get;
        set => SetAndRaise(ItemsSourceProperty, ref field, value);
    }

    public static readonly DirectProperty<SearchableDataGrid, IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.RegisterDirect<SearchableDataGrid, IEnumerable?>(nameof(ItemsSource),
            o => o.ItemsSource, (o, v) => o.ItemsSource = v);


    public string? FilterText
    {
        get { return (string?)GetValue(FilterTextProperty); }
        set { SetValue(FilterTextProperty, value); }
    }

    public bool ReadOnly
    {
        get { return (bool)GetValue(ReadOnlyProperty); }
        set { SetValue(ReadOnlyProperty, value); }
    }


    public static readonly StyledProperty<string?> FilterTextProperty =
        AvaloniaProperty.Register<SearchableDataGrid, string?>("FilterText");

    public SearchableDataGrid()
    {
        InitializeComponent();
        Loc = App.ServiceProvider!.GetRequiredService<LocalizationService>();
        _logger = App.ServiceProvider!.GetRequiredService<ILogger<SearchableDataGrid>>();
    }

    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // 检查 itemsSource里的数据类型
        if (ItemsSource == null) return;

        // 获取 ItemsSource 中元素的类型
        Type? itemType = null;

        // 尝试从 IEnumerable<T> 获取泛型类型
        var enumerableType = ItemsSource.GetType()
            .GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType != null)
        {
            itemType = enumerableType.GetGenericArguments()[0];
            // 调用泛型方法 ConfigureColumn
            GenericDataGridHelper.ConfigureColumn(e, key => App.Localizor?[key] ?? key, itemType);
        }
        else
        {
            // 如果不是泛型IEnumerable，尝试从第一个元素获取类型
            var enumerator = ItemsSource.GetEnumerator();
            using var enumerator1 = enumerator as IDisposable;
            if (enumerator.MoveNext())
            {
                itemType = enumerator.Current?.GetType();
            }

            // 调用泛型方法 ConfigureColumn
            if (itemType != null)
            {
                GenericDataGridHelper.ConfigureColumn(e, key => App.Localizor?[key] ?? key, itemType);
            }
        }
    }
}