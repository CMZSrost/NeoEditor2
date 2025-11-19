using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class TreasuretableTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(TreasuretableTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private TreasuretableTableViewModel? _vm;

    public TreasuretableTable()
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
        var control = (TreasuretableTable)d;
        Console.WriteLine($"[TreasuretableTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[TreasuretableTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[TreasuretableTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new TreasuretableTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[TreasuretableTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[TreasuretableTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new TreasuretableTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[TreasuretableTable] Unexpected type, cannot create ViewModel");
        }
    }
}