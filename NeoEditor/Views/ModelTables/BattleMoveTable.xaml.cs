using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class BattleMoveTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(BattleMoveTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private BattleMoveTableViewModel? _vm;

    public BattleMoveTable()
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
        var control = (BattleMoveTable)d;
        Console.WriteLine(
            $"[BattleMoveTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");

        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[BattleMoveTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[BattleMoveTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new BattleMoveTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine("[BattleMoveTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[BattleMoveTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new BattleMoveTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine("[BattleMoveTable] Unexpected type, cannot create ViewModel");
        }
    }
}