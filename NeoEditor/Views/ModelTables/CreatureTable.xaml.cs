using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class CreatureTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(CreatureTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private CreatureTableViewModel? _vm;

    public CreatureTable()
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
        var control = (CreatureTable)d;
        Console.WriteLine($"[CreatureTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");

        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[CreatureTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[CreatureTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new CreatureTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine("[CreatureTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[CreatureTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new CreatureTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine("[CreatureTable] Unexpected type, cannot create ViewModel");
        }
    }
}