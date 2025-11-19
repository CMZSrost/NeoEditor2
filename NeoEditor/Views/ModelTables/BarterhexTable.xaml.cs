using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class BarterhexTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(BarterhexTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private BarterhexTableViewModel? _vm;

    public BarterhexTable()
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
        var control = (BarterhexTable)d;
        Console.WriteLine($"[BarterhexTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[BarterhexTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[BarterhexTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new BarterhexTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[BarterhexTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[BarterhexTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new BarterhexTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[BarterhexTable] Unexpected type, cannot create ViewModel");
        }
    }
}