using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class DmcplaceTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(DmcplaceTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private DmcplaceTableViewModel? _vm;

    public DmcplaceTable()
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
        var control = (DmcplaceTable)d;
        Console.WriteLine($"[DmcplaceTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[DmcplaceTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[DmcplaceTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new DmcplaceTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[DmcplaceTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[DmcplaceTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new DmcplaceTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[DmcplaceTable] Unexpected type, cannot create ViewModel");
        }
    }
}