using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class EncountertriggerTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(EncountertriggerTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private EncountertriggerTableViewModel? _vm;

    public EncountertriggerTable()
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
        var control = (EncountertriggerTable)d;
        Console.WriteLine($"[EncountertriggerTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[EncountertriggerTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[EncountertriggerTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new EncountertriggerTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[EncountertriggerTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[EncountertriggerTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new EncountertriggerTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[EncountertriggerTable] Unexpected type, cannot create ViewModel");
        }
    }
}