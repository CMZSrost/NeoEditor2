using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class HextypeTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(HextypeTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private HextypeTableViewModel? _vm;

    public HextypeTable()
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
        var control = (HextypeTable)d;
        Console.WriteLine($"[HextypeTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");

        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[HextypeTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[HextypeTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new HextypeTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine("[HextypeTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[HextypeTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new HextypeTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine("[HextypeTable] Unexpected type, cannot create ViewModel");
        }
    }
}