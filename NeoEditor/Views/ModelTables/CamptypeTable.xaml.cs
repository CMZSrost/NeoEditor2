using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class CamptypeTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(CamptypeTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private CamptypeTableViewModel? _vm;

    public CamptypeTable()
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
        var control = (CamptypeTable)d;
        Console.WriteLine($"[CamptypeTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[CamptypeTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[CamptypeTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new CamptypeTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[CamptypeTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[CamptypeTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new CamptypeTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[CamptypeTable] Unexpected type, cannot create ViewModel");
        }
    }
}