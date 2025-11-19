using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class ConditionTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(ConditionTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private ConditionTableViewModel? _vm;

    public ConditionTable()
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
        var control = (ConditionTable)d;
        Console.WriteLine($"[ConditionTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");

        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[ConditionTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[ConditionTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new ConditionTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine("[ConditionTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[ConditionTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new ConditionTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine("[ConditionTable] Unexpected type, cannot create ViewModel");
        }
    }
}