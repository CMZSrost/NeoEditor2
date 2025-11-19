using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class EncounterTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(EncounterTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private EncounterTableViewModel? _vm;

    public EncounterTable()
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
        var control = (EncounterTable)d;
        Console.WriteLine($"[EncounterTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");

        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[EncounterTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[EncounterTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new EncounterTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine("[EncounterTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[EncounterTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new EncounterTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine("[EncounterTable] Unexpected type, cannot create ViewModel");
        }
    }
}