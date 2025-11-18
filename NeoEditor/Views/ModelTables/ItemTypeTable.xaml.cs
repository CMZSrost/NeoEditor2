using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class ItemTypeTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(ItemTypeTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private ItemTypeTableViewModel? _vm;

    public ItemTypeTable()
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
        var control = (ItemTypeTable)d;
        if (e.NewValue is ObservableCollection<object> items)
        {
            control._vm = new ItemTypeTableViewModel(items);
            control.DataContext = control._vm;
        }
    }
}
