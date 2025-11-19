using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class MapTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(MapTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private MapTableViewModel? _vm;

    public MapTable()
    {
        InitializeComponent();
    }

    public ObservableCollection<object>? ItemsSource
    {
        get => (ObservableCollection<object>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MapTable)d;
        Console.WriteLine($"[MapTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[MapTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[MapTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new MapTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[MapTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[MapTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new MapTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[MapTable] Unexpected type, cannot create ViewModel");
        }
    }
}