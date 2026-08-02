using System;
using System.IO;
using System.Threading.Tasks;
using NeoEditor.Plugins.ImageTools.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.Services;

/// <summary>
/// Processing pipeline over the unified <see cref="ImageSource"/>: both the file-path
/// and in-memory-bytes entries must produce identical output (one pipeline, no forks).
/// </summary>
public class ImageEditorProcessingServiceTests
{
    private readonly ImageEditorProcessingService _service;

    static ImageEditorProcessingServiceTests()
    {
        // Preview results are Avalonia Bitmaps — the headless platform (Skia) is required.
        TestApp.EnsureAvaloniaInitialized();
    }

    public ImageEditorProcessingServiceTests()
    {
        _service = new ImageEditorProcessingService(new PixelArtConversionService());
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var img = new Image<Rgba32>(width, height);
        img.Mutate(ctx => ctx.BackgroundColor(new Rgba32(255, 0, 0, 255)));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static string WriteTempPng(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), $"neoeditor-proc-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, CreatePngBytes(width, height));
        return path;
    }

    [Fact]
    public async Task CreatePixelArtPreviewAsync_FromPath_ProducesX2Result()
    {
        var path = WriteTempPng(32, 24);
        try
        {
            var request = new ImageEditorProcessingRequest(
                ImageSource.FromPath(path), NormalWidth: 16, NormalHeight: 12, CropRect: null);
            var result = await _service.CreatePixelArtPreviewAsync(request, new PixelArtConversionOptions(16, 12));

            Assert.NotNull(result);
            Assert.NotNull(result.PreviewBitmap);
            Assert.Equal(32, result.X2Width);
            Assert.Equal(24, result.X2Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CreatePixelArtPreviewAsync_FromBytes_MatchesPathSource()
    {
        // Same pixels fed through both entries — the unified pipeline must agree.
        var path = WriteTempPng(64, 32);
        try
        {
            var bytes = File.ReadAllBytes(path);

            var options = new PixelArtConversionOptions(20, 10, ColorCount: 8, EdgeEnhancement: true, Dithering: true);
            var fromPath = await _service.CreatePixelArtPreviewAsync(
                new ImageEditorProcessingRequest(ImageSource.FromPath(path), 20, 10, null), options);
            var fromBytes = await _service.CreatePixelArtPreviewAsync(
                new ImageEditorProcessingRequest(ImageSource.FromBytes(bytes), 20, 10, null), options);

            Assert.NotNull(fromPath);
            Assert.NotNull(fromBytes);
            Assert.Equal(fromPath.X2Width, fromBytes.X2Width);
            Assert.Equal(fromPath.X2Height, fromBytes.X2Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CreatePixelArtPreviewAsync_WithCrop_UsesCropRect()
    {
        var path = WriteTempPng(64, 64);
        try
        {
            // Crop a 16×16 region → output stays 20×10 regardless of the source size.
            var crop = new Avalonia.PixelRect(0, 0, 16, 16);
            var request = new ImageEditorProcessingRequest(
                ImageSource.FromPath(path), 20, 10, crop);
            var result = await _service.CreatePixelArtPreviewAsync(request, new PixelArtConversionOptions(20, 10));

            Assert.NotNull(result);
            Assert.Equal(40, result.X2Width);
            Assert.Equal(20, result.X2Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CreatePixelArtPreviewAsync_InvalidRequest_ReturnsNull()
    {
        // Missing file path.
        var missing = await _service.CreatePixelArtPreviewAsync(
            new ImageEditorProcessingRequest(ImageSource.FromPath("nope.png"), 16, 16, null),
            new PixelArtConversionOptions(16, 16));
        Assert.Null(missing);

        // Zero target size.
        var zero = await _service.CreatePixelArtPreviewAsync(
            new ImageEditorProcessingRequest(ImageSource.FromBytes(CreatePngBytes(8, 8)), 0, 16, null),
            new PixelArtConversionOptions(0, 16));
        Assert.Null(zero);
    }
}
