using System;
using Avalonia;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

public readonly record struct ImageCropSelection(int Left, int Top, int Right, int Bottom)
{
    public static ImageCropSelection Empty => new(0, 0, 0, 0);

    public int Width => Math.Max(0, Right - Left);
    public int Height => Math.Max(0, Bottom - Top);
    public double AspectRatio => Height <= 0 ? 1D : Width / (double)Height;

    public static ImageCropSelection FullImage(int imageWidth, int imageHeight)
    {
        return new(0, 0, Math.Max(0, imageWidth), Math.Max(0, imageHeight));
    }

    public static ImageCropSelection FromPixelRect(PixelRect rect)
    {
        return new(rect.X, rect.Y, rect.Right, rect.Bottom);
    }

    public static ImageCropSelection? Normalize(int left, int top, int right, int bottom, int imageWidth, int imageHeight, int minimumSize = 2)
    {
        var normalizedLeft = Math.Clamp(Math.Min(left, right), 0, imageWidth);
        var normalizedTop = Math.Clamp(Math.Min(top, bottom), 0, imageHeight);
        var normalizedRight = Math.Clamp(Math.Max(left, right), 0, imageWidth);
        var normalizedBottom = Math.Clamp(Math.Max(top, bottom), 0, imageHeight);

        var width = normalizedRight - normalizedLeft;
        var height = normalizedBottom - normalizedTop;
        if (width < minimumSize || height < minimumSize)
        {
            return null;
        }

        return new ImageCropSelection(normalizedLeft, normalizedTop, normalizedRight, normalizedBottom);
    }

    public PixelRect ToPixelRect()
    {
        return new PixelRect(Left, Top, Width, Height);
    }
}
