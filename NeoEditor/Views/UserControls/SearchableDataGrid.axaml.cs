using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.Views.UserControls;

public partial class SearchableDataGrid : UserControl
{
    public LocalizationService Loc { get; }
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
        get { return GetValue(FilterTextProperty); }
        set { SetValue(FilterTextProperty, value); }
    }

    public bool ReadOnly
    {
        get { return GetValue(ReadOnlyProperty); }
        set { SetValue(ReadOnlyProperty, value); }
    }

    public static readonly StyledProperty<string?> FilterTextProperty =
        AvaloniaProperty.Register<SearchableDataGrid, string?>("FilterText");

    public SearchableDataGrid()
    {
        InitializeComponent();
        Loc = App.ServiceProvider.GetRequiredService<LocalizationService>();
    }

    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (ItemsSource == null) return;

        Type? itemType = null;
        var enumerableType = ItemsSource.GetType()
            .GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType != null)
        {
            itemType = enumerableType.GetGenericArguments()[0];
            GenericDataGridHelper.ConfigureColumn(e, key => App.Localizor[key] ?? key, itemType);
        }
        else
        {
            var enumerator = ItemsSource.GetEnumerator();
            using var enumerator1 = enumerator as IDisposable;
            if (enumerator.MoveNext())
            {
                itemType = enumerator.Current?.GetType();
            }

            if (itemType != null)
            {
                GenericDataGridHelper.ConfigureColumn(e, key => App.Localizor[key] ?? key, itemType);
            }
        }
    }
}