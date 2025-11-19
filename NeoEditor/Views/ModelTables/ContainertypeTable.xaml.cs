using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class ContainertypeTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(ContainertypeTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private ContainertypeTableViewModel? _vm;

    public ContainertypeTable()
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
        var control = (ContainertypeTable)d;
        Console.WriteLine($"[ContainertypeTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[ContainertypeTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[ContainertypeTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new ContainertypeTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[ContainertypeTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[ContainertypeTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new ContainertypeTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[ContainertypeTable] Unexpected type, cannot create ViewModel");
        }
    }
}