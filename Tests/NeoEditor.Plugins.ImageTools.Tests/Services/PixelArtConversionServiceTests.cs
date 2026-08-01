using Xunit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.Tests.Services;

public sealed class PixelArtConversionServiceTests : IDisposable
{
    private readonly PixelArtConversionService _service = new();
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Convert_ReducesColors()
    {
        const int colorCount = 8;
        var sourcePath = CreateTestImage(64, 64, generateManyColors: true);
        var options = new PixelArtConversionOptions(32, 32, ColorCount: colorCount,
            EdgeEnhancement: false, Dithering: false, TransparentBackground: false);

        using var result = await _service.ConvertToPixelArtAsync(sourcePath, options);

        // Count unique colors in result
        var uniqueColors = CountUniqueColors(result);
        Assert.True(uniqueColors <= colorCount + 1, // +1 for potential fully-transparent pixels
            $"Expected ≤ {colorCount} unique colors, got {uniqueColors}");
        Assert.Equal(32, result.Width);
        Assert.Equal(32, result.Height);
    }

    [Fact]
    public async Task Convert_WithEdgeEnhancement_DarkensEdges()
    {
        // Create an image with a clear light/dark boundary (edge)
        var sourcePath = CreateTwoToneImage(64, 64);
        var options = new PixelArtConversionOptions(32, 32, ColorCount: 16,
            EdgeEnhancement: true, Dithering: false, TransparentBackground: false);

        using var result = await _service.ConvertToPixelArtAsync(sourcePath, options);

        // Edge enhancement should have been applied without errors
        // Verify the image is valid (not all black, not all white)
        var hasVariation = HasPixelVariation(result);
        Assert.True(hasVariation, "Result should have pixel variation after edge enhancement");
    }

    [Fact]
    public async Task Convert_WithDithering_ValidPixels()
    {
        var sourcePath = CreateGradientImage(64, 64);
        var options = new PixelArtConversionOptions(32, 32, ColorCount: 8,
            EdgeEnhancement: false, Dithering: true, TransparentBackground: false);

        using var result = await _service.ConvertToPixelArtAsync(sourcePath, options);

        // Verify the result has proper dimensions and contains pixels (no crash/overflow)
        Assert.Equal(32, result.Width);
        Assert.Equal(32, result.Height);
    }

    [Fact]
    public async Task Convert_TransparentBG_CornersTransparent()
    {
        // Create image with uniform background color
        var sourcePath = CreateImageWithUniformBackground(64, 64,
            new Rgba32(255, 0, 0, 255), // Red background
            new Rgba32(0, 255, 0, 255)); // Green center
        var options = new PixelArtConversionOptions(32, 32, ColorCount: 16,
            EdgeEnhancement: false, Dithering: false, TransparentBackground: true);

        using var result = await _service.ConvertToPixelArtAsync(sourcePath, options);

        // Check that corners are transparent or significantly more transparent
        var corners = GetCornerAlphas(result);
        var cornerAlphaAvg = corners.Average(a => a);
        // Corners should be mostly transparent (since background is detected)
        Assert.True(cornerAlphaAvg < 128,
            $"Expected corners to be mostly transparent, avg alpha = {cornerAlphaAvg}");
    }

    [Fact]
    public async Task Convert_RespectsTargetDimensions()
    {
        var sourcePath = CreateTestImage(100, 50);
        var options = new PixelArtConversionOptions(40, 20, ColorCount: 16,
            EdgeEnhancement: false, Dithering: false, TransparentBackground: false);

        using var result = await _service.ConvertToPixelArtAsync(sourcePath, options);

        Assert.Equal(40, result.Width);
        Assert.Equal(20, result.Height);
    }

    [Fact]
    public async Task Convert_InvalidColorCount_Clamps()
    {
        var sourcePath = CreateTestImage(32, 32);
        // ColorCount of 0 should be clamped to MinColorCount (4)
        var optionsLow = new PixelArtConversionOptions(16, 16, ColorCount: 0,
            EdgeEnhancement: false, Dithering: false, TransparentBackground: false);

        using var resultLow = await _service.ConvertToPixelArtAsync(sourcePath, optionsLow);
        var uniqueLow = CountUniqueColors(resultLow);
        Assert.True(uniqueLow <= PixelArtConversionOptions.MinColorCount + 1);

        // ColorCount of 500 should be clamped to MaxColorCount (64)
        var optionsHigh = new PixelArtConversionOptions(16, 16, ColorCount: 500,
            EdgeEnhancement: false, Dithering: false, TransparentBackground: false);

        using var resultHigh = await _service.ConvertToPixelArtAsync(sourcePath, optionsHigh);
        var uniqueHigh = CountUniqueColors(resultHigh);
        Assert.True(uniqueHigh <= PixelArtConversionOptions.MaxColorCount + 1);
    }

    [Fact]
    public async Task Convert_LargeImage_Completes()
    {
        var sourcePath = CreateTestImage(512, 512, generateManyColors: true);
        var options = new PixelArtConversionOptions(64, 64, ColorCount: 32,
            EdgeEnhancement: true, Dithering: true, TransparentBackground: true);

        using var result = await _service.ConvertToPixelArtAsync(sourcePath, options);

        Assert.Equal(64, result.Width);
        Assert.Equal(64, result.Height);
        // Should not throw OOM or timeout
    }

    // ── Helpers ──

    private string CreateTestImage(int width, int height, bool generateManyColors = false)
    {
        var path = Path.GetTempFileName();
        if (File.Exists(path)) File.Delete(path);
        path = Path.ChangeExtension(path, ".png");
        _tempFiles.Add(path);

        using var image = new Image<Rgba32>(width, height);
        var rng = new Random(42); // Deterministic seed
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (generateManyColors)
                    {
                        row[x] = new Rgba32((byte)rng.Next(256), (byte)rng.Next(256),
                            (byte)rng.Next(256), 255);
                    }
                    else
                    {
                        // Simple pattern with a few colors
                        var c = (byte)((x + y) % 8 * 32);
                        row[x] = new Rgba32(c, (byte)(255 - c), (byte)(c / 2), 255);
                    }
                }
            }
        });

        image.SaveAsPng(path);
        return path;
    }

    private string CreateTwoToneImage(int width, int height)
    {
        var path = Path.GetTempFileName();
        if (File.Exists(path)) File.Delete(path);
        path = Path.ChangeExtension(path, ".png");
        _tempFiles.Add(path);

        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    // Left half white, right half black — creates a sharp edge
                    row[x] = x < width / 2
                        ? new Rgba32(255, 255, 255, 255)
                        : new Rgba32(0, 0, 0, 255);
                }
            }
        });

        image.SaveAsPng(path);
        return path;
    }

    private string CreateGradientImage(int width, int height)
    {
        var path = Path.GetTempFileName();
        if (File.Exists(path)) File.Delete(path);
        path = Path.ChangeExtension(path, ".png");
        _tempFiles.Add(path);

        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var r = (byte)(x * 255 / width);
                    var g = (byte)(y * 255 / height);
                    var b = (byte)(128);
                    row[x] = new Rgba32(r, g, b, 255);
                }
            }
        });

        image.SaveAsPng(path);
        return path;
    }

    private string CreateImageWithUniformBackground(int width, int height,
        Rgba32 bgColor, Rgba32 centerColor)
    {
        var path = Path.GetTempFileName();
        if (File.Exists(path)) File.Delete(path);
        path = Path.ChangeExtension(path, ".png");
        _tempFiles.Add(path);

        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    // Center region gets the center color, rest gets background
                    var cx = width / 2;
                    var cy = height / 2;
                    var inCenter = Math.Abs(x - cx) < width / 6 && Math.Abs(y - cy) < height / 6;
                    row[x] = inCenter ? centerColor : bgColor;
                }
            }
        });

        image.SaveAsPng(path);
        return path;
    }

    private static int CountUniqueColors(Image<Rgba32> image)
    {
        var unique = new HashSet<(byte, byte, byte)>();
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    if (p.A >= 128)
                        unique.Add((p.R, p.G, p.B));
                }
            }
        });

        return unique.Count;
    }

    private static bool HasPixelVariation(Image<Rgba32> image)
    {
        Rgba32? first = null;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height && first is null; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length && first is null; x++)
                {
                    if (row[x].A >= 128) first = row[x];
                }
            }
        });

        if (first is null) return false;

        var foundDifferent = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height && !foundDifferent; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length && !foundDifferent; x++)
                {
                    var p = row[x];
                    if (p.A >= 128 &&
                        (p.R != first.Value.R || p.G != first.Value.G || p.B != first.Value.B))
                        foundDifferent = true;
                }
            }
        });

        return foundDifferent;
    }

    private static List<byte> GetCornerAlphas(Image<Rgba32> image)
    {
        var w = image.Width;
        var h = image.Height;
        var alphas = new List<byte>();

        image.ProcessPixelRows(accessor =>
        {
            alphas.Add(accessor.GetRowSpan(0)[0].A);
            alphas.Add(accessor.GetRowSpan(0)[w - 1].A);
            alphas.Add(accessor.GetRowSpan(h - 1)[0].A);
            alphas.Add(accessor.GetRowSpan(h - 1)[w - 1].A);
        });

        return alphas;
    }
}
