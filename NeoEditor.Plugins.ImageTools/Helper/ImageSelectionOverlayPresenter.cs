using System;
using Avalonia;
using Avalonia.Controls;

namespace NeoEditor.Plugins.ImageTools.Helper;

internal sealed class ImageSelectionOverlayPresenter
{
    private readonly Border[] _masks;
    private readonly Border[] _handles;
    private readonly Border _selectionBorder;
    private readonly double _edgeHandleThickness;
    private readonly double _cornerHandleSize;

    public ImageSelectionOverlayPresenter(
        Border selectionBorder,
        Border[] masks,
        Border[] handles,
        double edgeHandleThickness,
        double cornerHandleSize)
    {
        _selectionBorder = selectionBorder;
        _masks = masks;
        _handles = handles;
        _edgeHandleThickness = edgeHandleThickness;
        _cornerHandleSize = cornerHandleSize;
    }

    public void Show(Rect selectionRect, Size canvasSize)
    {
        if (!ImageSelectionViewportMapper.IsValidRect(selectionRect) || canvasSize.Width <= 0 || canvasSize.Height <= 0)
        {
            Hide();
            return;
        }

        SetVisible(true);

        SetCanvasRect(_masks[0], new Rect(0, 0, canvasSize.Width, Math.Max(0, selectionRect.Top)));
        SetCanvasRect(_masks[1], new Rect(0, selectionRect.Top, Math.Max(0, selectionRect.Left), selectionRect.Height));
        SetCanvasRect(_masks[2],
            new Rect(selectionRect.Right, selectionRect.Top, Math.Max(0, canvasSize.Width - selectionRect.Right),
                selectionRect.Height));
        SetCanvasRect(_masks[3],
            new Rect(0, selectionRect.Bottom, canvasSize.Width, Math.Max(0, canvasSize.Height - selectionRect.Bottom)));
        SetCanvasRect(_selectionBorder, selectionRect);

        var horizontalHandleWidth = double.Max(0, selectionRect.Width - 2 * _cornerHandleSize);
        var verticalHandleHeight = double.Max(0, selectionRect.Height - 2 * _cornerHandleSize);

        SetCanvasRect(_handles[0],
            new Rect(selectionRect.Left - _edgeHandleThickness / 2, selectionRect.Top + _cornerHandleSize,
                _edgeHandleThickness, verticalHandleHeight));
        SetCanvasRect(_handles[1],
            new Rect(selectionRect.Right - _edgeHandleThickness / 2, selectionRect.Top + _cornerHandleSize,
                _edgeHandleThickness, verticalHandleHeight));
        SetCanvasRect(_handles[2],
            new Rect(selectionRect.Left + _cornerHandleSize, selectionRect.Top - _edgeHandleThickness / 2,
                horizontalHandleWidth, _edgeHandleThickness));
        SetCanvasRect(_handles[3],
            new Rect(selectionRect.Left + _cornerHandleSize, selectionRect.Bottom - _edgeHandleThickness / 2,
                horizontalHandleWidth, _edgeHandleThickness));
        SetCanvasRect(_handles[4],
            new Rect(selectionRect.Left - _cornerHandleSize / 2, selectionRect.Top - _cornerHandleSize / 2,
                _cornerHandleSize, _cornerHandleSize));
        SetCanvasRect(_handles[5],
            new Rect(selectionRect.Right - _cornerHandleSize / 2, selectionRect.Top - _cornerHandleSize / 2,
                _cornerHandleSize, _cornerHandleSize));
        SetCanvasRect(_handles[6],
            new Rect(selectionRect.Left - _cornerHandleSize / 2, selectionRect.Bottom - _cornerHandleSize / 2,
                _cornerHandleSize, _cornerHandleSize));
        SetCanvasRect(_handles[7],
            new Rect(selectionRect.Right - _cornerHandleSize / 2, selectionRect.Bottom - _cornerHandleSize / 2,
                _cornerHandleSize, _cornerHandleSize));
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool isVisible)
    {
        _selectionBorder.IsVisible = isVisible;

        foreach (var mask in _masks)
        {
            mask.IsVisible = isVisible;
        }

        foreach (var handle in _handles)
        {
            handle.IsVisible = isVisible;
        }
    }

    private static void SetCanvasRect(Control control, Rect rect)
    {
        Canvas.SetLeft(control, rect.X);
        Canvas.SetTop(control, rect.Y);
        control.Width = Math.Max(0, rect.Width);
        control.Height = Math.Max(0, rect.Height);
    }
}
