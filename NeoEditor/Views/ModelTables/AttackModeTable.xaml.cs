using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class AttackModeTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(AttackModeTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    private AttackModeTableViewModel? _vm;

    public AttackModeTable()
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
        var control = (AttackModeTable)d;
        if (e.NewValue is ObservableCollection<object> items)
        {
            control._vm = new AttackModeTableViewModel(items);
            control.DataContext = control._vm;
        }
    }
}