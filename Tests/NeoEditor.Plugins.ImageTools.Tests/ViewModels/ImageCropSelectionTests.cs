using NeoEditor.Plugins.ImageTools.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.ViewModels;

/// <summary>
/// Crop selection normalization — the pure logic behind the crop overlay interaction:
/// coordinate clamping to image bounds, minimum size enforcement, and the full-image
/// default.
/// </summary>
public class ImageCropSelectionTests
{
    [Fact]
    public void Normalize_ClampsToImageBounds()
    {
        var crop = ImageCropSelection.Normalize(-10, -5, 200, 100, imageWidth: 64, imageHeight: 64);

        Assert.NotNull(crop);
        Assert.Equal(0, crop.Value.Left);
        Assert.Equal(0, crop.Value.Top);
        Assert.Equal(64, crop.Value.Right);
        Assert.Equal(64, crop.Value.Bottom);
    }

    [Fact]
    public void Normalize_AcceptsReversedCoordinates()
    {
        // Dragging from bottom-right to top-left swaps the corners.
        var crop = ImageCropSelection.Normalize(40, 40, 10, 10, imageWidth: 64, imageHeight: 64);

        Assert.NotNull(crop);
        Assert.Equal(10, crop.Value.Left);
        Assert.Equal(10, crop.Value.Top);
        Assert.Equal(40, crop.Value.Right);
        Assert.Equal(40, crop.Value.Bottom);
        Assert.Equal(30, crop.Value.Width);
        Assert.Equal(30, crop.Value.Height);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)] // below minimum size (2×2)
    [InlineData(0, 0, 2, 1)]
    [InlineData(0, 0, 1, 2)]
    [InlineData(0, 0, 0, 0)] // degenerate
    public void Normalize_RejectsSubMinimumSize(int left, int top, int right, int bottom)
    {
        var crop = ImageCropSelection.Normalize(left, top, right, bottom, imageWidth: 64, imageHeight: 64);

        Assert.Null(crop);
    }

    [Fact]
    public void Normalize_OutOfBoundsAndSubMinimum_ReturnsNull()
    {
        // Fully outside the image → clamped to zero size → invalid.
        var crop = ImageCropSelection.Normalize(100, 100, 120, 120, imageWidth: 64, imageHeight: 64);
        Assert.Null(crop);
    }

    [Fact]
    public void FullImage_CoversWholeImage()
    {
        var full = ImageCropSelection.FullImage(32, 16);

        Assert.Equal(0, full.Left);
        Assert.Equal(0, full.Top);
        Assert.Equal(32, full.Right);
        Assert.Equal(16, full.Bottom);
        Assert.Equal(2.0, full.AspectRatio, precision: 3);
    }

    [Fact]
    public void ToPixelRect_RoundTrips()
    {
        var crop = ImageCropSelection.Normalize(4, 6, 20, 26, imageWidth: 64, imageHeight: 64);
        Assert.NotNull(crop);

        var rect = crop.Value.ToPixelRect();
        Assert.Equal(4, rect.X);
        Assert.Equal(6, rect.Y);
        Assert.Equal(16, rect.Width);
        Assert.Equal(20, rect.Height);

        var back = ImageCropSelection.FromPixelRect(rect);
        Assert.Equal(crop.Value, back);
    }
}
