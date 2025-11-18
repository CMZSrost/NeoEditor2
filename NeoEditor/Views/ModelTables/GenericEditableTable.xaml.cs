using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.ViewModels.ModelTables;

namespace NeoEditor.Views.ModelTables;

public partial class GenericEditableTable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(GenericEditableTable),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty DtoTypeProperty = DependencyProperty.Register(
        nameof(DtoType), typeof(Type), typeof(GenericEditableTable),
        new PropertyMetadata(null, OnDtoTypeChanged));

    private ReflectionTableViewModel? _vm;

    public GenericEditableTable()
    {
        InitializeComponent();
    }

    public ObservableCollection<object>? ItemsSource
    {
        get => (ObservableCollection<object>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public Type? DtoType
    {
        get => (Type?)GetValue(DtoTypeProperty);
        set => SetValue(DtoTypeProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (GenericEditableTable)d;
        control.TryInitViewModel();
    }

    private static void OnDtoTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (GenericEditableTable)d;
        control.TryInitViewModel();
    }

    private void TryInitViewModel()
    {
        if (ItemsSource == null || DtoType == null) return;
        _vm = new ReflectionTableViewModel(ItemsSource, DtoType);
        DataContext = _vm;
    }
}
