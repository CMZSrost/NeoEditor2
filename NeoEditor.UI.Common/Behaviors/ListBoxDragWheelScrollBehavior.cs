using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace NeoEditor.Helper.Behaviors;

public class ListBoxDragWheelScrollBehavior : Behavior<ListBox>
{
    private const double DefaultAutoScrollMargin = 32d;
    private const double DefaultAutoScrollStep = 20d;

    private TopLevel? _topLevel;
    private DispatcherTimer? _autoScrollTimer;
    private ScrollViewer? _scrollViewer;
    private Point _lastDragPosition;
    private bool _isDragActive;
    private bool _isWheelHandlerAttached;

    public double WheelScrollAmount { get; set; } = 48d;
    public double AutoScrollMargin { get; set; } = DefaultAutoScrollMargin;
    public double AutoScrollStep { get; set; } = DefaultAutoScrollStep;

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is null)
        {
            return;
        }

        AssociatedObject.AddHandler(DragDrop.DragEnterEvent, OnDragEvent, handledEventsToo: true);
        AssociatedObject.AddHandler(DragDrop.DragOverEvent, OnDragEvent, handledEventsToo: true);
        AssociatedObject.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, handledEventsToo: true);
        AssociatedObject.AddHandler(DragDrop.DropEvent, OnDrop, handledEventsToo: true);
        AssociatedObject.AttachedToVisualTree += OnAttachedToVisualTree;
        AssociatedObject.DetachedFromVisualTree += OnDetachedFromVisualTree;
        _topLevel = TopLevel.GetTopLevel(AssociatedObject);
        EnsureAutoScrollTimer();
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.RemoveHandler(DragDrop.DragEnterEvent, OnDragEvent);
            AssociatedObject.RemoveHandler(DragDrop.DragOverEvent, OnDragEvent);
            AssociatedObject.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            AssociatedObject.RemoveHandler(DragDrop.DropEvent, OnDrop);
            AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;
            AssociatedObject.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        }

        DetachWheelHandler();
        StopAutoScroll();
        _isDragActive = false;
        _isWheelHandlerAttached = false;
        _scrollViewer = null;
        base.OnDetaching();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _topLevel = e.Root as TopLevel ?? TopLevel.GetTopLevel(AssociatedObject);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;
        DetachWheelHandler();
        StopAutoScroll();
        _topLevel = null;
        _isDragActive = false;
        _isWheelHandlerAttached = false;
        _scrollViewer = null;
    }

    private void OnDragEvent(object? sender, DragEventArgs e)
    {
        _ = sender;
        if (AssociatedObject is null)
        {
            return;
        }

        _isDragActive = true;
        _lastDragPosition = e.GetPosition(AssociatedObject);
        _scrollViewer ??= FindScrollViewer(AssociatedObject);
        AttachWheelHandler();
        StartAutoScroll();
    }

    private void OnDragLeave(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ResetDragState();
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        _ = sender;
        _ = e;
        ResetDragState();
    }

    private void EnsureAutoScrollTimer()
    {
        _autoScrollTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };

        _autoScrollTimer.Tick -= OnAutoScrollTick;
        _autoScrollTimer.Tick += OnAutoScrollTick;
    }

    private void StartAutoScroll()
    {
        EnsureAutoScrollTimer();
        if (_autoScrollTimer is { IsEnabled: false })
        {
            _autoScrollTimer.Start();
        }
    }

    private void StopAutoScroll()
    {
        if (_autoScrollTimer is { IsEnabled: true })
        {
            _autoScrollTimer.Stop();
        }
    }

    private void ResetDragState()
    {
        _isDragActive = false;
        DetachWheelHandler();
        StopAutoScroll();
    }

    private void AttachWheelHandler()
    {
        if (_isWheelHandlerAttached)
        {
            return;
        }

        _topLevel ??= TopLevel.GetTopLevel(AssociatedObject);
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Bubble,
            handledEventsToo: true);
        _isWheelHandlerAttached = true;
    }

    private void DetachWheelHandler()
    {
        if (_topLevel is null || !_isWheelHandlerAttached)
        {
            return;
        }

        _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        _isWheelHandlerAttached = false;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _ = sender;
        if (!_isDragActive || AssociatedObject is null)
        {
            return;
        }

        var position = e.GetPosition(AssociatedObject);
        if (position.X < 0 || position.Y < 0 || position.X > AssociatedObject.Bounds.Width ||
            position.Y > AssociatedObject.Bounds.Height)
        {
            return;
        }

        _scrollViewer ??= FindScrollViewer(AssociatedObject);
        if (_scrollViewer is null)
        {
            return;
        }

        if (!TryScrollVertically(_scrollViewer, -e.Delta.Y * WheelScrollAmount))
        {
            return;
        }

        e.Handled = true;
    }

    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (!_isDragActive || AssociatedObject is null)
        {
            StopAutoScroll();
            return;
        }

        _scrollViewer ??= FindScrollViewer(AssociatedObject);
        if (_scrollViewer is null)
        {
            return;
        }

        var margin = Math.Clamp(AutoScrollMargin, 8d, Math.Max(8d, AssociatedObject.Bounds.Height / 2d));
        double delta = 0;
        if (_lastDragPosition.Y <= margin)
        {
            delta = -GetAutoScrollStep(_lastDragPosition.Y, margin);
        }
        else if (_lastDragPosition.Y >= AssociatedObject.Bounds.Height - margin)
        {
            delta = GetAutoScrollStep(AssociatedObject.Bounds.Height - _lastDragPosition.Y, margin);
        }

        if (Math.Abs(delta) < 0.1d)
        {
            return;
        }

        TryScrollVertically(_scrollViewer, delta);
    }

    private double GetAutoScrollStep(double distanceToEdge, double margin)
    {
        var normalized = 1d - Math.Clamp(distanceToEdge / margin, 0d, 1d);
        return Math.Max(4d, AutoScrollStep * normalized);
    }

    private static bool TryScrollVertically(ScrollViewer scrollViewer, double delta)
    {
        var maxOffsetY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        var nextOffsetY = Math.Clamp(scrollViewer.Offset.Y + delta, 0, maxOffsetY);
        if (Math.Abs(nextOffsetY - scrollViewer.Offset.Y) < 0.1d)
        {
            return false;
        }

        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, nextOffsetY);
        return true;
    }

    private static ScrollViewer? FindScrollViewer(ListBox listBox)
    {
        return listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    }
}


