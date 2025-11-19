using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class IngredientTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(IngredientTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private IngredientTableViewModel? _vm;

    public IngredientTable()
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
        var control = (IngredientTable)d;
        Console.WriteLine(
            $"[IngredientTable] OnItemsSourceChanged called, NewValue type: {e.NewValue?.GetType().Name}");

        if (e.NewValue is ObservableCollection<object> objectItems)
        {
            Console.WriteLine($"[IngredientTable] Converting {objectItems.Count} objects to BaseDto");
            var baseDtoItems = new ObservableCollection<BaseDto>(objectItems.OfType<BaseDto>());
            Console.WriteLine($"[IngredientTable] Converted to {baseDtoItems.Count} BaseDto items");
            control._vm = new IngredientTableViewModel(baseDtoItems);
            control.DataContext = control._vm;
            Console.WriteLine("[IngredientTable] ViewModel created and DataContext set");
        }
        else if (e.NewValue is ObservableCollection<BaseDto> items)
        {
            Console.WriteLine($"[IngredientTable] Direct BaseDto collection with {items.Count} items");
            control._vm = new IngredientTableViewModel(items);
            control.DataContext = control._vm;
        }
        else
        {
            Console.WriteLine("[IngredientTable] Unexpected type, cannot create ViewModel");
        }
    }
}