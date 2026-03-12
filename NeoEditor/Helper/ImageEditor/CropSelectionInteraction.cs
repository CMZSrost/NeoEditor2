using System;
using Avalonia;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls.ImageEditor;

internal enum CropHandle
{
    None,
    Move,
    Left,
    Top,
    Right,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

internal sealed class CropSelectionInteraction
{
    private readonly int _minimumSize;
    private CropHandle _activeHandle;
    private ImageCropSelection _startSelection;
    private Point _startPixelPoint;

    public CropSelectionInteraction(int minimumSize)
    {
        _minimumSize = minimumSize;
    }

    public bool IsActive => _activeHandle != CropHandle.None;

    public bool TryBegin(CropHandle handle, Point startPoint, ImageCropSelection startSelection,
        ImageSelectionViewportGeometry geometry)
    {
        if (handle == CropHandle.None || !geometry.IsValid || startSelection.Width < _minimumSize ||
            startSelection.Height < _minimumSize)
        {
            return false;
        }

        _activeHandle = handle;
        _startSelection = startSelection;
        _startPixelPoint = geometry.MapViewportToPixel(startPoint);
        return true;
    }

    public ImageCropSelection Update(Point currentPoint, ImageSelectionViewportGeometry geometry)
    {
        if (!IsActive || !geometry.IsValid)
        {
            return _startSelection;
        }

        var currentPixel = geometry.MapViewportToPixel(currentPoint);

        switch (_activeHandle)
        {
            case CropHandle.Move:
                return Move(currentPixel, geometry.SourceWidth, geometry.SourceHeight);
            case CropHandle.Left:
                return new ImageCropSelection(
                    Math.Clamp((int)Math.Floor(currentPixel.X), 0, _startSelection.Right - _minimumSize),
                    _startSelection.Top,
                    _startSelection.Right,
                    _startSelection.Bottom);
            case CropHandle.Right:
                return new ImageCropSelection(
                    _startSelection.Left,
                    _startSelection.Top,
                    Math.Clamp((int)Math.Ceiling(currentPixel.X), _startSelection.Left + _minimumSize,
                        geometry.SourceWidth),
                    _startSelection.Bottom);
            case CropHandle.Top:
                return new ImageCropSelection(
                    _startSelection.Left,
                    Math.Clamp((int)Math.Floor(currentPixel.Y), 0, _startSelection.Bottom - _minimumSize),
                    _startSelection.Right,
                    _startSelection.Bottom);
            case CropHandle.Bottom:
                return new ImageCropSelection(
                    _startSelection.Left,
                    _startSelection.Top,
                    _startSelection.Right,
                    Math.Clamp((int)Math.Ceiling(currentPixel.Y), _startSelection.Top + _minimumSize,
                        geometry.SourceHeight));
            case CropHandle.TopLeft:
                return new ImageCropSelection(
                    Math.Clamp((int)Math.Floor(currentPixel.X), 0, _startSelection.Right - _minimumSize),
                    Math.Clamp((int)Math.Floor(currentPixel.Y), 0, _startSelection.Bottom - _minimumSize),
                    _startSelection.Right,
                    _startSelection.Bottom);
            case CropHandle.TopRight:
                return new ImageCropSelection(
                    _startSelection.Left,
                    Math.Clamp((int)Math.Floor(currentPixel.Y), 0, _startSelection.Bottom - _minimumSize),
                    Math.Clamp((int)Math.Ceiling(currentPixel.X), _startSelection.Left + _minimumSize,
                        geometry.SourceWidth),
                    _startSelection.Bottom);
            case CropHandle.BottomLeft:
                return new ImageCropSelection(
                    Math.Clamp((int)Math.Floor(currentPixel.X), 0, _startSelection.Right - _minimumSize),
                    _startSelection.Top,
                    _startSelection.Right,
                    Math.Clamp((int)Math.Ceiling(currentPixel.Y), _startSelection.Top + _minimumSize,
                        geometry.SourceHeight));
            case CropHandle.BottomRight:
                return new ImageCropSelection(
                    _startSelection.Left,
                    _startSelection.Top,
                    Math.Clamp((int)Math.Ceiling(currentPixel.X), _startSelection.Left + _minimumSize,
                        geometry.SourceWidth),
                    Math.Clamp((int)Math.Ceiling(currentPixel.Y), _startSelection.Top + _minimumSize,
                        geometry.SourceHeight));
            default:
                return _startSelection;
        }
    }

    public void End()
    {
        _activeHandle = CropHandle.None;
    }

    private ImageCropSelection Move(Point currentPixel, int sourceWidth, int sourceHeight)
    {
        var deltaX = (int)Math.Round(currentPixel.X - _startPixelPoint.X, MidpointRounding.AwayFromZero);
        var deltaY = (int)Math.Round(currentPixel.Y - _startPixelPoint.Y, MidpointRounding.AwayFromZero);
        var maxLeft = Math.Max(0, sourceWidth - _startSelection.Width);
        var maxTop = Math.Max(0, sourceHeight - _startSelection.Height);
        var left = Math.Clamp(_startSelection.Left + deltaX, 0, maxLeft);
        var top = Math.Clamp(_startSelection.Top + deltaY, 0, maxTop);
        return new ImageCropSelection(left, top, left + _startSelection.Width, top + _startSelection.Height);
    }
}