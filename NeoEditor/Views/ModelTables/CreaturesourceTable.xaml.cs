using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class CreaturesourceTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(CreaturesourceTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private CreaturesourceTableViewModel? _vm;

    public CreaturesourceTable()
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
        var control = (CreaturesourceTable)d;
        Console.WriteLine($"[CreaturesourceTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[CreaturesourceTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[CreaturesourceTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new CreaturesourceTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[CreaturesourceTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[CreaturesourceTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new CreaturesourceTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[CreaturesourceTable] Unexpected type, cannot create ViewModel");
        }
    }
}