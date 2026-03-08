using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace NeoEditor.Helper.Behaviors;

public class ContextMenuPlacementTargetBehavior : Behavior<Control>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject == null)
        {
            return;
        }

        AssociatedObject.PropertyChanged += OnAssociatedObjectPropertyChanged;
        AssociatedObject.PointerPressed += OnAssociatedObjectPointerPressed;
        UpdatePlacementTarget();
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PropertyChanged -= OnAssociatedObjectPropertyChanged;
            AssociatedObject.PointerPressed -= OnAssociatedObjectPointerPressed;
        }

        base.OnDetaching();
    }

    private void OnAssociatedObjectPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Control.ContextMenuProperty)
        {
            UpdatePlacementTarget();
        }
    }

    private void OnAssociatedObjectPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (AssociatedObject == null)
        {
            return;
        }

        var point = e.GetCurrentPoint(AssociatedObject);
        if (point.Properties.IsRightButtonPressed)
        {
            UpdatePlacementTarget();
        }
    }

    private void UpdatePlacementTarget()
    {
        if (AssociatedObject?.ContextMenu == null)
        {
            return;
        }

        AssociatedObject.ContextMenu.PlacementTarget = AssociatedObject;
    }
}