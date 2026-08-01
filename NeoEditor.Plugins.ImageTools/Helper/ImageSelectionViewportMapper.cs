using System;
using Avalonia;

namespace NeoEditor.Plugins.ImageTools.Helper;

internal readonly record struct ImageSelectionViewportGeometry(
    Rect ViewportBounds,
    Rect ImageBounds,
    int SourceWidth,
    int SourceHeight)
{
    private const double Epsilon = 1e-6;

    public bool IsValid => SourceWidth > 0
                           && SourceHeight > 0
                           && ImageBounds.Width > Epsilon
                           && ImageBounds.Height > Epsilon
                           && ViewportBounds.Width > Epsilon
                           && ViewportBounds.Height > Epsilon;

    public Point MapViewportToPixel(Point viewportPoint)
    {
        if (!IsValid)
        {
            return default;
        }

        var normalizedX = Math.Clamp((viewportPoint.X - ImageBounds.X) / ImageBounds.Width, 0D, 1D);
        var normalizedY = Math.Clamp((viewportPoint.Y - ImageBounds.Y) / ImageBounds.Height, 0D, 1D);
        return new Point(normalizedX * SourceWidth, normalizedY * SourceHeight);
    }

    public Rect Project(PixelRect pixelRect)
    {
        if (!IsValid || pixelRect.Width <= 0 || pixelRect.Height <= 0)
        {
            return default;
        }

        var left = ImageBounds.X + pixelRect.X / (double)SourceWidth * ImageBounds.Width;
        var top = ImageBounds.Y + pixelRect.Y / (double)SourceHeight * ImageBounds.Height;
        var right = ImageBounds.X + pixelRect.Right / (double)SourceWidth * ImageBounds.Width;
        var bottom = ImageBounds.Y + pixelRect.Bottom / (double)SourceHeight * ImageBounds.Height;
        return ImageSelectionViewportMapper.NormalizeRect(new Rect(new Point(left, top), new Point(right, bottom)));
    }
}

internal static class ImageSelectionViewportMapper
{
    private const double Epsilon = 1e-6;

    public static ImageSelectionViewportGeometry? TryCreateGeometry(Size viewportSize, int sourceWidth,
        int sourceHeight, double scale, double translateX, double translateY)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || viewportSize.Width <= Epsilon || viewportSize.Height <= Epsilon ||
            scale <= Epsilon)
        {
            return null;
        }

        var displayedWidth = sourceWidth * scale;
        var displayedHeight = sourceHeight * scale;
        if (displayedWidth <= Epsilon || displayedHeight <= Epsilon || double.IsNaN(displayedWidth) ||
            double.IsNaN(displayedHeight)
            || double.IsInfinity(displayedWidth) || double.IsInfinity(displayedHeight))
        {
            return null;
        }

        var left = (viewportSize.Width - displayedWidth) / 2D + translateX;
        var top = (viewportSize.Height - displayedHeight) / 2D + translateY;
        var geometry = new ImageSelectionViewportGeometry(new Rect(viewportSize),
            new Rect(left, top, displayedWidth, displayedHeight), sourceWidth, sourceHeight);
        return geometry.IsValid ? geometry : null;
    }

    /// <summary>
    /// Creates viewport geometry assuming the image is displayed with <see cref="Avalonia.Media.Stretch.Uniform"/>
    /// inside the viewport: the image is scaled uniformly to fit the container while preserving aspect ratio,
    /// and centered within any remaining space.
    /// </summary>
    public static ImageSelectionViewportGeometry? TryCreateGeometryFromUniformStretch(
        Size viewportSize, int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || viewportSize.Width <= Epsilon || viewportSize.Height <= Epsilon)
        {
            return null;
        }

        var scaleX = viewportSize.Width / sourceWidth;
        var scaleY = viewportSize.Height / sourceHeight;
        var uniformScale = Math.Min(scaleX, scaleY);
        if (uniformScale <= Epsilon || double.IsNaN(uniformScale) || double.IsInfinity(uniformScale))
        {
            return null;
        }

        var displayedWidth = sourceWidth * uniformScale;
        var displayedHeight = sourceHeight * uniformScale;
        var left = (viewportSize.Width - displayedWidth) / 2D;
        var top = (viewportSize.Height - displayedHeight) / 2D;

        var geometry = new ImageSelectionViewportGeometry(
            new Rect(viewportSize),
            new Rect(left, top, displayedWidth, displayedHeight),
            sourceWidth,
            sourceHeight);
        return geometry.IsValid ? geometry : null;
    }

    public static bool IsValidRect(Rect rect)
    {
        return rect.Width > Epsilon && rect.Height > Epsilon && !double.IsNaN(rect.Width) && !double.IsNaN(rect.Height);
    }

    public static Rect NormalizeRect(Rect rect)
    {
        var left = Math.Min(rect.Left, rect.Right);
        var top = Math.Min(rect.Top, rect.Bottom);
        var right = Math.Max(rect.Left, rect.Right);
        var bottom = Math.Max(rect.Top, rect.Bottom);
        return new Rect(new Point(left, top), new Point(right, bottom));
    }
}
