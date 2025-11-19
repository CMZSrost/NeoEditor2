using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NeoEditor.Data.Models.Dto;
using NeoEditor.Views.ModelTables;

// for GenericEditableTable

namespace NeoEditor.Views;

/// <summary>
///     Polymorphic host that resolves the correct table UserControl by convention ("{TableName}Table")
///     and falls back to <see cref="GenericEditableTable" />. This removes the need for dozens of XAML DataTemplates.
/// </summary>
public class TableHostControl : ContentControl
{
    public static readonly DependencyProperty TableNameProperty = DependencyProperty.Register(
        nameof(TableName), typeof(string), typeof(TableHostControl),
        new PropertyMetadata(string.Empty, OnAnyPropertyChanged));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(ObservableCollection<object>), typeof(TableHostControl),
        new PropertyMetadata(null, OnAnyPropertyChanged));

    public static readonly DependencyProperty DtoTypeProperty = DependencyProperty.Register(
        nameof(DtoType), typeof(Type), typeof(TableHostControl), new PropertyMetadata(null, OnAnyPropertyChanged));

    public string TableName
    {
        get => (string)GetValue(TableNameProperty);
        set => SetValue(TableNameProperty, value);
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

    private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TableHostControl)d).RecreateChild();
    }

    private void RecreateChild()
    {
        if (ItemsSource == null || (DtoType == null && string.IsNullOrWhiteSpace(TableName)))
        {
            Content = null;
            return;
        }

        // Prefer explicit TableName; fall back to DtoType.Name.
        var name = !string.IsNullOrWhiteSpace(TableName) ? TableName : DtoType!.Name;
        var controlTypeName = $"NeoEditor.Views.ModelTables.{name}Table"; // e.g. AttackModeTable
        var controlType = Type.GetType(controlTypeName);

        // If not found, attempt case-insensitive match among loaded types in current assembly.
        if (controlType == null)
        {
            var asm = typeof(TableHostControl).Assembly;
            controlType = asm.GetTypes().FirstOrDefault(t =>
                t.IsSubclassOf(typeof(UserControl)) && t.Namespace == "NeoEditor.Views.ModelTables" &&
                t.Name.Equals(name + "Table", StringComparison.OrdinalIgnoreCase));
        }

        UserControl child;
        if (controlType != null && controlType != typeof(GenericEditableTable))
        {
            // Instantiate specialized table
            child = (UserControl)Activator.CreateInstance(controlType)!;
            // Try set ItemsSource property if exists
            var prop = controlType.GetProperty("ItemsSource");
            if (prop != null)
            {
                // Convert if necessary
                var targetType = prop.PropertyType;
                object valueToAssign = ItemsSource;
                if (targetType.IsGenericType && targetType.GetGenericArguments().Length == 1)
                {
                    var genericArg = targetType.GetGenericArguments()[0];
                    if (genericArg != typeof(object) && genericArg.IsAssignableFrom(typeof(BaseDto)))
                    {
                        // Filter only BaseDto
                        var baseDtos = new ObservableCollection<BaseDto>(ItemsSource.OfType<BaseDto>());
                        valueToAssign = baseDtos;
                    }
                }

                prop.SetValue(child, valueToAssign);
            }
        }
        else
        {
            // Fallback to generic table
            var baseDtos = new ObservableCollection<BaseDto>(ItemsSource.OfType<BaseDto>());
            child = new GenericEditableTable
            {
                DtoType = DtoType,
                ItemsSource = baseDtos
            };
        }

        Content = child;
    }
}