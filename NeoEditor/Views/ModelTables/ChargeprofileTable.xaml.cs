using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class ChargeprofileTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(ChargeprofileTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private ChargeprofileTableViewModel? _vm;

    public ChargeprofileTable()
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
        var control = (ChargeprofileTable)d;
        Console.WriteLine($"[ChargeprofileTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[ChargeprofileTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[ChargeprofileTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new ChargeprofileTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[ChargeprofileTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[ChargeprofileTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new ChargeprofileTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[ChargeprofileTable] Unexpected type, cannot create ViewModel");
        }
    }
}