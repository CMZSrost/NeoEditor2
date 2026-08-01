using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace NeoEditor.Helper.AttachedProperties;

public sealed class ScrollViewerViewportOverviewAttached
{
    private sealed class OverviewState
    {
        public Control? Track { get; set; }
        public Control? ViewportThumb { get; set; }
        public EventHandler<AvaloniaPropertyChangedEventArgs>? ScrollViewerPropertyChangedHandler { get; set; }
        public EventHandler<SizeChangedEventArgs>? TrackSizeChangedHandler { get; set; }
        public EventHandler<PointerPressedEventArgs>? TrackPointerPressedHandler { get; set; }
        public EventHandler<PointerPressedEventArgs>? ThumbPointerPressedHandler { get; set; }
        public EventHandler<PointerEventArgs>? ThumbPointerMovedHandler { get; set; }
        public EventHandler<PointerReleasedEventArgs>? ThumbPointerReleasedHandler { get; set; }
        public EventHandler<PointerCaptureLostEventArgs>? ThumbPointerCaptureLostHandler { get; set; }
        public bool IsDragging { get; set; }
        public double DragStartPointerY { get; set; }
        public double DragStartOffsetY { get; set; }
    }

    private static readonly ConditionalWeakTable<ScrollViewer, OverviewState> States = new();

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewerViewportOverviewAttached, ScrollViewer, bool>("IsEnabled");

    public static readonly AttachedProperty<Control?> TrackProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewerViewportOverviewAttached, ScrollViewer, Control?>("Track");

    public static readonly AttachedProperty<Control?> ViewportThumbProperty =
        AvaloniaProperty
            .RegisterAttached<ScrollViewerViewportOverviewAttached, ScrollViewer, Control?>("ViewportThumb");

    public static readonly AttachedProperty<double> MinThumbHeightProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewerViewportOverviewAttached, ScrollViewer, double>("MinThumbHeight",
            12d);

    static ScrollViewerViewportOverviewAttached()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>(OnConfigurationChanged);
        TrackProperty.Changed.AddClassHandler<ScrollViewer>(OnConfigurationChanged);
        ViewportThumbProperty.Changed.AddClassHandler<ScrollViewer>(OnConfigurationChanged);
        MinThumbHeightProperty.Changed.AddClassHandler<ScrollViewer>(OnConfigurationChanged);
    }

    public static bool GetIsEnabled(AvaloniaObject element)
    {
        return element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(AvaloniaObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    public static Control? GetTrack(AvaloniaObject element)
    {
        return element.GetValue(TrackProperty);
    }

    public static void SetTrack(AvaloniaObject element, Control? value)
    {
        element.SetValue(TrackProperty, value);
    }

    public static Control? GetViewportThumb(AvaloniaObject element)
    {
        return element.GetValue(ViewportThumbProperty);
    }

    public static void SetViewportThumb(AvaloniaObject element, Control? value)
    {
        element.SetValue(ViewportThumbProperty, value);
    }

    public static double GetMinThumbHeight(AvaloniaObject element)
    {
        return element.GetValue(MinThumbHeightProperty);
    }

    public static void SetMinThumbHeight(AvaloniaObject element, double value)
    {
        element.SetValue(MinThumbHeightProperty, value);
    }

    private static void OnConfigurationChanged(ScrollViewer scrollViewer, AvaloniaPropertyChangedEventArgs e)
    {
        Unsubscribe(scrollViewer);

        if (!GetIsEnabled(scrollViewer) || GetTrack(scrollViewer) is null || GetViewportThumb(scrollViewer) is null)
        {
            return;
        }

        Subscribe(scrollViewer);
    }

    private static void Subscribe(ScrollViewer scrollViewer)
    {
        var state = States.GetOrCreateValue(scrollViewer);
        state.Track = GetTrack(scrollViewer);
        state.ViewportThumb = GetViewportThumb(scrollViewer);
        if (state.Track == null || state.ViewportThumb == null)
        {
            return;
        }

        state.ScrollViewerPropertyChangedHandler = (_, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty ||
                e.Property == ScrollViewer.ViewportProperty ||
                e.Property == ScrollViewer.ExtentProperty)
            {
                UpdateViewportThumb(scrollViewer);
            }
        };
        state.TrackSizeChangedHandler = (_, _) => UpdateViewportThumb(scrollViewer);
        state.TrackPointerPressedHandler = (_, e) => OnTrackPointerPressed(scrollViewer, e);
        state.ThumbPointerPressedHandler = (_, e) => OnThumbPointerPressed(scrollViewer, e);
        state.ThumbPointerMovedHandler = (_, e) => OnThumbPointerMoved(scrollViewer, e);
        state.ThumbPointerReleasedHandler = (_, e) => EndThumbDrag(scrollViewer, e.Pointer);
        state.ThumbPointerCaptureLostHandler = (_, _) => EndThumbDrag(scrollViewer, null);

        scrollViewer.PropertyChanged += state.ScrollViewerPropertyChangedHandler;
        state.Track.SizeChanged += state.TrackSizeChangedHandler;
        state.Track.PointerPressed += state.TrackPointerPressedHandler;
        state.ViewportThumb.PointerPressed += state.ThumbPointerPressedHandler;
        state.ViewportThumb.PointerMoved += state.ThumbPointerMovedHandler;
        state.ViewportThumb.PointerReleased += state.ThumbPointerReleasedHandler;
        state.ViewportThumb.PointerCaptureLost += state.ThumbPointerCaptureLostHandler;
        state.ViewportThumb.IsHitTestVisible = true;

        UpdateViewportThumb(scrollViewer);
    }

    private static void Unsubscribe(ScrollViewer scrollViewer)
    {
        if (!States.TryGetValue(scrollViewer, out var state))
        {
            return;
        }

        if (state.Track != null)
        {
            if (state.TrackSizeChangedHandler != null)
            {
                state.Track.SizeChanged -= state.TrackSizeChangedHandler;
            }

            if (state.TrackPointerPressedHandler != null)
            {
                state.Track.PointerPressed -= state.TrackPointerPressedHandler;
            }
        }

        if (state.ViewportThumb != null)
        {
            if (state.ThumbPointerPressedHandler != null)
            {
                state.ViewportThumb.PointerPressed -= state.ThumbPointerPressedHandler;
            }

            if (state.ThumbPointerMovedHandler != null)
            {
                state.ViewportThumb.PointerMoved -= state.ThumbPointerMovedHandler;
            }

            if (state.ThumbPointerReleasedHandler != null)
            {
                state.ViewportThumb.PointerReleased -= state.ThumbPointerReleasedHandler;
            }

            if (state.ThumbPointerCaptureLostHandler != null)
            {
                state.ViewportThumb.PointerCaptureLost -= state.ThumbPointerCaptureLostHandler;
            }

            state.ViewportThumb.IsVisible = false;
        }

        if (state.ScrollViewerPropertyChangedHandler != null)
        {
            scrollViewer.PropertyChanged -= state.ScrollViewerPropertyChangedHandler;
        }

        state.IsDragging = false;
        state.Track = null;
        state.ViewportThumb = null;
        state.ScrollViewerPropertyChangedHandler = null;
        state.TrackSizeChangedHandler = null;
        state.TrackPointerPressedHandler = null;
        state.ThumbPointerPressedHandler = null;
        state.ThumbPointerMovedHandler = null;
        state.ThumbPointerReleasedHandler = null;
        state.ThumbPointerCaptureLostHandler = null;
    }

    private static void OnTrackPointerPressed(ScrollViewer scrollViewer, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(scrollViewer).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!TryGetLayout(scrollViewer, out var state, out var trackHeight, out _, out _, out _))
        {
            return;
        }

        var point = e.GetPosition(state.Track!);
        var ratio = Math.Clamp(point.Y / trackHeight, 0d, 1d);
        ScrollToRatio(scrollViewer, ratio);
        e.Handled = true;
    }

    private static void OnThumbPointerPressed(ScrollViewer scrollViewer, PointerPressedEventArgs e)
    {
        if (!TryGetLayout(scrollViewer, out var state, out _, out _, out _, out _))
        {
            return;
        }

        if (!e.GetCurrentPoint(state.ViewportThumb!).Properties.IsLeftButtonPressed)
        {
            return;
        }

        state.IsDragging = true;
        state.DragStartPointerY = e.GetPosition(state.Track!).Y;
        state.DragStartOffsetY = scrollViewer.Offset.Y;
        e.Pointer.Capture(state.ViewportThumb);
        e.Handled = true;
    }

    private static void OnThumbPointerMoved(ScrollViewer scrollViewer, PointerEventArgs e)
    {
        if (!TryGetLayout(scrollViewer, out var state, out var trackHeight, out var thumbHeight, out _,
                out var maxOffset) ||
            !state.IsDragging)
        {
            return;
        }

        var trackRange = Math.Max(0, trackHeight - thumbHeight);
        if (trackRange <= 0 || maxOffset <= 0)
        {
            return;
        }

        var currentPointerY = e.GetPosition(state.Track!).Y;
        var deltaY = currentPointerY - state.DragStartPointerY;
        var deltaOffset = deltaY / trackRange * maxOffset;
        var nextOffsetY = Math.Clamp(state.DragStartOffsetY + deltaOffset, 0, maxOffset);
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, nextOffsetY);
        e.Handled = true;
    }

    private static void EndThumbDrag(ScrollViewer scrollViewer, IPointer? pointer)
    {
        if (!States.TryGetValue(scrollViewer, out var state) || !state.IsDragging)
        {
            return;
        }

        state.IsDragging = false;
        pointer?.Capture(null);
    }

    private static void ScrollToRatio(ScrollViewer scrollViewer, double ratio)
    {
        var maxOffsetY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, maxOffsetY * ratio);
    }

    private static void UpdateViewportThumb(ScrollViewer scrollViewer)
    {
        if (!TryGetLayout(scrollViewer, out var state, out var trackHeight, out var thumbHeight, out var currentOffset,
                out var maxOffset))
        {
            if (States.TryGetValue(scrollViewer, out var existingState) && existingState.ViewportThumb != null)
            {
                existingState.ViewportThumb.IsVisible = false;
                existingState.ViewportThumb.Height = 0;
                existingState.ViewportThumb.Margin = default;
            }

            return;
        }

        var trackRange = Math.Max(0, trackHeight - thumbHeight);
        var top = maxOffset <= 0 ? 0 : currentOffset / maxOffset * trackRange;

        state.ViewportThumb!.IsVisible = true;
        state.ViewportThumb.Height = thumbHeight;
        state.ViewportThumb.Margin = new Thickness(0, top, 0, 0);
    }

    private static bool TryGetLayout(ScrollViewer scrollViewer, out OverviewState state, out double trackHeight,
        out double thumbHeight, out double currentOffset, out double maxOffset)
    {
        state = States.GetOrCreateValue(scrollViewer);
        trackHeight = 0;
        thumbHeight = 0;
        currentOffset = 0;
        maxOffset = 0;

        state.Track ??= GetTrack(scrollViewer);
        state.ViewportThumb ??= GetViewportThumb(scrollViewer);
        if (state.Track == null || state.ViewportThumb == null)
        {
            return false;
        }

        trackHeight = state.Track.Bounds.Height;
        var extentHeight = scrollViewer.Extent.Height;
        if (trackHeight <= 0 || extentHeight <= 0)
        {
            return false;
        }

        var viewportHeight = Math.Min(scrollViewer.Viewport.Height, extentHeight);
        if (viewportHeight <= 0)
        {
            return false;
        }

        thumbHeight = Math.Max(GetMinThumbHeight(scrollViewer), viewportHeight / extentHeight * trackHeight);
        thumbHeight = Math.Min(thumbHeight, trackHeight);
        maxOffset = Math.Max(0, extentHeight - viewportHeight);
        currentOffset = Math.Clamp(scrollViewer.Offset.Y, 0, maxOffset);
        return true;
    }
}