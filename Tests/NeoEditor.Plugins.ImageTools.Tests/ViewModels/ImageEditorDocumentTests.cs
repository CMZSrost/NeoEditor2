using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Moq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Services;
using NeoEditor.Plugins.ImageTools.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.ViewModels;

/// <summary>
/// Image editor document (single-image editor): source slot with crop, result slot,
/// manual Apply with stale marking, per-slot save. The AI material-sourcing flows
/// live in the create-image document (Phase 3).
/// </summary>
public class ImageEditorDocumentTests
{
    static ImageEditorDocumentTests()
    {
        // Tests that decode Avalonia Bitmaps need the headless platform (Skia).
        TestApp.EnsureAvaloniaInitialized();
    }

    private static ImageEditorDocument CreateDoc(IImageEditorProcessingService? processing = null)
    {
        var loc = CreateLocMock();
        return new ImageEditorDocument(
            processing ?? new Mock<IImageEditorProcessingService>().Object,
            new ImageFileService(loc),
            loc);
    }

    /// <summary>Loc mock that returns the key itself — non-empty text for overlay/status
    /// assertions (the real resx values are not loaded in unit tests).</summary>
    private static ILocalizationService CreateLocMock()
    {
        var loc = new Mock<ILocalizationService>();
        loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        return loc.Object;
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
        var path = Path.Combine(Path.GetTempPath(), $"neoeditor-test-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, CreatePngBytes(width, height));
        return path;
    }

    [Fact]
    public void LoadImage_SetsSourceSlot_AndInitializesTargetSize()
    {
        var doc = CreateDoc();
        var path = WriteTempPng(64, 32);
        try
        {
            doc.LoadImage(path);

            Assert.True(doc.Source.HasImage);
            Assert.False(doc.Source.HasNoImage);
            Assert.False(doc.Result.HasImage);
            // Target size is initialized from the source dimensions (snapped to step).
            Assert.True(doc.TargetWidth > 0);
            Assert.True(doc.TargetHeight > 0);
            Assert.True(doc.CanApply);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadImage_WhenFileMissing_ClearsSource()
    {
        var doc = CreateDoc();
        doc.LoadImage(Path.Combine(Path.GetTempPath(), "does-not-exist-12345.png"));

        Assert.False(doc.Source.HasImage);
        Assert.False(doc.CanApply);
    }

    [Fact]
    public void SaveCommands_CanExecute_FollowSlotState()
    {
        var doc = CreateDoc();

        // Empty slots: no save enabled.
        Assert.False(doc.Source.SaveCommand.CanExecute(null));
        Assert.False(doc.Result.SaveCommand.CanExecute(null));

        var path = WriteTempPng(16, 16);
        try
        {
            doc.LoadImage(path);

            // Only the source slot is populated — its save is enabled, the result stays disabled.
            Assert.True(doc.Source.SaveCommand.CanExecute(null));
            Assert.False(doc.Result.SaveCommand.CanExecute(null));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SlotTitles_ShowDimensions_OnlyWhenPopulated()
    {
        var doc = CreateDoc();

        // Empty slots: bare title, no dimension suffix.
        Assert.DoesNotContain("(", doc.Source.Title);
        Assert.DoesNotContain("×", doc.Source.Title);

        var path = WriteTempPng(64, 64);
        try
        {
            doc.LoadImage(path);

            // Populated source slot: title includes the image dimensions. (Under the headless
            // test platform Avalonia decodes PNGs to a 1×1 placeholder, so we assert the
            // shape — "×" — rather than a concrete pixel count.)
            Assert.Contains("×", doc.Source.Title);
            Assert.Contains("px", doc.Source.Title);
            // Result slot stays bare.
            Assert.DoesNotContain("×", doc.Result.Title);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ApplyAsync_ProducesResultSlot_AndClearsStaleMarker()
    {
        var preview = new ImageFileService(new Mock<ILocalizationService>().Object)
            .FromBytes(CreatePngBytes(16, 16));
        var processing = new Mock<IImageEditorProcessingService>();
        processing.Setup(s => s.CreatePixelArtPreviewAsync(
                It.IsAny<ImageEditorProcessingRequest>(),
                It.IsAny<PixelArtConversionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageEditorProcessingResult(preview, 32, 32));

        var doc = CreateDoc(processing.Object);
        var path = WriteTempPng(16, 16);
        try
        {
            doc.LoadImage(path);
            doc.ColorCount = 8; // any option change while the result is empty — no marker yet
            Assert.False(doc.Result.HasImage);

            await doc.ApplyCommand.ExecuteAsync(null);

            Assert.True(doc.Result.HasImage);
            Assert.True(doc.Result.SaveCommand.CanExecute(null));
            Assert.False(doc.Result.HasOverlay);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ApplyAsync_PassesSourceAndOptions()
    {
        var preview = new ImageFileService(new Mock<ILocalizationService>().Object)
            .FromBytes(CreatePngBytes(16, 16));
        var processing = new Mock<IImageEditorProcessingService>();
        ImageEditorProcessingRequest? capturedRequest = null;
        PixelArtConversionOptions? capturedOptions = null;
        processing.Setup(s => s.CreatePixelArtPreviewAsync(
                It.IsAny<ImageEditorProcessingRequest>(),
                It.IsAny<PixelArtConversionOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ImageEditorProcessingRequest, PixelArtConversionOptions?, CancellationToken>(
                (r, o, _) =>
                {
                    capturedRequest = r;
                    capturedOptions = o;
                })
            .ReturnsAsync(new ImageEditorProcessingResult(preview, 32, 32));

        var doc = CreateDoc(processing.Object);
        var path = WriteTempPng(64, 64);
        try
        {
            doc.LoadImage(path);
            doc.LockAspectRatio = false;
            doc.TargetWidth = 20;
            doc.TargetHeight = 30;
            doc.ColorCount = 6;
            doc.EdgeEnhancement = false;
            doc.DitheringEnabled = true;
            doc.TransparentBackground = false;

            await doc.ApplyCommand.ExecuteAsync(null);

            Assert.NotNull(capturedRequest);
            Assert.Equal(Path.GetFullPath(path), capturedRequest.Source.FilePath);
            Assert.Equal(20, capturedRequest.NormalWidth);
            Assert.Equal(30, capturedRequest.NormalHeight);

            Assert.NotNull(capturedOptions);
            Assert.Equal(6, capturedOptions.ColorCount);
            Assert.False(capturedOptions.EdgeEnhancement);
            Assert.True(capturedOptions.Dithering);
            Assert.False(capturedOptions.TransparentBackground);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ChangingOptions_MarksResultStale_UntilApply()
    {
        var preview = new ImageFileService(new Mock<ILocalizationService>().Object)
            .FromBytes(CreatePngBytes(16, 16));
        var processing = new Mock<IImageEditorProcessingService>();
        processing.Setup(s => s.CreatePixelArtPreviewAsync(
                It.IsAny<ImageEditorProcessingRequest>(),
                It.IsAny<PixelArtConversionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageEditorProcessingResult(preview, 32, 32));

        var doc = CreateDoc(processing.Object);
        var path = WriteTempPng(16, 16);
        try
        {
            doc.LoadImage(path);
            await doc.ApplyCommand.ExecuteAsync(null);
            Assert.False(doc.Result.HasOverlay);

            // Option change marks the existing result stale.
            doc.ColorCount = 12;
            Assert.True(doc.Result.HasOverlay);

            // Target-size change keeps it stale.
            doc.TargetWidth = 24;
            Assert.True(doc.Result.HasOverlay);

            // Apply recomputes and clears the marker.
            await doc.ApplyCommand.ExecuteAsync(null);
            Assert.False(doc.Result.HasOverlay);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ChangingCrop_MarksResultStale()
    {
        var preview = new ImageFileService(new Mock<ILocalizationService>().Object)
            .FromBytes(CreatePngBytes(16, 16));
        var processing = new Mock<IImageEditorProcessingService>();
        processing.Setup(s => s.CreatePixelArtPreviewAsync(
                It.IsAny<ImageEditorProcessingRequest>(),
                It.IsAny<PixelArtConversionOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageEditorProcessingResult(preview, 32, 32));

        var doc = CreateDoc(processing.Object);
        var path = WriteTempPng(64, 64);
        try
        {
            doc.LoadImage(path);
            await doc.ApplyCommand.ExecuteAsync(null);
            Assert.False(doc.Result.HasOverlay);

            // The headless platform decodes PNGs to a 1×1 placeholder, so the crop
            // normalization clamps everything away. Feed a real-size WriteableBitmap
            // into the source slot to exercise the crop path.
            doc.Source.ShowBitmap(new WriteableBitmap(
                new PixelSize(64, 64), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque));
            doc.Source.SetCropBounds(8, 8, 24, 24);

            Assert.True(doc.Result.HasOverlay);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Dispose_ReleasesSlotImages()
    {
        var doc = CreateDoc();
        var path = WriteTempPng(16, 16);
        try
        {
            doc.LoadImage(path);
            Assert.True(doc.Source.HasImage);

            doc.Dispose();

            // Bitmaps released — the slots report empty.
            Assert.False(doc.Source.HasImage);
            Assert.False(doc.Result.HasImage);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
