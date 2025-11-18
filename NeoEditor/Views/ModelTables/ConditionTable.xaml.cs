using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class ConditionTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(ConditionTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private ConditionTableViewModel? _vm;

    public ConditionTable()
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
        var control = (ConditionTable)d;
        if (e.NewValue is ObservableCollection<object> items)
        {
            control._vm = new ConditionTableViewModel(items);
            control.DataContext = control._vm;
        }
    }
}
