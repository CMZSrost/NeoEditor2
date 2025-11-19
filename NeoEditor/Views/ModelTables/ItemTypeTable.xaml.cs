using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class ItemTypeTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(ItemTypeTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private ItemTypeTableViewModel? _vm;

    public ItemTypeTable()
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
        var control = (ItemTypeTable)d;
        Console.WriteLine($"[ItemTypeTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");

        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[ItemTypeTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[ItemTypeTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new ItemTypeTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine("[ItemTypeTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[ItemTypeTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new ItemTypeTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine("[ItemTypeTable] Unexpected type, cannot create ViewModel");
        }
    }
}