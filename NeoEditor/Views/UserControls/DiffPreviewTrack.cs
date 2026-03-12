using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls;

public readonly record struct DiffPreviewMarkerRun(
    int StartLineNumber,
    int LineCount,
    IBrush Brush);

public class DiffPreviewTrack : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<DiffPreviewTrack, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IReadOnlyList<DiffPreviewMarkerRun>> MarkerRunsProperty =
        AvaloniaProperty.Register<DiffPreviewTrack, IReadOnlyList<DiffPreviewMarkerRun>>(
            nameof(MarkerRuns), Array.Empty<DiffPreviewMarkerRun>());

    public static readonly StyledProperty<int> TotalLinesProperty =
        AvaloniaProperty.Register<DiffPreviewTrack, int>(nameof(TotalLines));

    public static readonly StyledProperty<int> CurrentBlockStartLineNumberProperty =
        AvaloniaProperty.Register<DiffPreviewTrack, int>(nameof(CurrentBlockStartLineNumber));

    public static readonly StyledProperty<int> CurrentBlockEndLineNumberProperty =
        AvaloniaProperty.Register<DiffPreviewTrack, int>(nameof(CurrentBlockEndLineNumber));

    static DiffPreviewTrack()
    {
        AffectsRender<DiffPreviewTrack>(BackgroundProperty, MarkerRunsProperty, TotalLinesProperty,
            CurrentBlockStartLineNumberProperty, CurrentBlockEndLineNumberProperty);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IReadOnlyList<DiffPreviewMarkerRun> MarkerRuns
    {
        get => GetValue(MarkerRunsProperty);
        set => SetValue(MarkerRunsProperty, value);
    }

    public int TotalLines
    {
        get => GetValue(TotalLinesProperty);
        set => SetValue(TotalLinesProperty, value);
    }

    public int CurrentBlockStartLineNumber
    {
        get => GetValue(CurrentBlockStartLineNumberProperty);
        set => SetValue(CurrentBlockStartLineNumberProperty, value);
    }

    public int CurrentBlockEndLineNumber
    {
        get => GetValue(CurrentBlockEndLineNumberProperty);
        set => SetValue(CurrentBlockEndLineNumberProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (Background != null)
        {
            context.DrawRectangle(Background, null, new Rect(bounds.Size));
        }

        if (TotalLines <= 0)
        {
            return;
        }

        var lineHeight = bounds.Height / TotalLines;
        foreach (var markerRun in MarkerRuns)
        {
            if (markerRun.LineCount <= 0)
            {
                continue;
            }

            var startY = Math.Max(0d, (markerRun.StartLineNumber - 1) * lineHeight);
            var height = Math.Max(2d, markerRun.LineCount * lineHeight);
            height = Math.Min(height, bounds.Height - startY);
            if (height <= 0)
            {
                continue;
            }

            context.DrawRectangle(markerRun.Brush, null, new Rect(0, startY, bounds.Width, height));
        }

        if (CurrentBlockStartLineNumber <= 0 || CurrentBlockEndLineNumber < CurrentBlockStartLineNumber)
        {
            return;
        }

        var currentStartY = Math.Max(0d, (CurrentBlockStartLineNumber - 1) * lineHeight);
        var currentHeight = Math.Max(3d, (CurrentBlockEndLineNumber - CurrentBlockStartLineNumber + 1) * lineHeight);
        currentHeight = Math.Min(currentHeight, bounds.Height - currentStartY);
        if (currentHeight <= 0)
        {
            return;
        }

        var currentRect = new Rect(0, currentStartY, bounds.Width, currentHeight);
        var borderPen = new Pen(HighlightBackgroundRenderHelper.GetNavigationPreviewBorderBrush());
        context.DrawRectangle(HighlightBackgroundRenderHelper.GetNavigationPreviewBrush(), borderPen, currentRect);
    }
}


