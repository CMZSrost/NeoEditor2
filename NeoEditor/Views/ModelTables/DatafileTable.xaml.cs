using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class DatafileTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(DatafileTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private DatafileTableViewModel? _vm;

    public DatafileTable()
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
        var control = (DatafileTable)d;
        Console.WriteLine($"[DatafileTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[DatafileTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[DatafileTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new DatafileTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[DatafileTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[DatafileTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new DatafileTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[DatafileTable] Unexpected type, cannot create ViewModel");
        }
    }
}