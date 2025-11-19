using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class ForbiddenhexTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(ForbiddenhexTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private ForbiddenhexTableViewModel? _vm;

    public ForbiddenhexTable()
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
        var control = (ForbiddenhexTable)d;
        Console.WriteLine($"[ForbiddenhexTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");
        
        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[ForbiddenhexTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[ForbiddenhexTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new ForbiddenhexTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine($"[ForbiddenhexTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[ForbiddenhexTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new ForbiddenhexTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine($"[ForbiddenhexTable] Unexpected type, cannot create ViewModel");
        }
    }
}