using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        AssociatedObject.AddHandler(InputElement.PointerPressedEvent, OnAssociatedObjectPointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PropertyChanged -= OnAssociatedObjectPropertyChanged;
            AssociatedObject.RemoveHandler(InputElement.PointerPressedEvent, OnAssociatedObjectPointerPressed);
        }

        base.OnDetaching();
    }

    private void OnAssociatedObjectPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Control.ContextMenuProperty)
        {
            ResetPlacementTarget();
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

    private void ResetPlacementTarget()
    {
        if (AssociatedObject?.ContextMenu == null)
        {
            return;
        }

        AssociatedObject.ContextMenu.PlacementTarget = null;
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